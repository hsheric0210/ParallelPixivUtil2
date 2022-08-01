using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ParallelPixivUtil2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private BackgroundWorker InitWorker;
		private MainViewModel vm = new();

		public MainWindow()
		{
			InitializeComponent();
			DataContext = vm;
			StartWorker();
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
			for (int i = 0; i < 5; i++)
			{
				InitWorker.ReportProgress(i, "Currently Initializing"); //값을 ReportProgress 매개변수로 전달
				Thread.Sleep(1000); //0.1초
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
		}
	}

	public class MainViewModel : PropertyChangeNotifier
	{
		private bool indeterminate = false;
		private int maxProgress = 100;
		private int progress = 0;
		private string progressDetails = "Initialization";

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
	}
}
