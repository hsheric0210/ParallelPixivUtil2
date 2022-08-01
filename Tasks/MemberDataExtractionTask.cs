using ParallelPixivUtil2.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class MemberDataExtractionTask : AbstractTask
	{
		private readonly PixivUtil2Parameter Parameter;

		public MemberDataExtractionTask(PixivUtil2Parameter parameter) : base("Retrieve member data") => Parameter = parameter;

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
