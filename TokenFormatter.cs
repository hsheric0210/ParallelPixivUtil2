namespace ParallelPixivUtil2
{
	public static class TokenFormatter
	{
		public static string FormatWithTokens(this string format, IDictionary<string, string> tokens)
		{
			foreach (KeyValuePair<string, string> token in tokens)
				format = format.Replace($"${{{token.Key}}}", token.Value);
			return format;
		}
	}
}
