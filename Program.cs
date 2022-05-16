using System.Diagnostics;
using NetMQ;
using log4net;

namespace ParallelPixivUtil2
{
	public sealed class Program
	{
		public const string ProgramName = "ParallelPixivUtil2";
		private const string MemberDataListFileName = "memberdata.txt";
		private const string ListFileName = "list.txt";
		const string IPCSocketAddress = "tcp://localhost:6974";

		private static readonly ILog MainLogger = LogManager.GetLogger("Main");
		private static readonly ILog IPCLogger = LogManager.GetLogger("IPC");

		private Program()
		{
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

			bool onlyPostProcessing = args.Length > 0 && args[0].Equals("onlypp", StringComparison.InvariantCultureIgnoreCase);

			var config = new Config();

			string? workingDirectory = config.ExtractorWorkingDirectory;
			if (string.IsNullOrWhiteSpace(workingDirectory))
				workingDirectory = Directory.GetCurrentDirectory();
			else if (workingDirectory.EndsWith('\\'))
				workingDirectory = Path.TrimEndingDirectorySeparator(workingDirectory);

			var pythonSourceFileExists = File.Exists($"{workingDirectory}\\PixivUtil2.py");
			if (!pythonSourceFileExists && RequireExists($"{workingDirectory}\\PixivUtil2.exe") || RequireExists(ListFileName) || RequireExists($"{workingDirectory}\\config.ini") || RequireExists($"{workingDirectory}\\{config.DownloaderLocation}"))
				return 1;

			int workerCount = Math.Max(config.MaxExtractorParallellism, Math.Max(config.MaxDownloaderParallellism, config.MaxPostprocessorParallellism)) + config.MaxFFmpegParallellism + 4;
			if (!ThreadPool.SetMinThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set min thread pool workers.");
			if (!ThreadPool.SetMaxThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set max thread pool workers.");

			try
			{
				var ffmpegSemaphore = new SemaphoreSlim(config.MaxFFmpegParallellism);
				using IpcConnection socket = IpcExtension.InitializeIPCSocket(IPCSocketAddress, (socket, uidFrame, group, message) =>
				{
					string uidString = uidFrame.ToUniqueIDString();
					switch (group)
					{
						case "HS":
							IPCLogger.InfoFormat("{0} | IPC Handshake received - '{1}'", uidString, message[0].ConvertToStringUTF8());
							socket.Send(uidFrame, group, new NetMQFrame(ProgramName));
							break;
						case "FFMPEG":
							IPCLogger.InfoFormat("{0} | FFmpeg execution request received : '{1}'", uidString, string.Join(' ', message.Select(arg => arg.ConvertToStringUTF8())));
							Task.Run(async () =>
							{
								await ffmpegSemaphore.WaitAsync();

								await Task.Run(() =>
								{
									int exitCode = -1;
									try
									{
										var ffmpeg = new Process();
										ffmpeg.StartInfo.FileName = config.FFmpegLocation;
										ffmpeg.StartInfo.WorkingDirectory = workingDirectory;
										ffmpeg.StartInfo.UseShellExecute = true;
										foreach (NetMQFrame arg in message)
											ffmpeg.StartInfo.ArgumentList.Add(arg.ConvertToStringUTF8());
										ffmpeg.Start();
										ffmpeg.WaitForExit();
										exitCode = ffmpeg.ExitCode;
									}
									catch (Exception ex)
									{
										exitCode = ex.HResult;
									}
									finally
									{
										ffmpegSemaphore.Release();

										IPCLogger.InfoFormat("{0} | FFmpeg execution exited with code {1}.", uidString, exitCode);
										socket.Send(uidFrame, "FFmpeg", new NetMQFrame(exitCode));
									}
								});
							});
							break;
						case "DL":
							IPCLogger.InfoFormat("{0} | Image {1} process result : {2}", uidString, message[0].ConvertToInt64(), (PixivDownloadResult)message[1].ConvertToInt32());
							socket.Send(uidFrame, group, NetMQFrame.Empty); // Return with empty response
							break;
					}
				});

				MainLogger.InfoFormat("Reading all lines of {0}", ListFileName);
				string[] memberIds = File.ReadAllLines(ListFileName);

				CreateDirectoryIfNotExists("databases");
				CreateDirectoryIfNotExists("logs");
				CreateDirectoryIfNotExists("aria2");
				CreateDirectoryIfNotExists("aria2-logs");

				// Extract URLs
				ExtractMemberDataList(pythonSourceFileExists, workingDirectory, memberIds);

				if (!File.Exists(MemberDataListFileName))
				{
					MainLogger.Error("Failed to dump member informations. (Dump file not found)");
					return 1;
				}

				IDictionary<long, ICollection<MemberPage>> memberPageList = ParseMemberDataList(out int totalCount);

				if (!onlyPostProcessing)
				{
					MainLogger.Info("Extracting member images.");
					using (var semaphore = new SemaphoreSlim(config.MaxExtractorParallellism))
					{
						await ExtractMemberImages(totalCount, workingDirectory, memberPageList, semaphore, pythonSourceFileExists);
					}

					MainLogger.Info("Start downloading.");
					using (var semaphore = new SemaphoreSlim(config.MaxDownloaderParallellism))
					{
						await DownloadImages(totalCount, workingDirectory, memberPageList, semaphore, config.DownloaderParameters);
					}
				}

				MainLogger.Info("Start post-processing.");
				using (var semaphore = new SemaphoreSlim(config.MaxPostprocessorParallellism))
				{
					await Postprocess(totalCount, workingDirectory, memberPageList, semaphore, pythonSourceFileExists);
				}
			}
			catch (Exception ex)
			{
				MainLogger.Error("Error occurred while processing.", ex);
			}

			return 0;
		}

		private static async Task Postprocess(int totalPageCount, string workingDir, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore, bool pythonSourceFileExists)
		{
			int remaining = totalPageCount;

			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach (MemberPage page in pages)
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.InfoFormat("Post-processing started: '{0}.p{1}'. (page {2})", memberId, page.FileIndex, page.Page);

						try
						{
							var postProcessor = new Process();
							postProcessor.StartInfo.FileName = pythonSourceFileExists ? "python.exe" : $"{workingDir}\\PixivUtil2.exe";
							postProcessor.StartInfo.WorkingDirectory = workingDir;
							postProcessor.StartInfo.Arguments = $"{(pythonSourceFileExists ? $"{workingDir}\\PixivUtil2.py" : "")} -s 1 {memberId} --sp={page.Page} --ep={page.Page} -x --pipe={IPCSocketAddress} --db=\"databases\\{memberId}.p{page.FileIndex}.db\" -l \"logs\\{memberId}.p{page.FileIndex}.pp.log\"";
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
							MainLogger.InfoFormat("Post-processing finished: '{0}.p{1}' (page {2}); waiting for {3} remaining operations.", memberId, page.FileIndex, page.Page, Interlocked.Decrement(ref remaining));
						}
					}));
				}
			}

			await Task.WhenAll(tasks);
		}

		private static async Task DownloadImages(int totalPageCount, string workingDir, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore, string parameters)
		{
			int remaining = totalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach (int fileIndex in pages.Select(page => page.FileIndex))
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.InfoFormat("Downloading started: '{0}.p{1}'.", memberId, fileIndex);

						try
						{
							var downloader = new Process();
							downloader.StartInfo.FileName = $"{workingDir}\\aria2c.exe";
							downloader.StartInfo.WorkingDirectory = workingDir;
							downloader.StartInfo.Arguments = $"-i \"aria2\\{memberId}.p{fileIndex}.txt\" -l \"aria2-logs\\{memberId}.p{fileIndex}.log\" {parameters}";
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
							MainLogger.InfoFormat("Donwloading finished: '{0}.p{1}'; waiting for {2} remaining operations.", memberId, fileIndex, Interlocked.Decrement(ref remaining));
						}
					}));
				}
			}

			await Task.WhenAll(tasks);
		}

		private static async Task ExtractMemberImages(int totalPageCount, string workingDir, IDictionary<long, ICollection<MemberPage>> memberPageList, SemaphoreSlim semaphore, bool pythonSourceFileExists)
		{
			int remaining = totalPageCount;
			var tasks = new List<Task>();
			foreach ((long memberId, ICollection<MemberPage> pages) in memberPageList)
			{
				foreach (MemberPage page in pages)
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.InfoFormat("Extraction started: '{0}.p{1}'. (page {2})", memberId, page.FileIndex, page.Page);

						try
						{
							var extractor = new Process();
							extractor.StartInfo.FileName = pythonSourceFileExists ? "python.exe" : $"{workingDir}\\PixivUtil2.exe";
							extractor.StartInfo.WorkingDirectory = workingDir;
							extractor.StartInfo.Arguments = $"{(pythonSourceFileExists ? $"{workingDir}\\PixivUtil2.py" : "")} -s 1 {memberId} --sp={page.Page} --ep={page.Page} -x --pipe={IPCSocketAddress} --db=\"databases\\{memberId}.p{page.FileIndex}.db\" -l \"logs\\{memberId}.p{page.FileIndex}.log\" --aria2=\"aria2\\{memberId}.p{page.FileIndex}.txt\"";
							extractor.StartInfo.UseShellExecute = true;
							extractor.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
							extractor.Start();
							extractor.WaitForExit();
						}
						catch (Exception ex)
						{
							MainLogger.Error("Failed to execute extractor.", ex);
						}
						finally
						{
							semaphore.Release();
							MainLogger.InfoFormat("Extraction finished: '{0}.p{1}' (page {2}); waiting for {3} remaining operations.", memberId, page.FileIndex, page.Page, Interlocked.Decrement(ref remaining));
						}
					}));
				}
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
				if (!long.TryParse(pieces[0], out long memberId) || !int.TryParse(pieces[1], out int memberTotalImages))
					continue;

				if (memberTotalImages > 0)
				{
					if (!memberPageList.ContainsKey(memberId))
						memberPageList[memberId] = new List<MemberPage>();

					const int maxImagesPerPage = 48;
					int pageCount = (memberTotalImages - memberTotalImages % maxImagesPerPage) / maxImagesPerPage + 1;
					for (int i = 1; i <= pageCount; i++)
						memberPageList[memberId].Add(new MemberPage(i, pageCount - i + 1));
					totalCount += pageCount;
					MainLogger.InfoFormat("Member {0} has {1} images -> {2} pages.", memberId, memberTotalImages, pageCount);
				}
				else
				{
					MainLogger.WarnFormat("Member {0} doesn't have any images! Skipping.", memberId);
				}
			}

			return memberPageList;
		}

		private static void ExtractMemberDataList(bool pythonSourceFileExists, string workingDir, string[] memberIds)
		{
			try
			{
				var extractor = new Process();
				extractor.StartInfo.FileName = pythonSourceFileExists ? "Python.exe" : $"{workingDir}\\PixivUtil2.exe";
				extractor.StartInfo.WorkingDirectory = workingDir;
				extractor.StartInfo.Arguments = $"{(pythonSourceFileExists ? $"{workingDir}\\PixivUtil2.py" : "")} -s q {MemberDataListFileName} {string.Join(' ', memberIds)} -x -l \"logs\\dumpMembers.log\"";
				extractor.StartInfo.UseShellExecute = true;
				extractor.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
				extractor.Start();
				extractor.WaitForExit();
			}
			catch (Exception ex)
			{
				MainLogger.Error("Failed to execute member-data extractor.", ex);
			}
		}
	}

	public sealed record MemberPage(int Page, int FileIndex);
}
