using System;
using System.IO;
using System.Text;

namespace VoidCraftLauncher.Services
{
    public static class LogService
    {
        private static string _logPath;
        private static readonly object _lock = new object();

        public static void Initialize(string basePath)
        {
            try
            {
                Directory.CreateDirectory(basePath);
                _logPath = Path.Combine(basePath, "launcher.log");
                
                // Rotation: If log is too big (>5MB), rename it
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 5 * 1024 * 1024)
                {
                    var backup = Path.Combine(basePath, "launcher_prev.log");
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(_logPath, backup);
                }

                Log("--- LAUNCHER STARTED ---", "INIT");
                Log($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}", "INIT");
                Log($"OS: {Environment.OSVersion}", "INIT");
            }
            catch (Exception ex)
            {
                // Last ditch effort
                System.Diagnostics.Debug.WriteLine($"Failed to init logger: {ex}");
            }
        }

        public static void Log(string message, string level = "INFO")
        {
            if (string.IsNullOrEmpty(_logPath)) return;

            try
            {
                var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                
                lock (_lock)
                {
                    File.AppendAllText(_logPath, logLine, Encoding.UTF8);
                }
                
                
                System.Diagnostics.Debug.Write(logLine);
                Console.Write(logLine);
            }
            catch
            {
                // Ignored
            }
        }

        public static void Error(string message, Exception ex = null)
        {
            var msg = message;
            if (ex != null)
            {
                msg += $"\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
            }
            Log(msg, "ERROR");
        }

        public static string GetLogContent()
        {
            if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath)) return "";
            try
            {
                lock (_lock)
                {
                    return File.ReadAllText(_logPath);
                }
            }
            catch
            {
                return "Could not read log file.";
            }
        }
        
        public static string GetLogPath() => _logPath;
    }
}
