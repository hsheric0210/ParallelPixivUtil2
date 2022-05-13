# Parallel PixivUtil2
A cheap way to run PixivUtil2 in parallel

# Known Error Messages

* ERROR: pixivutil2.exe is not located in working directory.

* ERROR: list.txt is not located in working directory.

* ERROR: config.ini is not located in working directory.

* ERROR: Failed to execute pixivutil2

# IPC format
* Handshake
  * Receive client handshake (example: Frames { UID, '', "HS", "PU2" }) ('PU2' stands for PixivUtil2)
  * Send server handshake (example: Frames { UID, '', "HS", Program Name and Version})

# Warning
I'd recommend you to use alternate pixiv account (secondary account).

Because sometimes you can get an account suspension for too many requests.

https://github.com/Nandaka/PixivUtil2/issues/477