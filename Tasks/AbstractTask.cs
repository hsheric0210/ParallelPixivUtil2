namespace ParallelPixivUtil2.Tasks
{
	public abstract class AbstractTask : PropertyChangeNotifier, IDisposable
	{
		private string taskName = "Generic";
		private int totalProgress = 10;
		private int currentProgress;
		private string details = "";
		private bool indeterminate = true;
		private bool error;

		public string TaskName
		{
			get => taskName;
			set
			{
				taskName = value;
				OnPropertyChanged(nameof(TaskName));
			}
		}

		public int TotalProgress
		{
			get => totalProgress;
			set
			{
				totalProgress = value;
				OnPropertyChanged(nameof(TotalProgress));
			}
		}

		public int CurrentProgress
		{
			get => currentProgress;
			set
			{
				currentProgress = value;
				OnPropertyChanged(nameof(CurrentProgress));
			}
		}

		public string Details
		{
			get => details;
			set
			{
				details = value;
				OnPropertyChanged(nameof(Details));
			}
		}

		public bool Indeterminate
		{
			get => indeterminate;
			set
			{
				indeterminate = value;
				OnPropertyChanged(nameof(Indeterminate));
			}
		}

		public bool Error
		{
			get => error;
			set
			{
				error = value;
				OnPropertyChanged(nameof(Error));
			}
		}

		public int ExitCode
		{
			get; protected set;
		}

		protected AbstractTask(string taskName) => TaskName = taskName;

		protected abstract bool Run();

		public virtual void Dispose()
		{
		}

		public void RunInternal()
		{
			Error = Run();
			Details += " - Finished!";
			Indeterminate = false;
			CurrentProgress = TotalProgress;
			Dispose();
		}
	}
}
