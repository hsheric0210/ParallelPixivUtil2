using log4net;
using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using ParallelPixivUtil2.Tasks;
using System.ComponentModel;
using System.Windows;

namespace ParallelPixivUtil2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public static readonly ILog MainLogger = LogManager.GetLogger(nameof(MainWindow));

		private BackgroundWorker InitWorker = null!;
		public MainViewModel ViewModel
		{
			get; private set;
		}

		// Will be initialized when constructor is finished
		public static MainWindow INSTANCE
		{
			get; private set;
		} = null!;

		public MainWindow()
		{
			ViewModel = new(Dispatcher);
			InitializeComponent();
			DataContext = ViewModel;
			StartWorker();
			INSTANCE = this;
		}

		public void RegisterTask(AbstractTask task) => ViewModel.AddTask(task);

		public void UnregisterTask(AbstractTask task) => ViewModel.RemoveTask(task);

		private void StartWorker()
		{
			InitWorker = new BackgroundWorker
			{
				WorkerReportsProgress = true
			};
			InitWorker.DoWork += DoWork;
			InitWorker.ProgressChanged += OnWorkerProgressChanged;
			InitWorker.RunWorkerCompleted += OnWorkerCompleted;
			InitWorker.RunWorkerAsync();

			IpcManager.OnIpcProcessNotify += OnIpcProcessNotify;
		}

		private void DoWork(object? sender, DoWorkEventArgs e)
		{
			try
			{
				ViewModel.ProgressDetails = "Parsing list file";
				var parseLines = new ParseListFileTask();
				if (StartTask(parseLines))
				{
					var ipcConfig = new IpcSubParameter
					{
						IPCCommunicationAddress = $"tcp://localhost:{App.Configuration.Magics.IPCCommunicatePort}",
						IPCTaskAddress = $"tcp://localhost:{App.Configuration.Magics.IPCTaskPort}"
					};

					try
					{
						IpcManager.InitFFmpegSemaphore(App.Configuration.Parallelism.MaxFFmpegParallellism);
						IpcManager.InitCommunication(ipcConfig.IPCCommunicationAddress);
						IpcManager.InitTaskRequest(App.ExtractorWorkingDirectory, ipcConfig.IPCTaskAddress);
					}
					catch (Exception ex)
					{
						MainLogger.Error("Failed to setup IpcManager.", ex);
						return;
					}

					string memberDataListFile = App.Configuration.MemberListFileName;
					var pixivutil2Params = new PixivUtil2Parameter(App.Configuration.Extractor.Executable, "Python.exe", App.Configuration.Extractor.PythonScript, App.IsExtractorScript, App.ExtractorWorkingDirectory, App.Configuration.LogFolderName)
					{
						ParameterFormat = App.Configuration.MemberListExtractor.Parameters,
						Aria2InputPath = App.Configuration.DownloadListFolderName,
						DatabasePath = App.Configuration.DatabaseFolderName,
						MemberDataListFile = memberDataListFile,
						Ipc = ipcConfig
					};
					string[] lines = parseLines.Lines!;
					pixivutil2Params.ExtraParameterTokens["memberIDs"] = string.Join(' ', lines);

					var aria2Params = new Aria2Parameter(App.Configuration.Downloader.Executable, App.ExtractorWorkingDirectory /* TODO: Fix this */, App.Configuration.LogFolderName, App.Configuration.DownloadListFolderName, App.Configuration.DatabaseFolderName)
					{
						ParameterFormat = App.Configuration.Downloader.Parameters
					};

					var archiverParams = new ArchiverParameter(App.Configuration.Archiver.Executable)
					{
						ParameterFormat = App.Configuration.Archiver.Parameters
					};

					var unarchiverParams = new ArchiverParameter(App.Configuration.Unarchiver.Executable)
					{
						ParameterFormat = App.Configuration.Unarchiver.Parameters
					};

					if (App.Configuration.AutoArchive)
					{
						var task = new CopyExistingArchiveFromRepositoryTask(lines);
						if (StartTask(task))
						{
							ViewModel.ProgressDetails = "Moving existing archives from the repository";
							string[] movedFiles = task.MovedFileList.ToArray();
							void RunUnarchiverIndividual(string file, ArchiverParameter param)
							{
								StartTask(new ArchiverTask(param with
								{
									ArchiveFile = file,
								}, false));
							}

							ViewModel.ProgressDetails = "Unarchiving the existing archives";
							if (App.Configuration.Unarchiver.AllAtOnce)
							{
								RunUnarchiverIndividual("", unarchiverParams with
								{
									ArchiveFiles = movedFiles
								});
							}
							else
							{
								RunForEachLine(movedFiles, App.Configuration.Parallelism.MaxUnarchiverParallellism, unarchiverParams, RunUnarchiverIndividual);
							}
						}
					}

					ViewModel.ProgressDetails = "Retrieveing member data list";
					if (StartTask(new MemberDataExtractionTask(pixivutil2Params)))
					{
						ViewModel.ProgressDetails = "Parsing member data list";
						var parseDataList = new ParseMemberDataListTask(memberDataListFile);
						if (StartTask(parseDataList))
						{
							ViewModel.MaxProgress = parseDataList.TotalImageCount;

							if (!App.OnlyPostprocessing)
							{
								DownloadQueueManager.BeginTimer(App.Configuration.Extractor.FlushDelay, App.Configuration.Extractor.FlushPeriod);

								// Run extractor
								ViewModel.ProgressDetails = "Retrieveing member images";
								RunForEachPage(parseDataList.Parsed, App.Configuration.Parallelism.MaxExtractorParallellism, pixivutil2Params with
								{
									ParameterFormat = App.Configuration.Extractor.Parameters
								}, (long memberId, MemberPage page, PixivUtil2Parameter param) =>
								{
									StartTask(new RetrieveImageTask(param with
									{
										Identifier = $"{memberId}_page{page.Page}",
										Member = new MemberSubParameter
										{
											MemberID = memberId,
											Page = page
										}
									}));
								});

								DownloadQueueManager.EndTimer();

								// Run downloader
								ViewModel.Progress = 0;
								ViewModel.MaxProgress = 10;
								ViewModel.IsCurrentProgressIndeterminate = true;
								ViewModel.ProgressDetails = "Downloading member images";
								RunForEachPage(parseDataList.Parsed, App.Configuration.Parallelism.MaxDownloaderParallellism, aria2Params, (long memberId, MemberPage page, Aria2Parameter param) =>
								{
									StartTask(new DownloadImageTask(param with
									{
										TargetMemberID = memberId,
										TargetPage = page
									}));
								});
							}

							// Reset Max-progress
							ViewModel.IsCurrentProgressIndeterminate = false;
							ViewModel.Progress = 0;
							ViewModel.MaxProgress = parseDataList.TotalImageCount;

							// Run post-processor
							ViewModel.ProgressDetails = "Post-processing member images";
							RunForEachPage(parseDataList.Parsed, App.Configuration.Parallelism.MaxPostprocessorParallellism, pixivutil2Params with
							{
								ParameterFormat = App.Configuration.Postprocessor.Parameters
							}, (long memberId, MemberPage page, PixivUtil2Parameter param) =>
							{
								StartTask(new PostprocessingTask(param with
								{
									Identifier = $"{memberId}_page{page.Page}",
									Member = new MemberSubParameter
									{
										MemberID = memberId,
										Page = page
									}
								}));
							});

							if (App.Configuration.AutoArchive)
							{
								ViewModel.ProgressDetails = "Re-enumerating the directories";
								var task = new ReenumerateDirectoryTask(lines);
								if (StartTask(task))
								{
									string[] detFiles = task.DetectedDirectoryList.ToArray();
									bool RunArchiverIndividual(string file, ArchiverParameter param)
									{
										return StartTask(new ArchiverTask(param with
										{
											ArchiveFile = file,
										}, true));
									}

									ViewModel.ProgressDetails = "Re-archiving archive directories";
									bool successful = true;
									if (App.Configuration.Archiver.AllAtOnce)
									{
										successful = RunArchiverIndividual("", archiverParams with
										{
											ArchiveFiles = detFiles
										}) && successful;
									}
									else
									{
										RunForEachLine(detFiles, App.Configuration.Parallelism.MaxArchiverParallellism, archiverParams, (f, p) => successful = RunArchiverIndividual(f, p) && successful);
									}

									ViewModel.ProgressDetails = "Copy updated archives to the repository";
									successful = StartTask(new CopyArchiveToReporitoryTask(detFiles)) && successful;

									if (successful && App.Configuration.Archive.DeleteWorkingAfterExecution)
									{
										// If anything is ok
										ViewModel.ProgressDetails = "Deleting working folder as anything is alright";
										StartTask(new DeleteWorkingFolderTask());
									}
								}
							}
						}
					}
				}
			}
			catch (Exception exc)
			{
				MainLogger.Fatal("Exception caught on the main thread!", exc);
			}

		}

		private static void RunForEachLine<T>(IEnumerable<string> list, int parallellismLimit, T parameter, Action<string, T> callback)
	where T : AbstractParameter
		{
			using var semaphore = new SemaphoreSlim(parallellismLimit);
			var tasks = new List<Task>();
			foreach (string line in list)
			{
				semaphore.Wait();
				tasks.Add(Task.Run(() =>
				{
					try
					{
						callback(line, parameter);
					}
					finally
					{
						semaphore.Release();
					}
				}));
			}

			Task.WhenAll(tasks).Wait();
		}

		private static void RunForEachPage<T>(IDictionary<long, ICollection<MemberPage>> memberPageList, int parallellismLimit, T parameter, Action<long, MemberPage, T> callback)
			where T : AbstractParameter
		{
			using var semaphore = new SemaphoreSlim(parallellismLimit);
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach (MemberPage page in pages)
				{
					semaphore.Wait();
					tasks.Add(Task.Run(() =>
					{
						try
						{
							callback(memberId, page, parameter);
						}
						finally
						{
							semaphore.Release();
						}
					}));
				}
			}

			Task.WhenAll(tasks).Wait();
		}

		public void OnIpcProcessNotify(object? sender, IpcEventArgs args)
		{
			// Update overall progress
			ViewModel.IsCurrentProgressIndeterminate = false;
			ViewModel.Progress++;
		}

		public bool StartTask(AbstractTask task)
		{
			RegisterTask(task);
			task.RunInternal();
			if (!task.Error)
				UnregisterTask(task);
			return !task.Error;
		}

		private void OnWorkerProgressChanged(object? sender, ProgressChangedEventArgs e)
		{
			ViewModel.Progress = e.ProgressPercentage;
			ViewModel.ProgressDetails = (string?)e.UserState ?? "";
		}

		private void OnWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
		{
			ViewModel.Progress = ViewModel.MaxProgress;
			ViewModel.ProgressDetails = "Finished";
			if (!App.NoExit)
				Environment.Exit(0);
		}

		private void ReloadConfig(object? sender, RoutedEventArgs e)
		{
			ReloadConfigButton.Content = "Reload config...";
			ReloadConfigButton.IsEnabled = false;
			App.ConfigInit();
			ReloadConfigButton.Content = "Reload config";
			ReloadConfigButton.IsEnabled = true;
		}
	}
}
