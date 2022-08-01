using log4net;
using NetMQ;
using ParallelPixivUtil2.Ipc;
using ParallelPixivUtil2.Parameters;
using ShellProgressBar;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

// TODO: Remove ShellProgressBar; Convert to WPF
namespace ParallelPixivUtil2
{
	public sealed class ParallelPixivUtil2Main
	{
		public const string ProgramName = "ParallelPixivUtil2";

		public static readonly ILog MainLogger = LogManager.GetLogger("Main");
		public static readonly ILog IPCLogger = LogManager.GetLogger("IPC");

		private static readonly IDictionary<byte[], string> IPCIdentifiers = new Dictionary<byte[], string>();
		private static readonly IDictionary<byte[], ChildProgressBar> IPCProgressBars = new Dictionary<byte[], ChildProgressBar>();
		private static readonly IDictionary<string, string> IPCProgressBarMessages = new Dictionary<string, string>();
		private static readonly IDictionary<int, Task<int>> FFmpegTasks = new Dictionary<int, Task<int>>();
		private static readonly IDictionary<string, IList<string>> DownloadInputQueue = new ConcurrentDictionary<string, IList<string>>();

		private static string CurrentPhaseName = "Unknown";
		private static int ProcessedImageCount;
		private static int TotalImageCount;

		private ParallelPixivUtil2Main()
		{
		}

		private static void FlushDownloadInputQueue()
		{
			MainLogger.Debug("Processing queued download input list...");
			var bar = ProgressBarUtils.SpawnIndeterminateChild("Flushing aria2 input buffer");

			var watch = new Stopwatch();
			watch.Start();

			var CopyQueue = new Dictionary<string, IList<string>>(DownloadInputQueue);
			DownloadInputQueue.Clear();

			Task.WhenAll(CopyQueue.Select((pair) =>
				Task.Run(() =>
				{
					var builder = new StringBuilder();
					foreach (string? item in pair.Value)
					{
						builder.Append(item);
					}
					File.AppendAllText(pair.Key, builder.ToString());
				}))).Wait();

			watch.Stop();
			MainLogger.DebugFormat("Processed queued download input list: Took {0}ms", watch.ElapsedMilliseconds);
			bar?.Finished();
		}

		private static void PhaseChange(string phaseName, int totalImageCount)
		{
			CurrentPhaseName = phaseName;
			ProcessedImageCount = 0;
			TotalImageCount = totalImageCount;
		}

		private static bool RequireExists(string fileName)
		{
			if (!File.Exists(fileName))
			{
				MainLogger.ErrorFormat("{0} is not located in working directory.", fileName);
				return true;
			}
			return false;
		}

		private static void CreateDirectoryIfNotExists(string dirName)
		{
			if (!Directory.Exists(dirName))
			{
				MainLogger.WarnFormat("Creating {0} directory as it is not exists.", dirName);
				Directory.CreateDirectory(dirName);
			}
		}

		public static async Task<int> Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");

			ProgressBarUtils.SetGlobal(5, "Preparing");

			bool onlyPostProcessing = args.Length > 0 && args[0].Equals("onlypp", StringComparison.OrdinalIgnoreCase);

			var config = new Config();

			string extractorExe = config.ExtractorExecutable;
			string extractorPy = config.ExtractorScript;
			string ipcCommAddr = $"tcp://localhost:{config.IPCCommPort}";
			string ipcTaskAddr = $"tcp://localhost:{config.IPCTaskPort}";
			bool pythonSourceFileExists = File.Exists(extractorPy);
			if (!pythonSourceFileExists && RequireExists(extractorExe))
				return 1;

			string? extractorWorkingDirectory = Path.GetDirectoryName(pythonSourceFileExists ? extractorPy : extractorExe);
			if (string.IsNullOrWhiteSpace(extractorWorkingDirectory))
			{
				MainLogger.ErrorFormat("Extractor working directory not available for extractor {0}.", pythonSourceFileExists ? extractorPy : extractorExe);
				return 1;
			}

			string listFile = config.ListFile;
			if (RequireExists(listFile) || RequireExists($"{extractorWorkingDirectory}\\config.ini") || RequireExists(config.DownloaderExecutable))
				return 1;

			ProgressBarUtils.TickGlobal("Thread-pool setup");
			int workerCount = Math.Max(config.MaxExtractorParallellism, Math.Max(config.MaxDownloaderParallellism, config.MaxPostprocessorParallellism)) + config.MaxFFmpegParallellism + 4;
			if (ThreadPool.SetMinThreads(workerCount, workerCount))
				MainLogger.Info($"Min thread pool workers: {workerCount}");
			else
				MainLogger.Warn("Failed to set min thread pool workers.");

			if (ThreadPool.SetMaxThreads(workerCount, workerCount))
				MainLogger.Info($"Max thread pool workers: {workerCount}");
			else
				MainLogger.Warn("Failed to set max thread pool workers.");

			try
			{
				var ffmpegSemaphore = new SemaphoreSlim(config.MaxFFmpegParallellism);

				ProgressBarUtils.TickGlobal("IPC communication socket initialization");

				CreateDirectoryIfNotExists(config.LogPath);
				CreateDirectoryIfNotExists(config.Aria2InputPath);
				CreateDirectoryIfNotExists(config.DatabasePath);

				ProgressBarUtils.TickGlobal($"Reading all lines of {listFile}");
				MainLogger.DebugFormat("Reading all lines of {0}", listFile);
				string[] memberIds = File.ReadAllLines(listFile);

				var extractor = new ExtractorRecord(extractorExe, extractorPy, pythonSourceFileExists, extractorWorkingDirectory, ipcCommAddr, ipcTaskAddr, config.LogPath, config.Aria2InputPath, config.DatabasePath);

				ProgressBarUtils.TickGlobal("Retrieveing member data");
				// Extract URLs
				RetrieveMemberDataList(extractor, config.MemberDataListParameters, config.MemberDataListFile, memberIds);

				if (!File.Exists(config.MemberDataListFile))
				{
					MainLogger.Error("Failed to dump member informations. (Member dump file not found)");
					return 1;
				}

				ProgressBarUtils.TickGlobal("Parsing member data");
				IDictionary<long, ICollection<MemberPage>> memberPageList = ParseMemberDataList(config.MemberDataListFile, config.MaxImagesPerPage, out int totalImageCount, out int totalPageCount);

				extractor.TotalPageCount = totalPageCount;
				var downloader = new DownloaderRecord(totalPageCount, config.DownloaderExecutable, extractorWorkingDirectory, config.LogPath, config.Aria2InputPath, config.DatabasePath);

				if (!onlyPostProcessing)
				{
					ProgressBarUtils.SetGlobal(totalImageCount, "Extracting member images");
					MainLogger.Debug("Extracting member images.");
					PhaseChange("Extraction", totalImageCount);
					BeginFlushDownloadInputQueueTimer(config.DownloadInputDelay, config.DownloadInputPeriod);
					using (var semaphore = new SemaphoreSlim(config.MaxExtractorParallellism))
					{
						await RetrieveMemberImages(extractor, config.ExtractorParameters, memberPageList, semaphore);
					}
					EndFlushDownloadInputQueueTimer();

					ProgressBarUtils.SetGlobal(totalPageCount, "Post-processing");
					MainLogger.Debug("Start downloading.");
					Console.Title = "Downloading phase";
					using (var semaphore = new SemaphoreSlim(config.MaxDownloaderParallellism))
					{
						await DownloadImages(downloader, config.DownloaderParameters, memberPageList, semaphore);
					}
				}

				ProgressBarUtils.SetGlobal(totalImageCount, "Post-processing");
				MainLogger.Debug("Start post-processing.");
				PhaseChange("Post-processing", totalImageCount);
				using (var semaphore = new SemaphoreSlim(config.MaxPostprocessorParallellism))
				{
					await Postprocess(extractor, config.PostprocessorParameters, memberPageList, semaphore);
				}
			}
			catch (Exception ex)
			{
				MainLogger.Error("Error occurred while processing.", ex);
			}

			return 0;
		}

		private static async Task Postprocess(ExtractorRecord extractor, string parameters, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore)
		{
			int remainingOperationCount = extractor.TotalPageCount;

			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach (MemberPage page in pages)
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.DebugFormat("Post-processing started: '{0}.p{1}'. (page {2})", memberId, page.FileIndex, page.Page);

						var postProcessor = new Process();
						try
						{
							string ident = $"{memberId}_page{page.Page}";
							IPCProgressBarMessages[ident] = $"Member {memberId} Page {page.Page}";

							postProcessor.StartInfo.FileName = extractor.Executable;
							postProcessor.StartInfo.WorkingDirectory = extractor.ExtractorWorkingDir;
							postProcessor.StartInfo.Arguments = extractor.ExtraArguments + FormatTokens(parameters, new Dictionary<string, string>
							{
								["memberID"] = memberId.ToString(),
								["page"] = page.Page.ToString(),
								["fileIndex"] = page.FileIndex.ToString(),
								["ipcAddress"] = ident + '|' + extractor.IPCCommAddress + '|' + extractor.IPCTaskAddress,
								["logPath"] = extractor.LogPath,
								["aria2InputPath"] = extractor.Aria2InputPath,
								["databasePath"] = extractor.DatabasePath
							});
							postProcessor.StartInfo.UseShellExecute = false;
							postProcessor.StartInfo.CreateNoWindow = true;
							postProcessor.Start();
							postProcessor.WaitForExit();
						}
						catch (Exception ex)
						{
							MainLogger.Error("Failed to execute post-processor.", ex);
						}
						finally
						{
							semaphore.Release();
							MainLogger.InfoFormat("Post-processing finished (Exit code {0}): '{1}.p{2}' (page {3}); waiting for {4} remaining operations.", postProcessor.ExitCode, memberId, page.FileIndex, page.Page, Interlocked.Decrement(ref remainingOperationCount));
							ProgressBarUtils.TickGlobal($"Download finished for member {memberId} page {page.Page}");
						}
					}));
				}
			}

			await Task.WhenAll(tasks);
		}

		private static async Task DownloadImages(DownloaderRecord downloaderOpts, string parameters, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore)
		{
			int remainingOperationCount = downloaderOpts.TotalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach ((int nPage, int fileIndex) in pages)
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.DebugFormat("Downloading started: '{0}.p{1}'.", memberId, fileIndex);

						var downloader = new Process();
						try
						{
							downloader.StartInfo.FileName = downloaderOpts.Executable;
							downloader.StartInfo.WorkingDirectory = downloaderOpts.ExtractorWorkingDir;
							downloader.StartInfo.Arguments = FormatTokens(parameters, new Dictionary<string, string>
							{
								["memberID"] = memberId.ToString(),
								["fileIndex"] = fileIndex.ToString(),
								["logPath"] = downloaderOpts.LogPath,
								["aria2InputPath"] = downloaderOpts.Aria2InputPath,
								["databasePath"] = downloaderOpts.DatabasePath
							});
							downloader.StartInfo.UseShellExecute = true;
							downloader.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
							downloader.Start();
							downloader.WaitForExit();
						}
						catch (Exception ex)
						{
							MainLogger.Error("Failed to execute downloader.", ex);
						}
						finally
						{
							semaphore.Release();
							MainLogger.InfoFormat("Downloading finished (Exit code {0}): '{1}.p{2}'; waiting for {3} remaining operations.", downloader.ExitCode, memberId, fileIndex, Interlocked.Decrement(ref remainingOperationCount));
							ProgressBarUtils.TickGlobal($"Download finished for member {memberId} page {nPage}");
						}
					}));
				}
			}

			await Task.WhenAll(tasks);
		}

		private static async Task RetrieveMemberImages(ExtractorRecord extractor, string parameters, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore)
		{
			int remaining = extractor.TotalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach (MemberPage page in pages)
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.InfoFormat("Extraction started: '{0}.p{1}'. (page {2})", memberId, page.FileIndex, page.Page);

						var retriever = new Process();
						try
						{
							string ident = $"{memberId}_page{page.Page}";
							IPCProgressBarMessages[ident] = $"Member {memberId} Page {page.Page}";

							retriever.StartInfo.FileName = extractor.Executable;
							retriever.StartInfo.WorkingDirectory = extractor.ExtractorWorkingDir;
							retriever.StartInfo.Arguments = extractor.ExtraArguments + FormatTokens(parameters, new Dictionary<string, string>
							{
								["memberID"] = memberId.ToString(),
								["page"] = page.Page.ToString(),
								["fileIndex"] = page.FileIndex.ToString(),
								["ipcAddress"] = ident + '|' + extractor.IPCCommAddress + '|' + extractor.IPCTaskAddress,
								["logPath"] = extractor.LogPath,
								["aria2InputPath"] = extractor.Aria2InputPath,
								["databasePath"] = extractor.DatabasePath
							});
							retriever.StartInfo.UseShellExecute = true;
							retriever.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
							retriever.Start();
							retriever.WaitForExit();
						}
						catch (Exception ex)
						{
							MainLogger.Error("Failed to execute extractor.", ex);
						}
						finally
						{
							semaphore.Release();
							MainLogger.InfoFormat("Extraction finished (Exit code {0}): '{1}.p{2}' (page {3}); waiting for {4} remaining operations.", retriever.ExitCode, memberId, page.FileIndex, page.Page, Interlocked.Decrement(ref remaining));
							ProgressBarUtils.TickGlobal($"Retrieveing finished for member {memberId} page {page.Page}");
						}
					}));
				}
			}

			await Task.WhenAll(tasks);
		}

		private static IDictionary<long, ICollection<MemberPage>> ParseMemberDataList(string memberDataList, int maxImagesPerPage, out int totalImageCount, out int totalPageCount)
		{
			var memberPageList = new Dictionary<long, ICollection<MemberPage>>();
			totalImageCount = 0;
			totalPageCount = 0;
			foreach (string line in File.ReadAllLines(memberDataList))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				string[] pieces = line.Split(',');
				if (!long.TryParse(pieces[0], out long memberId) || !int.TryParse(pieces[1], out int memberTotalImages))
					continue;

				if (memberTotalImages > 0)
				{
					if (!memberPageList.ContainsKey(memberId))
						memberPageList[memberId] = new List<MemberPage>();

					int pageCount = (memberTotalImages - memberTotalImages % maxImagesPerPage) / maxImagesPerPage + 1;
					for (int i = 1; i <= pageCount; i++)
						memberPageList[memberId].Add(new MemberPage(i, pageCount - i + 1));
					totalPageCount += pageCount;
					totalImageCount += memberTotalImages;
					MainLogger.DebugFormat("Member {0} has {1} images -> {2} pages.", memberId, memberTotalImages, pageCount);
				}
				else
				{
					MainLogger.WarnFormat("Member {0} doesn't have any images! Skipping.", memberId);
				}
			}

			return memberPageList;
		}

		private static void RetrieveMemberDataList(ExtractorRecord extractor, string parameters, string memberDataFile, string[] memberIds)
		{
			try
			{
				var retriever = new Process();
				retriever.StartInfo.FileName = extractor.Executable;
				retriever.StartInfo.WorkingDirectory = extractor.ExtractorWorkingDir;
				retriever.StartInfo.Arguments = extractor.ExtraArguments + FormatTokens(parameters, new Dictionary<string, string>
				{
					["memberDataList"] = memberDataFile,
					["memberIDs"] = string.Join(' ', memberIds),
					["logPath"] = extractor.LogPath,
					["aria2InputPath"] = extractor.Aria2InputPath,
					["databasePath"] = extractor.DatabasePath
				});
				retriever.StartInfo.UseShellExecute = true;
				retriever.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
				retriever.Start();
				retriever.WaitForExit();
			}
			catch (Exception ex)
			{
				MainLogger.Error("Failed to execute member-data extractor.", ex);
			}
		}

		private static string FormatTokens(string format, IDictionary<string, string> tokens)
		{
			foreach (KeyValuePair<string, string> token in tokens)
				format = format.Replace($"${{{token.Key}}}", token.Value);
			return format;
		}
	}

	public sealed record ExtractorRecord(string ExecutableExe, string ExecutablePy, bool PyExists, string ExtractorWorkingDir, string IPCCommAddress, string IPCTaskAddress, string LogPath, string Aria2InputPath, string DatabasePath)
	{
		public int TotalPageCount
		{
			get; set;
		} = -1;

		public string Executable => PyExists ? "Python.exe" : ExecutableExe;

		public string ExtraArguments => PyExists ? $"{ExecutablePy} " : "";
	}

	public sealed record DownloaderRecord(int TotalPageCount, string Executable, string ExtractorWorkingDir, string LogPath, string Aria2InputPath, string DatabasePath);
}
