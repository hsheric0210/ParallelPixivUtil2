using log4net;
using NetMQ;
using ShellProgressBar;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace ParallelPixivUtil2
{
	public sealed class Program
	{
		public const string ProgramName = "ParallelPixivUtil2";

		private static readonly ILog MainLogger = LogManager.GetLogger("Main");
		private static readonly ILog IPCLogger = LogManager.GetLogger("IPC");

		private static readonly IDictionary<byte[], string> IPCIdentifiers = new Dictionary<byte[], string>();
		private static readonly IDictionary<int, Task<int>> FFmpegTasks = new Dictionary<int, Task<int>>();
		private static readonly IDictionary<string, IList<string>> DownloadInputQueue = new ConcurrentDictionary<string, IList<string>>();

		private static Timer? DownloadInputQueueFlusher;

		private static string CurrentPhaseName = "Unknown";
		private static int ProcessedImageCount;
		private static int TotalImageCount;

		private Program()
		{
		}

		private static void FlushDownloadInputQueue()
		{
			MainLogger.Debug("Processing queued download input list...");
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
		}

		private static void BeginFlushDownloadInputQueueTimer(long delay, long period) => DownloadInputQueueFlusher = new Timer(_ => FlushDownloadInputQueue(), null, delay, period);

		private static void EndFlushDownloadInputQueueTimer()
		{
			if (DownloadInputQueueFlusher == null)
				return;

			DownloadInputQueueFlusher.Dispose();
			FlushDownloadInputQueue();
		}

		private static void PhaseChange(string phaseName, int totalImageCount)
		{
			CurrentPhaseName = phaseName;
			ProcessedImageCount = 0;
			TotalImageCount = totalImageCount;
		}

		private static string ImageProcessed()
		{
			int remaining = Interlocked.Increment(ref ProcessedImageCount);
			string progress = $"{remaining}/{TotalImageCount}";
			Console.Title = $"{CurrentPhaseName} phase : Processed {progress} images";
			return progress;
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

		private static readonly ProgressBarOptions DefaultProgressBarOpts = new()
		{
			BackgroundCharacter = '-',
			ProgressCharacter = '=',
			ForegroundColor = ConsoleColor.Blue,
			ForegroundColorError = ConsoleColor.Red,
			BackgroundColor = ConsoleColor.Yellow,
			CollapseWhenFinished = false
		};

		public static async Task<int> Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");


			using var progressBar = new ProgressBar(5, "Preparing...", DefaultProgressBarOpts);
			ProgressBarCompatibleConsoleAppender.ProgressBar = progressBar;

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

			IndeterminateChildProgressBar threadPoolSetupProgressBar = progressBar.SpawnIndeterminate("Setting thread pool worker counts", DefaultProgressBarOpts);
			int workerCount = Math.Max(config.MaxExtractorParallellism, Math.Max(config.MaxDownloaderParallellism, config.MaxPostprocessorParallellism)) + config.MaxFFmpegParallellism + 4;
			if (!ThreadPool.SetMinThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set min thread pool workers.");
			if (!ThreadPool.SetMaxThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set max thread pool workers.");
			threadPoolSetupProgressBar.Finished();
			progressBar.Tick();

			IndeterminateChildProgressBar ipcSetupProgressBar = progressBar.SpawnIndeterminate("Initializing IPC sockets: Communication socket", DefaultProgressBarOpts);
			try
			{
				var ffmpegSemaphore = new SemaphoreSlim(config.MaxFFmpegParallellism);

				// Channel for communication, notification and task requesting.
				using IpcConnection commSocket = IpcExtension.InitializeIPCSocket(ipcCommAddr, (socket, uidFrame, group, message) =>
				{
					string uidString = uidFrame.ToUniqueIDString();
					if (!IPCIdentifiers.TryGetValue(uidFrame.ToByteArray(), out string? identifier))
						uidString += $" ({identifier})";
					switch (group)
					{
						case IpcConstants.HANDSHAKE:
						{
							IPCLogger.InfoFormat("{0} | IPC Handshake received : '{1}'", uidString, message[0].ConvertToStringUTF8());
							string pipeType = message[1].ConvertToStringUTF8();
							if (pipeType.Equals("Comm", StringComparison.OrdinalIgnoreCase))
							{
								socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
							}
							else
							{
								IPCLogger.FatalFormat("Unexpected handshake: {0} - Comm expected. Did you swapped ipcCommAddress and ipcTaskAddress?", pipeType);
								socket.Send(uidFrame, group, IpcConstants.RETURN_ERROR);
							}

							break;
						}

						case IpcConstants.NOTIFY_IDENT:
						{
							IPCLogger.InfoFormat("{0} | IPC identifier change requested : '{1}'", uidString, message[0].ConvertToStringUTF8());
							IPCIdentifiers[uidFrame.ToByteArray()] = message[0].ConvertToStringUTF8();
							socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
							break;
						}

						case IpcConstants.NOTIFY_DOWNLOADED:
						{
							IPCLogger.InfoFormat("{0} | [{1}] Image {2} process result : {3}", uidString, ImageProcessed(), message[0].ConvertToInt64(), (PixivDownloadResult)message[1].ConvertToInt32());
							socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
							break;
						}

						case IpcConstants.NOTIFY_TITLE:
						{
							IPCLogger.InfoFormat("{0} | Title updated: {1}", uidString, message[0].ConvertToStringUTF8());
							socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
							break;
						}
					}
				});
				ipcSetupProgressBar.Message = "Initializing IPC sockets: Task request socket";

				// Socket for long-running or blocking tasks
				using IpcConnection taskRequestSocket = IpcExtension.InitializeIPCSocket(ipcTaskAddr, (socket, uidFrame, group, message) =>
				{
					string uidString = uidFrame.ToUniqueIDString();
					if (!IPCIdentifiers.TryGetValue(uidFrame.ToByteArray(), out string? identifier))
						uidString += $" ({identifier})";
					switch (group)
					{
						case IpcConstants.HANDSHAKE:
						{
							IPCLogger.InfoFormat("{0} | IPC Handshake received : '{1}'", uidString, message[0].ConvertToStringUTF8());
							string pipeType = message[1].ConvertToStringUTF8();
							if (pipeType.Equals("Task", StringComparison.OrdinalIgnoreCase))
							{
								socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
							}
							else
							{
								IPCLogger.FatalFormat("Unexpected handshake: {0} - Task expected. Did you swapped ipcCommAddress and ipcTaskAddress?", pipeType);
								socket.Send(uidFrame, group, IpcConstants.RETURN_ERROR);
							}

							break;
						}

						case IpcConstants.TASK_FFMPEG_REQUEST:
						{
							IPCLogger.InfoFormat("{0} | FFmpeg execution request received : '{1}'", uidString, string.Join(' ', message.Select(arg => arg.ConvertToStringUTF8())));

							// Generate task ID
							int taskID;
							do
							{
								taskID = Random.Shared.Next();
							} while (FFmpegTasks.ContainsKey(taskID));

							// Allocate task
							FFmpegTasks.Add(taskID, Task.Run(async () =>
							{
								IPCLogger.InfoFormat("{0} | FFmpeg execution '{1}' is waiting for semaphore...", uidString, taskID);
								await ffmpegSemaphore.WaitAsync();

								IPCLogger.InfoFormat("{0} | FFmpeg execution '{1}' is in process...", uidString, taskID);

								int exitCode = -1;
								try
								{
									var ffmpeg = new Process();
									ffmpeg.StartInfo.FileName = config.FFmpegExecutable;
									ffmpeg.StartInfo.WorkingDirectory = extractorWorkingDirectory;
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
									IPCLogger.Error(string.Format("{0} | FFmpeg execution failed with exception.", uidString), ex);
								}
								finally
								{
									ffmpegSemaphore.Release();
									IPCLogger.InfoFormat("{0} | FFmpeg execution exited with code {1}.", uidString, exitCode);
								}
								return exitCode;
							}));
							socket.Send(uidFrame, group, new NetMQFrame(BitConverter.GetBytes(taskID)));
							break;
						}

						case IpcConstants.TASK_FFMPEG_RESULT:
						{
							int taskID = BitConverter.ToInt32(message[0].Buffer);
							if (FFmpegTasks.ContainsKey(taskID))
							{
								IPCLogger.InfoFormat("{0} | Exit code of FFmpeg execution '{1}' requested.", uidString, taskID);
								Task.Run(async () => socket.Send(uidFrame, group, new NetMQFrame(BitConverter.GetBytes(await FFmpegTasks[taskID]))));
							}
							else
							{
								IPCLogger.WarnFormat("{0} | Exit code of non-existent FFmpeg execution '{1}' requested.", uidString, taskID);
								socket.Send(uidFrame, group, new NetMQFrame(BitConverter.GetBytes(-1)));
							}
							break;
						}

						case IpcConstants.TASK_ARIA2:
						{
							string fileName = Path.GetFullPath(message[0].ConvertToStringUTF8());
							if (!DownloadInputQueue.TryGetValue(fileName, out IList<string>? list))
							{
								list = new List<string>();
								DownloadInputQueue.Add(fileName, list);
							}

							list.Add(message[1].ConvertToStringUTF8());
							socket.Send(uidFrame, group, new NetMQFrame(BitConverter.GetBytes(0)));
							break;
						}
					}
				});
				ipcSetupProgressBar.Finished();
				progressBar.Tick();

				CreateDirectoryIfNotExists(config.LogPath);
				CreateDirectoryIfNotExists(config.Aria2InputPath);
				CreateDirectoryIfNotExists(config.DatabasePath);

				IndeterminateChildProgressBar readListProgressBar = progressBar.SpawnIndeterminate("Reading al lines of " + listFile, DefaultProgressBarOpts);
				MainLogger.InfoFormat("Reading all lines of {0}", listFile);
				string[] memberIds = File.ReadAllLines(listFile);
				readListProgressBar.Finished();
				progressBar.Tick();

				var extractor = new ExtractorRecord(extractorExe, extractorPy, pythonSourceFileExists, extractorWorkingDirectory, ipcCommAddr, ipcTaskAddr, config.LogPath, config.Aria2InputPath, config.DatabasePath);

				IndeterminateChildProgressBar retrieveMemberDataProgressBar = progressBar.SpawnIndeterminate("Retrieveing member data", DefaultProgressBarOpts);

				// Extract URLs
				RetrieveMemberDataList(extractor, config.MemberDataListParameters, config.MemberDataListFile, memberIds);
				retrieveMemberDataProgressBar.Finished();
				progressBar.Tick();

				if (!File.Exists(config.MemberDataListFile))
				{
					MainLogger.Error("Failed to dump member informations. (Member dump file not found)");
					return 1;
				}

				IndeterminateChildProgressBar parseMemberDataProgressBar = progressBar.SpawnIndeterminate("Parsing member data", DefaultProgressBarOpts);

				IDictionary<long, ICollection<MemberPage>> memberPageList = ParseMemberDataList(config.MemberDataListFile, out int totalImageCount, out int totalPageCount);
				parseMemberDataProgressBar.Finished();
				progressBar.Tick();

				await Task.Delay(5000);

				return 0;

				extractor.TotalPageCount = totalPageCount;
				var downloader = new DownloaderRecord(totalPageCount, config.DownloaderExecutable, extractorWorkingDirectory, config.LogPath, config.Aria2InputPath, config.DatabasePath);

				if (!onlyPostProcessing)
				{
					MainLogger.Info("Extracting member images.");
					PhaseChange("Extraction", totalImageCount);
					BeginFlushDownloadInputQueueTimer(config.DownloadInputDelay, config.DownloadInputPeriod);
					using (var semaphore = new SemaphoreSlim(config.MaxExtractorParallellism))
					{
						await RetrieveMemberImages(extractor, config.ExtractorParameters, memberPageList, semaphore);
					}
					EndFlushDownloadInputQueueTimer();

					MainLogger.Info("Start downloading.");
					Console.Title = "Downloading phase";
					using (var semaphore = new SemaphoreSlim(config.MaxDownloaderParallellism))
					{
						await DownloadImages(downloader, config.DownloaderParameters, memberPageList, semaphore);
					}
				}

				MainLogger.Info("Start post-processing.");
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
						MainLogger.InfoFormat("Post-processing started: '{0}.p{1}'. (page {2})", memberId, page.FileIndex, page.Page);

						var postProcessor = new Process();
						try
						{
							postProcessor.StartInfo.FileName = extractor.Executable;
							postProcessor.StartInfo.WorkingDirectory = extractor.ExtractorWorkingDir;
							postProcessor.StartInfo.Arguments = extractor.ExtraArguments + FormatTokens(parameters, new Dictionary<string, string>
							{
								["memberID"] = memberId.ToString(),
								["page"] = page.Page.ToString(),
								["fileIndex"] = page.FileIndex.ToString(),
								["ipcAddress"] = extractor.IPCCommAddress + '|' + extractor.IPCTaskAddress,
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
				foreach (int fileIndex in pages.Select(page => page.FileIndex))
				{
					await semaphore.WaitAsync();
					tasks.Add(Task.Run(() =>
					{
						MainLogger.InfoFormat("Downloading started: '{0}.p{1}'.", memberId, fileIndex);

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
							retriever.StartInfo.FileName = extractor.Executable;
							retriever.StartInfo.WorkingDirectory = extractor.ExtractorWorkingDir;
							retriever.StartInfo.Arguments = extractor.ExtraArguments + FormatTokens(parameters, new Dictionary<string, string>
							{
								["memberID"] = memberId.ToString(),
								["page"] = page.Page.ToString(),
								["fileIndex"] = page.FileIndex.ToString(),
								["ipcAddress"] = extractor.IPCCommAddress + '|' + extractor.IPCTaskAddress,
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
						}
					}));
				}
			}

			await Task.WhenAll(tasks);
		}

		private static IDictionary<long, ICollection<MemberPage>> ParseMemberDataList(string memberDataList, out int totalImageCount, out int totalPageCount)
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

					const int maxImagesPerPage = 48;
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

	public sealed record MemberPage(int Page, int FileIndex);

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
