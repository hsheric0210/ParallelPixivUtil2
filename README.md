# Parallel PixivUtil2
A cheap way to run PixivUtil2 in parallel

## [Configuration](Config.md)

You can specify the custom config file with `-c` switch.
To generate default config file, launch program with `-gc` switch applied.

## Warning
I'd recommend you to use alternative pixiv account (a.k.a. Alt).

Because sometimes you can get an account suspension for too many requests.

![#](sus.png)

https://github.com/Nandaka/PixivUtil2/issues/477

## Command-line switches

|Switch|Details|Usage|
|:---|---:|:---|
|-c|Specify the custom config file|-c"C:\\A\\B\\C\\D\\customConfig.json"|
|-gc|Generate the default config file with name `parallel.json`|-gc|
|-no-unarchive|Skip the unarchiving phase. Only have effect if AutoArchive option is enabled in config file.|-no-unarchive|
|-no-extract|Skip the extraction phase. Only process existing images and aria2 list files.|-no-extract|
|-no-download|Skip the download phase. Completely disables all aria2 calls.|-no-download|
|-no-postprocess|Skip the post-processing phase. Disables all kind of post-processings such as Ugoira-to-WebM conversion.|-no-postprocess|
|-no-rearchive|Skip the re-archiving phase. nly have effect if AutoArchive option is enabled in config file.|-no-rearchive|
|-noexit|Do not terminate the program after all phases are finished.|-noexit|
