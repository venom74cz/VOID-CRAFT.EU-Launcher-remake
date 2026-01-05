# IO - VOID-CRAFT Launcher

Official custom Minecraft launcher for the **VOID-CRAFT** community. Built with .NET 9 (C#) and Avalonia UI.

![VOID-CRAFT Logo](https://void-craft.eu/logo.png)

## ✨ Funkce
- 🚀 **Nativní Výkon**: Startuje rychleji a spotřebovává méně RAM než Electron verze.
- 🔄 **Smart Updates**: Automatické aktualizace modpacků bez ztráty vlastních módů.
- 🔐 **Microsoft Login**: Bezpečné přihlášení přes Microsoft účet.
- 🏴‍☠️ **Offline Mode**: Možnost hraní pro hráče bez originálního účtu (Warez/Offline).
- 🛠️ **Optimalizace**: Přednastavené JVM argumenty pro maximální FPS (G1GC, ZGC).
- 📁 **Centrální Data**: Všechny instance jsou uloženy v `Dokumenty/.voidcraft`.

## 📦 Instalace

### Windows 🪟
1. Stáhněte si **VoidCraftLauncher_Setup.exe** z [Releases](https://github.com/venom74cz/void-craft.eu-Launcher/releases).
2. Spusťte instalátor. Ten vytvoří zástupce na ploše a v nabídce Start.
3. **Automatické Aktualizace**: Launcher se sám aktualizuje při každém spuštění.

### Linux 🐧
1. Stáhněte si **VoidCraftLauncher-Linux-x64.AppImage**.
2. Nastavte souboru právo pro spuštění (`chmod +x VoidCraftLauncher-Linux-x64.AppImage`).
3. Spusťte.

*Pro pokročilé uživatele je k dispozici i čistá binárka.*

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
