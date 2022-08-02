using log4net;
using ParallelPixivUtil2.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace ParallelPixivUtil2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public static readonly ILog MainLogger = LogManager.GetLogger("Main");

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
			//vm.DisplayTaskList.Add(task);
			//Dispatcher.Invoke(() => TaskList.ItemsSource = vm.DisplayTaskList);
		}

		public void UnregisterTask(AbstractTask task)
		{
			vm.RemoveTask(task);
		}

		/// <summary>
		/// 프로그레스바 컨트롤 증가 버튼 이벤트 핸들러
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
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

		/// <summary>
		/// DoWorker 스레드 이벤트 핸들러
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void DoWork(object? sender, DoWorkEventArgs e)
		{
			try
			{
				new TestTask().Start();
			}
			catch (Exception ex)
			{
				MainLogger.Error("Test error", ex);
			}
			for (int i = 0; i < 10; i++)
			{
				Thread.Sleep(20); //0.1초
			}
		}

		private void OnWorkerProgressChanged(object? sender, ProgressChangedEventArgs e)
		{
			vm.Progress = e.ProgressPercentage;
			vm.ProgressDetails = (string?)e.UserState ?? "";
		}

		/// <summary>
		/// 프로그레스바 컨트롤 작업 끝났을 때
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
		{
			vm.Progress = vm.MaxProgress;
			vm.ProgressDetails = "Finished";
		}
	}

	public class MainViewModel : PropertyChangeNotifier
	{
		private Dispatcher Dispatcher;

		private bool indeterminate = false;
		private int maxProgress = 100;
		private int progress = 0;
		private string progressDetails = "Initialization";
		private CollectionViewSource taskListViewSource = new CollectionViewSource();
		private ObservableCollection<AbstractTask> taskList = new ObservableCollection<AbstractTask>();

		public MainViewModel(Dispatcher dispatcher)
		{
			Dispatcher = dispatcher;
			taskListViewSource.Source = taskList;
		}

		public bool IsCurrentProgressIndeterminate
		{
			get => indeterminate;
			set
			{
				indeterminate = value;
				OnPropertyChanged(nameof(IsCurrentProgressIndeterminate));
			}
		}

		public int MaxProgress
		{
			get => maxProgress;
			set
			{
				maxProgress = value;
				OnPropertyChanged(nameof(MaxProgress));
			}
		}

		public int Progress
		{
			get => progress;
			set
			{
				progress = value;
				OnPropertyChanged(nameof(Progress));
			}
		}

		public string ProgressDetails
		{
			get => progressDetails;
			set
			{
				progressDetails = value;
				OnPropertyChanged(nameof(ProgressDetails));
			}
		}

		public ObservableCollection<AbstractTask> DisplayTaskList
		{
			get => taskList;

			set
			{
				taskList = value;
				taskListViewSource.Source = taskList;
				OnPropertyChanged(nameof(DisplayTaskList));
			}
		}

		public ICollectionView TaskListView => taskListViewSource.View;

		public void AddTask(AbstractTask task)
		{
			Dispatcher.Invoke(() => DisplayTaskList.Add(task));
		}

		public void RemoveTask(AbstractTask task)
		{
			Dispatcher.Invoke(() => DisplayTaskList.Remove(task));
		}

		public void UpdateTaskList() => OnPropertyChanged(nameof(DisplayTaskList));
	}
}
