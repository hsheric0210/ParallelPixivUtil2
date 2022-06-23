using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelPixivUtil2
{
	public static class IpcConstants
	{
		public const string HANDSHAKE = "HS";
		public const string NOTIFY_IDENT = "IDENT";
		public const string NOTIFY_FFMPEG = "FFMPEG_REQ";
		public const string NOTIFY_DOWNLOADED = "DL";
		public const string NOTIFY_TITLE = "TITLE";
		public const string NOTIFY_ERROR = "ERROR"; // TODO

		public const string TASK_FFMPEG_RESULT = "FFMPEG_RET";
		public const string TASK_ARIA2 = "ARIA2";
	}
}
