namespace ParallelPixivUtil2.Parameters
{
	public sealed record Aria2Parameter(string Executable, string WorkingDirectory, string LogPath, string Aria2InputPath, string DatabasePath) : AbstractParameter
	{
		public override string FileName => Executable;

		public long? TargetMemberID
		{
			get; set;
		}

		public MemberPage? TargetPage
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

				if (TargetMemberID != null)
					dict["memberID"] = TargetMemberID.ToString()!;

				if (TargetPage != null)
				{
					dict["page"] = TargetPage!.Page.ToString();
					dict["fileIndex"] = TargetPage!.FileIndex.ToString();
				}

				return dict;
			}
		}
	}
}
