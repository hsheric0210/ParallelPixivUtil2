using log4net;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace ParallelPixivUtil2.Tasks
{
	public class DeleteWorkingFolderTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(DeleteWorkingFolderTask));

		public DeleteWorkingFolderTask() : base("Delete working folders.")
		{
		}

		protected override bool Run()
		{
			try
			{
				Indeterminate = true;

				string wfolder = App.Configuration.Archive.WorkingFolder;
				Details = $"Deleting working folder '{wfolder}'.";
				Directory.Delete(wfolder, true);

				string bfolder = App.Configuration.Archive.BackupFolder;
				Details = $"Deleting backup folder '{wfolder}'.";
				Directory.Delete(bfolder, true);
			}
			catch (Exception e)
			{
				Logger.Error("Failed to delete working folders.", e);
				return true;
			}

			return false;
		}
	}
}
