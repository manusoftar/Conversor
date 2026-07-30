using System;
using System.Collections.Generic;
using System.IO;

namespace Conversor
{
    public static class AppLogger
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<Action<string>> Sinks = new List<Action<string>>();
        private static readonly List<string> PendingMessages = new List<string>();

        private static bool fileLoggingEnabled;
        private static string logFilePath;

        public static void RegisterSink(Action<string> sink)
        {
            if (sink == null)
                return;

            List<string> bufferedMessages;

            lock (SyncRoot)
            {
                if (!Sinks.Contains(sink))
                    Sinks.Add(sink);

                bufferedMessages = new List<string>(PendingMessages);
                PendingMessages.Clear();
            }

            foreach (string entry in bufferedMessages)
                sink(entry);
        }

        public static void ConfigureFileLogging(bool enabled, string path)
        {
            if (!enabled)
            {
                lock (SyncRoot)
                {
                    fileLoggingEnabled = false;
                    logFilePath = null;
                }
                return;
            }

            try
            {
                string normalizedPath = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(normalizedPath);

                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (!File.Exists(normalizedPath))
                {
                    using (File.Create(normalizedPath))
                    {
                    }
                }

                lock (SyncRoot)
                {
                    fileLoggingEnabled = true;
                    logFilePath = normalizedPath;
                }
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    fileLoggingEnabled = false;
                    logFilePath = null;
                }

                WriteInternal("[WARN] No se pudo habilitar el log a archivo: " + ex.Message, false);
            }
        }

        public static void Info(string message)
        {
            WriteInternal(message, true);
        }

        public static void Warn(string message)
        {
            WriteInternal("[WARN] " + message, true);
        }

        public static void Error(string message)
        {
            WriteInternal("[ERROR] " + message, true);
        }

        public static void Exception(string context, Exception ex)
        {
            if (ex == null)
            {
                Error(context);
                return;
            }

            WriteInternal("[ERROR] " + context + Environment.NewLine + ex, true);
        }

        private static void WriteInternal(string message, bool allowFileWrite)
        {
            string entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            Action<string>[] targets;
            bool shouldWriteFile;
            string targetFile;

            lock (SyncRoot)
            {
                if (Sinks.Count == 0)
                    PendingMessages.Add(entry);

                targets = Sinks.ToArray();
                shouldWriteFile = allowFileWrite && fileLoggingEnabled && !string.IsNullOrWhiteSpace(logFilePath);
                targetFile = logFilePath;
            }

            foreach (Action<string> sink in targets)
                sink(entry);

            if (!shouldWriteFile)
                return;

            try
            {
                File.AppendAllText(targetFile, entry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                lock (SyncRoot)
                {
                    fileLoggingEnabled = false;
                    logFilePath = null;
                }

                WriteInternal("[WARN] Se deshabilitó el log a archivo: " + ex.Message, false);
            }
        }
    }
}
