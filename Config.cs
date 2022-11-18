using System.IO;

namespace ParallelPixivUtil2
{
	public class Config
	{
		public string MemberListFileName
		{
			get; set;
		} = "memberdata.txt";

		public string ListFileName
		{
			get; set;
		} = "list.txt";

		public string LogFolderName
		{
			get; set;
		} = "logs";

		public string DownloadListFolderName
		{
			get; set;
		} = "dl_list";

		public string DatabaseFolderName
		{
			get; set;
		} = "db";

		public ParallelismSection Parallelism
		{
			get; set;
		} = new ParallelismSection();

		public MemberListExtractorSection MemberListExtractor
		{
			get; set;
		} = new MemberListExtractorSection();

		public ExtractorSection Extractor
		{
			get; set;
		} = new ExtractorSection();

		public DownloaderSection Downloader
		{
			get; set;
		} = new DownloaderSection();

		public PostprocessorSection Postprocessor
		{
			get; set;
		} = new PostprocessorSection();

		public ArchiverSection Archiver
		{
			get; set;
		} = new ArchiverSection();

		public UnarchiverSection Unarchiver
		{
			get; set;
		} = new UnarchiverSection();

		public FFmpegSection FFmpeg
		{
			get; set;
		} = new FFmpegSection();

		public bool AutoArchive
		{
			get; set;
		} = false;

		public ArchiveSection Archive
		{
			get; set;
		} = new ArchiveSection();

		public MagicSection Magics
		{
			get; set;
		} = new MagicSection();
	}

	public class ParallelismSection
	{
		public int MaxExtractorParallellism
		{
			get; set;
		} = 8;

		public int MaxDownloaderParallellism
		{
			get; set;
		} = 4;

		public int MaxPostprocessorParallellism
		{
			get; set;
		} = 16;

		public int MaxFFmpegParallellism
		{
			get; set;
		} = 4;

		public int MaxUnarchiverParallellism
		{
			get; set;
		} = 4;

		public int MaxArchiverParallellism
		{
			get; set;
		} = 1;
	}

	public class MemberListExtractorSection
	{
		public string Executable
		{
			get; set;
		} = "PixivUtil2.exe";

		public string PythonScript
		{
			get; set;
		} = "PixivUtil2.py";

		public string Parameters
		{
			get; set;
		} = "-s q ${memberDataList} ${memberIDs} -x -l \"${logPath}\\dumpMembers.log\"";

		public bool ShowWindow
		{
			get; set;
		} = false;
	}

	public class ExtractorSection
	{
		public string Executable
		{
			get; set;
		} = "PixivUtil2.exe";

		public string PythonScript
		{
			get; set;
		} = "PixivUtil2.py";

		public string Parameters
		{
			get; set;
		} = "-s 1 ${memberID} --sp=${page} --ep=${page} -x --pipe=${ipcAddress} --db=\"${databasePath}\\${memberID}.p${fileIndex}.db\" -l \"${logPath}\\Extractor.${memberID}.p${fileIndex}.log\" --aria2=\"${aria2InputPath}\\${memberID}.p${fileIndex}.txt\"";

		public int FlushDelay
		{
			get; set;
		} = 1000;

		public int FlushPeriod
		{
			get; set;
		} = 10000;

		public bool ShowWindow
		{
			get; set;
		} = false;
	}

	public class DownloaderSection
	{
		public string Executable
		{
			get; set;
		} = "aria2c.exe";

		public string Parameters
		{
			get; set;
		} = "-i\"${aria2InputPath}\\${memberID}.p${fileIndex}.txt\" -l\"${logPath}\\aria2.${memberID}.p${fileIndex}.log\" -j16 -x2 -m0 -Rtrue --allow-overwrite=true --auto-file-renaming=false --auto-save-interval=15 --conditional-get=true --retry-wait=10 --no-file-allocation-limit=2M";

		public bool ShowWindow
		{
			get; set;
		} = false;
	}

	public class PostprocessorSection
	{
		public string Executable
		{
			get; set;
		} = "PixivUtil2.exe";

		public string PythonScript
		{
			get; set;
		} = "PixivUtil2.py";

		public string Parameters
		{
			get; set;
		} = "-s 1 ${memberID} --sp=${page} --ep=${page} -x --pipe=${ipcAddress} --db=\"${databasePath}\\${memberID}.p${fileIndex}.db\" -l \"${logPath}\\Postprocessor.${memberID}.p${fileIndex}.log\"";

		public bool ShowWindow
		{
			get; set;
		} = false;
	}

	public class UnarchiverSection
	{
		public string Executable
		{
			get; set;
		} = "7z.exe";

		public string Parameters
		{
			get; set;
		} = "x -bsp2 -o${destination}\\${archiveName} ${archive}";

		public bool AllAtOnce
		{
			get; set;
		} = false;
	}

	public class ArchiverSection
	{
		public string Executable
		{
			get; set;
		} = "Hybrid7z.exe";

		public string Parameters
		{
			get; set;
		} = "-nopause ${archives}";

		public bool AllAtOnce
		{
			get; set;
		} = true;
	}

	public class ArchiveSection
	{
		public string ArchiveFolder
		{
			get; set;
		} = "";

		public string BackupFolder
		{
			get; set;
		} = "";

		public string WorkingFolder
		{
			get; set;
		} = "";

		public string ArchiveFormatWildcard
		{
			get; set;
		} = "*.7z";

		public string ArchiveFormatRegex
		{
			get; set;
		} = "\\d+\\.7z";

		public string DirectoryFormatWildcard
		{
			get; set;
		} = "*";

		public string DirectoryFormatRegex
		{
			get; set;
		} = "^\\d+$";

		public bool SearchTopDirectoryOnly
		{
			get; set;
		} = true;

		public bool DeleteWorkingAfterExecution
		{
			get; set;
		} = false;
	}

	public class FFmpegSection
	{
		public string Executable
		{
			get; set;
		} = "FFmpeg.exe";
	}

	public class MagicSection
	{
		public int MaxImagesPerPage
		{
			get; set;
		} = 48;

		public int IPCCommunicatePort
		{
			get; set;
		} = 6974;

		public int IPCTaskPort
		{
			get; set;
		} = 7469;
	}
}
