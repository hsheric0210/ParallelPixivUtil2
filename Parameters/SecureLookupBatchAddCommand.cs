using System.IO;
using System.Text;

namespace ParallelPixivUtil2.Parameters
{
	public sealed record SecureLookupBatchAddCommand(string UserId, string UserName, string UserUrl, string SourcePath, string BatchFile) : AbstractParameter
	{
		public override string FileName => "";

		protected override IDictionary<string, string> ParameterTokens
		{
			get
			{
				return new Dictionary<string, string>
				{
					["UserID"] = UserId,
					["UserName"] = UserName,
					["UserUrl"] = UserUrl,
					["ArchiveFolder"] = App.Configuration.Archive.ArchiveFolder,
					["WorkingFolder"] = App.Configuration.Archive.WorkingFolder,
					["Source"] = SourcePath,
					["BatchFile"] = BatchFile,
				};
			}
		}
	}
}
