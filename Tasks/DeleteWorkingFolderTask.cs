using System.IO;
using Serilog;

namespace ParallelPixivUtil2.Tasks
{
	public class DeleteWorkingFolderTask : AbstractTask
	{
		public DeleteWorkingFolderTask() : base("Delete working folders.")
		{
		}

		protected override bool Run()
		{
			try
			{
				Indeterminate = true;

				var wfolder = App.Configuration.Archive.WorkingFolder;
				Details = $"Deleting working folder '{wfolder}'.";
				Log.Information("Deleting '{0}'.", wfolder);
				Directory.Delete(wfolder, true);

				var bfolder = App.Configuration.Archive.BackupFolder;
				Log.Information("Deleting '{0}'.", bfolder);
				Details = $"Deleting backup folder '{bfolder}'.";
				Directory.Delete(bfolder, true);
			}
			catch (Exception e)
			{
				Log.Error(e, "Failed to delete working folders.");
				return true;
			}

			return false;
		}
	}
}
