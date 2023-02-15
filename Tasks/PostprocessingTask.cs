using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using Serilog;
using System.Diagnostics;

namespace ParallelPixivUtil2.Tasks
{
	public class PostprocessingTask : AbstractTask
	{
		private readonly PixivUtil2Parameter Parameter;
		private readonly string? MyIdentifier;

		public PostprocessingTask(PixivUtil2Parameter parameter) : base("Post-processing")
		{
			if (parameter.Member == null)
				throw new ArgumentException("parameter.Member can't be null when initializing " + nameof(PostprocessingTask));

			Parameter = parameter;

			Details = $"Post-processing of {parameter.Member?.MemberID} page {parameter.Member?.Page!.Page} (File index {parameter.Member?.Page!.FileIndex})";

			MyIdentifier = parameter.Identifier;
			IpcManager.OnIpcTotalNotify += OnTotalNotify;
			IpcManager.OnIpcProcessNotify += OnProcessNotify;
		}

		protected override bool Run()
		{
			try
			{
				var show = App.Configuration.Postprocessor.ShowWindow;

				var postProcessor = new Process();
				postProcessor.StartInfo.FileName = Parameter.FileName;
				postProcessor.StartInfo.WorkingDirectory = Parameter.WorkingDirectory;
				postProcessor.StartInfo.Arguments = Parameter.Parameter;
				postProcessor.StartInfo.UseShellExecute = show;
				postProcessor.StartInfo.CreateNoWindow = !show;
				postProcessor.LogAndStart();
				postProcessor.WaitForExit();
				ExitCode = postProcessor.ExitCode;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error occurred while post-processing");
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}

		public void OnTotalNotify(object? sender, IpcTotalNotifyEventArgs args)
		{
			if (args.Identifier != MyIdentifier)
				return;

			TotalProgress = args.Total;
			Indeterminate = false;
		}

		public void OnProcessNotify(object? sender, IpcEventArgs args)
		{
			if (args.Identifier == MyIdentifier && !Indeterminate)
				CurrentProgress++;
		}

		public override void Dispose()
		{
			IpcManager.OnIpcTotalNotify -= OnTotalNotify;
			IpcManager.OnIpcProcessNotify -= OnProcessNotify;
		}
	}
}
