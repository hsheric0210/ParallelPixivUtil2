﻿using log4net;
using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using System.Diagnostics;

namespace ParallelPixivUtil2.Tasks
{
	public class RetrieveImageTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(DownloadImageTask));

		private readonly PixivUtil2Parameter Parameter;
		private readonly string? MyIdentifier;

		public RetrieveImageTask(PixivUtil2Parameter parameter) : base("Retrieve member image")
		{
			if (parameter.Member == null)
				throw new ArgumentException("parameter.Member can't be null when initializing " + nameof(RetrieveImageTask));

			Parameter = parameter;

			Details = $"Retrieve member image of {parameter.Member?.MemberID} page {parameter.Member?.Page}";

			MyIdentifier = parameter.Identifier;
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
				retriever.StartInfo.UseShellExecute = false;
				retriever.Start();
				retriever.WaitForExit();
				ExitCode = retriever.ExitCode;
			}
			catch (Exception ex)
			{
				Logger.Error("Error occurred while retrieveing member image", ex);
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
