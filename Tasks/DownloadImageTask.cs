using log4net;
using ParallelPixivUtil2.Parameters;
using System.Diagnostics;

namespace ParallelPixivUtil2.Tasks
{
	public class DownloadImageTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(DownloadImageTask));

		private readonly Aria2Parameter Parameter;

		public DownloadImageTask(Aria2Parameter parameter) : base($"Download member image of {parameter.TargetMemberID} page {parameter.TargetPage!.Page} (File index {parameter.TargetPage!.FileIndex})") => Parameter = parameter;

		protected override bool Run()
		{
			try
			{
				Details = "Retrieveing member data list";

				var retriever = new Process();
				retriever.StartInfo.FileName = Parameter.FileName;
				retriever.StartInfo.WorkingDirectory = Parameter.WorkingDirectory;
				retriever.StartInfo.Arguments = Parameter.Parameter;
				retriever.StartInfo.UseShellExecute = false;
				retriever.StartInfo.CreateNoWindow = true;
				retriever.Start();
				retriever.WaitForExit();
				ExitCode = retriever.ExitCode;
			}
			catch (Exception ex)
			{
				Logger.Error("Error occurred while downloading member image", ex);
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}
	}
}
