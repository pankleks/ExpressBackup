using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ExpressBackup
{
    enum LogSeverity
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    static class Log
    {
        public static int
            LogLevel = 0;
        readonly static Dictionary<LogSeverity, char>
            severityCode = new Dictionary<LogSeverity, char>
            {
                { LogSeverity.Debug, 'D' },
                { LogSeverity.Info, 'I' },
                { LogSeverity.Warning, 'W' },
                { LogSeverity.Error, 'E' }
            };

        public static void Entry(LogSeverity severity, string text, params object[] args)
        {
            if (LogLevel > (int)severity)
                return;

            Trace.WriteLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") + ' ' + severityCode[severity] + ": " + string.Format(text, args));
        }
    }
}
