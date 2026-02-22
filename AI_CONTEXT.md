# AI Context: VoidCraft Launcher

## 1) Project Snapshot
- **Name**: VoidCraft Launcher
- **Current version**: **1.2.8**
- **Stack**: .NET 9 + Avalonia UI + MVVM Toolkit
- **Primary goal**: Community Minecraft launcher (VOID-CRAFT) with modpack install/update, Microsoft auth, offline mode, and cross-platform builds.

## 2) Current Architecture
The app is still a classic MVVM desktop app with a single main window.

- **UI**: `VoidCraftLauncher/src/Views/MainWindow.axaml`
  - Sidebar + right panel sections (Home/Settings/ModpackSettings/Browser).
  - Login modal overlay is embedded directly in this view.

- **Main VM**: `VoidCraftLauncher/src/ViewModels/MainViewModel.cs`
  - Large orchestration VM (navigation, auth state, modpack lifecycle, update loops, settings, screenshots, presets).
  - Uses async background loops for server status and installed-modpack update checks.

- **Services**:
  - `AuthService.cs`: Microsoft/Offline auth, MSAL token cache, Xbox/XSTS/Minecraft token chain.
  - `LauncherService.cs`: launch preparation + game process execution.
  - `ModpackInstaller.cs`: install/update pipeline, manifest management, protected files, resilient overrides extraction.
  - `CurseForgeApi.cs` / `ModrinthApi.cs`: remote metadata/search.
  - `LogService.cs`: centralized logging.

- **Models**:
  - `ModpackInfo`: installed pack state + UI-computed properties (play button text/color, version transition).
  - `LauncherConfig`: global launcher settings and options presets.
  - `InstanceConfig`: per-pack overrides (RAM/JVM/potato mode toggles).

## 3) Key Features Implemented (Current State)

### Modpack cards / updates
- Installed and latest versions are shown directly in "Moje Modpacky".
- Update check runs at startup and then every **5 seconds**.
- Update availability detection uses **FileId-first** comparison (fallback by name).
- Card button changes between Play/Update state and style.

### Update reliability
- Update/install targets latest FileId when needed.
- Prevents game launch when required update did not complete (avoids mixed old/new files crash state).
- After successful install, current version state is re-synced from manifest info.
- Locked files in overrides are retried and then safely skipped instead of failing whole update.

### Screenshots gallery (per pack)
- Reads from `screenshots` (fallback `screenshoty`) in instance folder.
- Shown in modpack settings, supports wheel scrolling and click-to-open.

### In-launcher mod manager
- Separate `ModManagerWindow` with search.
- Enable/disable mod by renaming `.jar` <-> `.jar.disabled`.
- Import local mod `.jar` files into current pack.

### Global options presets
- Save current pack `options.txt` under a custom preset name.
- Load preset into any selected pack.
- Delete presets.
- Presets are stored globally in launcher config.

### Microsoft login behavior (important)
- **Windows**: default classic interactive Microsoft browser login.
- **Linux**: uses Device Code flow directly (stability over localhost callback).
- Login modal now displays ongoing status + Device Code and allows copy/open actions.
- Clear guidance is shown for `AADSTS700021` (when public client flow is disabled in Azure App Registration).

## 4) Build / Release / Packaging
- CI workflow: `.github/workflows/build.yml`
- **Windows publish**: single-file self-contained + native self-extract.
- **Linux publish**: single-file self-contained **with** native self-extract (`IncludeNativeLibrariesForSelfExtract=true`) to ship Skia native libs properly.
- Linux artifacts: raw binary + AppImage.

## 5) Data & Paths
- Base data folder: `Documents/.voidcraft`
- Important files:
  - `launcher_config.json`
  - `installed_modpacks.json`
  - `launcher.log`
  - `instances/{ModpackName}/...`

## 6) Critical Integration Settings (Azure)
- MSAL Client ID in code: `a12295b0-3505-46f1-a299-88ae9cc80174`
- For Device Code to work, Azure App Registration must have:
  - Platform: **Mobile and desktop applications**
  - Redirect URI including `http://localhost`
  - **Allow public client flows = Yes**

## 7) Known Technical Debt
- `MainViewModel.cs` is still a large "god object".
- Some generic catch blocks and limited typed error handling remain.
- Navigation is state-flag based instead of routed/view-composed architecture.
- Existing non-blocking compiler warnings remain (not introduced by latest changes):
  - Unused exception variable in `PotatoModsViewModel.cs`
  - Non-awaited call warning in `MainViewModel.cs`

## 8) Latest Release Notes Context
- `1.2.5`: Linux AppImage Skia native library packaging fix.
- `1.2.6`: Linux login callback reliability + fallback guidance.
- `1.2.7`: Login modal UX improvements for Device Code visibility.
- `1.2.8`: Linux Device Code-first flow + robust code parsing (`kód:` / `code:`), Windows login unchanged.

---
*Last updated: 2026-02-14*
