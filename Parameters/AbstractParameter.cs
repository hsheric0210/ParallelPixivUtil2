namespace ParallelPixivUtil2.Parameters
{
	public abstract record AbstractParameter(string ParameterFormat)
	{
		protected abstract IDictionary<string, string> ParameterTokens
		{
			get;
		}

		public abstract string FileName
		{
			get;
		}

		public virtual string ExtraParameters => "";

		public int TotalPageCount
		{
			get; set;
		} = -1;

		public string Parameter => ExtraParameters + FormatTokens(ParameterFormat, ParameterTokens);

		private static string FormatTokens(string format, IDictionary<string, string> tokens)
		{
			foreach (KeyValuePair<string, string> token in tokens)
				format = format.Replace($"${{{token.Key}}}", token.Value);
			return format;
		}
	}
}
