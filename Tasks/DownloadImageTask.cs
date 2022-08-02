using ParallelPixivUtil2.Parameters;
using System.Diagnostics;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class DownloadImageTask : AbstractTask
	{
		private readonly Config Configuration;
		private readonly Aria2Parameter Parameter;

		public DownloadImageTask(Config config, Aria2Parameter parameter, MemberSubParameter member) : base($"Download member image of {member.MemberID} page {member.Page}")
		{
			Configuration = config;
			Parameter = parameter;
		}

		protected override bool Run()
		{
			try
			{
				Details = "Retrieveing member data list";

				var retriever = new Process();
				retriever.StartInfo.FileName = Parameter.FileName;
				retriever.StartInfo.WorkingDirectory = Parameter.WorkingDirectory;
				retriever.StartInfo.Arguments = Parameter.Parameter;
				retriever.StartInfo.UseShellExecute = true;
				retriever.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
				retriever.Start();
				retriever.WaitForExit();
				ExitCode = retriever.ExitCode;
			}
			catch (Exception ex)
			{
				ParallelPixivUtil2Main.MainLogger.Error("Error occurred while retrieveing member data list", ex);
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}
	}
}
