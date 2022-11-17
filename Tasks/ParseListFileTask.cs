using log4net;
using System.IO;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class ParseListFileTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(ParseListFileTask));
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
				Logger.Error("Exception occurred while parsing all lines from list file.", e);
				return true;
			}
			return false;
		}
	}
}
