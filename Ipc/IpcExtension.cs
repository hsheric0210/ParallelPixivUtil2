using NetMQ;
using NetMQ.Sockets;
using System.Text;

namespace ParallelPixivUtil2.Ipc
{
	public static class IpcExtension
	{
		public static IpcConnection InitializeIPCSocket(string address, Action<NetMQSocket, NetMQFrame, string, NetMQFrame[]> callback)
		{
			var socket = new RouterSocket();
			socket.Bind(address);

			var poller = new NetMQPoller { socket };
			socket.ReceiveReady += (_, args) =>
			{
				NetMQMessage? msg = null;
				if (args.Socket.TryReceiveMultipartMessage(ref msg) && msg.FrameCount >= 4)
					callback(args.Socket, msg[0], msg[2].ConvertToString(Encoding.UTF8).ToUpperInvariant(), msg.Skip(3).ToArray());
			};

			poller.RunAsync("IPCMessagePoller", true);

			return new IpcConnection(socket, poller);
		}

		public static void Send(this NetMQSocket socket, NetMQFrame uidFrame, string group, params NetMQFrame[] messageFrames)
		{
			var response = new NetMQMessage(4);
			response.Append(uidFrame);
			response.AppendEmptyFrame();
			response.Append(group);
			foreach (NetMQFrame messageFrame in messageFrames)
				response.Append(messageFrame);
			socket.SendMultipartMessage(response);
		}

		public static string ConvertToStringUTF8(this NetMQFrame frame) => frame.ConvertToString(Encoding.UTF8);

		public static string ToUniqueIDString(this NetMQFrame uidFrame) => string.Concat(uidFrame.ToByteArray().Select(b => Convert.ToString(b, 16).ToUpperInvariant()));
	}

	public sealed record IpcConnection(NetMQSocket Socket, NetMQPoller Poller) : IDisposable
	{
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				Poller.Stop();
				Poller.Dispose();

				Socket.Dispose();
			}
		}
	}
}
