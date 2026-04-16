# Changelog

## 3.2.0 - 2026-04-16

### Selektivní verze & Update Prompt

#### Správa verzí
- Zavedena podpora pro výběr konkrétní verze modpacku přímo v knihovně přes nové dropdown menu.
- Přidán režim **`⭐ Latest`**, který automaticky sleduje nejnovější dostupné verze a upozorňuje na ně.
- Výběr verze je okamžitě perzistentní a ukládá se do konfigurace modpacku při každé změně.
- Pinned mode: Pokud je vybrána specifická verze, launcher ji při startu tiše nainstaluje (pokud chybí) a neobtěžuje uživatele dotazy na aktualizace.

#### Interaktivní aktualizace
- Přidán modul **`UpdatePromptSheet`** ve stylu M3, který se zobrazí před spuštěním hry, pokud je v režimu `⭐ Latest` dostupná nová verze.
- Prompt umožňuje prohlížet **Changelog** (seznam změn) stažený přímo z platformy (CurseForge, Modrinth, VOID Registry).
- Tři možnosti volby: `Aktualizovat a HRÁT`, `Zálohovat a aktualizovat` (vyvolá Backup Prompt) nebo `Ne, spustit stávající verzi`.

#### Integrace a API
- Přidána implementace pro transformaci HTML changelogů z CurseForge API do čitelného textu.
- Rozšířena `ModpackInfo` logika o odlišení `IsTrackingLatest`, `ResolvedTargetVersion` a podmíněného `IsUpdateAvailable`.
- Opravena detekce aktualizací pro hybridní zdroje (VOID Registry).

#### UI & Fixy
- Opraven kritický bug (event bubbling), kdy kliknutí na šipku výběru verze v kartě instance otevřelo detail modpacku.
- Vylepšen vizuál karet v knihovně a zajištěna plynulejší interakce s překryvnými vrstvami.
- Synchronizace `⭐ Latest` sentinelu při načítání dat i offline cold-startu.

#### Release metadata
- Srovnána release verze na `3.2.0` v launcher projektu, installeru a meta informacích.


## 3.1.11 - 2026-04-06

### Creator release governance + GitHub repair

#### Release governance
- `Creator Studio` v `Release` tabu nove nacita historii verzi z `VOID Registry`, pending public approvals a projektovy governance stav.
- Schvaleni public releasu, navrat zpet na internal a `yank` chybne verze jde spoustet primo z launcheru nad registry projektem.
- Release panel se po zmene workspace a manifestu obnovuje tak, aby drzel stejnou governance realitu jako web.

#### GitHub / creator workflow
- `Pouzit jako origin` uz umi opravit rozbite lokalni `.git` metadata misto generickeho failu pri napojeni existujiciho GitHub repa.
- Invalidni `.git` slozka uz se v Creator Studiu netvari jako platny git repository jen kvuli existenci adresare; kontrola probiha skutecnym `git rev-parse` probe.
- Pri selhani nastavovani `origin` launcher vraci konkretnejsi git detail, aby bylo jasne, co se pokazilo.

#### UX / privacy polish
- `VOID ID` copy v launcheru uz nemluvi o administratorskych opravnenich a neukazuje profilove CTA, ktere by prozrazovalo admin surface.
- Dashboard, Identity a nav rail jsou srovnane na bezny webovy profil a projektove workflow misto interni access terminologie.

#### Release metadata
- Srovnana release verze na `3.1.11` v launcher projektu, installeru, fallback `User-Agent` hodnotach a referencni dokumentaci.

## 3.1.10.1 - 2026-04-05

### Creator Studio + auth hotfix

#### Opravy crashu
- `Creator Studio` uz necrashne po obnoveni nebo novem prihlaseni `VOID ID`, kdyz se po nacteni collaborator sekce buildi deferred `DataTemplate` ve `Streaming Tools`.
- Problemove command bindingy z item templatu uz nepouzivaji krehky cast na `vm:MainViewModel` pres `$parent[ItemsControl]`, ale stabilni root binding pres pojmenovany control.
- GitHub repository refresh pri restore session uz marshaluje mutace bindovanych kolekci a stavu na UI thread, takze login restore nevytvari dalsi nestabilitu pri otevrenem Creator Studiu.

#### UI polish
- `ContextDock` ma jemnejsi spacing mezi sekcemi a rozmanitejsi community linky misto generickych textovych glyfu.
- Web, Discord, GitHub a YouTube odkazy dostaly brand-aware ikony a kartovy vizual, aby shell nepusobil jednotvarne.

#### Release metadata
- Srovnana release verze na `3.1.10.1` v launcher projektu, installeru, fallback `User-Agent` hodnotach a referencni dokumentaci.

## 3.1.10 - 2026-04-05

### Launcher shell + VOID ID control plane

#### Home / shell
- Levy rail uz neni icon-only sidebar. Expanduje se na hover/focus, ukazuje nazvy sekci a drzi jasne rozdeleni `Core`, `Play & Community` a `Workflow`.
- `Dashboard` byl prestaveny na command-center home s dalsim krokem, server pulse, VOID ID vrstvou a rychlymi moduly misto puvodni vyplnove plochy.

#### VOID ID
- Launcher ma dedikovanou `VOID ID` surface pro linked accounts, aktivni sessions, GitHub provider state a registry membership prehled.
- Identity vrstva v launcheru uz rozlisuje skutecnou DB admin roli, Discord team access a capability-based admin surface bez zploštění bezpecnostni semantiky.

#### Registry / projekty
- `Discover` nově počítá i s `VOID Registry` jako třetím zdrojem vedle CurseForge a Modrinthu, včetně install/update flow přes registry manifest.
- `Creator Studio` umí po přihlášení přes VOID ID načíst role projektu a spravovat spolupracovníky přímo nad registry projektem.

#### Stabilita
- Nav rail a dashboard XAML byly dotazene do build-clean stavu; `dotnet build TESTLAUNCHER3.sln` po opravach znovu prochazi.

### Creator Studio - UX Redesign (kompletni implementace blueprintu)

#### Header & Overview
- Kompaktni 2-radkovy header s pack identity, status badges (git branch, dirty signal, export status) a quick action buttony (Mods slozka, Export, Snapshot, Refresh).
- Redesigned Overview tab s workspace picker, stats gridem (Mods/Files/Notes/Snapshots/Exports), activity timeline a quick links.

#### Identity tab (nahrazuje Metadata)
- Romdeleni do dvou sub-tabu: **Profile** (formular s live preview) a **Branding** (brand profil, asset checklist, promo galerie).
- Metadata→Identity prejmenovani probiha napric celym codebase (enum, tab mapping, VM).

#### Notes tab
- 4 sub-mody: **Docs** (sidebar + markdown editor), **Wiki** (prolinkované stránky), **Canvas** (vizuální graf uzlů – Questline/Gate/Boss/Dimension/Recipe/Blocker/Idea), **Mind Map** (přehled canvas grafů).
- Plne funkční CRUD: vytvořit/otevřít/uložit/smazat dokumenty primo v launcheru.
- Notes service skenuje `notes/` a `notes/canvas/` slozky workspace.

#### Git tab
- Repository status, branch summary, remote label, ahead/behind counts.
- Changes list se stage/unstage checkboxy, revert per-file, stage all.
- Commit area s commit message + Commit / Commit & Push akce.
- Historie commitu (short hash, message, author, time ago).
- Branch list + git init pro workspace bez repo.
- Pull/Push akce s toast feedback.

#### Release metadata
- Srovnana release verze na `3.1.10` v launcher projektu, installeru, fallback `User-Agent` hodnotach a aktualni dokumentaci.

## 3.1.9 - 2026-04-05

### Creator Studio publish, auth a branding hardening

#### Auth & session persistence
- GitHub a VOID ID auth jsou soustredene u hlavniho MC login/context flow, takze Creator Studio uz nema prihlaseni rozhazene po vice plochach.
- Secure storage zapisuje session pod lockem a atomicky pres temp file replace, takze soubezny restore uz nerozbije perzistenci VOID ID a GitHub session.
- VOID ID access token expiry se po restore znovu nacita nebo dopocte z JWT `exp`, session se pred publish flow refreshuje s dostatecnou rezervou a registry auth chyby vraceji citelnejsi hlasky.

#### GitHub / release pipeline
- Creator publish uz nepouziva fake-async git wrapper; dlouhe git operace bezí mimo UI thread a `Stage all`/publish uz nelaguje launcher.
- Publish scope je srovnany s realnym `.voidpack` export payloadem misto celeho workspace a scoped commit uz na Windows nepada na command-line limit ani na quoted path jmena.
- Creator Git panel je zjednoduseny na tok `Vygenerovat .voidpack` -> `Nahrat na repo`, upload vytvori/pushne release tag a GitHub workflow se umi bootstrapnout i znovu zapsat z launcheru.
- Workflow po uploadu assetu automaticky publikuje existujici draft release, takze launcher vidi release asset pres public GitHub endpoint a muze pokracovat do VOID Registry.

#### Branding & export
- Export/publish flow si umi automaticky pripravit fallback `creator_manifest.json` i pro importovane nebo read-only workspace instance.
- Verejne logo CurseForge/Modrinth packu se pri exportu/publish automaticky stahne do `assets/branding`, i kdyz bezny Creator workspace zustava read-only preview.
- `.voidpack` export i GitHub workflow build ted pribalují `assets/branding`, takze logo a square icon skonci v archivu, v repu/tagu i v raw URL pro VOID Registry.
- Lokální export `.voidpack` uz necrashne, kdyz cilovy archiv existuje; zapis probiha pres temp archiv a atomicky replace.

#### Release metadata
- Srovnana release verze na `3.1.9` v launcher projektu, installeru, fallback `User-Agent` hodnotach a aktualni dokumentaci.

## 3.1.8.1 - 2026-04-04

### Startup a archive download hotfix
- Opraven bootstrap crashe pri `dotnet run`: `Streaming Tools` uz nepouziva nevalidni Avalonia gesture `Ctrl+/`, ktera rozbijela XAML parser pri startu launcheru.
- Opraven lock pri presunu docasneho `.download` souboru po stazeni archivu a modu: move se provadi az po zavreni streamu a s kratkym retry proti transientnim Windows lockum.
- Srovnana release verze na `3.1.8.1` v launcher projektu, installeru a referencni dokumentaci.

#### Release tab
- Release pipeline vizualizace (Version → Snapshot → Validate → Notes → Publish).
- Validacni checklist (metadata/logo/mods/cover/loader/size kontroly).
- Export profile cards (.voidpack / CurseForge / .mrpack) se status indikatory.
- Changelog editor pro release notes.
- Release historie ze skenovani `.voidcraft/exports/`.
- Zachovany původni snapshot/export akce.

#### Global Search (Ctrl+K)
- Overlay panel pres cely Creator Studio, hledani napric soubory, mody, poznamkami, git commity a manifestem.
- Vyber vysledku automaticky naviguje na spravny tab a vybere item.

#### Nové služby a modely
- `CreatorGitService` — git CLI wrapper (status, stage, unstage, commit, pull, push, revert, branches, init).
- `CreatorNotesService` — notes CRUD (discover, load, save, create, delete, canvas graphs).
- `CreatorReleaseService` — release pipeline, validace, export profily, release historie.
- `CreatorOverviewModels`, `CreatorGitModels`, `CreatorReleaseModels`, `CreatorNotesModels` — datove tridy.
- 5 novych ViewModel partial files (CreatorOverview, CreatorGit, CreatorNotes, CreatorRelease, CreatorSearch).

#### State wiring
- Workspace change automaticky refreshuje Git, Notes, Release pipeline, Quick Links a Overview stats.
- Vybrany Creator workspace se po restartu launcheru obnovuje spolehliveji podle ulozeneho `SelectedWorkspaceId`; obnoveni uz neprebije nahodny fallback na jinou instanci.
- Pri zakladani noveho Creator profilu se autor predvyplni z aktualne prihlasene identity a pri stavbe manifestu existuje stejny fallback, takze novy workspace uz nevznika s prazdnym autorem jen kvuli tomu, ze uzivatel pole neupravil.
- `Mods` tab ted jasne oddeluje `Katalog modu` od `Nainstalovano ve workspace`, zobrazuje cilovy runtime a kdyz kompatibilni hledani nic nenajde, umi spadnout do sirsiho katalogu misto mrtve prazdne plochy.
- `Files` tab byl prestaveny na sirokou pracovni plochu s jednim hlavnim editorem, levym seznamem souboru a jednim pomocnym panelem, aby nepusobil jako nekolik konkurujicich editoru vedle sebe.

### Creator Studio - Mods & .voidpack
- `Mods` tab uz neni napul odkaz do dalsiho okna. Drzi vyhledani pres CurseForge/Modrinth, local `.jar` import, multi-select, bulk add/remove a zapinani/vypinani modu primo nad vybranym workspacem.
- Creator mod workflow ted pracuje i s `.jar.disabled`, takze launcher udrzi stav vypnutych modu bez bocniho manageru a umi ho zapsat do mod metadata.
- `.voidpack` export uz nebalí cele `mods/*.jar`; mody zapisuje do textoveho `voidpack_modlist.json` manifestu s identitou zdroje a import se je pokusi znovu stahnout.
- Import `.voidpack` ted po obnoveni souboru vypise, kolik modu slo automaticky obnovit z modlistu a kolik kusu potrebuje rucni doplneni.

### Creator Studio - Files Workbench
- `Files` tab uz neni plain-text-only placeholder. Creator Workbench dostal realny editor host s rezimy `Structured`, `Raw`, `Split` a `Diff`.
- Structured parser vrstva ted umi `json`, `json5`, `toml`, `yaml/yml`, `ini/cfg` a `properties`, takze bezne workspace configy jdou cist a upravovat pres scalar field inspector misto slepeho raw textu.
- `js`, `zs`, `scripts` a `kubejs` soubory dostaly syntax-aware outline nad funkcemi, hooky a eventy, i kdyz vlastni editace zustava raw-first.
- Files host ted umi outline, search, focus/quick jump a diff proti `nactene bazi`, `poslednimu snapshotu`, `poslednimu exportu` a u `config/defaultconfigs` i proti default counterpartu.
- Parser chyby a structured-save tradeoffy se ted zobrazuji lidsky citelne primo v launcheru misto ticheho failu.
- Structured save je zamerne normalizacni vrstva: u `json5`, `toml`, `yaml`, `ini` a `properties` muze zmenit formatting, poradi nebo komentare, a UI na to ted upozornuje jeste pred ulozenim.
- Files tab dostal produkcnejsi copy: misto internich nazvu typu `Structured preview` nebo `Diff viewer` ted pouziva citelnejsi rezimy a popisy.

## 3.1.8 - 2026-04-04

### Modpack download hardening
- CurseForge install/update flow uz po padu primarniho file URL zkousi dalsi kandidaty, umi si dodelat metadata pro jednotlive soubory a required mody nepusti do falesne uspesne instalace.
- Modrinth `.mrpack` i interni mod download flow ted zkousi vsechny dostupne mirror URL misto jednoho endpointu.
- Launcher pred preskocenim uz existujiciho modu overuje velikost/hash, aby se nerozjizdela dalsi instalace nad rozbitym nebo utnutym souborem.
- Download top-level `.zip`/`.mrpack` baliku v browser i launch update flow presel na retry + fallback kandidaty.

### Release metadata
- Srovnana release verze na `3.1.8` v launcher projektu, installeru, fallback `User-Agent` verzi pro social feed a referencni dokumentaci.

## 3.1.7 - 2026-04-02

### Auth & launch hotfix
- Opraven regression z launch flow, kdy launcher mohl po přihlášení spustit hru jako `Guest`; před startem teď znovu ověřuje session proti aktivnímu účtu a Microsoft launch nepustí do tichého guest fallbacku.
- Auto-login a account recovery si teď drží správné `MsalAccountId` naposledy ověřeného Microsoft účtu místo náhodného prvního záznamu z cache, takže se uložený profil nerozjede proti jiné relaci.

### Nastavení instance
- Detail instance teď při otevření normalizuje efektivní stav optimalizačních flagů a GC override z globální konfigurace, takže UI pro `Optimalizace` odpovídá tomu, co se při launchi opravdu přidá do JVM argumentů.

### Release workflow
- GitHub Actions Discord notifikace teď zkracuje příliš dlouhý changelog na bezpečnou délku pro Discord embed a webhook request už při HTTP chybě neselže potichu.
- Doplněn aktuální `DISCORD-ANNOUNCEMENT-3.1.7.md` a release index v dokumentaci už ukazuje jen existující announcementy.

### Release metadata
- Srovnana release verze na `3.1.7` v launcher projektu, installeru, fallback `User-Agent` verzi pro social feed a referencni dokumentaci.

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
