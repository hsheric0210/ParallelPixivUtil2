namespace ParallelPixivUtil2
{
	public class Config
	{
		private const string FileName = "parallel.ini";

		private const string MaxExtractorParallellismKey = "MaxPixivUtil2ExtractParallellism";
		private const int DefaultMaxExtractorParallellism = 8;
		private const string MaxDownloaderParallellismKey = "MaxAria2Parallellism";
		private const int DefaultMaxDownloaderParallellism = 4;
		private const string MaxPostprocessorParallellismKey = "MaxPixivUtil2PostprocessParallellism";
		private const int DefaultMaxPostprocessorParallellism = 4;
		private const string DownloaderParametersKey = "Aria2Parameters";
		private const string DefaultDownloaderParameters = "--allow-overwrite=true --conditional-get=true --remote-time=true --auto-file-renaming=false --auto-save-interval=10 -j16 -x2";
		private const string MaxImagesPerPageKey = "MaxImagesPerPage";
		private const int DefaultMaxImagesPerPage = 48;
		private const string ExtractorWorkingDirectoryKey = "WorkingDir";
		private const string DefaultExtractorWorkingDirectory = "";
		private const string FFmpegLocationKey = "FFmpeg";
		private const string DefaultFFmpegLocationKey = "FFmpeg.exe";
		private const string DownloaderLocationKey = "Aria2";
		private const string DefaultDownloaderLocation = "Aria2c.exe";

		private readonly IniFile Ini;

		public int MaxExtractorParallellism => ParseInt(MaxExtractorParallellismKey, DefaultMaxExtractorParallellism);

		public int MaxDownloaderParallellism => ParseInt(MaxDownloaderParallellismKey, DefaultMaxDownloaderParallellism);

		public int MaxPostprocessorParallellism => ParseInt(MaxPostprocessorParallellismKey, DefaultMaxPostprocessorParallellism);

		public string DownloaderParameters => ParseString(DownloaderParametersKey, DefaultDownloaderParameters);

		public int MaxImagesPerPage => ParseInt(MaxImagesPerPageKey, DefaultMaxImagesPerPage);

		public string ExtractorWorkingDirectory => ParseString(ExtractorWorkingDirectoryKey, DefaultExtractorWorkingDirectory);

		public string FFmpegLocation => ParseString(FFmpegLocationKey, DefaultFFmpegLocationKey);

		public string DownloaderLocation => ParseString(DownloaderLocationKey, DefaultDownloaderLocation);

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

		private void WriteDefaultConfig()
		{
			Ini.Write(MaxExtractorParallellismKey, DefaultMaxExtractorParallellism.ToString());
			Ini.Write(MaxDownloaderParallellismKey, DefaultMaxDownloaderParallellism.ToString());
			Ini.Write(MaxPostprocessorParallellismKey, DefaultMaxPostprocessorParallellism.ToString());
			Ini.Write(DownloaderParametersKey, DefaultDownloaderParameters);
			Ini.Write(MaxImagesPerPageKey, DefaultMaxImagesPerPage.ToString());
			Ini.Write(ExtractorWorkingDirectoryKey, DefaultExtractorWorkingDirectory);
			Ini.Write(FFmpegLocationKey, DefaultFFmpegLocationKey);
			Ini.Write(DownloaderLocationKey, DefaultDownloaderLocation);
		}
	}
}
