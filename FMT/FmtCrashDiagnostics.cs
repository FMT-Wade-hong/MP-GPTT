using System;
using System.IO;
using System.Text;
using System.Threading;
using MissionPlanner.Utilities;

namespace MissionPlanner.FMT
{
    // Local evidence survives even when the exception-report window cannot open.
    // Never send these records to a server. Keep at most three small files.
    internal static class FmtCrashDiagnostics
    {
        private static readonly object Sync = new object();
        [ThreadStatic] private static bool recording;
        internal const int MaximumBytes = 256 * 1024;

        internal static void Record(string context, Exception exception = null)
        {
            if (recording) return;
            recording = true;
            try
            {
                Append(Path.Combine(Settings.GetDataDirectory(), "Diagnostics", "crash.log"),
                    context, exception);
            }
            catch { /* Diagnostics must not cause a second application failure. */ }
            finally { recording = false; }
        }

        internal static void Append(string path, string context, Exception exception)
        {
            var details = exception?.ToString() ?? "";
            if (details.Length > 24000) details = details.Substring(0, 24000) + "\n[truncated]";
            var entry = DateTime.UtcNow.ToString("O") + " | " + context + " | " +
                typeof(FmtCrashDiagnostics).Assembly.GetName().Version + " | " +
                (Environment.Is64BitProcess ? "64-bit" : "32-bit") + " | thread " +
                Thread.CurrentThread.ManagedThreadId + Environment.NewLine + details + Environment.NewLine;
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(entry) > MaximumBytes)
                {
                    if (File.Exists(path + ".2")) File.Delete(path + ".2");
                    if (File.Exists(path + ".1")) File.Move(path + ".1", path + ".2");
                    File.Move(path, path + ".1");
                }
                File.AppendAllText(path, entry, new UTF8Encoding(false));
            }
        }
    }
}
