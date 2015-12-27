using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Serialization;

namespace ExpressBackup
{
    class FtpHelper
    {
        readonly string
            host,
            user,
            password,
            path;
        public event Action<long, long>
            Progress;

        public FtpHelper(string host, string user, string password, string path = null)
        {
            this.host = host;
            this.user = user;
            this.password = password;

            this.path = path;

            if (this.path == null)
                this.path = string.Empty;

            if (this.path.Length > 0 && this.path[0] != '/')
                this.path = '/' + this.path;
        }

        FtpWebRequest PrepeareRequest(string fileName, string method)
        {
            var 
                request = (FtpWebRequest)WebRequest.Create(this.host + this.path + "/" + fileName);
            
            request.Method = method;
            request.Credentials = new NetworkCredential(this.user, this.password);
            request.UseBinary = true;
            request.KeepAlive = false;

            return request;
        }

        FtpWebResponse ValidateResponse(FtpWebRequest request, FtpStatusCode validStatus)
        {
            var
                response = (FtpWebResponse)request.GetResponse();

            if (response.StatusCode != validStatus)
                throw new Exception("unexpected ftp status: " + response.StatusDescription);

            return response;
        }

        const int
            bufLength = 1024 * 64;

        public void UploadFile(string fileName)
        {
            var
                fileInfo = new FileInfo(fileName);
            var 
                request = PrepeareRequest(fileInfo.Name, WebRequestMethods.Ftp.UploadFile);

            request.ContentLength = fileInfo.Length;

            using (var output = request.GetRequestStream())
            {
                var
                    buf = new byte[bufLength];

                using (var input = fileInfo.OpenRead())
                {
                    int
                        length;
                    long
                        n = 0;

                    while ((length = input.Read(buf, 0, bufLength)) != 0)
                    {
                        output.Write(buf, 0, length);

                        if (this.Progress != null)
                        {
                            n += length;
                            this.Progress(n, fileInfo.Length);
                        }
                    }
                }
            }

            ValidateResponse(request, FtpStatusCode.ClosingData).Close();
        }

        public string[] GetFileList(string path)
        {
            var 
                response = ValidateResponse(
                    PrepeareRequest(string.Empty, WebRequestMethods.Ftp.ListDirectory), 
                    FtpStatusCode.OpeningData);

            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var temp = reader.ReadToEnd();

                response.Close();

                return temp.Replace("\r\n", "\n").Split('\n').Where(e => e != string.Empty).ToArray();
            }            
        }

        public void DeleteFile(string fileName)
        {
            ValidateResponse(
                PrepeareRequest(fileName, WebRequestMethods.Ftp.DeleteFile), 
                FtpStatusCode.FileActionOK).Close();
        }
    }
}
