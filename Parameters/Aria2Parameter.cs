namespace ParallelPixivUtil2.Parameters
{
	public sealed record Aria2Parameter(string Executable, string WorkingDirectory, string LogPath, string Aria2InputPath, string DatabasePath, long TargetMemberID, MemberPage TargetPage) : AbstractParameter
	{
		public override string FileName => Executable;

		protected override IDictionary<string, string> ParameterTokens => new Dictionary<string, string>
		{
			["memberID"] = TargetMemberID.ToString(),
			["page"] = TargetPage.Page.ToString(),
			["fileIndex"] = TargetPage.FileIndex.ToString(),
			["logPath"] = LogPath,
			["aria2InputPath"] = Aria2InputPath,
			["databasePath"] = DatabasePath
		};
	}
}
