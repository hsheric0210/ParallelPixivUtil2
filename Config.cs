namespace ParallelPixivUtil2
{
	public class Config
	{
		private const string FileName = "parallel.ini";

		// Parallellism

		private const string MaxExtractorParallellismKey = "MaxPixivUtil2ExtractParallellism";
		private const int DefaultMaxExtractorParallellism = 8;
		private const string MaxDownloaderParallellismKey = "MaxAria2Parallellism";
		private const int DefaultMaxDownloaderParallellism = 4;
		private const string MaxPostprocessorParallellismKey = "MaxPixivUtil2PostprocessParallellism";
		private const int DefaultMaxPostprocessorParallellism = 16;
		private const string MaxFFmpegParallellismKey = "FFmpegParallellism";
		private const int DefaultMaxFFmpegParallellism = 4;

		// Downloader

		private const string DownloaderLocationKey = "DownloaderExecutable";
		private const string DefaultDownloaderLocation = "Aria2c.exe";
		private const string DownloaderParametersKey = "DownloaderParameters";
		private const string DefaultDownloaderParameters = "-i\"${aria2InputPath}\\${memberID}.p${fileIndex}.txt\" -l\"${logPath}\\aria2.${memberID}.p${fileIndex}.log\" -j16 -x2 -m0 -Rtrue --allow-overwrite=true --auto-file-renaming=false --auto-save-interval=15 --conditional-get=true --retry-wait=10 --no-file-allocation-limit=2M";

		// Extractor

		private const string ExtractorExecutableKey = "ExtractorExecutable";
		private const string DefaultExtractorExecutable = "PixivUtil2.exe";
		private const string ExtractorScriptKey = "ExtractorScript";
		private const string DefaultExtractorScript = "PixivUtil2.py";
		private const string ExtractorParametersKey = "ExtractorParameters";
		private const string DefaultExtractorParameters = "-s 1 ${memberID} --sp=${page} --ep=${page} -x --pipe=${ipcAddress} --db=\"${databasePath}\\${memberID}.p${fileIndex}.db\" -l \"${logPath}\\Extractor.${memberID}.p${fileIndex}.log\" --aria2=\"${aria2InputPath}\\${memberID}.p${fileIndex}.txt\"";
		private const string PostprocessorParametersKey = "PostprocessorParameters";
		private const string DefaultPostprocessorParameters = "-s 1 ${memberID} --sp=${page} --ep=${page} -x --pipe=${ipcAddress} --db=\"${databasePath}\\${memberID}.p${fileIndex}.db\" -l \"${logPath}\\Postprocessor.${memberID}.p${fileIndex}.log\"";
		private const string MemberDataListParametersKey = "ExtractMemberDataListParameters";
		private const string DefaultMemberDataListParameters = "-s q ${memberDataList} ${memberIDs} -x -l \"${logPath}\\dumpMembers.log\"";

		// FFmpeg

		private const string FFmpegExecutableKey = "FFmpeg";
		private const string DefaultFFmpegExecutableKey = "FFmpeg.exe";

		// Miscellaneous

		private const string MaxImagesPerPageKey = "MaxImagesPerPage";
		private const int DefaultMaxImagesPerPage = 48;
		private const string MemberDataListFileKey = "MemberDataFile";
		private const string DefaultMemberDataListFile = "memberdata.txt";
		private const string ListFileKey = "ListFile";
		private const string DefaultListFile = "list.txt";
		private const string IPCCommPortKey = "IPCCommunicatePort";
		private const int DefaultIPCCommPort = 6974;
		private const string IPCTaskPortKey = "IPCTaskPort";
		private const int DefaultIPCTaskPort = 7469;

		private const string LogPathKey = "LogFolder";
		private const string DefaultLogPath = "logs";
		private const string Aria2InputPathKey = "Aria2InputFileFolder";
		private const string DefaultAria2InputPath = "aria2";
		private const string DatabasePathKey = "DatabaseFolder";
		private const string DefaultDatabasePath = "databases";

		private readonly IniFile Ini;

		public int MaxExtractorParallellism => ParseInt(MaxExtractorParallellismKey, DefaultMaxExtractorParallellism);

		public int MaxDownloaderParallellism => ParseInt(MaxDownloaderParallellismKey, DefaultMaxDownloaderParallellism);

		public int MaxPostprocessorParallellism => ParseInt(MaxPostprocessorParallellismKey, DefaultMaxPostprocessorParallellism);

		public int MaxFFmpegParallellism => ParseInt(MaxFFmpegParallellismKey, DefaultMaxFFmpegParallellism);

		public string ExtractorExecutable => ParsePath(ExtractorExecutableKey, DefaultExtractorExecutable);

		public string ExtractorScript => ParsePath(ExtractorScriptKey, DefaultExtractorScript);

		public string DownloaderExecutable => ParsePath(DownloaderLocationKey, DefaultDownloaderLocation);

		public string DownloaderParameters => ParseString(DownloaderParametersKey, DefaultDownloaderParameters);

		public int MaxImagesPerPage => ParseInt(MaxImagesPerPageKey, DefaultMaxImagesPerPage);

		public string FFmpegExecutable => ParsePath(FFmpegExecutableKey, DefaultFFmpegExecutableKey);

		public string MemberDataListFile => ParsePath(MemberDataListFileKey, DefaultMemberDataListFile);

		public string ListFile => ParsePath(ListFileKey, DefaultListFile);

		public int IPCCommPort => ParseInt(IPCCommPortKey, DefaultIPCCommPort);
		public int IPCTaskPort => ParseInt(IPCTaskPortKey, DefaultIPCTaskPort);

		public string ExtractorParameters => ParseString(ExtractorParametersKey, DefaultExtractorParameters);

		public string PostprocessorParameters => ParseString(PostprocessorParametersKey, DefaultPostprocessorParameters);

		public string MemberDataListParameters => ParseString(MemberDataListParametersKey, DefaultMemberDataListParameters);

		public string LogPath => ParsePath(LogPathKey, DefaultLogPath);

		public string Aria2InputPath => ParsePath(Aria2InputPathKey, DefaultAria2InputPath);

		public string DatabasePath => ParsePath(DatabasePathKey, DefaultDatabasePath);

		public Config()
		{
			Ini = new IniFile(FileName);
			if (!File.Exists(FileName))
				WriteDefaultConfig();
		}

		private int ParseInt(string key, int defaultValue)
		{
			if (Ini.KeyExists(key) && int.TryParse(Ini.Read(key), out int result))
				return result;
			Ini.Write(key, defaultValue);
			return defaultValue;
		}

		private string ParseString(string key, string defaultValue)
		{
			if (Ini.KeyExists(key))
				return Ini.Read(key);
			Ini.Write(key, defaultValue);
			return defaultValue;
		}

		private string ParsePath(string key, string defaultValue)
		{
			string path = ParseString(key, defaultValue);
			if (Path.IsPathFullyQualified(path))
				return path;
			return Path.GetFullPath(path);
		}

		private void WriteDefaultConfig()
		{
			Ini.Write(MaxExtractorParallellismKey, DefaultMaxExtractorParallellism);
			Ini.Write(MaxDownloaderParallellismKey, DefaultMaxDownloaderParallellism);
			Ini.Write(MaxPostprocessorParallellismKey, DefaultMaxPostprocessorParallellism);
			Ini.Write(MaxFFmpegParallellismKey, DefaultMaxFFmpegParallellism);

			Ini.Write(DownloaderLocationKey, DefaultDownloaderLocation);
			Ini.Write(DownloaderParametersKey, DefaultDownloaderParameters);

			Ini.Write(ExtractorExecutableKey, DefaultExtractorExecutable);
			Ini.Write(ExtractorScriptKey, DefaultExtractorScript);
			Ini.Write(ExtractorParametersKey, DefaultExtractorParameters);
			Ini.Write(PostprocessorParametersKey, DefaultPostprocessorParameters);
			Ini.Write(MemberDataListParametersKey, DefaultMemberDataListParameters);

			Ini.Write(FFmpegExecutableKey, DefaultFFmpegExecutableKey);

			Ini.Write(MaxImagesPerPageKey, DefaultMaxImagesPerPage);
			Ini.Write(MemberDataListFileKey, DefaultMemberDataListFile);
			Ini.Write(ListFileKey, DefaultListFile);
			Ini.Write(IPCCommPortKey, DefaultIPCCommPort);

			Ini.Write(LogPathKey, DefaultLogPath);
			Ini.Write(Aria2InputPathKey, DefaultAria2InputPath);
			Ini.Write(DatabasePathKey, DefaultDatabasePath);
		}
	}
}
