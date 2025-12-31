using Avalonia;
using System;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher
{
    class Program
    {
        public static string? PendingAuthCode { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            // Init Global Logging
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var basePath = System.IO.Path.Combine(docs, ".voidcraft");
            LogService.Initialize(basePath);

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                LogService.Error("Unhandled Exception (AppDomain)", error.ExceptionObject as Exception);
            };

            try
            {
                LogService.Log("Launcher starting...");
                
                // Register protocol handler on startup
                ProtocolHandler.RegisterProtocol();

                // Check if launched via protocol with auth code
                var code = ProtocolHandler.ExtractAuthCode(args);
                if (!string.IsNullOrEmpty(code))
                {
                    LogService.Log("Launched with Auth Code");
                    // We were launched by browser redirect - write code to file for main instance
                    ProtocolHandler.WriteAuthCodeToFile(code);
                    
                    PendingAuthCode = code;
                }

                // BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                LogService.Error("Fatal Startup Error", ex);
                throw; // Crash gracefully
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
