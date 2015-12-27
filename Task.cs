using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Serialization;

namespace ExpressBackup
{
    public class Config
    {
        public string
            LogFile = "ExpressBackup.log",
            ZipPassword;
        public int
            LogLevel = 0;
        public bool
            StopAtError = false;
        public Smtp
            Smtp;
        public string
            OnFailureMail;
        [
            XmlArrayItem("BackupTask", typeof(BackupTask)),
            XmlArrayItem("BackupCleanupTask", typeof(BackupCleanupTask)),
            XmlArrayItem("IndexRebuildTask", typeof(IndexRebuildTask)),
            XmlArrayItem("UpdateStatsTask", typeof(UpdateStatsTask)),
            XmlArrayItem("BackupDirectoryTask", typeof(BackupDirectoryTask))
        ]
        public List<Task>
            Tasks;
    }

    public abstract class Task
    {
        [XmlAttribute]
        public string
            ID;
        [XmlAttribute]
        public bool
            Disabled = false;

        public abstract void Execute(Config config, DateTime t);

        public virtual void Validate(Config config)
        {
        }

        protected void Check<T>(string field, params T[] values)
        {
            var
                fi = this.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);

            Debug.Assert(fi != null);

            var
                fieldValue = (T)fi.GetValue(this);

            foreach (var value in values)
                if (object.Equals(fieldValue, value))
                    throw new Exception("task [" + this.ID + "] > [" + field + "] is required");
        }
    }

    public abstract class SqlTask : Task
    {
        // Sql connection
        public string
            SqlServer,
            SqlDatabase,
            SqlUser,
            SqlPassword;
        public bool
            IntegratedSecurity;

        public override void Validate(Config config)
        {
            Check("SqlServer", null, string.Empty);
            Check("SqlDatabase", null, string.Empty);
        }
    }

    public enum BackupType
    {
        Full,
        Differential,
        TransactionLog
    }

    public class BackupTask : SqlTask
    {
        public BackupType
            SqlBackupType;
        // Local
        public string
            LocalPath;
        // Uploader
        [
            XmlElement("Ftp", typeof(FtpUploader)),
            XmlElement("Sftp", typeof(SftpUploader))
        ]
        public Uploader
            Uploader;

        public override void Execute(Config config, DateTime t)
        {
            Executor.Backup(config, this, t);
        }
    }

    public class BackupDirectoryTask : SqlTask
    {
        // Local
        public string
            LocalPath,
            BackupPath,
            BackupPrefix;
        // Uploader
        [
            XmlElement("Ftp", typeof(FtpUploader)),
            XmlElement("Sftp", typeof(SftpUploader))
        ]
        public Uploader
            Uploader;

        public override void Execute(Config config, DateTime t)
        {
            Executor.BackupDirectory(config, this, t);
        }

        public override void Validate(Config config)
        {
            Check("LocalPath", null, string.Empty);
            Check("BackupPath", null, string.Empty);
            Check("BackupPrefix", null, string.Empty);
        }
    }

    public class BackupCleanupTask : Task
    {
        public string
            CleanupFilePrefix,
            CleanupFileExtention = ".7z",
            CleanupFileMatch = "yyyyMMddHHmm";
        public int
            CleanupKeepDays = -1,
            CleanupFtpKeepDays = -1;
        public string
            LocalPath;
        // Uploader
        [
            XmlElement("Ftp", typeof(FtpUploader)),
            XmlElement("Sftp", typeof(SftpUploader))
        ]
        public Uploader
            Uploader;

        public override void Execute(Config config, DateTime t)
        {    
            Executor.Cleanup(config, this, t);
        }

        public override void Validate(Config config)
        {
            Check("LocalPath", null, string.Empty);
            Check("CleanupKeepDays", -1);

            if (this.CleanupFtpKeepDays == -1)
                this.CleanupFtpKeepDays = this.CleanupKeepDays;
        }
    }

    public class IndexRebuildTask : SqlTask
    {
        public bool
            IndexAllowRebuild = true,
            IndexTryOnline = true;

        public override void Execute(Config config, DateTime t)
        {       
            Executor.Indexes(config, this);
        }
    }

    public class UpdateStatsTask : SqlTask
    {
        public int
            Percent = 100;
        public bool
            Recompute = false;

        public override void Execute(Config config, DateTime t)
        {
            Executor.Statistics(config, this);
        }
    }
}