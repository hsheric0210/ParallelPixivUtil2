using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using Serilog;

namespace ParallelPixivUtil2.Tasks
{
	public class CopyExistingArchiveFromRepositoryTask : AbstractTask
	{
		private readonly string[] FileList;
		public ICollection<string> MovedFileList
		{
			get; private set;
		} = null!;

		public CopyExistingArchiveFromRepositoryTask(string[] files) : base("Copying archives from the archive repository") => FileList = files;

		protected override bool Run()
		{
			try
			{
				Regex? pattern = string.IsNullOrWhiteSpace(App.Configuration.Archive.ArchiveFormatRegex) ? null : new Regex(App.Configuration.Archive.ArchiveFormatRegex);
				var archiveList = new HashSet<string>();
				Log.Information("Searching pattern {0} from {1}...", App.Configuration.Archive.ArchiveFormatWildcard, App.Configuration.Archive);
				foreach (var path in Directory.EnumerateFiles(App.Configuration.Archive.ArchiveFolder, App.Configuration.Archive.ArchiveFormatWildcard, App.Configuration.Archive.SearchTopDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories))
				{
					if (FileList.Contains(Path.GetFileNameWithoutExtension(path)) && pattern?.IsMatch(Path.GetFileName(path)) != false)
						archiveList.Add(path);
				}

				var total = archiveList.Count;
				Indeterminate = false;
				TotalProgress = total;

				var movedFiles = new HashSet<string>(total);
				foreach (var archive in archiveList)
				{
					var archiveName = Path.GetFileName(archive);
					var destination = App.Configuration.Archive.BackupFolder + Path.DirectorySeparatorChar + archiveName;

					if (File.Exists(destination))
						Log.Warning("'{0}' already exists in '{1}' - Renamed to '{2}'.", archiveName, App.Configuration.Archive.BackupFolder, FileUtils.PerformRollingFileRename(destination));
					Details = $"Copy existing archive '{archive}' to '{destination}'.";

					File.Copy(archive, destination);
					movedFiles.Add(destination);

					Log.Information("Copy finished for '{0}'.", destination);
					CurrentProgress++;
				}

				MovedFileList = movedFiles.ToImmutableList();
			}
			catch (Exception e)
			{
				Log.Error(e, "Failed to copy existing archives.");
				return true;
			}

			return false;
		}
	}
}
