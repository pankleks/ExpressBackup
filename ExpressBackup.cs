using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using System.Linq;

namespace ExpressBackup
{
    /*
    Express Backup (http://expressbackup.codeplex.com/license)
    Copyright (c) 2012 Krzysztof Heim

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
    
    This code uses 3'rd party libraries under following licenses:
        - SSH.NET (http://sshnet.codeplex.com/license)
     */
    class ExpressBackup
    {
        const string
            Version = "0.4.8";        

        static int Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.AutoFlush = true;

            Log.Entry(LogSeverity.Info, "ExpressBackup v{0}", Version);

            if (args.Length < 1 || args[0] == "?")
            {
                Console.WriteLine("Usage: ExpressBackup.exe config_file.xml [test]");
                Console.WriteLine("For more info and updates visit http://expressbackup.codeplex.com");
                return 1;
            }

            Config
                config;

            try
            {
                var
                    serializer = new XmlSerializer(typeof(Config));

                using (var fs = File.OpenText(args[0]))
                    config = (Config)serializer.Deserialize(fs);

                // task validation
                config.Tasks.ForEach(e => e.Validate(config));
            }
            catch (Exception ex)
            {
                Log.Entry(LogSeverity.Error, "config load failed: {0}", ex);
                return 2;
            }

            Log.LogLevel = config.LogLevel;
            Trace.Listeners.Add(new TextWriterTraceListener(config.LogFile));

            if (args.Length > 1 && args[1] == "test")
            {
                if (config.Smtp != null)
                {
                    Console.WriteLine("sending test e-mail to: " + config.OnFailureMail);

                    SmtpHelper.Send(config.Smtp, config.OnFailureMail, "ExpressBackup test mail", "If you read this everything looks ok!");
                }

                return 0;
            }

            var
                timeStamp = DateTime.UtcNow;

            Log.Entry(LogSeverity.Info, "time stamp {0}, zip {1}, smtp {2}",
                timeStamp.ToString("yyyy-MM-dd HH:mm:ss"),
                string.IsNullOrEmpty(config.ZipPassword) ? "no encryption" : config.ZipPassword,
                config.Smtp == null ? "no" : "yes");

            foreach (var task in config.Tasks.Where(e => !e.Disabled))
            {
                Log.Entry(LogSeverity.Info, "executing task [{0}]", task.ID);

                DateTime
                    start = DateTime.UtcNow;
                try
                {
                    task.Execute(config, timeStamp);
                    Log.Entry(LogSeverity.Info, "task [{0}] succeeded, t = {1}", task.ID, DateTime.UtcNow - start);
                }
                catch (Exception ex)
                {
                    Log.Entry(LogSeverity.Error, "task [{0}] failed, {1}", task.ID, ex);

                    if (config.Smtp != null && !string.IsNullOrEmpty(config.OnFailureMail))
                    {
                        Log.Entry(LogSeverity.Debug, "failure mail to {0}", config.OnFailureMail);

                        SmtpHelper.Send(
                            config.Smtp,
                            config.OnFailureMail,
                            string.Format("ExpressBackup task {0} failed", task.ID),
                            string.Format("<p>ExpressBackup task {0} failed</p><p>timeStamp = {1}<p>{2}</p>", task.ID, timeStamp, ex));
                    }

                    if (config.StopAtError)
                    {
                        Log.Entry(LogSeverity.Debug, "stop due to failure");
                        return 4;
                    }
                }
            }

            return 0;
        }
    }
}