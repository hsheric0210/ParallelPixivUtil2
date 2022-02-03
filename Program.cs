using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ParallelPixivUtil2
{
	public class Program
	{
		public static int Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");
			Console.WriteLine("Note that this program should located NEXT TO pixivutil2.exe");
			Console.WriteLine("I'd recommend you do not use the MAIN pixiv account. It could be suspended due unnatural activities. (DDoS)");

			if (!File.Exists("pixivutil2.exe"))
			{
				Console.WriteLine("ERROR: pixivutil2.exe is not located in working directory.");
				return 1;
			}

			if (!File.Exists("list.txt"))
			{
				Console.WriteLine("ERROR: list.lst is not located in working directory.");
				return 1;
			}

			if (!File.Exists("config.ini"))
			{
				Console.WriteLine("WARN: config.ini is not located in working directory.");
				return 1;
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

				Console.WriteLine("Executing PixivUtil2 (in parallel)");
				Parallel.ForEach(memberIds, new ParallelOptions { MaxDegreeOfParallelism = 8 }, memberId =>
				{
					try
					{
						var pixivutil2 = new Process();
						pixivutil2.StartInfo.FileName = "pixivutil2.exe";
						pixivutil2.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						pixivutil2.StartInfo.Arguments = $"-s 1 {memberId} -x -c \"config\\{memberId}.ini\"";
						pixivutil2.StartInfo.UseShellExecute = true;
						pixivutil2.Start();
						pixivutil2.WaitForExit();
					}
					catch (Exception ex2)
					{
						Console.WriteLine("ERROR: Failed to execute pixivutil2:" + ex2.ToString());
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine("ERROR:" + ex.ToString());
			}

			return 0;
		}
	}
}
