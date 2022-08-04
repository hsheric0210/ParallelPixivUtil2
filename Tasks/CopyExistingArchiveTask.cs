using log4net;
using System.IO;

namespace ParallelPixivUtil2.Tasks
{
	public class CopyExistingArchiveTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(CopyExistingArchiveTask));

		private readonly string FileName;

		public CopyExistingArchiveTask(string fileName) : base($"Copying '{fileName}' from the archive repository") => FileName = fileName;

		protected override bool Run()
		{
			try
			{
				File.Copy(FileName, FileName, true);
				Logger.InfoFormat("Copied existing archive {0} from the archive repository.", FileName);
			}
			catch (Exception e)
			{
				Logger.Error("Failed to copy existing archive.", e);
				return true;
			}

			return false;
		}
	}
}
