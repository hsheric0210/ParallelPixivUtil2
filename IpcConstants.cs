using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelPixivUtil2
{
	public static class IpcConstants
	{
		public const string IPC_HANDSHAKE = "HS";
		public const string IPC_IDENT = "IDENT";
		public const string IPC_FFMPEG_REQUEST = "FFMPEG_REQ";
		public const string IPC_FFMPEG_RESULT = "FFMPEG_RET";
		public const string IPC_DOWNLOADED = "DL";
		public const string IPC_TITLE = "TITLE";
	}
}
