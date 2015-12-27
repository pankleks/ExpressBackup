# ExpressBackup
Complete backup, clean-up and maintenance solution for MS-SQL and/or file system.

Features:

* Backup database
  * Backup databases to local drive or network share
  * Compress backup (with encryption)
  * Upload backup with FTP or SFTP (NEW!)
  * Cleanup old backup (local, FTP, SFTP) - cleanup can be used also to any other files (for example IIS logs)
* Manage indexes
  * Fragmentation analyze
  * Rebuild or Reorganize
  * Online operations (if possible)
* Manage statistics
  * Update statistics (percent based)
* E-Mail notifications
  * Sends notification of failed task to multiple users
* Command line tool (easy to schedule with the Task Scheduler)
  * Flexible configuration via xml file
* Supports Express editions (2005, 2008, 2012)
* Clears any files older than ... (useful for old log clearing)
* Backup local directory, zip it, and upload to FTP/SFTP
  * Usefull for SVN repository backup

ExpressBackup is command line tool, that executes different actions configured in given XML file.

You can schedule execution of these actions by Windows Task Scheduler service.

Tool requires 7zip to be installed. Just copy 7z.exe to tool directory or add PATH to 7z.exe
