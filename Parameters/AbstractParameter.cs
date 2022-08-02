namespace ParallelPixivUtil2.Parameters
{
	public abstract record AbstractParameter
	{
		protected abstract IDictionary<string, string> ParameterTokens
		{
			get;
		}

		public abstract string FileName
		{
			get;
		}

		public string? ParameterFormat
		{
			get; set;
		}

		public virtual string ExtraParameters => "";

		public int TotalPageCount
		{
			get; set;
		} = -1;

		public string Parameter
		{
			get
			{
				if (ParameterFormat == null)
					throw new InvalidOperationException(nameof(ParameterFormat) + " is not set");
				return ExtraParameters + FormatTokens(ParameterFormat, ParameterTokens);
			}
		}

		private static string FormatTokens(string format, IDictionary<string, string> tokens)
		{
			foreach (KeyValuePair<string, string> token in tokens)
				format = format.Replace($"${{{token.Key}}}", token.Value);
			return format;
		}
	}
}
