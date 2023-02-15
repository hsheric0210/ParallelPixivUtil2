using System.Diagnostics;
using System.IO;
using System.Text;
using Serilog;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class DownloadQueueFlushTask : AbstractTask
	{
		private readonly IDictionary<string, IList<string>> DownloadQueue;
		public IDictionary<string, IList<string>> RetryQueue = new Dictionary<string, IList<string>>();

		public DownloadQueueFlushTask(IDictionary<string, IList<string>> queue) : base("Flush aria2 input queue") => DownloadQueue = queue;

		protected override bool Run()
		{
			try
			{
				Log.Debug("Processing queued download input list...");
				var watch = new Stopwatch();
				watch.Start();

				Task.WhenAll(DownloadQueue.Select((pair) =>
					Task.Run(() =>
					{
						var builder = new StringBuilder();
						foreach (var item in pair.Value)
							builder.Append(item);
						try
						{
							File.AppendAllText(pair.Key, builder.ToString());
						}
						catch (Exception ex)
						{
							Log.Warning(ex, "Exception occurred while writing. Added to retry queue.");
							RetryQueue.Add(pair);
						}
					}))).Wait();

				watch.Stop();
				Log.Debug("Processed queued download input list: Took {0}ms", watch.ElapsedMilliseconds);
			}
			catch (Exception e)
			{
				Log.Error(e, "An error occurred.");
				return true;
			}

			return false;
		}
	}
}
