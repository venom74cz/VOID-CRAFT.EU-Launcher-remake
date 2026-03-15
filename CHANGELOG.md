# Changelog

## 2.1.0 - 2026-03-15

### 🧠 Chytrý Update Configů
- **Hash-based config update**: Při aktualizaci modpacku se config soubory porovnávají pomocí SHA256 hashů. Přepíšou se pouze ty, které autor modpacku skutečně změnil — uživatelské úpravy zůstanou zachovány.
- **config_hashes.json**: Nový tracking soubor pro porovnání configů mezi verzemi.
- **Podpora CurseForge i Modrinth**: Smart config update funguje pro oba formáty modpacků.
- **Fix**: Config soubory se už neresetují při updatu modpacku.

---

## 1.2.8

### 🔧 Rychlé Opravy (Hotfix)
- **Linux Microsoft login**: Na Linuxu se nyní používá Device Code flow přímo, takže odpadá problémový `localhost` callback.
- **Zobrazení kódu v launcheru**: Přihlašovací kód se zobrazí okamžitě v login modalu + lze ho zkopírovat tlačítkem.
- **Spolehlivější parsování**: Launcher rozpozná kód ve formátu `kód:` i `code:`.
- **Windows login**: Beze změny (zůstává klasický Microsoft browser login).

## 1.2.7

### 🔧 Rychlé Opravy (Hotfix)
- **Linux Microsoft login stabilita**: Upravena logika přihlášení tak, aby primárně používala standardní browser flow a Device Code využila jako fallback při selhání.
- **Device Code UX v launcheru**: Kód pro propojení se nyní zobrazuje přímo v login modalu (včetně stavu průběhu), takže je možné ho snadno opsat nebo zkopírovat.
- **Srozumitelné chyby AADSTS700021**: Přidána jasná hláška při nepovoleném Device Code flow v Azure App Registration.
- **Windows login**: Beze změny (klasický Microsoft browser login zůstává výchozí).

## 1.2.6

### 🔧 Rychlé Opravy (Hotfix)
- **Linux Microsoft login**: Opraven problém s callbackem na `localhost` v browser OAuth flow (stránka „Unable to connect“).
- **Fallback přihlášení**: Přidán Device Code fallback, takže přihlášení funguje i když lokální callback nedoběhne.
- **Windows**: Beze změny, release neobsahuje žádný zásah do Windows login/build flow.

## 1.2.5

### 🔧 Rychlé Opravy (Hotfix)
- **Linux AppImage start**: Opraven crash při spuštění (`Unable to load shared library 'libSkiaSharp'`).
- **Build pipeline (Linux)**: `dotnet publish` nyní používá `IncludeNativeLibrariesForSelfExtract=true`, aby se nativní Skia knihovny správně přibalily do single-file buildu.
- **Linux Microsoft login**: Přidán bezpečný fallback na Device Code flow (bez `localhost` callbacku), aby přihlášení fungovalo i když browser callback selže.
- **Windows build**: Beze změny (fix je omezený pouze na Linux workflow).

## 1.2.4

### 🔧 Opravy & Vylepšení
- **Update flow stabilita**: Opraveno vyhodnocení dostupné aktualizace modpacku podle `FileId` (méně false-positive stavu „AKTUALIZOVAT“).
- **Cílení na nejnovější build**: Při kliknutí na aktualizaci launcher konzistentně používá nejnovější dostupný `FileId`.
- **Synchronizace po instalaci**: Po úspěšném updatu se interní stav nainstalované verze okamžitě přepíše na reálný `manifest_info.json`.
- **Prevence pádu po neúplném updatu**: Pokud update nedoběhne, hra se nespustí v rozbitém stavu (smíchané staré/nové soubory).
- **Locked file handling**: Zamčené soubory v `overrides` už neshodí celý update; po retry se problematický soubor bezpečně přeskočí a instalace pokračuje.
- **Diagnostika**: Zlepšené logování chyb při update/instalaci modpacku.

## 1.2.3

### ✨ Nové Funkce
- **Moje Modpacky – verze & update stav**: Každý modpack nyní zobrazuje nainstalovanou verzi a při dostupném updatu i přechod na novou verzi (`stará → nová`) + tlačítko **AKTUALIZOVAT**.
- **Automatická kontrola update modpacků**: Kontrola proběhne při startu launcheru a následně každých 5 sekund.
- **Galerie screenshotů per modpack**: V nastavení modpacku je nová sekce **SCREENSHOTS / Galerie** načítaná ze složky `screenshots` (resp. `screenshoty`).
- **Interakce v galerii**: Kolečko myši scrolluje přímo v galerii a klik na screenshot otevře obrázek v systémovém prohlížeči.
- **In-launcher Správa modů**: Nový editor modů přímo v launcheru (vyhledávání, zapnout/vypnout mod, přidání lokálních `.jar` souborů).
- **Globální options.txt presety**: Ukládání presetů pod vlastním názvem a jejich načítání mezi různými modpacky.
- **Smazání options presetu**: Přidána možnost odstranit vybraný globální preset.

### 🔧 Opravy & Vylepšení
- **Ikony a branding**: Stabilizace práce s ikonou aplikace a úpravy zobrazení brandingu v UI.

## 1.2.2

### ✨ Nové Funkce
- **Potato Mode UI**: Nové grafické rozhraní pro výběr vypnutých modů.
    - Nahrazuje ruční editaci souboru `potato_mods.json`.
    - Umožňuje snadné vyhledávání a filtrování modů.
- **Metadata**: Launcher nyní ukládá metadata modů (Client-Side/Server-Side) pro chytřejší filtrování v budoucnu.

### 🔧 Vylepšení
- **Robustnost**: Seznam zakázaných modů nyní preferuje stabilní identifikátory (slugy) před názvy souborů.

## 1.2.1

### ✨ Nové Funkce
- **Potato Mode**: Přidán režim pro slabší počítače ("Bramborový režim").
    - Vypíná náročné vizuální módy (Shadery, Animace, Fyzika) pro zvýšení FPS.
    - Nastavení je specifické pro každý modpack (výchozí stav: Vypnuto).
    - Možnost upravit seznam zakázaných modů (`potato_mods.json`).
- **Chytré Aktualizace**: Installer nyní respektuje vypnuté módy i při aktualizaci modpacku (zůstanou vypnuté).

### 🔧 Vylepšení & Opravy
- **UI**: Lokalizace "Potato Mode" na "Bramborový režim".
- **Build**: Oprava kompilace seznamu modů.

## 1.1.1

### 🔧 Rychlé Opravy (Hotfix)
- **UI Branding**: Opraveno názvosloví na **“VOID-CRAFT”** a přidáno logo copyrightu.
- **Tlačítka**: Dynamická změna tlačítka “HRÁT” na “AKTUALIZOVAT” a “Instalovat” na “Stáhnout”.
- **Offline Login**: Opraveno automatické přihlášení pro offline (Warez) účty.
- **CI/CD Fix**: Opravena cesta k souborům pro instalátor v GitHub Actions.

## 1.1.0

### ✨ Nové Funkce
- **Instalátor**: Profesionální instalátor (`Setup.exe`), který vytvoří zástupce na ploše a umožní snadnou správu aplikace.
- **Auto-Update**: Plně automatický systém aktualizací. Launcher sám stáhne novou verzi, spustí instalátor a restartuje se.
- **Offline Login**: Přidána možnost přihlášení pro "Warez" hráče (Offline Mode) přímo v aplikaci.
- **Ukládání Relace**: Offline přezdívka se nyní pamatuje i po restartu.
- **In-App Login**: Přepracováno UI přihlašování – nyní se používá moderní vyskakovací okno (Overlay).
- **Odhlášení**: Přidáno tlačítko pro odhlášení uživatele.
- **Linux**: Oficiální podpora pro Linux (AppImage + Binárka).

### 🔧 Opravy & Změny
- **Auth**: Aktualizováno Microsoft Auth Client ID (schváleno Mojang).
- **UI**: Vylepšen vzhled uživatelského profilu v postranním panelu a zobrazení verze.

## 1.0.3-alpha

### 🔧 Opravy & Vylepšení
- **Smart Install**: Launcher nyní kontroluje nainstalovanou verzi modpacku. Pokud je aktuální, instalace se přeskočí (zrychlení startu a zachování configů).
- **File Routing**: Resource Packy a Shader Packy se nyní automaticky instalují do správných složek (`resourcepacks/`, `shaderpacks/`) místo `mods/`.
- **Overrides Fix**: Opraveno kopírování prázdných složek z `overrides` (např. pro shadery).

## 1.0.2-alpha

### 🔧 Opravy Hotfix
- **Critical fix**: Opraven pád při spuštění způsobený poškozeným souborem ikony (`icon.ico`).

## 1.0.1-alpha

### 🔧 Opravy Hotfix
- **Auto-Update**: Opraven URL repozitáře pro kontrolu aktualizací (nyní `venom74cz/VOID-CRAFT.EU-Launcher-remake`).
- **Instalace**: Launcher je nyní "Self-Contained" (obsahuje .NET Runtime), takže hráči nemusí nic instalovat.
- **Ikona**: Přidána ikona aplikace.
- **Build**: Opraveny problémy s CI/CD workflow.

## 1.0.0-alpha

### ✨ Nové Funkce
- **Nový Launcher**: Kompletní přepis launcheru do C# (Avalonia UI) pro vyšší výkon a stabilitu.
- **Logovací Systém**: Centrální logování do `Dokumenty/.voidcraft/launcher.log` (zachytává pády i výstup ze hry).
- **Smart Update**: Aktualizace modpacků nyní zachovávají uživatelem přidané módy (např. shadery, mapy).
- **Update Checker**: Automatická kontrola nové verze launcheru při spuštění.
- **Optimalizace**: Integrované JVM argumenty pro lepší výkon (G1GC / ZGC).

### 🔧 Opravy & Změny
- Opravena cesta instalace modpacků z prohlížeče.
- Odstraněna ochrana `config/` složky (nyní se aktualizuje s modpackem).
- Zabezpečen modpack "VOID-BOX 2" proti smazání.
- Vylepšené UI pro přihlášení (Microsoft Auth).

### ⚠️ Známé Chyby
- Fabric modpacky zatím nelze instalovat automaticky (chybí podpora v knihovně).
