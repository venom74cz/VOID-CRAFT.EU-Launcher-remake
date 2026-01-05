# Changelog

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
