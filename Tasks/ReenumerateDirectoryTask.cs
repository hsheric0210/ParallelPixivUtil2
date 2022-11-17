using log4net;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace ParallelPixivUtil2.Tasks
{
	public class ReenumerateDirectoryTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(ReenumerateDirectoryTask));

		private readonly string[] FileList;
		public ICollection<string> DetectedDirectoryList
		{
			get; private set;
		} = null!;

		public ReenumerateDirectoryTask(string[] files) : base("Re-enumerating updated member directories") => FileList = files;

		protected override bool Run()
		{
			try
			{
				Regex? pattern = string.IsNullOrWhiteSpace(App.Configuration.Archive.DirectoryFormatRegex) ? null : new Regex(App.Configuration.Archive.DirectoryFormatRegex);
				var directoryList = new HashSet<string>();
				foreach (string directory in Directory.EnumerateDirectories(App.Configuration.Archive.WorkingFolder, App.Configuration.Archive.DirectoryFormatWildcard, SearchOption.TopDirectoryOnly))
				{
					if (pattern?.IsMatch(Path.GetFileName(directory)) != false)
						directoryList.Add(directory);
				}

				DetectedDirectoryList = directoryList.ToImmutableList();
			}
			catch (Exception e)
			{
				Logger.Error("Failed to copy existing archives.", e);
				return true;
			}

			return false;
		}
	}
}
