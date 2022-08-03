using log4net;
using ParallelPixivUtil2.Parameters;
using System.Diagnostics;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class MemberDataExtractionTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(MemberDataExtractionTask));

		private readonly PixivUtil2Parameter Parameter;

		public MemberDataExtractionTask(PixivUtil2Parameter parameter) : base("Retrieve member data") => Parameter = parameter;

		protected override bool Run()
		{
			try
			{
				var retriever = new Process();
				retriever.StartInfo.FileName = Parameter.FileName;
				retriever.StartInfo.WorkingDirectory = Parameter.WorkingDirectory;
				retriever.StartInfo.Arguments = Parameter.Parameter;
				retriever.StartInfo.UseShellExecute = false;
				retriever.Start();
				retriever.WaitForExit();
				ExitCode = retriever.ExitCode;
			}
			catch (Exception ex)
			{
				Logger.Error("Error occurred while retrieveing member data list", ex);
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}
	}
}
