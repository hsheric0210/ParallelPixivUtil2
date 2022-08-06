using log4net;
using ParallelPixivUtil2.Parameters;
using System.Diagnostics;
using System.IO;

namespace ParallelPixivUtil2.Tasks
{
	public class ArchiverTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(ArchiverTask));

		private readonly ArchiverParameter Parameter;

		public ArchiverTask(ArchiverParameter parameter, bool archive) : base("Running archiver/unarchiver")
		{
			Parameter = parameter;
			Details = (archive ? "Archiving " : "Unarchiving ") + parameter.ArchiveFile;
		}

		protected override bool Run()
		{
			try
			{
				var archiver = new Process();
				archiver.StartInfo.FileName = Parameter.FileName;
				archiver.StartInfo.WorkingDirectory = App.Configuration.ArchiveWorkingDirectory;
				archiver.StartInfo.Arguments = Parameter.Parameter;
				archiver.StartInfo.UseShellExecute = false;
				archiver.StartInfo.CreateNoWindow = true;
				archiver.StartInfo.RedirectStandardError = true;
				archiver.Start();
				archiver.ErrorDataReceived += ReceiveProgress;
				archiver.BeginErrorReadLine();

				Indeterminate = false;
				TotalProgress = 100;

				archiver.WaitForExit();
				ExitCode = archiver.ExitCode;
			}
			catch (Exception ex)
			{
				Logger.Error("Error occurred during archiving", ex);
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}

		public void ReceiveProgress(object? sender, DataReceivedEventArgs args)
		{
			string? line = args.Data?.Trim();
			// '  93% 41'
			if (line?.Length >= 3 && line[2] == '%' && int.TryParse(line[..2], out int prog))
				CurrentProgress = prog;
		}
	}
}
