using log4net;
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

		private BackgroundWorker InitWorker;
		public MainViewModel vm;

		// Will be initialized when constructor is finished
		public static MainWindow INSTANCE
		{
			get; private set;
		} = null!;

		public MainWindow()
		{
			vm = new(Dispatcher);
			InitializeComponent();
			DataContext = vm;
			StartWorker();
			INSTANCE = this;
		}

		public void RegisterTask(AbstractTask task)
		{
			vm.AddTask(task);
		}

		public void UnregisterTask(AbstractTask task)
		{
			vm.RemoveTask(task);
		}

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
		}

		private void DoWork(object? sender, DoWorkEventArgs e)
		{
			if (StartTask(new ParseListFileTask()))
			{
				var ipcConfig = new IpcSubParameter
				{
					IPCCommunicationAddress = $"tcp://localhost:{App.Configuration.IPCCommPort}",
					IPCTaskAddress = $"tcp://localhost:{App.Configuration.IPCTaskPort}"
				};

				string memberDataListFile = App.Configuration.MemberDataListFile;
				var pixivutil2Params = new PixivUtil2Parameter(App.Configuration.ExtractorExecutable, "Python.exe", App.Configuration.ExtractorScript, App.IsExtractorScript, App.ExtractorWorkingDirectory, App.Configuration.LogPath)
				{
					ParameterFormat = App.Configuration.MemberDataListParameters,
					Aria2InputPath = App.Configuration.Aria2InputPath,
					DatabasePath = App.Configuration.DatabasePath,
					MemberDataListFile = memberDataListFile,
					Ipc = ipcConfig
				};

				if (StartTask(new MemberDataExtractionTask(pixivutil2Params)))
				{
					var parseDataList = new ParseMemberDataListTask(memberDataListFile);
					if (StartTask(parseDataList))
					{
					}
				}
			}
		}

		public bool StartTask(AbstractTask task)
		{
			MainWindow.INSTANCE.RegisterTask(task);
			task.RunInternal();
			if (!task.Error)
				MainWindow.INSTANCE.UnregisterTask(task);
			return !task.Error;
		}

		private void OnWorkerProgressChanged(object? sender, ProgressChangedEventArgs e)
		{
			vm.Progress = e.ProgressPercentage;
			vm.ProgressDetails = (string?)e.UserState ?? "";
		}

		private void OnWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
		{
			vm.Progress = vm.MaxProgress;
			vm.ProgressDetails = "Finished";
		}
	}
}
