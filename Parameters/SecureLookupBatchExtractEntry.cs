namespace ParallelPixivUtil2.Parameters
{
	public sealed record SecureLookupBatchExtractEntry(IEnumerable<string> UserIds) : AbstractParameter
	{
		public override string FileName => "";

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				return new Dictionary<string, string>
				{
					["UserIDs"] = string.Join(';', UserIds),
					["ArchiveFolder"] = App.Configuration.Archive.ArchiveFolder,
					["WorkingFolder"] = App.Configuration.Archive.WorkingFolder
				};
			}
		}
	}
}
