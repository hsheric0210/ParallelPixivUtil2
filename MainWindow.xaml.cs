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
			ViewModel.ProgressDetails = "Parsing list file";
			if (StartTask(new ParseListFileTask()))
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

				var aria2Params = new Aria2Parameter(App.Configuration.DownloaderExecutable, App.ExtractorWorkingDirectory /* TODO: Fix this */, App.Configuration.LogPath, App.Configuration.Aria2InputPath, App.Configuration.DatabasePath)
				{
					ParameterFormat = App.Configuration.DownloaderParameters
				};

				ViewModel.ProgressDetails = "Retrieveing member data list";
				if (StartTask(new MemberDataExtractionTask(pixivutil2Params)))
				{
					var parseDataList = new ParseMemberDataListTask(memberDataListFile);
					if (StartTask(parseDataList))
					{
						ViewModel.MaxProgress = parseDataList.TotalImageCount;

						if (!App.OnlyPostprocessing)
						{
							DownloadQueueManager.BeginTimer(App.Configuration.DownloadInputDelay, App.Configuration.DownloadInputPeriod);

							// Run extractor
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
							RunForEachPage(parseDataList.Parsed, App.Configuration.MaxDownloaderParallellism, aria2Params, (long memberId, MemberPage page, Aria2Parameter param) =>
							{
								StartTask(new DownloadImageTask(param with
								{
									TargetMemberID = memberId,
									TargetPage = page
								}));
							});
						}

						// Run post-processor
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
					}
				}
			}
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
		}
	}
}
