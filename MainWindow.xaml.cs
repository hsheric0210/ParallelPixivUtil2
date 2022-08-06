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
						IPCCommunicationAddress = $"tcp://localhost:{App.Configuration.IPCCommPort}",
						IPCTaskAddress = $"tcp://localhost:{App.Configuration.IPCTaskPort}"
					};

					try
					{
						IpcManager.InitFFmpegSemaphore(App.Configuration.MaxFFmpegParallellism);
						IpcManager.InitCommunication(ipcConfig.IPCCommunicationAddress);
						IpcManager.InitTaskRequest(App.ExtractorWorkingDirectory, ipcConfig.IPCTaskAddress);
					}
					catch (Exception ex)
					{
						MainLogger.Error("Failed to setup IpcManager.", ex);
						return;
					}

					string memberDataListFile = App.Configuration.MemberDataListFile;
					var pixivutil2Params = new PixivUtil2Parameter(App.Configuration.ExtractorExecutable, "Python.exe", App.Configuration.ExtractorScript, App.IsExtractorScript, App.ExtractorWorkingDirectory, App.Configuration.LogPath)
					{
						ParameterFormat = App.Configuration.MemberDataListParameters,
						Aria2InputPath = App.Configuration.Aria2InputPath,
						DatabasePath = App.Configuration.DatabasePath,
						MemberDataListFile = memberDataListFile,
						Ipc = ipcConfig
					};
					string[] lines = parseLines.Lines!;
					pixivutil2Params.ExtraParameterTokens["memberIDs"] = string.Join(' ', lines);

					var aria2Params = new Aria2Parameter(App.Configuration.DownloaderExecutable, App.ExtractorWorkingDirectory /* TODO: Fix this */, App.Configuration.LogPath, App.Configuration.Aria2InputPath, App.Configuration.DatabasePath)
					{
						ParameterFormat = App.Configuration.DownloaderParameters
					};

					var archiverParams = new ArchiverParameter(App.Configuration.Archiver)
					{
						ParameterFormat = App.Configuration.ArchiverParameter
					};

					var unarchiverParams = new ArchiverParameter(App.Configuration.Unarchiver)
					{
						ParameterFormat = App.Configuration.UnarchiverParameter
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
							if (App.Configuration.UnarchiverAllInOne)
							{
								RunUnarchiverIndividual("", unarchiverParams with
								{
									ArchiveFiles = movedFiles
								});
							}
							else
							{
								RunForEachLine(movedFiles, App.Configuration.UnarchiverParallellism, unarchiverParams, RunUnarchiverIndividual);
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
								DownloadQueueManager.BeginTimer(App.Configuration.DownloadInputDelay, App.Configuration.DownloadInputPeriod);

								// Run extractor
								ViewModel.ProgressDetails = "Retrieveing member images";
								RunForEachPage(parseDataList.Parsed, App.Configuration.MaxExtractorParallellism, pixivutil2Params with
								{
									ParameterFormat = App.Configuration.ExtractorParameters
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
								RunForEachPage(parseDataList.Parsed, App.Configuration.MaxDownloaderParallellism, aria2Params, (long memberId, MemberPage page, Aria2Parameter param) =>
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
							RunForEachPage(parseDataList.Parsed, App.Configuration.MaxPostprocessorParallellism, pixivutil2Params with
							{
								ParameterFormat = App.Configuration.PostprocessorParameters
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
									void RunArchiverIndividual(string file, ArchiverParameter param)
									{
										StartTask(new ArchiverTask(param with
										{
											ArchiveFile = file,
										}, true));
									}

									ViewModel.ProgressDetails = "Re-archiving archive directories";
									if (App.Configuration.ArchiverAllInOne)
									{
										RunArchiverIndividual("", archiverParams with
										{
											ArchiveFiles = detFiles
										});
									}
									else
									{
										RunForEachLine(detFiles, App.Configuration.ArchiverParallellism, archiverParams, RunArchiverIndividual);
									}

									ViewModel.ProgressDetails = "Copy updated archives to the repository";
									StartTask(new CopyArchiveToReporitoryTask(detFiles));
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
	}
}
