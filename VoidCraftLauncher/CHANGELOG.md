# Changelog - VOID-CRAFT Launcher

Všechny důležité změny v projektu jsou dokumentovány v tomto souboru.

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
