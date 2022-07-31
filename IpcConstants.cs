using NetMQ;

namespace ParallelPixivUtil2
{
	public static class IpcConstants
	{
		public static readonly NetMQFrame RETURN_OK = new("Ok");
		public static readonly NetMQFrame RETURN_ERROR = new("Error");
	
		public const string HANDSHAKE = "HS";
		public const string NOTIFY_TOTAL = "TOTAL";
		public const string NOTIFY_DOWNLOADED = "DL";
		public const string NOTIFY_TITLE = "TITLE";
		public const string NOTIFY_ERROR = "ERROR"; // TODO

		public const string TASK_FFMPEG_REQUEST = "FFMPEG_REQ";
		public const string TASK_FFMPEG_RESULT = "FFMPEG_RET";
		public const string TASK_ARIA2 = "ARIA2";
	}
}
