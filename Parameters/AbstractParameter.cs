using Serilog;
using StringTokenFormatter;

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
				return ExtraParameters + ParameterFormat.FormatDictionary(ParameterTokens);
			}
		}
	}
}
