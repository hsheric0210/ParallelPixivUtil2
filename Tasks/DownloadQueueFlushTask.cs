using log4net;
using System.Diagnostics;
using System.IO;
using System.Text;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class DownloadQueueFlushTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(DownloadQueueFlushTask));

		private readonly IDictionary<string, IList<string>> DownloadQueue;

		public DownloadQueueFlushTask(IDictionary<string, IList<string>> queue) : base("Flush aria2 input queue") => DownloadQueue = queue;

		protected override bool Run()
		{
			Logger.Debug("Processing queued download input list...");
			var watch = new Stopwatch();
			watch.Start();

			Task.WhenAll(DownloadQueue.Select((pair) =>
				Task.Run(() =>
				{
					var builder = new StringBuilder();
					foreach (string? item in pair.Value)
					{
						builder.Append(item);
					}
					File.AppendAllText(pair.Key, builder.ToString());
				}))).Wait();

			watch.Stop();
			Logger.DebugFormat("Processed queued download input list: Took {0}ms", watch.ElapsedMilliseconds);
			return false;
		}
	}
}
