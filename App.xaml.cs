using log4net;
using ParallelPixivUtil2.Ipc;
using System.IO;
using System.Windows;

namespace ParallelPixivUtil2
{
	public partial class App : Application
	{
		public static readonly ILog MainLogger = LogManager.GetLogger(nameof(App));

		public static Config Configuration
		{
			get; private set;
		} = null!;

		public static bool OnlyPostprocessing
		{
			get; private set;
		}

		public static bool NoExit
		{
			get; private set;
		}

		public static bool IsExtractorScript
		{
			get; private set;
		}

		public static string ExtractorWorkingDirectory
		{
			get; private set;
		} = null!;

		public App()
		{
			// Initialize Configuration
			try
			{
				Configuration = new Config();
			}
			catch (Exception e)
			{
				MessageBox.Show("Failed to load the configuration.\nFollowing exception occurred:\n" + e.ToString(), "Configuration load error", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(1);
			}

			string extractorExecutable = Configuration.ExtractorExecutable;
			string extractorScript = Configuration.ExtractorScript;
			IsExtractorScript = File.Exists(extractorScript);
			if (!IsExtractorScript && RequireExists(extractorExecutable, "Extractor executable", silent: true))
			{
				MessageBox.Show("Neither PixivUtil2 executable nor PixivUtil2.py specified.", "Extractor not specified", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(1);
			}

			string extractorPath = IsExtractorScript ? extractorScript : extractorExecutable;
			string? extractorWorkingDirectory = Path.GetDirectoryName(extractorPath);
			if (string.IsNullOrWhiteSpace(extractorWorkingDirectory))
			{
				MessageBox.Show($"Extractor directory unavailable ({extractorPath}).", "Extractor directory unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
				Environment.Exit(1);
			}
			else
			{
				ExtractorWorkingDirectory = extractorWorkingDirectory!;
			}

			string listFile = Configuration.ListFile;
			string downloaderExecutable = Configuration.DownloaderExecutable;
			if (RequireExists(listFile, "List file") || RequireExists(downloaderExecutable, "Downloader executable") || RequireExists($"{extractorWorkingDirectory}\\config.ini", "Extractor configuration", "\nIf this is the first run, make sure to run PixivUtil2 once to generate the default configuration file."))
				Environment.Exit(1);

			CreateDirectoryIfNotExists(Configuration.LogPath);
			CreateDirectoryIfNotExists(Configuration.Aria2InputPath);
			CreateDirectoryIfNotExists(Configuration.DatabasePath);


			int workerCount = Math.Max(Configuration.MaxExtractorParallellism, Math.Max(Configuration.MaxDownloaderParallellism, Configuration.MaxPostprocessorParallellism)) + Configuration.MaxFFmpegParallellism + 4; // 4 for fallback
			if (!ThreadPool.SetMinThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set min thread pool workers.");
			if (!ThreadPool.SetMaxThreads(workerCount, workerCount))
				MainLogger.Warn("Failed to set max thread pool workers.");
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			string[] args = e.Args;
			OnlyPostprocessing = args.Any(s => s.Equals("onlypp", StringComparison.OrdinalIgnoreCase));
			NoExit = args.Any(s => s.Equals("noexit", StringComparison.OrdinalIgnoreCase));
		}

		private static bool RequireExists(string fileName, string fileDetails, string? extraComments = null, bool silent = false)
		{
			if (!File.Exists(fileName))
			{
				if (!silent)
				{
					MainLogger.ErrorFormat("{0} is not available. ({1})", fileName, fileDetails);
					MessageBox.Show($"File '{fileName}' ({fileDetails}) unavailable.{extraComments ?? ""}", "Requirements unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
				}
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
	}
}
