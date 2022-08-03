using log4net;
using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using System.Diagnostics;

namespace ParallelPixivUtil2.Tasks
{
	public class PostprocessingTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(DownloadImageTask));

		private readonly PixivUtil2Parameter Parameter;
		private readonly string? MyIdentifier;

		public PostprocessingTask(PixivUtil2Parameter parameter) : base("Post-processing")
		{
			if (parameter.Member == null)
				throw new ArgumentException("parameter.Member can't be null when initializing " + nameof(PostprocessingTask));

			Parameter = parameter;

			Details = $"Post-processing of {parameter.Member?.MemberID} page {parameter.Member?.Page}";

			MyIdentifier = parameter.Identifier;
			IpcManager.OnIpcTotalNotify += OnTotalNotify;
			IpcManager.OnIpcProcessNotify += OnProcessNotify;
		}

		protected override bool Run()
		{
			try
			{
				var postProcessor = new Process();
				postProcessor.StartInfo.FileName = Parameter.FileName;
				postProcessor.StartInfo.WorkingDirectory = Parameter.WorkingDirectory;
				postProcessor.StartInfo.Arguments = Parameter.Parameter;
				postProcessor.StartInfo.UseShellExecute = false;
				postProcessor.Start();
				postProcessor.WaitForExit();
				ExitCode = postProcessor.ExitCode;
			}
			catch (Exception ex)
			{
				Logger.Error("Error occurred while post-processing", ex);
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}

		public void OnTotalNotify(object? sender, IpcTotalNotifyEventArgs args)
		{
			if (args.Identifier != MyIdentifier)
				return;

			Indeterminate = false;
			TotalProgress = args.Total;
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
