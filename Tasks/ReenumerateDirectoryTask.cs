using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using Serilog;

namespace ParallelPixivUtil2.Tasks
{
	public class ReenumerateDirectoryTask : AbstractTask
	{
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
				foreach (var directory in Directory.EnumerateDirectories(App.Configuration.Archive.WorkingFolder, App.Configuration.Archive.DirectoryFormatWildcard, SearchOption.TopDirectoryOnly))
				{
					if (pattern?.IsMatch(Path.GetFileName(directory)) != false)
						directoryList.Add(directory);
				}

				DetectedDirectoryList = directoryList.ToImmutableList();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to copy existing archives.");
				return true;
			}

			return false;
		}
	}
}
