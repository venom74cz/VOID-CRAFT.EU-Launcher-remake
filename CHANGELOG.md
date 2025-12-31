# Changelog

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
