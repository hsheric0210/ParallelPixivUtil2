using System.Diagnostics;
using System.Text;

namespace ParallelPixivUtil2
{
	public class Program
	{
		private const string STDOUT_LOG_NAME = "{0:yyyy-MM-dd_HH-mm-ss-FFFFFF}-{1}-STDOUT.log";
		private const string STDERR_LOG_NAME = "{0:yyyy-MM-dd_HH-mm-ss-FFFFFF}-{1}-STDERR.log";
		private static readonly UTF8Encoding UTF_8_WITHOUT_BOM = new(false);
		private static readonly FileStreamOptions LOG_FILE_STREAM_OPTIONS = new()
		{
			Access = FileAccess.Write,
			Mode = FileMode.Create
		};

		public static int Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");

			Console.WriteLine($"DEBUG: Current console encoding is '{Console.OutputEncoding.EncodingName}'");

			if (!File.Exists("pixivutil2.exe"))
			{
				Console.WriteLine("ERROR: pixivutil2.exe is not located in working directory.");
				return 1;
			}

			if (!File.Exists("list.txt"))
			{
				Console.WriteLine("ERROR: list.txt is not located in working directory.");
				return 1;
			}

			if (!File.Exists("config.ini"))
			{
				Console.WriteLine("ERROR: config.ini is not located in working directory.");
				return 1;
			}

			if (!File.Exists("parallel.ini"))
			{
				var ini = new IniFile("parallel.ini");
				ini.write("maxParallellism", "5");
			}

			try
			{
				Console.WriteLine("Reading all lines of list.txt");
				string[] memberIds = File.ReadAllLines("list.txt");

				Console.WriteLine("Reading all lines of config.ini");
				string[] cfgLines = File.ReadAllLines("config.ini");

				if (!Directory.Exists("config"))
				{
					Console.WriteLine("Creating config directory");
					Directory.CreateDirectory("config");
				}

				Console.WriteLine("Writing member-specific config files (in parallel)");
				Parallel.ForEach(memberIds, memberId =>
				{
					var file = $"config\\{memberId}.ini";
					if (File.Exists(file))
						Console.WriteLine($"Config file already exists for {memberId}");
					else
					{
						Console.WriteLine($"Creating member-specific config for {memberId}");

						int lineCount = cfgLines.Length;
						string[] newListLines = new string[lineCount];
						for (int i = 0; i < lineCount; i++)
						{
							string line = cfgLines[i];
							string modifiedLine;
							if (line.StartsWith("dbPath"))
							{
								int eqindex = line.IndexOf('=');
								if (eqindex + 1 < line.Length && char.IsWhiteSpace(line[eqindex + 1]))
									eqindex++;
								modifiedLine = $"{line[..(eqindex + 1)]}.\\databases\\{memberId}.sqlite";
							}
							//else if (line.StartsWith("rootDirectory"))
							//{
							//	int eqindex = line.IndexOf('=');
							//	if (eqindex + 1 < line.Length && char.IsWhiteSpace(line[eqindex + 1]))
							//		eqindex++;
							//	modifiedLine = $"{line[..(eqindex + 1)]}.\\{memberId}";
							//}
							else
								modifiedLine = line;
							newListLines[i] = modifiedLine;
						}

						File.WriteAllLines(file, newListLines);
					}
				});

				if (!Directory.Exists("databases"))
				{
					Console.WriteLine("Creating databases directory");
					Directory.CreateDirectory("databases");
				}

				if (!Directory.Exists("logs"))
				{
					Console.WriteLine("Creating logs directory");
					Directory.CreateDirectory("logs");
				}

				var ini = new IniFile("parallel.ini");
				if (!int.TryParse(ini.read("maxParallellism"), out int parallellism))
					parallellism = 5;

				Console.WriteLine("Executing PixivUtil2 (in parallel)");

				Parallel.ForEach(memberIds, new ParallelOptions { MaxDegreeOfParallelism = parallellism }, memberId =>
				{
					DateTime now = DateTime.Now;
					var stdoutFile = $"logs\\{string.Format(STDOUT_LOG_NAME, now, memberId)}";
					var stderrFile = $"logs\\{string.Format(STDERR_LOG_NAME, now, memberId)}";

					Console.WriteLine($"Executing PixivUtil2 for {memberId}");

					try
					{
						var pixivutil2 = new Process();
						pixivutil2.StartInfo.FileName = "pixivutil2.exe";
						pixivutil2.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						pixivutil2.StartInfo.Arguments = $"-s 1 {memberId} -x -c \"config\\{memberId}.ini\"";
						pixivutil2.StartInfo.UseShellExecute = false;
						pixivutil2.StartInfo.RedirectStandardOutput = true;
						pixivutil2.StartInfo.RedirectStandardError = true;
						pixivutil2.Start();

						// Redirect STDOUT, STDERR
						var stdoutBuffer = new StringBuilder();
						var stderrBuffer = new StringBuilder();
						pixivutil2.OutputDataReceived += getBufferRedirectHandler(stdoutBuffer, false);
						pixivutil2.ErrorDataReceived += getBufferRedirectHandler(stderrBuffer, true);
						pixivutil2.BeginOutputReadLine();
						pixivutil2.BeginErrorReadLine();

						pixivutil2.WaitForExit();

						// Write log buffer to the file and print the path
						try
						{
							if (writeLog(stdoutFile, stdoutBuffer))
								Console.WriteLine($"STDOUT log for {memberId}: \"{stdoutFile}\"");
							if (writeLog(stderrFile, stderrBuffer))
								Console.WriteLine($"STDERR log for {memberId}: \"{stderrFile}\"");
						}
						catch (Exception ex)
						{
							Console.WriteLine($"ERROR: Failed to write log: {ex}");
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute pixivutil2: {ex}");
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: {ex}");
			}

			return 0;
		}

		private static bool writeLog(string path, StringBuilder sb)
		{
			if (sb.Length > 0)
			{
				var stream = new StreamWriter(path, UTF_8_WITHOUT_BOM, LOG_FILE_STREAM_OPTIONS);
				stream.Write(sb.ToString());
				stream.Close();

				return true;
			}

			return false;
		}

		private static DataReceivedEventHandler getBufferRedirectHandler(StringBuilder buffer, bool alsoConsole) => new((_, param) =>
		{
			string? data = param.Data;
			if (!string.IsNullOrEmpty(data) && !data.StartsWith('['))
			{
				StringComparison strCmpOpts = StringComparison.InvariantCultureIgnoreCase;
				if (data.StartsWith("Start downloading...", strCmpOpts))
				{
					data = data.Replace("?", "");
					int indexOfCompleted = data.LastIndexOf("Completed in", strCmpOpts);
					if (indexOfCompleted > 0 && !data.Contains("Creating directory", strCmpOpts))
						data = data[..21 /* "Start downloading... ".Length */] + data[indexOfCompleted..];
					else
						data = data[..20 /* "Start downloading...".Length */];
				}
				else if (data.StartsWith('?') && data.Contains("iB", strCmpOpts))
				{
					data = data.Replace("?", "");
					int indexOfCompleted = data.LastIndexOf("Completed in", strCmpOpts);
					if (indexOfCompleted > 0)
						data = data[indexOfCompleted..];
					else
						data = null;
				}

				if (data != null)
				{
					buffer.AppendLine(data);
					if (alsoConsole)
						Console.WriteLine(data);
				}
			}
		});
	}
}