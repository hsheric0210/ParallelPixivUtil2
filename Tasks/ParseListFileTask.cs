using System.IO;
using Serilog;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class ParseListFileTask : AbstractTask
	{
		public string[]? Lines
		{
			get; private set;
		}

		public ParseListFileTask() : base("Parsing list file")
		{
		}

		protected override bool Run()
		{
			try
			{
				Lines = File.ReadAllLines(App.Configuration.ListFileName);
			}
			catch (Exception e)
			{
				Log.Error(e, "Exception occurred while parsing all lines from list file.");
				return true;
			}
			return false;
		}
	}
}
