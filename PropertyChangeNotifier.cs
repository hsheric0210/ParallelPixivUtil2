using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ParallelPixivUtil2
{
	public class PropertyChangeNotifier : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
