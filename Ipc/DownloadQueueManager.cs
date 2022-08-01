using ParallelPixivUtil2.Tasks;
using System.Collections.Concurrent;

namespace ParallelPixivUtil2.Ipc
{
	public static class DownloadQueueManager
	{
		private static readonly IDictionary<string, IList<string>> DownloadQueue = new ConcurrentDictionary<string, IList<string>>();
		private static Timer? Timer;

		public static void BeginTimer(long delay, long period) => Timer = new Timer(_ => FlushQueue(), null, delay, period);

		public static void EndTimer()
		{
			if (Timer == null)
				return;

			Timer.Dispose();
			FlushQueue();
		}

		public static void Add(string fileName, string data)
		{
			if (!DownloadQueue.TryGetValue(fileName, out IList<string>? list))
			{
				list = new List<string>();
				DownloadQueue.Add(fileName, list);
			}

			DownloadQueue[fileName].Add(data);
		}

		private static void FlushQueue()
		{
			// FlushDownloadInputQueue();
			var task = new DownloadQueueFlushTask(DownloadQueue);
			task.RunInternal();
		}
	}
}
