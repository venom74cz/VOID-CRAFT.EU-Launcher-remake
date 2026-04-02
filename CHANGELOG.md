# Changelog

## 3.1.6 - 2026-04-02

### Modpack detail polish
- `Instance Workspace` už v headeru a přehledu neukazuje interní/dev copy kolem promo hero obrázků, snapshotů ani Quick Connect flow; detail teď používá kratší hráčský jazyk místo changelog formulací.
- Seznam nainstalovaných modů v detailu instance už neobsahuje mrtvé checkboxy, neaktivní toggle ani prázdné menu; zůstaly jen skutečné akce, které dávají smysl.
- Horní vyhledávání v záložce `Obsah` už opravdu filtruje jen nainstalované mody a není svázané se search flow pro přidávání nových modů.
- `Historie pádů` ve výkonové záložce už není placeholder; launcher teď ukládá poslední pády instance včetně exit code, délky běhu, výpisu logu a odkazu na dostupný log nebo crash-report.

### Instance Workspace overview & zálohy
- Opraven detail nově vytvořených custom instancí: `Instance Workspace` už při otevření nepřebírá starý Creator Studio fallback pack, takže breadcrumb, MOTD i pravý metadata panel patří opravdu právě otevřené instanci.
- Opraven remote overview flow pro CurseForge/Modrinth packy: krátký `MOTD packu` už zůstává summary ze zdroje a celý remote popis se renderuje jen v `Celý popisek`, místo aby se oba bloky míchaly dohromady.
- `Přehled` detailu instance teď rozděluje krátký `MOTD packu` a plný popis podobně jako Curse stránky, ale v čistším launcher layoutu: vpravo běží stabilní metadata panel `Pack / Autoři / Minecraft / Loader / Channel` a pod dělicí linkou se ukazuje celý text packu, pokud je k dispozici.
- Plný popis packu se teď neukazuje jako jeden dlouhý blok. Launcher si umí z CurseForge HTML i Modrinth markdownu vytáhnout intro + samostatné sekce a v overview je renderuje jako čitelnější content karty.
- Overview už zbytečně neduplikuje stejný úvod dvakrát. Když se intro shoduje s horním summary/MOTD, launcher ho ve full description části přeskočí.
- `Zálohy` nově neukazují jen celý snapshot instance; launcher také načítá konkrétní světy ze složky `saves` a u každého nabízí samostatné `Zálohovat svět` přímo do launcher backup workspace.
- Každý svět teď v launcheru ukazuje i vlastní historii world backup snapshotů a přímé `Obnovit svět`; před restore se navíc automaticky vytvoří safety backup původního stavu, aby šlo návrat vrátit zpět.
- World backup snapshoty teď jdou z launcheru i mazat, takže historie konkrétního světa nezůstává bez údržby.
- Opraven regression v launch flow, kdy se některé custom profily nebo instance bez čerstvého `manifest_info.json` spouštěly jako čistá vanilla. Launcher si teď runtime fallback skládá z `creator_manifest.json` a uložených runtime polí modpacku, takže se korektně použije i loader.

### Creator Studio access rules
- Creator Studio teď rozlišuje release pack a authoring workspace: stažené CurseForge, Modrinth a release `.voidpack` instance jsou jen read-only preview, zatímco editace zůstává povolená pouze pro custom profily a dev workspace instance.
- Import přes `.voidpack` flow se nově zapisuje jako `VOID` source místo editable custom profilu, takže release buildy neskončí omylem v creator authoring režimu.

### Creator Studio promo screenshoty
- `Creator Studio` nově umí kurátorovat screenshoty přímo nad workspace: každý screenshot lze označit jako `official`, `release candidate` nebo `archive` a jeden z nich připnout jako hlavní favorit.
- Metadata promo screenshotů se ukládají do `creator_manifest.json`, takže výběr nezmizí po restartu launcheru ani po běžném uložení creator metadat.
- `Metadata` tab dostal novou sekci s featured preview, rychlým otevřením screenshot složky a přímými akcemi nad jednotlivými screenshoty.
- Export `media kitu` teď kromě branding assetů přibaluje i curated promo screenshoty a root `featured-screenshot.*` alias pro hlavní vizuál.
- Vybraný promo screenshot se teď propisuje i do branding preview a do hero headeru detailu instance, takže je okamžitě vidět, co launcher opravdu bere jako hlavní vizuál packu.
- Screenshoty teď lze přímo z galerie použít jako `cover` nebo `social preview`; launcher je při tom automaticky ořízne a přepočítá do správného slot formátu místo tvrdého failu na poměru stran.

### Release metadata
- Srovnana release verze na `3.1.6` v launcher projektu, installeru, fallback `User-Agent` verzi pro social feed a aktualni dokumentaci vcetne release announcementu.

## 3.1.5 - 2026-04-01

### Creator Studio hotfix
- `Creator Studio` uz pri obnoveni posledni instance nepada do placeholder stavu `Načítání...`; workspace metadata se znovu skladaji nad realne vybranou instanci misto nahodneho fallback kontextu.
- Importovane `CurseForge` a `Modrinth` packy si umi pri otevreni Creator Studia automaticky dohydrat nazev, autory, popis, odkaz a logo ze zdroje nebo z lokalniho `manifest_info.json`.
- Existujici `creator_manifest.json` uz pri fallback syncu neblokuje opravena source metadata, pokud v nem zustaly placeholder nebo prazdne hodnoty.

### Branding & preview
- Verejne logo importovaneho packu se pri prvnim otevreni workspace umi samo zapsat do `assets/branding` jako `logo` a `square icon`, pokud lokalni branding jeste chybi.
- Branding preview v `Creator Studiu` preslo na stejnou image pipeline jako zbytek launcheru, takze URL i lokalni soubory se skutecne renderuji misto prazdneho boxu.
- `Logo` a `Square icon` uz nevyzaduji alpha kanal; pruhlednost je jen doporucena.

### Release metadata
- Srovnana release verze na `3.1.5` v launcher projektu, installeru a referencni dokumentaci.

## 3.1.4 - 2026-03-30

### Achievement Hub & Leaderboard
- Leaderboard se nyní primárně seskupuje podle `TeamId` (pokud je k dispozici); týmy se počítají jako jedna příčka.
- `Podium finish` (badge) nyní hodnotí postavení podle týmové pozice a souhrnná karta "Pozice" zobrazuje týmovou pozici.

### UI opravy
- Přidán posuvný efekt (marquee) pro dlouhá jména týmů v Achievements view.?

## 3.1.3 - 2026-03-29

### Branding & Creator Studio
- Přidána UI podpora pro upload `logo`, `cover`, `square icon`, `wide hero` a `social preview`.
- Live preview variant brandingu přímo v launcheru.
- Zaveden resize/crop pipeline a základní validace (rozlišení, poměr stran, průhlednost) přes SkiaSharp.
- Assety ukládány do `assets/branding` a navázány na `creator_manifest.json`.
- Přidána možnost exportu media kitu pro release materiály.
- Automatický import veřejného loga z CF/MR packu při prvním generování manifestu.
- Branding se nyní propaguje do launcher card/header preview.

## 3.1.1 - 2026-03-23

### Hotfix detailu instance a Server Hubu
- `Instance Workspace` uz po otevreni korektne prepocita odvozene hlavicky a overview, kdyz se metadata modpacku dotahnou asynchronne z API.
- Opraven placeholder stav `Nacitani...` v detailu instance, ktery zustaval viset i po tom, co uz launcher znal realny nazev, autora a popis packu.
- `Server Hub` ted nacita a renderuje server favicony cisteji: fallback glyph se zobrazi jen kdyz chybi realna ikona a nacitena ikona uz neni ztlumena overlay vrstvou.
- Vazba server -> modpack umi pouzit logo modpacku jako fallback iconu, kdyz server neposila vlastni favicon.

## 3.1.0 - 2026-03-23

### Creator Studio jako skutecny workspace
- `Creator Studio` uz neni jen dekorativni panel. Dostalo vlastni workflow shell se zalozkami `Overview`, `Metadata`, `Mods`, `Files`, `Notes`, `Git` a `Release`.
- Pravy sloupec launcheru se umi v creator rezimu prepnout na `Copilot Desk` a vedle nej funguje sekundarni `Notes Drawer` pro rychly handoff a planning bez opusteni aktualniho workflow.
- `CreatorWorkspaceContext`, `CreatorShellState` a nova service vrstva v `CreatorStudio` namespace centralizuji vybrany workspace, aktivni scope, git signal, notes layout, snapshoty a export stav.

### Bootstrap a metadata modpacku
- Wizard pro novou custom instanci ted umi realne bootstrap varianty `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git` a `Restore Snapshot`.
- Pri bootstrapu se zapisuje `creator_manifest.json`, vytvari se standardni creator struktura slozek a template presety umi rovnou naplnit `docs`, `notes`, `qa` a `quests` baseline soubory.
- Metadata packu se uz upravuji primo v launcheru a po ulozeni se synchronizuji zpet do `ModpackInfo`, takze `Instance Workspace` i Creator overview berou pack identitu z jednoho zdroje pravdy.

### Bezpecnost a release workflow
- Workspace-scope metadata apply flow je nově chraneny `snapshot-before-apply` guardem, ktery umi pred vetsim creator zapisem vytvorit obnovitelny snapshot relevantnich casti workspace.
- Creator shell drzi persistentni posledni workspace, posledni aktivitu a release warning signal primo v launcher configu.

### Dokumentace a release metadata
- Release metadata jsou srovnana na verzi `3.1.0` v projektu, installeru a release dokumentech.
- README a reference dokumentace byly doplnene tak, aby popisovaly aktualni stav launcheru po Creator Studio F0/F1 implementaci.

## 3.0.0 - 2026-03-22

### Kompletní redesign launcheru
- Úplně nový shell launcheru: ikonový `NavRail`, produkční `Dashboard`, živý `Context Dock`, page transitions a sjednocený design systém místo původního monolitického layoutu.
- Přepracované hlavní surfaces: modernější `Knihovna`, `Discover`, detail instance jako `Instance Workspace` a obsahově oddělený home/dashboard.
- Nové produktové moduly: `Server Hub`, `Achievement Hub`, `Skin Studio`, `Creator Studio`, `Themes`, `Localization` a nová `Future` stránka načítaná živě z GitHub `future.md`.

### Komunitní a content vrstva
- `Server Hub` s pinned VOID-CRAFT serverem, custom/community servery, vazbou server → modpack, quick connect flow a viditelným launch feedbackem.
- `Novinky` a dashboard feed sjednocené přes Discord, YouTube a official Minecraft článek, včetně cache, timeout izolace a fallbacku při výpadku zdroje.
- `Achievement Hub` napojený na sezonní backend snapshoty s leaderboardem, progres badge a lokální cache vrstvou pro fallback scénáře.
- `Skin Studio` navázané na identitu účtu, UUID, veřejnou historii skinů a account tooling místo placeholder surface.

### Instance workflow a release tooling
- `Instance Workspace` teď řeší přehled, obsah, galerii, performance, uložené snapshoty, export/import a server bindings v jednom produkčním flow.
- `Creator Studio` se změnilo z dekorativního panelu na instance workbench s výběrem upravitelných souborů, editorem obsahu a uložením zpět do instance.
- Přidané prémiové overlay flow: nový login sheet, create profile wizard, backup prompt, crash drawer a toast host.
- Quick Connect připravuje auto-connect přes `servers.dat`, legacy server argumenty a kompatibilní quick-play parametry.

### UX, theming a architektura
- Runtime `Theme Engine` s více built-in motivy, motion preference režimy a rozšířitelným token systémem.
- Runtime CZ/EN lokalizace přes `.resx` zdroje a okamžité přepnutí bez restartu.
- Nasazené loading skeletony, empty states, nový icon system, lepší responsive layout a konzistentní produkční copy napříč launcherem.
- Rozdělení `MainViewModel` do samostatných oblastí, zavedení `NavigationService`, DI/service composition, structured logging a secure storage základu.

### Opravy před veřejným releasem
- Opraven build blocker v launcher projektu: SDK už nekompiluje validační `obj-*` a `bin-*` artefakty do hlavního buildu.
- Znovu ověřené `Debug` i `Release` buildy po redesign a runtime fix passu.
- `Podium finish` přesunuto do sezonního progresu, aby achievement surface odpovídal reálnému produktovému významu.

## 2.1.1 - 2026-03-20

### 🧹 Vyčištění UI
- **Odstraněny nefunkční záložky**: Odstraněny falešné filtrační taby v Knihovně (Všechny instance/Modpacky/Vlastní), Objevování (Modpacky/Mody/Resource Packy/Shadery), Detailu instance (Mody/Shadery/Resource Packy) a Nastavení (Java, Správa zdrojů).
- **Opravený Instance Detail TabControl**: Obsah přesunut dovnitř jednotlivých TabItemů (Obsah/Přehled/Nastavení/Galerie) – dříve se zobrazoval mimo TabControl.

### 📰 Živé novinky z changelogu
- **Novinky z GitHubu**: Panel „Novinky" na hlavní stránce nyní živě stahuje CHANGELOG.md z GitHub repozitáře.
- **Rozbalovací záznamy**: Každý záznam changelogu je kliknutím rozbalovací/sbalovací pro lepší přehlednost.
- **Shrnutí a detail**: Ve sbaleném stavu se zobrazí první 3 položky + počet dalších, po rozbalení kompletní výpis.

### 🔽 Minimalizace do systémové lišty
- **Automatická minimalizace při hře**: Launcher se automaticky minimalizuje do systémové lišty (tray) po spuštění Minecraftu.
- **Automatické obnovení**: Po ukončení hry se launcher automaticky obnoví z tray ikony zpět na obrazovku.
- **Tray ikona**: Kliknutím na tray ikonu lze launcher kdykoli obnovit ručně.

---

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
