# VOID-CRAFT Launcher

Oficialni vlastni Minecraft launcher pro komunitu **VOID-CRAFT**. Postaveny na
.NET 9 (C#) a Avalonia UI.

Aktualni release: **3.1.8.1**

![VOID-CRAFT Logo](https://void-craft.eu/logo.png)

> [!IMPORTANT]
> Tento repozitar je verejny kvuli transparentnosti a schvalene spolupraci.
> Nejde o **open-source software**.
> Jakekoli pouziti, kopirovani, upravy, redistribuce, nasazeni nebo odvozene
> dilo vyzaduje predchozi pisemne povoleni drzitele autorskych prav.
> Plne podminky jsou v [LICENSE](LICENSE).

## Funkce

- Nativni desktopovy vykon bez Electron stacku
- Chytre aktualizace modpacku bez mazani schvaleneho uzivatelskeho obsahu
- Prihlaseni pres Microsoft ucet
- Centralizovana data launcheru v `Documents/.voidcraft`
- Produkcni `Dashboard`, `Server Hub`, `Achievement Hub`, `Skin Studio` a `Instance Workspace`
- `Creator Studio` pro bootstrap modpacku, metadata, workbench soubory a release-ready creator workflow
- Instalacni a release flow pro oficialni distribuci VOID-CRAFT Launcheru

## Instalace

### Windows

1. Stahni si nejnovejsi `VoidCraftLauncher_Setup.exe` z release sekce repa.
2. Spust instalator. Vytvori zastupce v nabidce Start a na plose.
3. Spust aplikaci. Oficialni build si kontroluje aktualizace automaticky.

### Linux

1. Stahni si nejnovejsi `VoidCraftLauncher-Linux-x64.AppImage`.
2. Nastav mu pravo ke spusteni pomoci `chmod +x VoidCraftLauncher-Linux-x64.AppImage`.
3. Spust AppImage.

## Build

Tahle sekce je urcena jen pro schvalene spolupracovniky nebo interni vyvoj
VOID-CRAFT.

Pozadavky:

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

Prikazy:

```powershell
git clone https://github.com/venom74cz/VOID-CRAFT.EU-Launcher-remake.git
cd VOID-CRAFT.EU-Launcher-remake

dotnet run --project VoidCraftLauncher
dotnet publish VoidCraftLauncher -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Reseni problemu

Logy launcheru najdes v:

`%USERPROFILE%\\Documents\\.voidcraft\\launcher.log`

Tento soubor priloz pri hlaseni chyby pres oficialni VOID-CRAFT support kanaly.

## Licence

Licencni model: **source-available / vsechna prava vyhrazena**

- Repo je verejne viditelne, ale neposkytuje open-source prava k pouziti.
- Bez predchoziho pisemneho souhlasu nesmis launcher ani jeho casti pouzivat,
  kopirovat, upravovat, sirit, rebrandovat, nasazovat ani znovu vyuzivat.
- Zavislosti tretich stran zustavaji pod svymi vlastnimi licencemi.

Zavazne podminky jsou v [LICENSE](LICENSE) a pravidla pro spolupraci v
[docs/reference/CONTRIBUTING.md](docs/reference/CONTRIBUTING.md).

## Dokumentace

Dalsi projektove dokumenty jsou ulozene ve slozce [docs](docs/README.md), hlavne:

- aktualni realita produktu a dokumentacni mapa: [docs/reference/CURRENT-STATE.md](docs/reference/CURRENT-STATE.md)
- changelog aktualniho releasu: [CHANGELOG.md](CHANGELOG.md)
- implementacni plan Creator Workbenche: [docs/planning/creator-workbench.md](docs/planning/creator-workbench.md)
- redesign a UX blueprint: [docs/planning/LAUNCHER-OBSIDIAN-REDESIGN-BLUEPRINT.md](docs/planning/LAUNCHER-OBSIDIAN-REDESIGN-BLUEPRINT.md)
- produktova roadmapa: [future.md](future.md)
- historicky implementation plan: [docs/planning/IMPLEMENTATION_PLAN.md](docs/planning/IMPLEMENTATION_PLAN.md)
