using System;

namespace VoidCraftLauncher.Models
{
    public class AccountProfile
    {
        /// <summary>Unique ID for this profile</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Display name / MC username</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Minecraft UUID (null for offline accounts)</summary>
        public string? Uuid { get; set; }

        /// <summary>Account type: Microsoft or Offline</summary>
        public AccountType Type { get; set; }

        /// <summary>MSAL account identifier for silent login (MS accounts only)</summary>
        public string? MsalAccountId { get; set; }

        /// <summary>Last time this profile was used</summary>
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    }

    public enum AccountType
    {
        Microsoft,
        Offline
    }
}
