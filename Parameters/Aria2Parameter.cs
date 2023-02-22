namespace ParallelPixivUtil2.Parameters
{
	public sealed record Aria2Parameter(string Executable, string WorkingDirectory, string LogPath, string Aria2InputPath, string DatabasePath) : AbstractParameter
	{
		public override string FileName => Executable;

		public MemberPage TargetPage
		{
			get; set;
		}

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				var dict = new Dictionary<string, string>
				{
					["logPath"] = LogPath,
					["aria2InputPath"] = Aria2InputPath,
					["databasePath"] = DatabasePath
				};

				if (TargetPage != null)
				{
					dict["memberID"] = TargetPage.MemberId.ToString()!;
					dict["page"] = TargetPage!.Page.ToString();
					dict["fileIndex"] = TargetPage!.FileIndex.ToString();
				}

				return dict;
			}
		}
	}
}
