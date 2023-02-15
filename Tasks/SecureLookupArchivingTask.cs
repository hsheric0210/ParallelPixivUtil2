using ParallelPixivUtil2.Parameters;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace ParallelPixivUtil2.Tasks
{
	public class SecureLookupArchivingTask : AbstractTask
	{
		private readonly SecureLookupParameter Parameter;
		private readonly IEnumerable<SecureLookupBatchAddCommand> Commands;
		private readonly bool ShowWindow;

		public SecureLookupArchivingTask(SecureLookupParameter parameter, IEnumerable<SecureLookupBatchAddCommand> commands, bool showWindow) : base("Adding & Archiving SecureLookup-protected archives")
		{
			Parameter = parameter;
			Commands = commands;
			ShowWindow = showWindow;
			Indeterminate = true;
		}

		protected override bool Run()
		{
			try
			{
				foreach (SecureLookupBatchAddCommand command in Commands)
				{
					File.WriteAllLines(App.Configuration.SecureLookup.BatchFileName, Commands.Select(cmd => cmd.Parameter));
				}
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
				Log.Error(ex, "Error occurred during archiving");
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}
	}
}
