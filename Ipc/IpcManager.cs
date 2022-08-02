using log4net;
using NetMQ;
using ParallelPixivUtil2.Tasks;
using System.IO;

namespace ParallelPixivUtil2.Ipc
{
	public static class IpcManager
	{
		private static readonly ILog IPCLogger = LogManager.GetLogger("IPC");

		private static IpcConnection? Communication;
		private static IpcConnection? TaskRequest;

		private static readonly IDictionary<byte[], string> IPCIdentifiers = new Dictionary<byte[], string>();
		private static readonly IDictionary<string, string> IPCProgressBarMessages = new Dictionary<string, string>();
		private static readonly IDictionary<int, Task<int>> FFmpegTasks = new Dictionary<int, Task<int>>();

		public static event EventHandler<IpcEventArgs>? OnIpcOpened;
		public static event EventHandler<IpcTotalNotifyEventArgs>? OnIpcTotalNotify;
		public static event EventHandler<IpcEventArgs>? OnIpcProcessNotify;

		private static SemaphoreSlim FFmpegSemaphore;

		public static void InitFFmpegSemaphore(int maxParallellism) => FFmpegSemaphore = new SemaphoreSlim(maxParallellism);

		public static void InitCommunication(string address)
		{
			Communication = IpcExtension.InitializeIPCSocket(address, (socket, uidFrame, group, message) =>
			{
				string uidString = uidFrame.ToUniqueIDString();
				if (IPCIdentifiers.TryGetValue(uidFrame.ToByteArray(), out string? identifier))
					uidString += $" ({identifier})";
				else
					identifier = "Unregistered";
				switch (group)
				{
					case IpcConstants.HANDSHAKE:
					{
						string newIdentifier = message[0].ConvertToStringUTF8();
						IPCLogger.DebugFormat("{0} | IPC Handshake received : '{1}'", uidString, newIdentifier);
						string pipeType = message[1].ConvertToStringUTF8();
						if (pipeType.Equals("Comm", StringComparison.OrdinalIgnoreCase))
						{
							IPCIdentifiers[uidFrame.ToByteArray()] = newIdentifier;
							OnIpcOpened?.Invoke(null, new IpcEventArgs(IpcType.Communication, newIdentifier));
							// var bar = ProgressBarUtils.SpawnChild(config.MaxImagesPerPage, IPCProgressBarMessages[newIdentifier]);
							// if (bar != null)
							// 	IPCProgressBars[uidFrame.ToByteArray()] = bar;
							socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
						}
						else
						{
							IPCLogger.FatalFormat("Unexpected handshake '{0}' from {1} - Comm expected. Did you swapped ipcCommAddress and ipcTaskAddress?", pipeType, uidString);
							socket.Send(uidFrame, group, IpcConstants.RETURN_ERROR);
						}

						break;
					}

					case IpcConstants.NOTIFY_TOTAL:
					{
						int total = message[0].ConvertToInt32();
						IPCLogger.DebugFormat("{0} | IPC sent total job count : {1}", uidString, total);
						OnIpcTotalNotify?.Invoke(null, new IpcTotalNotifyEventArgs(IpcType.Communication, identifier, total));
						// if (IPCProgressBars.TryGetValue(uidFrame.ToByteArray(), out ChildProgressBar? bar))
						// 	bar.MaxTicks = total;
						socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
						break;
					}

					case IpcConstants.NOTIFY_DOWNLOADED:
					{
						var status = (PixivDownloadResult)message[1].ConvertToInt32();
						IPCLogger.DebugFormat("{0} | Image {1} process result : {2}", uidString, message[0].ConvertToInt64(), status);
						OnIpcProcessNotify?.Invoke(null, new IpcEventArgs(IpcType.Communication, identifier));
						// if (IPCProgressBars.TryGetValue(uidFrame.ToByteArray(), out ChildProgressBar? bar))
						// 	bar.Tick();
						socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
						break;
					}

					case IpcConstants.NOTIFY_TITLE:
					{
						IPCLogger.DebugFormat("{0} | Title updated: {1}", uidString, message[0].ConvertToStringUTF8());
						socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
						break;
					}
				}
			});
		}

		public static void InitTaskRequest(Config config, string extractorWorkingDirectory, string address)
		{
			TaskRequest = IpcExtension.InitializeIPCSocket(address, (socket, uidFrame, group, message) =>
			{
				string uidString = uidFrame.ToUniqueIDString();
				if (IPCIdentifiers.TryGetValue(uidFrame.ToByteArray(), out string? identifier))
					uidString += $" ({identifier})";
				else
					identifier = "Unregistered";
				switch (group)
				{
					case IpcConstants.HANDSHAKE:
					{
						string newIdentifier = message[0].ConvertToStringUTF8();
						IPCLogger.DebugFormat("{0} | IPC Handshake received : '{1}'", uidString, newIdentifier);
						string pipeType = message[1].ConvertToStringUTF8();
						if (pipeType.Equals("Task", StringComparison.OrdinalIgnoreCase))
						{
							IPCIdentifiers[uidFrame.ToByteArray()] = newIdentifier;
							OnIpcOpened?.Invoke(null, new IpcEventArgs(IpcType.TaskRequest, newIdentifier));
							socket.Send(uidFrame, group, IpcConstants.RETURN_OK);
						}
						else
						{
							IPCLogger.FatalFormat("Unexpected handshake '{0}' from {1} - Task expected. Did you swapped ipcCommAddress and ipcTaskAddress?", pipeType, uidString);
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
							if (FFmpegSemaphore == null)
							{
								IPCLogger.Warn("FFmpeg semaphore not initialized. Bypassing parallellism limit.");
							}
							else
							{
								IPCLogger.DebugFormat("{0} | FFmpeg execution '{1}' is waiting for semaphore...", uidString, taskID);
								await FFmpegSemaphore.WaitAsync();
							}

							IPCLogger.InfoFormat("{0} | FFmpeg execution '{1}' is in process...", uidString, taskID);
							// var bar = ProgressBarUtils.SpawnIndeterminateChild($"FFmpeg execution request by '{uidString}'");

							// TODO: Register FFmpegTask to MainWindow

							var task = new FFmpegTask(config, uidString, extractorWorkingDirectory, message.Select(msg => msg.ConvertToStringUTF8()), FFmpegSemaphore);
							task.Start();
							return task.ExitCode;
						}));
						socket.Send(uidFrame, group, new NetMQFrame(BitConverter.GetBytes(taskID)));
						break;
					}

					case IpcConstants.TASK_FFMPEG_RESULT:
					{
						int taskID = BitConverter.ToInt32(message[0].Buffer);
						if (FFmpegTasks.ContainsKey(taskID))
						{
							IPCLogger.DebugFormat("{0} | Exit code of FFmpeg execution '{1}' requested.", uidString, taskID);
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
						DownloadQueueManager.Add(Path.GetFullPath(message[0].ConvertToStringUTF8()), message[1].ConvertToStringUTF8());
						socket.Send(uidFrame, group, new NetMQFrame(BitConverter.GetBytes(0)));
						break;
					}
				}
			});
		}


		public static void Unload()
		{
			Communication?.Dispose();
			TaskRequest?.Dispose();
		}
	}

	public class IpcEventArgs : EventArgs
	{
		public IpcType Type
		{
			get;
		}

		public string Identifier
		{
			get;
		}

		public IpcEventArgs(IpcType type, string identifier)
		{
			Type = type;
			Identifier = identifier;
		}
	}

	public class IpcTotalNotifyEventArgs : IpcEventArgs
	{
		public int Total
		{
			get;
		}

		public IpcTotalNotifyEventArgs(IpcType type, string identifier, int total) : base(type, identifier) => Total = total;
	}

	public enum IpcType
	{
		Communication,
		TaskRequest
	}
}
