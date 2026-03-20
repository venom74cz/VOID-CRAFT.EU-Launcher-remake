# Changelog - VOID-CRAFT Launcher

Všechny důležité změny v projektu jsou dokumentovány v tomto souboru.

## [2.1.1] - 2026-03-20

### 🧹 Vyčištění UI

### Přidáno
- **Odstraněny nefunkční záložky**: Falešné filtrační taby v Knihovně, Objevování, Detailu instance a Nastavení byly odstraněny pro čistší UI.
- **Opravený Instance Detail TabControl**: Obsah jednotlivých tabů (Obsah/Přehled/Nastavení/Galerie) je nyní správně uvnitř TabItemů.

### 📰 Živé novinky z changelogu

### Přidáno
- **Novinky z GitHubu**: Panel „Novinky" stahuje CHANGELOG.md živě z GitHub repozitáře.
- **Rozbalovací záznamy**: Changelog záznamy jsou kliknutím rozbalovací/sbalovací.
- **Shrnutí a detail**: Sbalený stav zobrazí 3 položky + počet dalších, rozbalený kompletní výpis.

### 🔽 Minimalizace do systémové lišty

### Přidáno
- **Auto-minimize při hře**: Launcher se minimalizuje do tray po spuštění Minecraftu.
- **Auto-restore po hře**: Po ukončení Minecraftu se launcher automaticky obnoví.
- **Tray ikona**: Kliknutím na ikonu v tray lze launcher kdykoli obnovit.

---

## [2.1.0] - 2026-03-15
### 🧠 Chytrý Update Configů

### Přidáno
- **Hash-based config update**: Při aktualizaci modpacku se config soubory porovnávají pomocí SHA256 hashů. Přepíšou se pouze ty, které autor modpacku skutečně změnil — uživatelské úpravy zůstanou zachovány.
- **config_hashes.json**: Nový soubor v každé instanci, který uchovává hashe configů z poslední instalace pro porovnání při updatu.
- **Podpora pro oba formáty**: Smart config update funguje jak pro CurseForge, tak pro Modrinth modpacky.

### Opraveno
- **Config soubory se už neresetují při updatu**: Opraven problém, kdy update modpacku přepsal všechny uživatelské konfigurace (např. VOID-BOX 2).

### Změněno
- **config/ složka vyňata z IsProtected()**: Config soubory se už neblokují plošně, ale řeší se individuálně přes hash porovnání.

---

## [2.0.0] - 2026-03-08
### 🚀 Velká Aktualizace: Modernizace & Social
Tato verze představuje kompletní facelift launcheru a přidání důležitých komunitních funkcí.

### Přidáno
- **Discord Rich Presence (RPC)**: Plná integrace. Launcher aktivně sdílí tvůj status (prohlížení knihovny, hledání modpacků, aktivní hraní).
- **Asynchronní Full Descriptions**: Modpacky nyní zobrazují kompletní podrobné popisy přímo z CurseForge a Modrinth.
- **Detekce Platformy & Odkazy**: Launcher rozlišuje mezi CurseForge a Modrinth, zobrazuje autora a přidává tlačítko "Navštívit web".
- **Stránkování (Load More)**: Implementováno dynamické načítání dalších výsledků (offset/index) v prohlížeči modpacků – už žádné limity na 50 výsledků.
- **Nové UI prvky**: Záložka "Přehled" v detailu instance a vylepšené hlavičky sekcí.

### Opraveno & Vylepšeno
- **Kompletní UI Facelift**:
    - Karty modpacků mají moderní design s hloubkou (BoxShadow) a glow efektem.
    - Přidány plynulé animace (Scaling + Background transitions) při přejetí myší.
    - Vylepšená barevná paleta pro prémiový vzhled.
- **Scrolling & Layout**:
    - Opraven problém s uříznutým obsahem na konci seznamů (přepracovaný padding a vnořené kontejnery).
    - Zvýšeno hlavní okno aplikace o 50 % pro více místa na modpacky.
- **Technické pod kapotou**:
    - Integrace `HtmlAgilityPack` pro čisté čištění HTML kódu z popisků.
    - Rozšíření `ModpackInfo` modelu o podporu více platforem a metadat.
    - Optimalizace API požadavků pro rychlejší odezvu prohlížeče.

## [1.2.8] - 2026-03-07
- Základní verze před modernizací.
- Podpora pro CurseForge a Modrinth API.
- Základní správa instancí.
