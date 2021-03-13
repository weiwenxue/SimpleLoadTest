using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SimpleLoadTest
{
    public static class Logger
    {
        private static Mutex m_mutex = new Mutex();

        private static string logFilePath;

        public static void Init(string header = null)
        {
            DateTime dt = DateTime.Now;
            string logFolder = string.Format("{0}\\Log", Environment.CurrentDirectory);
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
            logFilePath = string.Format("{0}\\log_{1:yyyyMMdd}-{1:HHmmss}.csv", logFolder, dt);
            if (!string.IsNullOrEmpty(header))
            {
                DoLog(header);
            }
        }

        public static string log_file_path { get { return logFilePath; } }
        public static void Log(string content)
        {
            DateTime now = DateTime.Now;
            string toLog = string.Format("{0:yyyy-MM-dd HH:mm:ss tt}.{1:000}, {2}", now, now.Millisecond, content);
            Thread t = new Thread(() => DoLog(toLog));
            t.Start();
        }

        private static void DoLog(string content)
        {
            m_mutex.WaitOne();
            if (string.IsNullOrEmpty(logFilePath))
            {
                Init();
            }
            StreamWriter sw = new StreamWriter(logFilePath, true);
            sw.WriteLine(content);
            sw.Close();
            m_mutex.ReleaseMutex();
        }
    }
}