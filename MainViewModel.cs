using ParallelPixivUtil2.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;

namespace ParallelPixivUtil2
{
	public class MainViewModel : PropertyChangeNotifier
	{
		private readonly Dispatcher MainDispatcher;
		private readonly CollectionViewSource taskListViewSource = new();

		private bool indeterminate = true;
		private int maxProgress = 100;
		private int progress = 0;
		private string progressDetails = "Initialization";
		private ObservableCollection<AbstractTask> taskList = new();

		public MainViewModel(Dispatcher dispatcher)
		{
			MainDispatcher = dispatcher;
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
			MainDispatcher.Invoke(() => DisplayTaskList.Add(task));
		}

		public void RemoveTask(AbstractTask task)
		{
			MainDispatcher.Invoke(() => DisplayTaskList.Remove(task));
		}

		public void UpdateTaskList() => OnPropertyChanged(nameof(DisplayTaskList));
	}
}
