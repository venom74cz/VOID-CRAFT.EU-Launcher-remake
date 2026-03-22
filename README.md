# VOID-CRAFT Launcher

Oficiální vlastní Minecraft launcher pro komunitu **VOID-CRAFT**. Postavený na
.NET 9 (C#) a Avalonia UI.

![VOID-CRAFT Logo](https://void-craft.eu/logo.png)

> [!IMPORTANT]
> Tento repozitář je veřejný kvůli transparentnosti a schválené spolupráci.
> Nejde o **open-source software**.
> Jakékoli použití, kopírování, úpravy, redistribuce, nasazení nebo odvozené
> dílo vyžaduje předchozí písemné povolení držitele autorských práv.
> Plné podmínky jsou v [LICENSE](LICENSE).

## Funkce

- Nativní desktopový výkon bez Electron stacku
- Chytré aktualizace modpacku bez mazání schváleného uživatelského obsahu
- Přihlášení přes Microsoft účet
- Centralizovaná data launcheru v `Documents/.voidcraft`
- Instalační a release flow pro oficiální distribuci VOID-CRAFT Launcheru

## Instalace

### Windows

1. Stáhni si nejnovější `VoidCraftLauncher_Setup.exe` z release sekce repa.
2. Spusť instalátor. Vytvoří zástupce v nabídce Start a na ploše.
3. Spusť aplikaci. Oficiální build si kontroluje aktualizace automaticky.

### Linux

1. Stáhni si nejnovější `VoidCraftLauncher-Linux-x64.AppImage`.
2. Nastav mu právo ke spuštění pomocí `chmod +x VoidCraftLauncher-Linux-x64.AppImage`.
3. Spusť AppImage.

## Build

Tahle sekce je určená jen pro schválené spolupracovníky nebo interní vývoj
VOID-CRAFT.

Požadavky:

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

Příkazy:

```powershell
git clone https://github.com/venom74cz/VOID-CRAFT.EU-Launcher-remake.git
cd VOID-CRAFT.EU-Launcher-remake

dotnet run --project VoidCraftLauncher
dotnet publish VoidCraftLauncher -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Řešení problémů

Logy launcheru najdeš v:

`%USERPROFILE%\Documents\.voidcraft\launcher.log`

Tento soubor přilož při hlášení chyby přes oficiální VOID-CRAFT support kanály.

## Licence

Licenční model: **source-available / všechna práva vyhrazena**

- Repo je veřejně viditelné, ale neposkytuje open-source práva k použití.
- Bez předchozího písemného souhlasu nesmíš launcher ani jeho části používat,
  kopírovat, upravovat, šířit, rebrandovat, nasazovat ani znovu využívat.
- Závislosti třetích stran zůstávají pod svými vlastními licencemi.

Závazné podmínky jsou v [LICENSE](LICENSE) a pravidla pro spolupráci v
[CONTRIBUTING.md](CONTRIBUTING.md).
