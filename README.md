# IO - VoidCraft Launcher

Official custom Minecraft launcher for the **VOID-CRAFT** community. Built with .NET 9 (C#) and Avalonia UI.

![VoidCraft Logo](https://void-craft.eu/logo.png)

## ✨ Funkce
- 🚀 **Nativní Výkon**: Startuje rychleji a spotřebovává méně RAM než Electron verze.
- 🔄 **Smart Updates**: Automatické aktualizace modpacků bez ztráty vlastních módů.
- 🔐 **Microsoft Login**: Bezpečné přihlášení přes Microsoft účet.
- 🛠️ **Optimalizace**: Přednastavené JVM argumenty pro maximální FPS (G1GC, ZGC).
- 📁 **Centrální Data**: Všechny instance jsou uloženy v `Dokumenty/.voidcraft`.

## 📦 Instalace
1. Stáhněte si nejnovější verzi z [Releases](https://github.com/venom74cz/void-craft.eu-Launcher/releases).
2. Spusťte `VoidCraftLauncher.exe`.
   - *Poznámka: Při prvním spuštění může Windows Defender zahlásit neznámou aplikaci (SmartScreen). Klikněte na "Přesto spustit".*

## 🛠️ Sestavení (Build)
Pro vývojáře, kteří chtějí launcher upravit nebo sestavit sami.

**Požadavky:**
- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

**Příkazy:**
```powershell
# Klonování repozitáře
git clone https://github.com/venom74cz/void-craft.eu-Launcher.git
cd void-craft.eu-Launcher

# Spuštění (Debug)
dotnet run --project VoidCraftLauncher

# Sestavení (Release)
dotnet publish VoidCraftLauncher -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 🐛 Řešení Problémů
Logy aplikace najdete ve složce:
`%userprofile%\Documents\.voidcraft\launcher.log`

Tento soubor přiložte k hlášení chyby na Discordu.

## 📄 Licence
Tento projekt je určen výhradně pro potřeby serveru Void-Craft.eu.
