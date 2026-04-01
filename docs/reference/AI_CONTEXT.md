# AI Context: VoidCraft Launcher

## 1) Project Snapshot
- **Name**: VoidCraft Launcher
- **Current version**: **3.1.5**
- **Stack**: .NET 9 + Avalonia UI 11.3 + CommunityToolkit.Mvvm 8.4
- **MC Core**: CmlLib.Core 4.0.6
- **Auth**: Microsoft.Identity.Client (MSAL) + custom Xbox/XSTS/MC token chain
- **Primary goal**: Community Minecraft launcher (VOID-CRAFT.EU) with modpack install/update, Microsoft auth, offline mode, multi-account, and cross-platform builds.

## 2) Current Architecture

### UI Layer
- **Main Window**: `src/Views/MainWindow.axaml` – shell orchestrator s `NavRail`, hlavním content hostem, `ContextDock`/creator dock switchem a overlay vrstvami
- **Views / surfaces**: Dashboard, Library, Discover, Future, Server Hub, Achievement Hub, Skin Studio, Theme Switcher, Localization, Creator Studio a Instance Workspace
- **Login / create profile / backup flow**: launcher používá overlay sheet vrstvy místo starých izolovaných dialogů
- **Creator Studio**: production workspace surface pro bootstrap modpacku, metadata, workbench soubory, notes handoff a release workflow
- **Potato Mods**: `PotatoModsWindow.axaml` – config pro low-end PC mod disabling

### MVVM Architecture
- **MainViewModel.cs** – Orchestration VM rozdeleny do partial oblasti pro navigation, auth, install/update, settings, server hub, news feed, Creator Studio a instance workflow
- **ModManagerViewModel.cs** – Mod toggle logic
- **PotatoModsViewModel.cs** – Potato mode mod list management
- **ViewModelBase.cs** – Base class

### Services
| Service | File | Purpose |
|---------|------|---------|
| **AuthService** | `AuthService.cs` | Microsoft MSAL + Xbox/XSTS/MC token chain, Device Code flow (Linux), Offline auth |
| **LauncherService** | `LauncherService.cs` | CmlLib wrapper – shared path structure, config I/O, game process management |
| **ModpackInstaller** | `ModpackInstaller.cs` | CurseForge manifest + Modrinth .mrpack install pipeline, smart update, overrides |
| **CurseForgeApi** | `CurseForgeApi.cs` | CF REST API wrapper (search, files, batch resolve) |
| **ModrinthApi** | `ModrinthApi.cs` | Modrinth v2 API wrapper |
| **ModUtils** | `ModUtils.cs` | Potato mode file renaming (.jar ↔ .jar.disabled) |
| **LogService** | `LogService.cs` | Centralized file + debug logging |
| **ProtocolHandler** | `ProtocolHandler.cs` | URL protocol handling |
| **CreatorWorkspaceService** | `Services/CreatorStudio/CreatorWorkspaceService.cs` | Shared creator workspace context, standard folder layout, git signal, snapshot/export summary |
| **CreatorManifestService** | `Services/CreatorStudio/CreatorManifestService.cs` | `creator_manifest.json` load/save, slug normalization, workspace structure bootstrap |

### Models
| Model | File | Purpose |
|-------|------|---------|
| **ModpackInfo** | `ModpackInfo.cs` | Installed pack state + UI-computed properties (play button text/color, version transition) |
| **ModpackVersion** | `ModpackInfo.cs` | Name + FileId + ReleaseDate |
| **ModpackItem** | `ModpackItem.cs` | Browser search result DTO |
| **LauncherConfig** | `LauncherConfig.cs` | Global settings: JavaPath, RAM, GC type, optimization flags, instance overrides, options presets |
| **InstanceConfig** | `InstanceConfig.cs` | Per-pack overrides: RAM, Java, GC, Potato mode, optimization flags |
| **ModMetadata** | `ModMetadata.cs` | Per-mod info cached from CurseForge (name, slug, summary, categories) |
| **CurseForgeModels** | `CurseForge/CurseForgeModels.cs` | DTOs for CF manifest, files, mods batch API |
| **ModpackManifestInfo** | `ModpackInstaller.cs` | Install result: MC version, mod loader ID/type, mod count, FileId |

## 3) Key Features Implemented

### Mod Loader Support
- **NeoForge**: Full auto-install via `CmlLib.Core.Installer.NeoForge` 4.0.0
- **Forge**: Full auto-install via `CmlLib.Core.Installer.Forge` 1.1.1
- **Fabric**: ❌ NOT YET IMPLEMENTED (commented out in LauncherService.cs:171-188)

### Shared Path Architecture
```
Documents/.voidcraft/
├── shared/                    # Shared MC data (assets, versions, libraries, runtime)
├── instances/{ModpackName}/   # Per-modpack game directory (mods, config, saves)
├── launcher_config.json       # Global launcher settings
├── installed_modpacks.json    # Saved modpack library
└── launcher.log               # Log file
```
- `MinecraftPath` base = shared folder (saves disk space)
- Each launch creates custom `MinecraftPath` with instance folder as BasePath but shared Assets/Libs/Versions/Runtime

### Modpack Install Pipeline
1. Download modpack ZIP from CurseForge CDN
2. Parse `manifest.json` (CurseForge) or `modrinth.index.json` (Modrinth)
3. Batch-resolve mod download URLs via CF API
4. Smart update: delete old mods (tracked via `installed_files.json`), skip existing
5. Category-aware routing: mods → `mods/`, resource packs → `resourcepacks/`, shaders → `shaderpacks/`
6. Extract overrides with protected paths (options.txt, servers.dat, saves/, shaderpacks/) + hash-based smart config update (config/ files only overwritten if modpack author changed them, tracked via `config_hashes.json`)
7. Save `manifest_info.json` for subsequent launches
8. Save `mods_metadata.json` for mod manager display

### Authentication
- **Microsoft**: MSAL interactive browser flow (Windows) / Device Code flow (Linux)
- **Offline**: `MSession.CreateOfflineSession(username)` with saved `LastOfflineUsername`
- **Silent login**: Auto-login at startup from MSAL token cache or saved offline username
- **Multi-Account**: ❌ NOT YET IMPLEMENTED (single account only)

### Launch Flow (PlayModpack)
1. Check/download/verify modpack files (integrity check every launch)
2. Version comparison (FileId-based, fallback by name)
3. Block launch if required update failed (prevents mixed mod state)
4. Load manifest info → determine MC version + mod loader
5. Build JVM arguments (optimization flags, GC config, per-instance overrides)
6. Apply Potato Mode (rename mods)
7. Call `LauncherService.LaunchAsync()` → CmlLib installs mod loader → installs version → builds process
8. Start process with stdout/stderr logging

### UI Features
- Modpack grid cards with background image + gradient overlay
- Play/Update/Stop button states per modpack
- Progress bar during install/launch
- Modpack browser (CurseForge + Modrinth search + one-click install)
- Per-modpack settings (RAM, Java, GC override, Potato mode)
- Global options.txt presets (save/load/delete)
- Screenshots gallery
- Server Hub + quick connect flow s modpack bindingy
- Achievement Hub, Skin Studio, runtime themes a localization surfaces
- Creator Studio shell s `creator_manifest.json`, bootstrap workflow a creator workbench editaci
- Server status card (mcsrvstat.us API)
- Launcher self-update (GitHub Releases)

## 4) NuGet Dependencies
| Package | Version |
|---------|---------|
| Avalonia | 11.3.10 |
| Avalonia.Desktop | 11.3.10 |
| Avalonia.Themes.Fluent | 11.3.10 |
| Avalonia.Fonts.Inter | 11.3.10 |
| AsyncImageLoader.Avalonia | 3.5.0 |
| CmlLib.Core | 4.0.6 |
| CmlLib.Core.Installer.Forge | 1.1.1 |
| CmlLib.Core.Installer.NeoForge | 4.0.0 |
| CommunityToolkit.Mvvm | 8.4.0 |
| Microsoft.Identity.Client | 4.79.2 |
| RestSharp | 113.0.0 |
| WebView.Avalonia | 11.0.0.1 |
| XboxAuthNet.Game | 1.4.1 |
| XboxAuthNet.Game.Msal | 0.1.3 |

## 5) Build / Release / Packaging
- CI: `.github/workflows/build.yml`
- **Windows**: single-file self-contained + native self-extract
- **Linux**: single-file self-contained + native self-extract (`IncludeNativeLibrariesForSelfExtract=true`) + AppImage
- **Installer**: Inno Setup (`setup.iss`)

## 6) Critical Integration Settings
- **MSAL Client ID**: `a12295b0-3505-46f1-a299-88ae9cc80174`
- **Azure App Registration**: Platform = Mobile/Desktop, Redirect URI includes `http://localhost`, Allow public client flows = Yes
- **CurseForge API**: Requires API Key (in `CurseForgeApi.cs`)
- **VOID-BOX CurseForge Project ID**: `1402056`

## 7) Known Technical Debt
- `MainViewModel.cs` is a ~2080-line "god object" (navigation, auth, launch, browser, settings all in one)
- Some generic catch blocks and limited typed error handling
- Navigation is state-flag based (`RightViewType` enum) instead of routed views
- Fabric support commented out (no NuGet package exists for CmlLib Fabric installer)
- Single-account only (no multi-profile support)
- No GregTech/GTNH-specific launch handling
- Non-blocking compiler warnings in `PotatoModsViewModel.cs` and `MainViewModel.cs`

## 8) Release History
| Version | Changes |
|---------|---------|
| 3.1.5 | Creator Studio hotfix: spravne rozliseni workspace metadata, auto-hydration CF/MR pack info a render/import branding preview assetu |
| 3.1.1 | Hotfix pro metadata detailu instance a ciste renderovani server ikon v Server Hubu |
| 3.1.0 | Creator Studio F0/F1: workflow shell, creator_manifest.json, bootstrap modes, metadata sync, snapshot-before-apply |
| 3.0.0 | Kompletní redesign shellu, Dashboard, Server Hub, Achievement Hub, Skin Studio, Context Dock, runtime themes/localization |
| 1.2.5 | Linux AppImage Skia native library packaging fix |
| 1.2.6 | Linux login callback reliability + fallback guidance |
| 1.2.7 | Login modal UX improvements for Device Code visibility |
| 1.2.8 | Linux Device Code-first flow + robust code parsing |

---
*Last updated: 2026-04-01*
