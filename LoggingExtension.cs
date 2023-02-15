using Serilog;
using System.Diagnostics;
using System.IO;

namespace ParallelPixivUtil2;
public static class LoggingExtension
{
	public static void LogAndStart(this Process proc)
	{
		Log.Debug("Executing file='{File}' with args='{Arguments}' and workingDir='{WorkingDirectory}'", Path.GetFullPath(proc.StartInfo.FileName), proc.StartInfo.Arguments, proc.StartInfo.WorkingDirectory);
		proc.Start();
	}
}
