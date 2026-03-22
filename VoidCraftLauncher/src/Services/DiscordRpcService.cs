using DiscordRPC;
using DiscordRPC.Logging;
using System;

namespace VoidCraftLauncher.Services
{
    public class DiscordRpcService : IDisposable
    {
        private DiscordRpcClient? _client;
        private const string ApplicationId = "1480215609156435988";
        private DateTime _startTime;

        public bool IsInitialized => _client?.IsInitialized == true;
        public string CurrentDetails { get; private set; } = "";
        public string CurrentState { get; private set; } = "";
        public event Action? PresenceChanged;

        public DiscordRpcService()
        {
            _startTime = DateTime.UtcNow;
        }

        public void Initialize()
        {
            _client = new DiscordRpcClient(ApplicationId);
            
            // Set logger to avoid missing errors, but keep it quiet otherwise
            _client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            _client.OnReady += (sender, e) =>
            {
                Console.WriteLine("Discord RPC Ready: {0}", e.User.Username);
            };

            _client.Initialize();

            SetState("V hlavní nabídce", "Prochází modpacky");
        }

        public void SetState(string details, string state, string largeImageKey = "icon", string largeImageText = "VOID-CRAFT.EU Launcher")
        {
            CurrentDetails = details;
            CurrentState = state;

            if (_client == null || !_client.IsInitialized)
            {
                PresenceChanged?.Invoke();
                return;
            }

            _client.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets()
                {
                    LargeImageKey = largeImageKey,
                    LargeImageText = largeImageText
                },
                Timestamps = new Timestamps(_startTime)
            });

            PresenceChanged?.Invoke();
        }

        public void SetPlayingState(string modpackName)
        {
            CurrentDetails = $"Hraje: {modpackName}";
            CurrentState = "Ve hře";

            if (_client == null || !_client.IsInitialized)
            {
                PresenceChanged?.Invoke();
                return;
            }

            _client.SetPresence(new RichPresence()
            {
                Details = CurrentDetails,
                State = CurrentState,
                Assets = new Assets()
                {
                    LargeImageKey = "icon",
                    LargeImageText = "VOID-CRAFT.EU Launcher"
                },
                Timestamps = Timestamps.Now // Reset time for playing
            });

            PresenceChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }
    }
}
