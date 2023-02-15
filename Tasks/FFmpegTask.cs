using System.Diagnostics;
using Serilog;

// TODO: Add progress notification support to ExtractMemberPhase

namespace ParallelPixivUtil2.Tasks
{
	public class FFmpegTask : AbstractTask
	{
		private readonly string RequestedBy;
		private readonly string ExtractorWorkingDirectory;
		private readonly IEnumerable<string> Parameters;
		private readonly SemaphoreSlim? FFmpegSemaphore;

		public FFmpegTask(string requestedBy, string workingDirectory, IEnumerable<string> parameters, SemaphoreSlim? ffmpegSemaphore) : base("FFmpeg")
		{
			RequestedBy = requestedBy;
			ExtractorWorkingDirectory = workingDirectory;
			Parameters = parameters;
			FFmpegSemaphore = ffmpegSemaphore;
			Details = "FFmpeg execution requested by " + requestedBy;
		}

		protected override bool Run()
		{
			var exitCode = -1;
			try
			{
				var ffmpeg = new Process();
				ffmpeg.StartInfo.FileName = App.Configuration.FFmpeg.Executable;
				ffmpeg.StartInfo.WorkingDirectory = ExtractorWorkingDirectory;
				ffmpeg.StartInfo.UseShellExecute = false;
				ffmpeg.StartInfo.CreateNoWindow = true;
				foreach (var param in Parameters)
					ffmpeg.StartInfo.ArgumentList.Add(param.Trim('\"')); // The last failsafe: FFmpeg emits error if received parameter covered with commas
				ffmpeg.Start();
				ffmpeg.WaitForExit();
				exitCode = ffmpeg.ExitCode;
			}
			catch (Exception ex)
			{
				exitCode = ex.HResult;
				Log.Error(ex, "{0} | FFmpeg execution failed with exception.", RequestedBy);

				return true;
			}
			finally
			{
				FFmpegSemaphore?.Release();
				Log.Debug("{0} | FFmpeg execution exited with code {1}.", RequestedBy, exitCode);
			}

			ExitCode = exitCode;

			return false;
		}
	}
}
