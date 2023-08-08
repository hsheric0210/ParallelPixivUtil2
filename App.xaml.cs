﻿using System.IO;
using System.Windows;
using System.Text.Json;
using Serilog;

namespace ParallelPixivUtil2
{
    public partial class App : Application
    {
        public readonly static JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static Config Configuration
        {
            get; private set;
        } = null!;

        public static string ConfigName
        {
            get; private set;
        } = "parallel.json";

        public static bool NoUnarchive
        {
            get; private set;
        }

        public static bool NoExtract
        {
            get; private set;
        }

        public static bool NoDownload
        {
            get; private set;
        }

        public static bool NoPostprocess
        {
            get; private set;
        }

        public static bool NoRearchive
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

        private const string ConfigFileSwitch = "-c";
        private const string GenerateConfigSwitch = "-gc";
        private const string NoUnarchiveSwitch = "-no-unarchive";
        private const string NoExtractSwitch = "-no-extract";
        private const string NoDownloadSwitch = "-no-download";
        private const string NoPostprocessSwitch = "-no-postprocess";
        private const string NoRearchiveSwitch = "-no-rearchive";
        private const string NoExitSwitch = "-noexit";

        public static void ConfigInit()
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(
                        nameof(ParallelPixivUtil2) + ".log",
                        fileSizeLimitBytes: 4194304,
                        buffered: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1),
                        rollOnFileSizeLimit: true)
                    .CreateLogger();
            }
            catch (Exception e)
            {
                MessageBox.Show("Can't create logger.\nFollowing exception occurred:\n" + e.ToString(), "Configuration load error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            // Initialize Configuration
            try
            {
                Configuration = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigName))!;

                // Sanitize paths
                Configuration.LogFolderName = Path.GetFullPath(Configuration.LogFolderName);
                Configuration.DownloadListFolderName = Path.GetFullPath(Configuration.DownloadListFolderName);
                Configuration.DatabaseFolderName = Path.GetFullPath(Configuration.DatabaseFolderName);
                if (Configuration.AutoArchive)
                {
                    Configuration.Archive.WorkingFolder = Path.GetFullPath(Configuration.Archive.WorkingFolder);
                    Configuration.Archive.BackupFolder = Path.GetFullPath(Configuration.Archive.BackupFolder);
                    Configuration.Archive.ArchiveFolder = Path.GetFullPath(Configuration.Archive.ArchiveFolder);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to load the configuration.\nFollowing exception occurred:\n" + e.ToString(), "Configuration load error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        public App()
        {
            var cmdline = Environment.GetCommandLineArgs();

            var configName = cmdline.Where(line => line.StartsWith(ConfigFileSwitch)).Select(line => line[2..]).FirstOrDefault();
            if (configName == null)
                configName = "parallel.json";
            ConfigName = configName;

            if (cmdline.Any(line => line.Equals(GenerateConfigSwitch, StringComparison.OrdinalIgnoreCase)))
            {
                File.WriteAllText(configName, JsonSerializer.Serialize(new Config(), JsonOptions));
                MessageBox.Show($"Default configuration wrote to '{configName}'.", "Configuration generation", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }

            if (!new FileInfo(configName).Exists)
            {
                MessageBox.Show($"Configuration file '{configName}' not found. Use '-gc' switch to generate the default configuration file.", "Configuration load error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            ConfigInit();


            NoUnarchive = cmdline.Any(s => s.Equals(NoUnarchiveSwitch, StringComparison.OrdinalIgnoreCase));
            Log.Information("Skip archiving phase ({switch}): {state}", NoUnarchiveSwitch, NoUnarchive);

            NoExtract = cmdline.Any(s => s.Equals(NoExtractSwitch, StringComparison.OrdinalIgnoreCase));
            Log.Information("Skip extraction phase ({switch}): {state}", NoExtractSwitch, NoExtract);

            NoDownload = cmdline.Any(s => s.Equals(NoDownloadSwitch, StringComparison.OrdinalIgnoreCase));
            Log.Information("Skip download phase ({switch}): {state}", NoDownloadSwitch, NoDownload);

            NoPostprocess = cmdline.Any(s => s.Equals(NoPostprocessSwitch, StringComparison.OrdinalIgnoreCase));
            Log.Information("Skip post-processing phase ({switch}): {state}", NoPostprocessSwitch, NoPostprocess);

            NoRearchive = cmdline.Any(s => s.Equals(NoRearchiveSwitch, StringComparison.OrdinalIgnoreCase));
            Log.Information("Skip re-archiving phase ({switch}): {state}", NoRearchiveSwitch, NoRearchive);

            NoExit = cmdline.Any(s => s.Equals(NoExitSwitch, StringComparison.OrdinalIgnoreCase));
            Log.Information("No exit ({switch}): {state}", NoExitSwitch, NoExit);

            var extractorExecutable = Configuration.Extractor.Executable;
            var extractorScript = Configuration.Extractor.PythonScript;
            IsExtractorScript = File.Exists(extractorScript);
            if (!IsExtractorScript && RequireExists(extractorExecutable, "Extractor executable", silent: true))
            {
                MessageBox.Show("Neither PixivUtil2 executable nor PixivUtil2.py specified.", "Extractor not specified", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            var extractorPath = IsExtractorScript ? extractorScript : extractorExecutable;
            var extractorWorkingDirectory = Path.GetDirectoryName(extractorPath);
            if (string.IsNullOrWhiteSpace(extractorWorkingDirectory))
            {
                MessageBox.Show($"Extractor directory unavailable ({extractorPath}).", "Extractor directory unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            else
            {
                ExtractorWorkingDirectory = extractorWorkingDirectory!;
            }

            var listFile = Configuration.ListFileName;
            var downloaderExecutable = Configuration.Downloader.Executable;
            if (RequireExists(listFile, "List file") || RequireExists(downloaderExecutable, "Downloader executable") || RequireExists($"{extractorWorkingDirectory}\\config.ini", "Extractor configuration", "\nIf this is the first run, make sure to run PixivUtil2 once to generate the default configuration file."))
                Environment.Exit(1);

            CreateDirectoryIfNotExists(Configuration.LogFolderName);
            CreateDirectoryIfNotExists(Configuration.DownloadListFolderName);
            CreateDirectoryIfNotExists(Configuration.DatabaseFolderName);
            if (Configuration.AutoArchive)
            {
                CreateDirectoryIfNotExists(Configuration.Archive.ArchiveFolder);
                CreateDirectoryIfNotExists(Configuration.Archive.BackupFolder);
                CreateDirectoryIfNotExists(Configuration.Archive.WorkingFolder);
            }

            var workerCount = Math.Max(Configuration.Parallelism.MaxExtractorParallellism, Math.Max(Configuration.Parallelism.MaxDownloaderParallellism, Configuration.Parallelism.MaxPostprocessorParallellism)) + Configuration.Parallelism.MaxFFmpegParallellism + 4; // 4 for fallback
            if (!ThreadPool.SetMinThreads(workerCount, workerCount))
                Log.Warning("Failed to set min thread pool workers.");
            if (!ThreadPool.SetMaxThreads(workerCount, workerCount))
                Log.Warning("Failed to set max thread pool workers.");
        }

        private static bool RequireExists(string fileName, string fileDetails, string? extraComments = null, bool silent = false)
        {
            if (!File.Exists(fileName))
            {
                if (!silent)
                {
                    Log.Error("{name} is not available: {details}", fileName, fileDetails);
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
                Log.Warning("Creating {name} directory as it does not exists.", dirName);
                Directory.CreateDirectory(dirName);
            }
        }
    }
}
