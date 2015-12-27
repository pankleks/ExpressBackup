using Renci.SshNet;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace ExpressBackup
{
    public abstract class Uploader
    {
        public string
            Host,
            User,
            Password,
            Path;
        [XmlAttribute]
        public bool
            Disabled = false;

        public abstract void Upload(string file);
        public abstract void Delete(string file);
        public abstract string[] GetFileList(string path);

        protected void PrintProgress(long uploaded, long total)
        {
            string
                progress = string.Format("{0:0.0}%", (float)uploaded / total * 100);

            Console.Write(progress);

            foreach (var c in progress)
                Console.Write("\b");
        }
    }

    public class FtpUploader : Uploader
    {
        public override void Upload(string file)
        {
            var
                helper = new FtpHelper(this.Host, this.User, this.Password, this.Path);

            helper.Progress += (uploaded, total) => PrintProgress(uploaded, total);

            Log.Entry(LogSeverity.Debug, "ftp upload {0}.7z to {1}", file, this.Host);

            Console.Write("done ");

            helper.UploadFile(file + ".7z");

            Console.WriteLine();
        }

        public override void Delete(string file)
        {
            new FtpHelper(this.Host, this.User, this.Password, this.Path).DeleteFile(file);
        }

        public override string[] GetFileList(string path)
        {
            return new FtpHelper(this.Host, this.User, this.Password, this.Path).GetFileList(path);
        }
    }

    public class SftpUploader : Uploader
    {
        public int
            Port = 22;

        public override void Upload(string file)
        {
            var
                fi = new FileInfo(file + ".7z");

            Log.Entry(LogSeverity.Debug, "sftp upload {0} to {1}", fi.FullName, this.Host);

            Execute(sftp =>
            {
                sftp.ChangeDirectory(this.Path);

                Console.Write("done ");

                using (var fs = File.OpenRead(fi.FullName))
                    sftp.UploadFile(fs, fi.Name, true, uploaded => PrintProgress((long)uploaded, fs.Length));

                Console.WriteLine();
            });
        }

        public override void Delete(string file)
        {
            Execute(sftp =>
            {
                sftp.ChangeDirectory(this.Path);
                sftp.DeleteFile(file);
            });
        }

        public override string[] GetFileList(string path)
        {
            var
                temp = new string[0];

            Execute(sftp =>
            {
                temp = sftp.ListDirectory(path).Where(e => e.IsRegularFile).Select(e => e.Name).ToArray();
            });

            return temp;
        }

        void Execute(Action<SftpClient> action)
        {
            using (var client = new SftpClient(this.Host, this.Port, this.User, this.Password))
            {
                client.Connect();

                try
                {
                    action(client);
                }
                finally
                {
                    client.Disconnect();
                }
            }
        }
    }
}
