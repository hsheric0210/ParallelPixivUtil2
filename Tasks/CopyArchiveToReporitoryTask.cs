using System.IO;
using Serilog;

namespace ParallelPixivUtil2.Tasks
{
	public class CopyArchiveToReporitoryTask : AbstractTask
	{
		private readonly string[] FileList;

		public CopyArchiveToReporitoryTask(string[] files) : base("Copying updated archives to the archive repository") => FileList = files;

		protected override bool Run()
		{
			try
			{
				Indeterminate = false;
				TotalProgress = FileList.Length;

				foreach (var _archive in FileList)
				{
					var archive = $"{_archive}.7z";
					var archiveName = Path.GetFileName(archive);
					var destination = App.Configuration.Archive.ArchiveFolder + Path.DirectorySeparatorChar + archiveName;
					if (File.Exists(destination))
						Log.Warning("'{0}' already exists in '{1}' - Renamed to '{2}.", archive, App.Configuration.Archive, FileUtils.PerformRollingFileRename(destination));
					Log.Information("Copy updated archive '{0}' to '{1}'.", archive, destination);
					Details = $"Copy updated archive '{archive}' to '{destination}'.";
					File.Copy(archive, destination);
					Log.Information("Copy finished for '{0}'.", archive);
					CurrentProgress++;
				}
			}
			catch (Exception e)
			{
				Log.Error(e, "Failed to copy archives.");
				return true;
			}

			return false;
		}
	}
}
