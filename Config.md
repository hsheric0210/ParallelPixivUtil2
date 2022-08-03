# Configuration
This document explains about the configuration file of ParallelPixivUtil2

## Notes
* The configuration uses 'INI' file format
* Section feature of INI is not available these following sections are just the groups of settings to improve readbility, not a *REAL* INI section.

## Parallellism
* MaxPixivUtil2ExtractParallellism (Default: 8)
    - Maximum simultaneous instances of [PixivUtil2](https://github.com/Nandaka/PixivUtil2) that could run in parallel. In "Extraction" phase.
* MaxAria2Parallellism (Default: 4)
    - Maximum simultaneous instanceos [aria2](https://github.com/aria2/aria2) that could run in parallel.
* MaxPixivUtil2PostprocessParallellism (Default: 16)
    - Maximum simultaneous instances of [PixivUtil2](https://github.com/Nandaka/PixivUtil2) that could run in parallel. But in "Post-processing" phase.
* FFmpegParallellism (Default: 4)
    - Maximum simultaneous instances of [FFmpeg](https://github.com/FFmpeg/FFmpeg) that could run in parallel.
    - This only applied on the FFmpeg tasks requested by PixivUtil2

## Extractor (+ Member Data Extractor and Post-processor)
* ExtractorExecutable (Default: PixivUtil2.exe)
    - The PixivUtil2 executable name (Usually PixivUtil2.exe)
* ExtractorScript (Default: PixivUtil2.py)
    - The PixivUtil2 python script name (Usually PixivUtil2.py)
    - Not required if the file named 'ExtractorExecutable' is exists and accessible.
* ExtractMemberDataListParameters (Default: -s q ${memberDataList} ${memberIDs} -x -l "${logPath}\dumpMembers.log")
    - PixivUtil2 parameters used on "Extract Member Data" phase.
* ExtractorParameters (Default: -s 1 ${memberID} --sp=${page} --ep=${page} -x --pipe=${ipcAddress} --db="${databasePath}${memberID}.p${fileIndex}.db" -l "${logPath}Extractor.${memberID}.p${fileIndex}.log" --aria2="${aria2InputPath}${memberID}.p${fileIndex}.txt")
    - PixivUtil2 parameters used on "Extraction" phase.
* PostprocessorParameters (Default: -s 1 ${memberID} --sp=${page} --ep=${page} -x --pipe=${ipcAddress} --db="${databasePath}${memberID}.p${fileIndex}.db" -l "${logPath}Postprocessor.${memberID}.p${fileIndex}.log")
    - PixivUtil2 parameters used on "Post-processing" phase.

## Downloader Input Buffering
ParallelPixivUtil2 captures extracted image URL from PixivUtil2 by IPC and 'buffers' it for few seconds and write to aria2 input file AT ONCE.
* DownloadInputFlushDelay
    - Downloader input buffer flush timer delay
* DownloadInputFlushPeriod
    - Downloader input buffer flush timer period

## Downloader
* DownloaderExecutable (Default: aria2c.exe)
    - The aria2 executable name (Usually aria2c.exe)
* DownloaderParameters (Default: -i"${aria2InputPath}${memberID}.p${fileIndex}.txt" -l"${logPath}aria2.${memberID}.p${fileIndex}.log" -j16 -x2 -m0 -Rtrue --allow-overwrite=true --auto-file-renaming=false --auto-save-interval=15 --conditional-get=true --retry-wait=10 --no-file-allocation-limit=2M)
    - aria2 parameters used on download phase.
    - Tuning aria2 parameters according to your network status is HIGHLY ENCOURAGED. [#aria2c Options](https://aria2.github.io/manual/en/html/aria2c.html)

## FFmpeg
* FFmpegExecutable (Default: FFmpeg.exe)
    - The FFmpeg executable name (Usually FFmpeg.exe)

## File Names
* MemberDataFile (Default: memberdata.txt)
    - The output file name of Member Data Extractor.
* ListFile (Default: list.txt)
    - The list file. All member ids should be listed in this file.

## IPC
* IPCCommunicatePort (Default: 6974)
    - The port number which the IPC Communicate Socket uses.
* IPCTaskPort (Default: 7469)
    - The port number which the IPC Task-request Socket uses.

// TODO
## Auto-archive
The integration of [PixivArchiver](https://github.com/hsheric0210/PixivArchiver).
* AutoArchive (Default: false)
    - Should we have to enable the Auto Archive feature?
* ArchiveRepository
    - The archive repository, where the member archive files should be stored.
* UnarchiverExecutable (Default: 7z.exe)
    - The unarchiver to unarchive existing member archives from archive repo.
    - 7-Zip recommended
* UnarchiverParameter (Default: x -o${destination}\${archiveName} ${archive})
    - Unarchiver parameter format
* ArchiverExecutable (Default: Hybrid7z.exe)
    - The archiver to re-archive the updated archive.
    - [Hybrid7z](https://github.com/hsheric0210/Hybrid7z) recommended, but you can still use 7-Zip. (You need to edit parameters according to 7-Zip when using 7-Zip instead of Hybrid7z)
* ArchiverParameter (Default: -nopause ${archives})
    - Archiver parameter format
* ArchiveBackupDirectory
    - The directory where the existing member archive files should be copied to.
    - Should be differ to ArchiveWorkingDirectory
* ArchiveWorkingDirectory
    - The directory where the downloaded images and things located in.
    - Should be same with the "Settings.rootDirectory" of PixivUtil2's configuration. (Or the archiving sequences will not work as intended)
* ArchivedFileFormatWildCard (Default: *.7z)
* ArchivedFileFormatRegex (Default: \d+\.7z)
* ArchiveDirectoryFormatWildCard (Default: *)
* ArchiveDirectoryFormatRegex (Default: ^\d+$)

## Miscellaneous
* MaxImagesPerPage (Default: 48)
    - Maximum image count of a page can have in Pixiv
    - Don't change this value! Unless the Pixiv updates their site layout/configuration.
