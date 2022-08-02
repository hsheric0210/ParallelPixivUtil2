﻿using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using System.Diagnostics;

namespace ParallelPixivUtil2.Tasks
{
	public class RetrieveImageTask : AbstractTask
	{
		private readonly PixivUtil2Parameter Parameter;
		private readonly string? MyIdentifier;

		public RetrieveImageTask(PixivUtil2Parameter parameter) : base("Retrieve member image")
		{
			if (parameter.Member == null)
				throw new ArgumentException("parameter.Member can't be null when initializing " + nameof(RetrieveImageTask));
			if (parameter.Ipc == null)
				throw new ArgumentException("parameter.Ipc can't be null when initializing " + nameof(RetrieveImageTask));

			Parameter = parameter;

			Details = $"Retrieve member image of {parameter.Member?.MemberID} page {parameter.Member?.Page}";

			MyIdentifier = parameter.Ipc?.Identifier;
			IpcManager.OnIpcTotalNotify += OnTotalNotify;
			IpcManager.OnIpcProcessNotify += OnProcessNotify;
		}

		protected override bool Run()
		{
			try
			{
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
				ParallelPixivUtil2Main.MainLogger.Error("Error occurred while retrieveing member image", ex);
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
