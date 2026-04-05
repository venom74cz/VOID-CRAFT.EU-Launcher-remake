using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher
{
    class Program
    {
        private const string PrimaryInstanceMutexName = @"Local\VoidCraftLauncher.PrimaryInstance";
        private static Mutex? _primaryInstanceMutex;

        public static string? PendingAuthCode { get; private set; }
        public static ProtocolInstallRequest? PendingInstallRequest { get; private set; }
        public static bool IsPrimaryInstance { get; private set; }

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

                var protocolRequest = ProtocolHandler.ParseLaunchRequest(args);

                _primaryInstanceMutex = new Mutex(false, PrimaryInstanceMutexName);
                IsPrimaryInstance = TryAcquirePrimaryInstanceMutex(_primaryInstanceMutex);

                if (!IsPrimaryInstance && protocolRequest?.InstallRequest != null)
                {
                    LogService.Log($"Forwarding install deeplink for {protocolRequest.InstallRequest.Slug} to running instance.");
                    ProtocolHandler.WriteInstallRequestToFile(protocolRequest.InstallRequest);
                    return;
                }

                // Registry registration is low priority; keep cold start focused on bringing up the shell.
                _ = Task.Run(ProtocolHandler.RegisterProtocol);

                if (!string.IsNullOrEmpty(protocolRequest?.AuthCode))
                {
                    LogService.Log("Launched with Auth Code");
                    // We were launched by browser redirect - write code to file for main instance
                    ProtocolHandler.WriteAuthCodeToFile(protocolRequest.AuthCode);
                    
                    PendingAuthCode = protocolRequest.AuthCode;
                }

                if (protocolRequest?.InstallRequest != null)
                {
                    LogService.Log($"Launched with install deeplink for {protocolRequest.InstallRequest.Slug}.");
                    PendingInstallRequest = protocolRequest.InstallRequest;
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

        public static ProtocolInstallRequest? TakePendingInstallRequest()
        {
            var request = PendingInstallRequest;
            PendingInstallRequest = null;
            return request;
        }

        private static bool TryAcquirePrimaryInstanceMutex(Mutex mutex)
        {
            try
            {
                return mutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }
    }
}
