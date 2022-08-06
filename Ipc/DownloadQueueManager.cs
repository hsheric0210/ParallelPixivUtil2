﻿using log4net;
using ParallelPixivUtil2.Tasks;
using System.Collections.Concurrent;

namespace ParallelPixivUtil2.Ipc
{
	public static class DownloadQueueManager
	{
		private readonly static ILog Logger = LogManager.GetLogger(nameof(DownloadQueueManager));

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
			list.Add(data);
		}

		private static void FlushQueue()
		{
			try
			{
				var task = new DownloadQueueFlushTask(new Dictionary<string, IList<string>>(DownloadQueue)); // create copy
				DownloadQueue.Clear();
				MainWindow.INSTANCE.StartTask(task);
				foreach (var pair in task.RetryQueue)
				{
					if (!DownloadQueue.Contains(pair))
						DownloadQueue.Add(pair);
				}
			}
			catch (Exception ex)
			{
				Logger.Error("Error flushing download queue.", ex);
			}
		}
	}
}
