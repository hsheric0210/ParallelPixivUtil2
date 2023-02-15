using ParallelPixivUtil2.Parameters;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace ParallelPixivUtil2.Tasks
{
	public class SecureLookupUnarchivingTask : AbstractTask
	{
		private readonly SecureLookupParameter Parameter;
		private readonly SecureLookupBatchExtractEntry Command;
		private readonly bool ShowWindow;

		public SecureLookupUnarchivingTask(SecureLookupParameter parameter, SecureLookupBatchExtractEntry command, bool showWindow) : base("Extracting SecureLookup-protected archives")
		{
			Parameter = parameter;
			Command = command;
			ShowWindow = showWindow;
			Indeterminate = true;
		}

		protected override bool Run()
		{
			try
			{
				File.WriteAllText(App.Configuration.SecureLookup.BatchFileName, Command.Parameter);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error writing batch file.");
			}

			try
			{
				var file = Path.GetFullPath(Parameter.FileName);
				var archiver = new Process();
				archiver.StartInfo.FileName = file;
				archiver.StartInfo.WorkingDirectory = Path.GetDirectoryName(file);
				archiver.StartInfo.Arguments = Parameter.Parameter;
				archiver.StartInfo.UseShellExecute = false;

				if (!ShowWindow)
				{
					archiver.StartInfo.CreateNoWindow = !ShowWindow;
					archiver.StartInfo.RedirectStandardOutput = true;
					archiver.StartInfo.RedirectStandardError = true;
				}
				archiver.LogAndStart();
				archiver.WaitForExit();
				ExitCode = archiver.ExitCode;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error occurred during unarchiving");
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}
	}
}
