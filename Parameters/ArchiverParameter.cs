using System.IO;

namespace ParallelPixivUtil2.Parameters
{
	public sealed record ArchiverParameter(string Executable) : AbstractParameter
	{
		public override string FileName => Executable;

		public string? ArchiveFile
		{
			get; set;
		}

		public string[]? ArchiveFiles
		{
			get; set;
		}

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				var dict = new Dictionary<string, string>
				{
					["destination"] = App.Configuration.ArchiveWorkingDirectory,
				};

				if (ArchiveFile != null)
				{
					dict["archive"] = ArchiveFile;
					dict["archiveName"] = Path.GetFileNameWithoutExtension(ArchiveFile);
				}

				if (ArchiveFiles != null)
					dict["archives"] = string.Join(' ', ArchiveFiles);

				return dict;
			}
		}
	}
}
