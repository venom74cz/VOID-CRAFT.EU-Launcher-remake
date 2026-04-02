# Changelog - VOID-CRAFT Launcher

Všechny důležité změny v projektu jsou dokumentovány v tomto souboru.

## [3.1.7] - 2026-04-02

### 🛠️ Auth launch hotfix a release pipeline hardening

### Opraveno
- **Guest launch regression**: launcher si před spuštěním znovu skládá launch session z aktivního účtu, takže Microsoft účet už neskončí ve hře jako `Guest` jen kvůli rozpadlé runtime session.
- **MSAL account recovery**: auto-login a obnova uloženého profilu používají naposledy ověřený `MsalAccountId`, ne náhodný první účet z cache.
- **Instance optimization defaults**: `Optimalizace` v detailu instance teď dědí skutečný effective stav z globální konfigurace a nejdou proti reálně použitým JVM flagům.
- **Discord release webhook**: CI zkracuje dlouhé release notes na bezpečnou délku pro Discord embed a webhook při chybě vrátí fail místo tichého úspěchu.

### Změněno
- **Release metadata**: projekt, installer, fallback `User-Agent`, dokumentace a nový Discord announcement jsou srovnané na verzi `3.1.7`.

## [3.1.6] - 2026-04-02

### ✨ Instance Workspace, Creator Studio a release polish

### Přidáno
- **Historie pádů**: `Performance` záložka teď ukládá reálné pády instance včetně exit code, délky běhu, výpisu logu a odkazu na log nebo crash-report.
- **World backup flow**: detail instance umí zálohovat, obnovit i mazat jednotlivé světy a před restore automaticky vytváří pojistnou zálohu původního stavu.
- **Promo screenshot curation**: `Creator Studio` umí tagovat `official`, `release candidate` a `archive` screenshoty, připnout favorit a exportovat curated screenshoty do media kitu.

### Změněno
- **Instance Workspace copy**: detail instance používá kratší hráčský copy, čistší přehled a samostatný full description rendering místo interních placeholder formulací.
- **Installed mods filter**: vyhledávání v `Obsah` záložce teď filtruje jen nainstalované mody a nesdílí query se search flow pro přidávání nových modů.
- **Creator Studio access rules**: importované `CurseForge`, `Modrinth` a release `.voidpack` instance jsou v creator režimu jen read-only preview.

### Opraveno
- **Launch fallback**: custom instance bez čerstvého `manifest_info.json` už neskončí jako čistá vanilla a launcher si správně dopočítá loader z creator/runtime metadata.
- **Overview description flow**: summary a plný popis packu se už v detailu nemíchají ani neduplikují.
- **Release metadata**: projekt, installer, fallback `User-Agent` verze a dokumentace jsou srovnané na `3.1.6`.

## [3.1.5] - 2026-04-01

### 🛠️ Creator Studio Metadata & Branding Hotfix

### Opraveno
- **Workspace metadata resolve**: `Creator Studio` uz bere pack identitu z realne vybrane instance nebo persistovaneho workspace id, ne z nahodneho fallback `CurrentModpack` kontextu.
- **Placeholder `Načítání...` v creator overview**: metadata editor i workspace baseline se po otevreni znovu synchronizuji nad spravnym packem a neuviznou na placeholder stavu.
- **CF/MR source hydration**: importovane packy umi pri otevreni workspace doplnit autory, summary, odkazy a logo z lokalniho `manifest_info.json` i z verejnych API zdroju.
- **Branding preview render**: Creator branding preview uz pouziva stejnou async image pipeline jako zbytek launcheru, takze lokalni branding assety i source logo se skutecne vykresli.
- **Auto-import loga**: pokud workspace nema vlastni branding, verejne logo packu se umi zapsat do `assets/branding` jako `logo` a `square icon` uz pri prvnim otevreni.

### Změněno
- **Branding validace**: `logo` a `square icon` uz nevyzaduji pruhledne pozadi jako hard fail; alpha kanal je pouze doporuceny.
- **Release metadata**: projekt, installer a dokumentace jsou srovnane na verzi `3.1.5`.

## [3.1.1] - 2026-03-23

### 🛠️ Hotfix Instance Workspace a Server Hub

### Opraveno
- **Metadata detailu instance**: `Instance Workspace` po otevření znovu přepočítá breadcrumb, titulek i overview ve chvíli, kdy se metadata modpacku dotáhnou asynchronně z API.
- **Placeholder `Načítání...`**: opraven stav, kdy detail instance zůstával na fallback názvu, prázdném popisu a neznámém autorovi i po úspěšném načtení dat.
- **Server icon layering**: favicony serverů v `Server Hubu` už nejsou ztlumené parent overlay vrstvou a fallback glyph se skryje, jakmile je k dispozici skutečná ikona.
- **Fallback icon metadata**: community i pinned servery umí použít logo navázaného modpacku jako bezpečný fallback, když status API nevrátí vlastní favicon.

## [3.1.0] - 2026-03-23

### ✨ Creator Studio F0/F1

### Přidáno
- **Creator workflow shell**: `Creator Studio` má vlastní tabbed shell s `Overview`, `Metadata`, `Mods`, `Files`, `Notes`, `Git` a `Release` místo jedné dlouhé stránky.
- **Creator context layer**: nové kontrakty `CreatorWorkspaceContext`, `CreatorShellState` a service vrstva v `CreatorStudio` namespace centralizují workspace stav, scope, git signal, notes layout, snapshoty a export summary.
- **Copilot Desk a Notes Drawer**: pravý dock se v creator režimu umí přepnout do creator-only pracovního sloupce a otevřít rychlý notes drawer přes aktuální workflow.
- **Bootstrap varianty nové instance**: wizard teď umí `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git` a `Restore Snapshot`.
- **creator_manifest.json**: nový source of truth pro metadata workspace zapisovaný už při založení creator instance.

### Změněno
- **Metadata workflow**: launcher umí načíst, upravit a uložit `creator_manifest.json` bez externího editoru.
- **Metadata sync do launcheru**: uložená creator metadata se propisují zpět do `ModpackInfo`, takže `Instance Workspace` header i Creator overview používají stejnou identitu packu.
- **Template bootstrap**: built-in presety už nejsou kosmetické a seedují curated baseline soubory do `docs`, `notes`, `qa` a `quests`.

### Opraveno
- **Snapshot-before-apply**: workspace-scope metadata apply flow už nepíše naslepo a před větším zápisem vytvoří snapshot relevantních částí workspace.
- **Release metadata sync**: verze projektu, installeru a release dokumentace jsou znovu srovnané na stejný release.

## [3.0.0] - 2026-03-22

### ✨ Kompletní redesign launcheru

### Přidáno
- **Nový shell launcheru**: Ikonový `NavRail`, nový `Dashboard`, `Context Dock` a page transitions místo starého monolitického layoutu.
- **Nové hlavní surfaces**: `Server Hub`, `Achievement Hub`, `Skin Studio`, `Creator Studio`, `Themes`, `Localization` a `Future` roadmap stránka načítaná živě z GitHub `future.md`.
- **Instance Workspace**: Detail instance je nově rozdělený na produkční workspace s galerií, performance, snapshoty, export/import flow a server bindings.
- **Community content feed**: `Novinky` a dashboard sjednocují Discord, YouTube a official Minecraft feed do jednoho launcherového surface.
- **Prémiové overlay flow**: Nový login sheet, create profile wizard, backup prompt, crash drawer a toast host.

### Změněno
- **Creator Studio**: Původní dekorativní streaming panel se změnil na reálný instance workbench s editací souborů a ukládáním zpět do instance.
- **Server Quick Connect**: Flow má nově launch feedback, přípravu `servers.dat`, legacy `--server/--port` parametry i quick-play kompatibilní argumenty.
- **Achievement Hub**: Achievementy jsou sezonně strukturované, napojené na backend snapshot a `Podium finish` už patří do sezonního progresu.
- **Theme a localization runtime**: Motivy i jazyk se přepínají okamžitě bez restartu a běží nad novým token/resource systémem.

### Technicky
- **Architektura**: `MainViewModel` je rozdělený do samostatných oblastí, přidaný `NavigationService`, DI/service composition, structured logging a secure storage vrstva.
- **UX systém**: Nasazené loading skeletony, empty states, nový icon system, motion preference režimy a sjednocené produkční copy napříč launcherem.
- **Social feed resilience**: Feed má cache, timeout izolaci per zdroj a fallback na direct `Minecraft.net` články při problému backendu.

### Opraveno
- **Build blocker**: SDK už nebere validační `obj-*` a `bin-*` artefakty do hlavního buildu, takže `Debug` i `Release` znovu čistě procházejí.

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
