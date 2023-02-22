using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using Serilog;
using System.Diagnostics;

namespace ParallelPixivUtil2.Tasks
{
	public class RetrieveImageTask : AbstractTask
	{
		private readonly PixivUtil2Parameter Parameter;
		private readonly string? MyIdentifier;

		public RetrieveImageTask(PixivUtil2Parameter parameter) : base("Retrieve member image")
		{
			Parameter = parameter;

			Details = $"Retrieve member image of {parameter.Page.MemberId} page {parameter.Page.Page} (File index {parameter.Page.FileIndex})";

			MyIdentifier = parameter.Identifier;
			IpcManager.OnIpcTotalNotify += OnTotalNotify;
			IpcManager.OnIpcProcessNotify += OnProcessNotify;
		}

		protected override bool Run()
		{
			try
			{
				var show = App.Configuration.Extractor.ShowWindow;

				var retriever = new Process();
				retriever.StartInfo.FileName = Parameter.FileName;
				retriever.StartInfo.WorkingDirectory = Parameter.WorkingDirectory;
				retriever.StartInfo.Arguments = Parameter.Parameter;
				retriever.StartInfo.UseShellExecute = show;
				retriever.StartInfo.CreateNoWindow = !show;
				retriever.LogAndStart();
				retriever.WaitForExit();
				ExitCode = retriever.ExitCode;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error occurred while retrieveing member image");
				Details = $"Error: '{ex.Message}' (see log for details)";
				return true;
			}

			return false;
		}

		public void OnTotalNotify(object? sender, IpcTotalNotifyEventArgs args)
		{
			if (args.Identifier != MyIdentifier)
				return;
			Log.Debug("Total image count of {ident} is {count}.", MyIdentifier, args.Total);
			TotalProgress = args.Total;
			Indeterminate = false;
		}

		public void OnProcessNotify(object? sender, IpcEventArgs args)
		{
			if (args.Identifier == MyIdentifier)
				CurrentProgress++;
		}

		public override void Dispose()
		{
			IpcManager.OnIpcTotalNotify -= OnTotalNotify;
			IpcManager.OnIpcProcessNotify -= OnProcessNotify;
		}
	}
}
