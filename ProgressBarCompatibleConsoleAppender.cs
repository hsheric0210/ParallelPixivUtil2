using log4net.Appender;
using log4net.Core;
using ShellProgressBar;

namespace ParallelPixivUtil2
{
	public class ProgressBarCompatibleConsoleAppender : ConsoleAppender
	{
		public static ProgressBarBase? ProgressBar
		{
			get; set;
		}

		protected override void Append(LoggingEvent loggingEvent)
		{
			if (loggingEvent.Level.Value is >= 70000 and <= 120000)
				ProgressBar?.WriteErrorLine(RenderLoggingEvent(loggingEvent));
			else
				ProgressBar?.WriteLine(RenderLoggingEvent(loggingEvent));
		}
	}
}
