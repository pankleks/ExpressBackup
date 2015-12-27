using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.SqlServer.Management.Smo;
using System.Linq;

namespace ExpressBackup
{
    static class Executor
    {
        static Server ConnectDatabase(SqlTask task)
        {
            var
                server = new Server(task.SqlServer);

            if (!string.IsNullOrEmpty(task.SqlUser))
            {
                server.ConnectionContext.LoginSecure = false;
                server.ConnectionContext.Login = task.SqlUser;
                server.ConnectionContext.Password = task.SqlPassword;
            }

            return server;
        }

        static string FileWithoutExtention(string file)
        {
            int
                i = file.LastIndexOf('.');
            if (i == -1)
                throw new Exception("no file extention: " + file);

            return file.Substring(i);
        }

        static void Zip(Config config, string zipFile, string toCompress)
        {
            var
                psi = new ProcessStartInfo
                {
                    FileName = "7z.exe",
                    Arguments = string.Format("u {0}.7z {1}{2}", zipFile, toCompress, string.IsNullOrEmpty(config.ZipPassword) ? string.Empty : " -p" + config.ZipPassword),
                    //WindowStyle = ProcessWindowStyle.Hidden
                };

            Log.Entry(LogSeverity.Debug, "7z.exe " + psi.Arguments);

            Process
                p;
            try
            {
                p = Process.Start(psi);
            }
            catch (Exception ex)
            {
                throw new Exception("can't start 7z.exe", ex);
            }

            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception("7z.exe exited with code: " + p.ExitCode);            
        }

        public static void Backup(Config config, BackupTask task, DateTime t)
        {
            var
                server = ConnectDatabase(task);
            var
                bat = BackupActionType.Database;
            bool
                incremental = false;
            string
                prefix = "full";

            switch (task.SqlBackupType)
            {
                case BackupType.Differential:                        
                    incremental = true;
                    prefix = "diff";
                    break;
                case BackupType.TransactionLog:
                    bat = BackupActionType.Log;
                    prefix = "tlog";
                    break;
            }

            var
                backup = new Backup
                    {
                        Action = bat,
                        Database = task.SqlDatabase,
                        Incremental = incremental,
                        LogTruncation = BackupTruncateLogType.Truncate
                    };

            string
                temp = string.Format("{0}\\{1}_{2}_{3}", task.LocalPath, task.SqlDatabase, prefix, t.ToString("yyyyMMddHHmm"));
            var
                device = new BackupDeviceItem(temp + ".bak", DeviceType.File);

            backup.Devices.Add(device);

            Log.Entry(LogSeverity.Debug, "backup to {0}.bak", temp);
            backup.SqlBackup(server);

            Zip(config, temp, temp + ".bak");

            Log.Entry(LogSeverity.Debug, "delete {0}.bak", temp);
            File.Delete(temp + ".bak");

            if (task.Uploader != null && !task.Uploader.Disabled)
                task.Uploader.Upload(temp);
        }

        public static void BackupDirectory(Config config, BackupDirectoryTask task, DateTime t)
        {
            string
                zipFile = string.Format("{0}\\{1}_{2}", task.BackupPath, task.BackupPrefix, t.ToString("yyyyMMddHHmm"));

            Zip(config, zipFile, task.LocalPath);

            if (task.Uploader != null && !task.Uploader.Disabled)
                task.Uploader.Upload(zipFile);
        }

        public static void Cleanup(Config config, BackupCleanupTask task, DateTime t)
        {
            Log.Entry(LogSeverity.Debug, "local cleanup, keep {0} days", task.CleanupKeepDays);
            var
                directory = new DirectoryInfo(task.LocalPath);

            Clean(task, task.CleanupKeepDays, directory.GetFiles().ToList().ConvertAll(e => e.Name), t, e => File.Delete(task.LocalPath + '\\' + e));

            if (task.Uploader != null && !task.Uploader.Disabled)
            {
                Log.Entry(LogSeverity.Debug, "remote cleanup, keep {0} days", task.CleanupFtpKeepDays);

                Clean(task, task.CleanupFtpKeepDays, task.Uploader.GetFileList(task.Uploader.Path), t, task.Uploader.Delete);
            }
        }      

        static bool MatchFile(BackupCleanupTask task, string file)
        {
            foreach (string prefix in task.CleanupFilePrefix.Split(';').Where(e => e != string.Empty))
                if (file.StartsWith(prefix) && file.EndsWith(task.CleanupFileExtention))
                    return true;

            return false;
        }

        static bool GetBackupFileDate(BackupCleanupTask task, string file, out DateTime backupDate)
        {
            backupDate = DateTime.MaxValue;

            return 
                file.Length >= task.CleanupFileMatch.Length + task.CleanupFileExtention.Length && 
                DateTime.TryParseExact(
                    file.Substring(file.Length - (task.CleanupFileMatch.Length + task.CleanupFileExtention.Length), task.CleanupFileMatch.Length), 
                    task.CleanupFileMatch, 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, 
                    out backupDate);
        }

        static void Clean(BackupCleanupTask task, int keepDays, IEnumerable<string> files, DateTime t, Action<string> deleteF)
        {
            DateTime
                backupDate;

            foreach (string file in files.Where(e => MatchFile(task, e)))
                if (GetBackupFileDate(task, file, out backupDate))
                {
                    double
                        days = (t - backupDate).TotalDays;

                    if (days > keepDays)
                    {
                        Log.Entry(LogSeverity.Info, "deleting file {0}, {1:0.0} days old", file, days);
                        try
                        {
                            deleteF(file);
                        }
                        catch (Exception ex)
                        {
                            Log.Entry(LogSeverity.Error, "can't delete file {0}, {1}", file, ex.Message);
                        }
                    }
                    else
                        Log.Entry(LogSeverity.Debug, "file {0} keept, {1:0.0} days old", file, days);
                }
                else
                    Log.Entry(LogSeverity.Warning, "can't extract backup date, file {0}", file);                    
        }

        public static void Indexes(Config config, IndexRebuildTask task)
        {
            var
                server = ConnectDatabase(task);

            foreach (Table table in server.Databases[task.SqlDatabase].Tables)
                foreach (Index index in table.Indexes)
                    AnalyzeIndex(task, index);

            foreach (View view in server.Databases[task.SqlDatabase].Views)
                foreach (Index index in view.Indexes)
                    AnalyzeIndex(task, index);
        }

        static void AnalyzeIndex(IndexRebuildTask task, Index index)
        {
            try
            {
                var
                    temp = index.EnumFragmentation(FragmentationOption.Sampled);
                double
                    f = 0;
                int
                    n = 0;

                foreach (DataRow e in temp.Rows)
                {
                    f += (double)e["AverageFragmentation"];
                    ++n;
                }

                if (n == 0)
                {
                    Log.Entry(LogSeverity.Warning, "Index {0} have not frag. entries");
                    return;
                }

                f /= n;

                Log.Entry(LogSeverity.Debug, "Index {0} frag. {1:0.0}", index.Name, f);

                if (f > 10)
                {
                    if (f > 40 && task.IndexAllowRebuild)
                    {
                        Exception
                            ex = null;

                        if (task.IndexTryOnline)
                        {
                            if (Rebuild(index, true) != null)  
                                ex = Rebuild(index, false); // online failed (only SQL enterprise) - try offline
                        }
                        else
                            ex = Rebuild(index, false);

                        if (ex != null)
                            throw ex;
                    }
                    else
                    {
                        Log.Entry(LogSeverity.Debug, "Index {0} reorganize", index.Name);
                        index.OnlineIndexOperation = true;
                        index.Reorganize();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Entry(LogSeverity.Error, "Index {0} action failed {1}", index.Name, ex);
            }            
        }

        static Exception Rebuild(Index index, bool online)
        {
            try
            {
                Log.Entry(LogSeverity.Debug, "Index {0} rebuild {1}", index.Name, online ? "online" : "offline");

                index.OnlineIndexOperation = online;
                index.Rebuild();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static void Statistics(Config config, UpdateStatsTask task)
        {
            var
                server = ConnectDatabase(task);

            if (task.Percent < 1)
                task.Percent = 1;
            if (task.Percent > 100)
                task.Percent = 100;

            Log.Entry(LogSeverity.Debug, "sample {0}%, recompute {1}", task.Percent, task.Recompute ? "yes" : "no");

            foreach (Table table in server.Databases[task.SqlDatabase].Tables)
            {
                Log.Entry(LogSeverity.Debug, "updating stats {0}", table.Name);

                table.UpdateStatistics(StatisticsTarget.All, StatisticsScanType.Percent, task.Percent, task.Recompute);
            }
        }
    }
}
