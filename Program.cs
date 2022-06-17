﻿using log4net;
using NetMQ;
using System.Diagnostics;

namespace ParallelPixivUtil2
{
	public sealed class Program
	{
		public const string ProgramName = "ParallelPixivUtil2";

		private static readonly ILog MainLogger = LogManager.GetLogger("Main");
		private static readonly ILog IPCLogger = LogManager.GetLogger("IPC");

		private static readonly IDictionary<byte[], string> IPCIdentifiers = new Dictionary<byte[], string>();
		private static readonly IDictionary<int, Task<int>> ffmpegTasks = new Dictionary<int, Task<int>>();

		private static string CurrentPhaseName = "Unknown";
		private static int ProcessedImageCount;
		private static int TotalImageCount;

		private Program()
		{
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

		public static async Task<int> Main(string[] args)
		{
			Console.WriteLine("ParallelPixivUtil2 - PixivUtil2 with parallel download support");

			bool onlyPostProcessing = args.Length > 0 && args[0].Equals("onlypp", StringComparison.InvariantCultureIgnoreCase);

			var config = new Config();

			string extractorExe = config.ExtractorExecutable;
			string extractorPy = config.ExtractorScript;
			string ipcAddress = $"tcp://localhost:{config.IPCPort}";
			var pythonSourceFileExists = File.Exists(extractorPy);
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

			int workerCount = Math.Max(config.MaxExtractorParallellism, Math.Max(config.MaxDownloaderParallellism, config.MaxPostprocessorParallellism)) + config.MaxFFmpegParallellism + 4;
			if (!ThreadPool.SetMinThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set min thread pool workers.");
			if (!ThreadPool.SetMaxThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set max thread pool workers.");

			try
			{
				var ffmpegSemaphore = new SemaphoreSlim(config.MaxFFmpegParallellism);
				using IpcConnection socket = IpcExtension.InitializeIPCSocket(ipcAddress, (socket, uidFrame, group, message) =>
				{
					string uidString = uidFrame.ToUniqueIDString();
					if (!IPCIdentifiers.TryGetValue(uidFrame.ToByteArray(), out string? identifier))
						uidString += $" ({identifier})";
					switch (group)
					{
						case IpcConstants.IPC_HANDSHAKE:
						{
							IPCLogger.InfoFormat("{0} | IPC Handshake received : '{1}'", uidString, message[0].ConvertToStringUTF8());
							socket.Send(uidFrame, group, new NetMQFrame(ProgramName));
							break;
						}

						case IpcConstants.IPC_IDENT:
						{
							IPCLogger.InfoFormat("{0} | IPC identifier change requested : '{1}'", uidString, message[0].ConvertToStringUTF8());
							IPCIdentifiers[uidFrame.ToByteArray()] = message[0].ConvertToStringUTF8();
							socket.Send(uidFrame, group, NetMQFrame.Empty);
							break;
						}

						case IpcConstants.IPC_FFMPEG_REQUEST:
						{
							IPCLogger.InfoFormat("{0} | FFmpeg execution request received : '{1}'", uidString, string.Join(' ', message.Select(arg => arg.ConvertToStringUTF8())));

							// Generate task ID
							int taskID;
							do
							{
								taskID = Random.Shared.Next();
							} while (ffmpegTasks.ContainsKey(taskID));

							ffmpegTasks.Add(taskID, Task.Run(async () =>
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
									socket.Send(uidFrame, "FFmpeg", new NetMQFrame(exitCode));
								}
								return exitCode;
							}));
							socket.Send(uidFrame, group, new NetMQFrame(taskID));
							break;
						}

						case IpcConstants.IPC_FFMPEG_RESULT:
						{
							int taskID = message[0].ConvertToInt32();
							if (ffmpegTasks.ContainsKey(taskID))
							{
								IPCLogger.InfoFormat("{0} | Exit code of FFmpeg execution '{1}' requested.", uidString, taskID);
								Task.Run(async () =>
								{
									socket.Send(uidFrame, group, new NetMQFrame(await ffmpegTasks[taskID]));
								});
							}
							else
							{
								IPCLogger.WarnFormat("{0} | Exit code of non-existent FFmpeg execution '{1}' requested.", uidString, taskID);
								socket.Send(uidFrame, group, new NetMQFrame(-1));
							}
							break;
						}

						case IpcConstants.IPC_DOWNLOADED:
						{
							IPCLogger.InfoFormat("{0} | [{1}] Image {2} process result : {3}", uidString, ImageProcessed(), message[0].ConvertToInt64(), (PixivDownloadResult)message[1].ConvertToInt32());
							socket.Send(uidFrame, group, NetMQFrame.Empty); // Return with empty response
							break;
						}

						case IpcConstants.IPC_TITLE:
						{
							IPCLogger.InfoFormat("{0} | Title updated: {1}", uidString, message[0].ConvertToStringUTF8());
							socket.Send(uidFrame, group, NetMQFrame.Empty); // Return with empty response
							break;
						}
					}
				});

				CreateDirectoryIfNotExists(config.LogPath);
				CreateDirectoryIfNotExists(config.Aria2InputPath);
				CreateDirectoryIfNotExists(config.DatabasePath);

				MainLogger.InfoFormat("Reading all lines of {0}", listFile);
				string[] memberIds = File.ReadAllLines(listFile);

				var extractor = new ExtractorRecord(extractorExe, extractorPy, pythonSourceFileExists, extractorWorkingDirectory, ipcAddress, config.LogPath, config.Aria2InputPath, config.DatabasePath);

				// Extract URLs
				RetrieveMemberDataList(extractor, config.MemberDataListParameters, config.MemberDataListFile, memberIds);

				if (!File.Exists(config.MemberDataListFile))
				{
					MainLogger.Error("Failed to dump member informations. (Member dump file not found)");
					return 1;
				}

				IDictionary<long, ICollection<MemberPage>> memberPageList = ParseMemberDataList(config.MemberDataListFile, out int totalImageCount, out int totalPageCount);
				extractor.TotalPageCount = totalPageCount;
				var downloader = new DownloaderRecord(totalPageCount, config.DownloaderExecutable, extractorWorkingDirectory, config.LogPath, config.Aria2InputPath, config.DatabasePath);

				if (!onlyPostProcessing)
				{
					MainLogger.Info("Extracting member images.");
					PhaseChange("Extraction", totalImageCount);
					using (var semaphore = new SemaphoreSlim(config.MaxExtractorParallellism))
					{
						await RetrieveMemberImages(extractor, config.ExtractorParameters, memberPageList, semaphore);
					}

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
								["ipcAddress"] = extractor.IPCAddress,
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
								["ipcAddress"] = extractor.IPCAddress,
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
					MainLogger.InfoFormat("Member {0} has {1} images -> {2} pages.", memberId, memberTotalImages, pageCount);
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

	public sealed record ExtractorRecord(string ExecutableExe, string ExecutablePy, bool PyExists, string ExtractorWorkingDir, string IPCAddress, string LogPath, string Aria2InputPath, string DatabasePath)
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
