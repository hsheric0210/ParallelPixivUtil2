using log4net;
using System.Diagnostics;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class FFmpegTask : AbstractTask
	{
		private static readonly ILog Logger = LogManager.GetLogger(nameof(FFmpegTask));

		private readonly string RequestedBy;
		private readonly string ExtractorWorkingDirectory;
		private readonly IEnumerable<string> Parameters;
		private readonly SemaphoreSlim? FFmpegSemaphore;

		public FFmpegTask(string requestedBy, string workingDirectory, IEnumerable<string> parameters, SemaphoreSlim? ffmpegSemaphore) : base("FFmpeg execution requested by " + requestedBy)
		{
			RequestedBy = requestedBy;
			ExtractorWorkingDirectory = workingDirectory;
			Parameters = parameters;
			FFmpegSemaphore = ffmpegSemaphore;
		}

		protected override bool Run()
		{
			int exitCode = -1;
			try
			{
				var ffmpeg = new Process();
				ffmpeg.StartInfo.FileName = App.Configuration.FFmpegExecutable;
				ffmpeg.StartInfo.WorkingDirectory = ExtractorWorkingDirectory;
				ffmpeg.StartInfo.UseShellExecute = false;
				ffmpeg.StartInfo.CreateNoWindow = true;
				foreach (string param in Parameters)
					ffmpeg.StartInfo.ArgumentList.Add(param);
				ffmpeg.Start();
				ffmpeg.WaitForExit();
				exitCode = ffmpeg.ExitCode;
			}
			catch (Exception ex)
			{
				exitCode = ex.HResult;
				Logger.Error(string.Format("{0} | FFmpeg execution failed with exception.", RequestedBy), ex);

				return true;
			}
			finally
			{
				FFmpegSemaphore?.Release();
				Logger.DebugFormat("{0} | FFmpeg execution exited with code {1}.", RequestedBy, exitCode);
			}

			ExitCode = exitCode;

			return false;
		}
	}
}
