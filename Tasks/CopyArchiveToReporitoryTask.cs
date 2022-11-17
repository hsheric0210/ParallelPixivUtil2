﻿using log4net;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace ParallelPixivUtil2.Tasks
{
	public class CopyArchiveToReporitoryTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(CopyArchiveToReporitoryTask));

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
					string archiveName = Path.GetFileName(archive);
					string destination = App.Configuration.Archive.ArchiveFolder + Path.DirectorySeparatorChar + archiveName;
					if (File.Exists(destination))
						Logger.WarnFormat("'{0}' already exists in '{1}' - Renamed to '{2}.", archive, App.Configuration.Archive, FileUtils.PerformRollingFileRename(destination));
					Logger.InfoFormat("Copy updated archive '{0}' to '{1}'.", archive, destination);
					Details = $"Copy updated archive '{archive}' to '{destination}'.";
					File.Copy(archive, destination);
					Logger.InfoFormat("Copy finished for '{0}'.", archive);
					CurrentProgress++;
				}
			}
			catch (Exception e)
			{
				Logger.Error("Failed to copy archives.", e);
				return true;
			}

			return false;
		}
	}
}
