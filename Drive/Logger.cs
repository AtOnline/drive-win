using DokanNet.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Drive
{
    public class Logger : ILogger
    {
        string logFilePath;
        object locker = new object();

        public Logger(string prefix)
        {
             logFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            logFilePath = Path.Combine(logFilePath, "AtOnline", "Drive");
            logFilePath += "\\"+prefix + " - "+System.DateTime.Today.ToString("MM-dd-yyyy") + "." + "log";
        }

        public void Debug(string message, params object[] args)
        {
#if LOG_ENABLED && LOG_DEBUG
            
            ThreadPool.QueueUserWorkItem(task =>
            {
                WriteLog("[Debug] " + message);
                if(args != null){
                    foreach (var a in args)
                    {
                        WriteLog("[Debug - Args] " + a?.ToString());
                    }
                }
            });
           
           
#endif
        }

        public void Error(string message, params object[] args)
        {
#if LOG_ENABLED  && LOG_ERROR
            ThreadPool.QueueUserWorkItem(task =>
            {
                WriteLog("[Error] " + message);
                if (args != null)
                {
                    foreach (var a in args)
                    {
                        WriteLog("[Error - Args] " + a?.ToString());
                    }
                }
            });

#endif
        }

        public void Fatal(string message, params object[] args)
        {
#if LOG_ENABLED  && LOG_FATAL
            ThreadPool.QueueUserWorkItem(task =>
            {
                WriteLog("[Fatal] " + message);
                if (args != null)
                {
                    foreach (var a in args)
                    {
                        WriteLog("[Fatal - Args] " + a?.ToString());
                    }
                }
            });

#endif
        }

        public void Info(string message, params object[] args)
        {
#if LOG_ENABLED  && LOG_INFO
            ThreadPool.QueueUserWorkItem(task =>
            {
                WriteLog("[Info] " + message);
                 if(args != null){
                    foreach (var a in args)
                    {
                        WriteLog("[Info - Args] " + a?.ToString());
                    }
                }
            });

#endif
        }

        public void Warn(string message, params object[] args)
        {
#if LOG_ENABLED  && LOG_WARN
            ThreadPool.QueueUserWorkItem(task =>
            {
                WriteLog("[Warn] " + message);
                if (args != null)
                {
                    foreach (var a in args)
                    {
                        WriteLog("[Warn - Args] " + a?.ToString());
                    }
                }
            });

#endif
        }

        public void WriteLog(string strLog)
        {
#if LOG_ENABLED
            lock (locker)
            {
                StreamWriter log;
                FileStream fileStream = null;
                DirectoryInfo logDirInfo = null;
                FileInfo logFileInfo;

                logFileInfo = new FileInfo(logFilePath);
                logDirInfo = new DirectoryInfo(logFileInfo.DirectoryName);
                if (!logDirInfo.Exists) logDirInfo.Create();
                if (!logFileInfo.Exists)
                {
                    fileStream = logFileInfo.Create();
                }
                else
                {
                    fileStream = new FileStream(logFilePath, FileMode.Append);
                }
                log = new StreamWriter(fileStream);
                log.WriteLine(strLog);
                log.Dispose();
                log.Close();

                fileStream.Dispose();
                fileStream.Close();
            }
#endif
        }
    }
}
