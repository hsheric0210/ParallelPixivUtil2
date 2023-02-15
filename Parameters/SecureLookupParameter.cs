using System.IO;

namespace ParallelPixivUtil2.Parameters
{
	public sealed record SecureLookupParameter(string Executable, string BatchFile) : AbstractParameter
	{
		public override string FileName => Executable;

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				return new Dictionary<string, string>
				{
					["BatchFile"] = BatchFile,
				};
			}
		}
	}
}
