using System.Diagnostics;

namespace ParallelPixivUtil2
{
	public class Program
	{
		private static bool RequireExists(string fileName)
		{
			if (!File.Exists(fileName))
			{
				Console.WriteLine($"ERROR: {fileName} is not located in working directory.");
				return true;
			}
			return false;
		}

		private static void CreateDirectoryIfNotExists(string dirName)
		{
			if (!Directory.Exists(dirName))
			{
				Console.WriteLine($"Creating {dirName} directory");
				Directory.CreateDirectory(dirName);
			}
		}

		public static int Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");

			Console.WriteLine($"DEBUG: Current console encoding is '{Console.OutputEncoding.EncodingName}'");

			var py = File.Exists("pixivutil2.py");
			if (!py && RequireExists("PixivUtil2.exe") || RequireExists("list.txt") || RequireExists("config.ini") || RequireExists("aria2c.exe"))
				return 1;

			if (!File.Exists("parallel.ini"))
			{
				var ini = new IniFile("parallel.ini");
				ini.Write("MaxPixivUtil2Parallellism", "5");
				ini.Write("MaxAria2Parallellism", "5");
				ini.Write("Aria2Parameters", "--allow-overwrite=true --conditional-get=true --remote-time=true --auto-file-renaming=false --auto-save-interval=10 -j16 -x2");
			}

			try
			{
				Console.WriteLine("Reading all lines of list.txt");
				string[] memberIds = File.ReadAllLines("list.txt");

				CreateDirectoryIfNotExists("databases");
				CreateDirectoryIfNotExists("logs");
				CreateDirectoryIfNotExists("aria2");
				CreateDirectoryIfNotExists("aria2-logs");

				// Dump member informations
				try
				{
					var pixivutil2 = new Process();
					pixivutil2.StartInfo.FileName = py ? "Python.exe" : "PixivUtil2.exe";
					pixivutil2.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
					pixivutil2.StartInfo.Arguments = $"{(py ? "PixivUtil2.py" : "")} -s q memberdata.txt {string.Join(' ', memberIds)} -x -l \"logs\\dumpMembers.log\"";
					pixivutil2.StartInfo.UseShellExecute = true;
					pixivutil2.Start();
					pixivutil2.WaitForExit();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"ERROR: Failed to execute PixivUtil2: {ex}");
				}

				if (!File.Exists("memberdata.txt"))
					Console.WriteLine($"ERROR: Failed to dump member informations (Dump file not found)");

				var memberPageList = new List<(long, int, int)>();

				foreach (string line in File.ReadAllLines("memberdata.txt"))
				{
					if (string.IsNullOrWhiteSpace(line))
						continue;

					string[] pieces = line.Split(',');
					if (!long.TryParse(pieces[0], out long memberId) || !int.TryParse(pieces[1], out int totalImages))
						continue;

					if (totalImages > 0)
					{
						int maxImagesPerPage = 48;
						int pageCount = (totalImages - totalImages % maxImagesPerPage) / maxImagesPerPage + 1;
						for (int i = 1; i <= pageCount; i++)
							memberPageList.Add((memberId, i, pageCount - i + 1));
					}
					else
						Console.WriteLine($"Member {memberId} doesn't have any images! Skipping");
				}

				var ini = new IniFile("parallel.ini");
				if (!int.TryParse(ini.Read("MaxPixivUtil2Parallellism"), out int pixivutil2Parallellism) && !int.TryParse(ini.Read("MaxParallellism"), out pixivutil2Parallellism))
					pixivutil2Parallellism = 5;

				if (!int.TryParse(ini.Read("MaxAria2Parallellism"), out int aria2Parallellism) && !int.TryParse(ini.Read("MaxParallellism"), out pixivutil2Parallellism))
					aria2Parallellism = 5;

				var aria2Parameters = ini.Read("Aria2Parameters");

				Console.WriteLine("Start creating aria2 input list");

				TaskScheduler pixivutil2Scheduler = new LimitedConcurrencyLevelTaskScheduler(pixivutil2Parallellism);
				TaskScheduler aria2Scheduler = new LimitedConcurrencyLevelTaskScheduler(aria2Parallellism);

				int startIndex = memberPageList.Count;
				int finishIndex = memberPageList.Count;
				Parallel.ForEach(memberPageList, new ParallelOptions { TaskScheduler = pixivutil2Scheduler }, (member, _, _) =>
				{
					(long memberId, int page, int fileIndex) = member;

					Interlocked.Decrement(ref startIndex);
					Console.WriteLine($"Executing PixivUtil2: '{memberId}.p{fileIndex}' (page {page}); {startIndex} operations are remain");

					try
					{
						var pixivutil2 = new Process();
						pixivutil2.StartInfo.FileName = py ? "python.exe" : "PixivUtil2.exe";
						pixivutil2.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						pixivutil2.StartInfo.Arguments = $"{(py ? "PixivUtil2.py" : "")} -s 1 {memberId} --sp={page} --ep={page} -x --db=\"databases\\{memberId}.p{fileIndex}.db\" -l \"logs\\{memberId}.p{fileIndex}.log\" --aria2=\"aria2\\{memberId}.p{fileIndex}.txt\"";
						pixivutil2.StartInfo.UseShellExecute = true;
						pixivutil2.Start();
						pixivutil2.WaitForExit();
						Interlocked.Decrement(ref finishIndex);
						Console.WriteLine($"Operation finished: '{memberId}.p{fileIndex}' (page {page}); waiting for {finishIndex} remaining operations");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute PixivUtil2: {ex}");
					}
				});

				Console.WriteLine("Start downloading");

				startIndex = memberPageList.Count;
				finishIndex = memberPageList.Count;
				Parallel.ForEach(memberPageList, new ParallelOptions { TaskScheduler = aria2Scheduler }, (member, _, _) =>
				{
					(long memberId, int page, int fileIndex) = member;

					Interlocked.Decrement(ref startIndex);
					Console.WriteLine($"Executing Aria2: '{memberId}.p{fileIndex}'; {startIndex} operations are remain");

					try
					{
						var aria2 = new Process();
						aria2.StartInfo.FileName = "aria2c.exe";
						aria2.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						aria2.StartInfo.Arguments = $"-i \"aria2\\{memberId}.p{fileIndex}.txt\" -l \"aria2-logs\\{memberId}.p{fileIndex}.log\" {aria2Parameters}";
						aria2.StartInfo.UseShellExecute = true;
						aria2.Start();
						aria2.WaitForExit();
						Interlocked.Decrement(ref finishIndex);
						Console.WriteLine($"Operation finished: '{memberId}.p{fileIndex}'; waiting for {finishIndex} remaining operations");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute PixivUtil2: {ex}");
					}
				});

				Console.WriteLine("Start post-processing");

				startIndex = memberPageList.Count;
				finishIndex = memberPageList.Count;
				Parallel.ForEach(memberPageList, new ParallelOptions { TaskScheduler = pixivutil2Scheduler }, (member, _, _) =>
				{
					(long memberId, int page, int fileIndex) = member;

					Interlocked.Decrement(ref startIndex);
					Console.WriteLine($"Executing PixivUtil2: '{memberId}.p{fileIndex}' (page {page}); {startIndex} operations are remain");

					try
					{
						var pixivutil2 = new Process();
						pixivutil2.StartInfo.FileName = py ? "python.exe" : "PixivUtil2.exe";
						pixivutil2.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						pixivutil2.StartInfo.Arguments = $"{(py ? "PixivUtil2.py" : "")} -s 1 {memberId} --sp={page} --ep={page} -x --db=\"databases\\{memberId}.p{fileIndex}.db\" -l \"logs\\{memberId}.p{fileIndex}.pp.log\"";
						pixivutil2.StartInfo.UseShellExecute = true;
						pixivutil2.Start();
						pixivutil2.WaitForExit();
						Interlocked.Decrement(ref finishIndex);
						Console.WriteLine($"Operation finished: '{memberId}.p{fileIndex}' (page {page}); waiting for {finishIndex} remaining operations");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute PixivUtil2: {ex}");
					}
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: {ex}");
			}

			return 0;
		}
	}
}
