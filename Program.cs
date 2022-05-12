using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace ParallelPixivUtil2
{
	public sealed class Program
	{
		private const string MemberDataListFileName = "memberdata.txt";

		private Program()
		{
		}

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

		public static async Task<int> Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");

			bool onlyPostProcessing = args.Length > 0 && args[0].Equals("onlypp", StringComparison.InvariantCultureIgnoreCase);

			var pythonSourceFileExists = File.Exists("pixivutil2.py");
			if (!pythonSourceFileExists && RequireExists("PixivUtil2.exe") || RequireExists("list.txt") || RequireExists("config.ini") || RequireExists("aria2c.exe"))
				return 1;

			try
			{
				Console.WriteLine("Reading all lines of list.txt");
				string[] memberIds = File.ReadAllLines("list.txt");

				CreateDirectoryIfNotExists("databases");
				CreateDirectoryIfNotExists("logs");
				CreateDirectoryIfNotExists("aria2");
				CreateDirectoryIfNotExists("aria2-logs");

				var config = new Config();

				// Extract URLs
				ExtractMemberDataList(pythonSourceFileExists, memberIds);

				if (!File.Exists(MemberDataListFileName))
					Console.WriteLine("ERROR: Failed to dump member informations (Dump file not found)");

				IDictionary<long, ICollection<MemberPage>> memberPageList = ParseMemberDataList(out int totalCount);

				int workerCount = Math.Max(config.MaxExtractorParallellism, Math.Max(config.MaxDownloaderParallellism, config.MaxPostprocessorParallellism));
				if (!ThreadPool.SetMinThreads(workerCount, workerCount))
					Console.WriteLine("WARN: Failed to set min thread pool workers");
				if (!ThreadPool.SetMaxThreads(workerCount, workerCount))
					Console.WriteLine("WARN: Failed to set max thread pool workers");

				if (!onlyPostProcessing)
				{
					Console.WriteLine("Extracting member images");
					using (var semaphore = new SemaphoreSlim(config.MaxExtractorParallellism))
					{
						await ExtractMemberImages(totalCount, memberPageList, semaphore, pythonSourceFileExists);
					}

					Console.WriteLine("Start downloading");
					using (var semaphore = new SemaphoreSlim(config.MaxDownloaderParallellism))
					{
						await DownloadImages(totalCount, memberPageList, semaphore, config.DownloaderParameters);
					}
				}

				Console.WriteLine("Start post-processing");
				using (var semaphore = new SemaphoreSlim(config.MaxPostprocessorParallellism))
				{
					await Postprocess(totalCount, memberPageList, semaphore, pythonSourceFileExists);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: {ex}");
			}

			return 0;
		}

		private static async Task Postprocess(int totalPageCount, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore, bool pythonSourceFileExists)
		{
			int remaining = totalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				tasks.AddRange(pages.Select(page => Task.Run(() =>
				{
					semaphore.Wait();
					Console.WriteLine($"Executing post-processor: '{memberId}.p{page.FileIndex}' (page {page.Page})");

					try
					{
						var postProcessor = new Process();
						postProcessor.StartInfo.FileName = pythonSourceFileExists ? "python.exe" : "PixivUtil2.exe";
						postProcessor.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						postProcessor.StartInfo.Arguments = $"{(pythonSourceFileExists ? "PixivUtil2.py" : "")} -s 1 {memberId} --sp={page.Page} --ep={page.Page} -x --db=\"databases\\{memberId}.p{page.FileIndex}.db\" -l \"logs\\{memberId}.p{page.FileIndex}.pp.log\"";
						postProcessor.StartInfo.UseShellExecute = true;
						postProcessor.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
						postProcessor.Start();
						postProcessor.WaitForExit();
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute post-processor: {ex}");
					}
					finally
					{
						semaphore.Release();
						Console.WriteLine($"Operation finished: '{memberId}.p{page.FileIndex}' (page {page.Page}); waiting for {Interlocked.Decrement(ref remaining)} remaining operations");
					}
				})));
			}

			await Task.WhenAll(tasks);
		}

		private static async Task DownloadImages(int totalPageCount, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore, string parameters)
		{
			int remaining = totalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				tasks.AddRange(pages.Select(page => Task.Run(() =>
				{
					semaphore.Wait();
					Console.WriteLine($"Executing downloader: '{memberId}.p{page.FileIndex}'");

					try
					{
						var downloader = new Process();
						downloader.StartInfo.FileName = "aria2c.exe";
						downloader.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						downloader.StartInfo.Arguments = $"-i \"aria2\\{memberId}.p{page.FileIndex}.txt\" -l \"aria2-logs\\{memberId}.p{page.FileIndex}.log\" {parameters}";
						downloader.StartInfo.UseShellExecute = true;
						downloader.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
						downloader.Start();
						downloader.WaitForExit();
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute downloader: {ex}");
					}
					finally
					{
						semaphore.Release();
						Console.WriteLine($"Operation finished: '{memberId}.p{page.FileIndex}'; waiting for {Interlocked.Decrement(ref remaining)} remaining operations");
					}
				})));
			}
			await Task.WhenAll(tasks);
		}

		private static async Task ExtractMemberImages(int totalPageCount, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore, bool pythonSourceFileExists)
		{
			int remaining = totalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				tasks.AddRange(pages.Select(page => Task.Run(() =>
				{
					semaphore.Wait();
					Console.WriteLine($"Executing extractor: '{memberId}.p{page.FileIndex}' (page {page.Page})");

					try
					{
						var extractor = new Process();
						extractor.StartInfo.FileName = pythonSourceFileExists ? "python.exe" : "PixivUtil2.exe";
						extractor.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
						extractor.StartInfo.Arguments = $"{(pythonSourceFileExists ? "PixivUtil2.py" : "")} -s 1 {memberId} --sp={page.Page} --ep={page.Page} -x --db=\"databases\\{memberId}.p{page.FileIndex}.db\" -l \"logs\\{memberId}.p{page.FileIndex}.log\" --aria2=\"aria2\\{memberId}.p{page.FileIndex}.txt\"";
						extractor.StartInfo.UseShellExecute = true;
						extractor.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
						extractor.Start();
						extractor.WaitForExit();
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: Failed to execute extractor: {ex}");
					}
					finally
					{
						semaphore.Release();
						Console.WriteLine($"Operation finished: '{memberId}.p{page.FileIndex}' (page {page.Page}); waiting for {Interlocked.Decrement(ref remaining)} remaining operations");
					}
				})));
			}

			await Task.WhenAll(tasks);
		}

		private static IDictionary<long, ICollection<MemberPage>> ParseMemberDataList(out int totalCount)
		{
			var memberPageList = new Dictionary<long, ICollection<MemberPage>>();
			totalCount = 0;
			foreach (string line in File.ReadAllLines(MemberDataListFileName))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				string[] pieces = line.Split(',');
				if (!long.TryParse(pieces[0], out long memberId) || !int.TryParse(pieces[1], out int totalImages))
					continue;

				if (totalImages > 0)
				{
					if (!memberPageList.ContainsKey(memberId))
						memberPageList[memberId] = new List<MemberPage>();

					const int maxImagesPerPage = 48;
					int pageCount = (totalImages - totalImages % maxImagesPerPage) / maxImagesPerPage + 1;
					for (int i = 1; i <= pageCount; i++)
						memberPageList[memberId].Add(new MemberPage(i, pageCount - i + 1));
					totalCount += pageCount;
				}
				else
				{
					Console.WriteLine($"Member {memberId} doesn't have any images! Skipping");
				}
			}

			return memberPageList;
		}

		private static void ExtractMemberDataList(bool pythonSourceFileExists, string[] memberIds)
		{
			try
			{
				var extractor = new Process();
				extractor.StartInfo.FileName = pythonSourceFileExists ? "Python.exe" : "PixivUtil2.exe";
				extractor.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
				extractor.StartInfo.Arguments = $"{(pythonSourceFileExists ? "PixivUtil2.py" : "")} -s q {MemberDataListFileName} {string.Join(' ', memberIds)} -x -l \"logs\\dumpMembers.log\"";
				extractor.StartInfo.UseShellExecute = true;
				extractor.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
				extractor.Start();
				extractor.WaitForExit();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: Failed to execute PixivUtil2: {ex}");
			}
		}
	}

	public sealed record MemberPage(int Page, int FileIndex);
}
