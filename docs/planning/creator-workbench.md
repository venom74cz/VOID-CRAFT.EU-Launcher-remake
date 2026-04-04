# Creator Workbench

Kompletni implementacni plan pro premenu soucasneho `Creator Studio` na plnohodnotny modpack workspace od prvni instance az po finalni release.

Dokument je postaveny na dvou zdrojich:

- produktovy smer z [`future.md`](../../future.md)
- realny stav launcheru v kodu k `2026-03-23`

## North Star

Cil je jednoduchy:

- prijdu do launcheru
- vytvorim nebo naklonuju novy modpack workspace
- nastavim identitu, metadata, assety a exportni cile
- upravim configy, skripty, questy, notes a release obsah bez odchodu do peti jinych aplikaci
- napojim Git a GitHub
- pouziju AI pres `Copilot SDK` na upravy workbench obsahu
- udelam build, playtest, diff, changelog a vydani

Creator Workbench ma byt misto, odkud autor odchazi az ve chvili, kdy je build venku.

## Reality Check

Tohle uz je dnes realne hotove a ma se znovu pouzit, ne zahodit:

- [x] Custom instance wizard pro novou instanci s nazvem, MC verzi, loaderem a loader buildem.
- [x] Pridavani a odebirani modu z CurseForge a Modrinthu pro custom profily.
- [x] Instance workspace s `Overview`, `Content`, `Gallery`, `Performance`, `Saves and Backups`, `Advanced`.
- [x] Snapshoty a lokalni backup workspace.
- [x] `.voidpack` export a import.
- [x] Creator Studio picker pracovni instance, otevreni workspace, screenshotu a logu.
- [x] Creator Workbench plain-text editor pro male textove soubory z `config`, `defaultconfigs`, `scripts`, `kubejs`.
- [x] Zakladni Git tab nad lokalnim `git` CLI se zmenami, stage flow, commitem, branchemi a compact historii.
- [x] GitHub-backed roadmap/changelog/update awareness v launcheru.

Relevantni zdrojove body:

- [`MainViewModel.CustomProfile.cs`](../../VoidCraftLauncher/src/ViewModels/MainViewModel.CustomProfile.cs)
- [`MainViewModel.Streaming.cs`](../../VoidCraftLauncher/src/ViewModels/MainViewModel.Streaming.cs)
- [`CreatorWorkbenchService.cs`](../../VoidCraftLauncher/src/Services/CreatorWorkbenchService.cs)
- [`InstanceExportService.cs`](../../VoidCraftLauncher/src/Services/InstanceExportService.cs)
- [`InstanceWorkspaceView.axaml`](../../VoidCraftLauncher/src/Controls/InstanceWorkspaceView.axaml)

Tohle dnes chybi a je potreba dodat:

- [ ] metadata a branding vrstva pri bootstrapu instance
- [ ] structured editory misto plain text-only flow
- [ ] notes/wiki + quest/progression canvas
- [ ] GitHub auth, create repo, private clone, sync parity ala GitHub Desktop a plny GitHub desk
- [ ] CurseForge/Modrinth kompatibilni packaging a release validace
- [ ] VOID ID launcher + backend identity vrstva pro ucty, projekty, role a release registraci
- [ ] AI-native editace nad celym workbenchem pres `Copilot SDK`
- [ ] release board, QA gate, playtest kanaly a rollback flow

## Delivery Strategy

Zakladni pravidla implementace:

- nestavet vse znovu uvnitr `MainViewModel.Streaming.cs`
- zachovat dnesni funkcni flow a rozsirivat ho po vertikalnich slices
- kazda faze musi skoncit pouzitelnym kusem produktu, ne jen scaffoldem
- `Copilot SDK` prijde az ve chvili, kdy bude existovat stabilni model scope, diffu a apply flow
- vsechny AI zmeny musi mit preview, diff a undo
- export a release vrstva nesmi rozbit soucasny `.voidpack`

## Cile V1

V1 Creator Workbenche je hotovy, kdyz plati vsechno:

- autor umi vytvorit novou modpack instanci i z templatu nebo gitu
- autor umi nahrat logo a dalsi assety primo v launcheru
- autor umi editovat configy a bezne datove soubory citelneji nez v raw textu
- autor umi vest notes a progression/quest plan bez odchodu jinam
- autor umi syncovat workspace s git repozitarem
- autor umi vyrobit `.voidpack`, CurseForge-compatible export a `Modrinth .mrpack`
- autor umi pres AI nechat vysvetlit i upravit libovolny workbench obsah
- autor umi z launcheru dokoncit playtest, changelog, release gate a publish pripravu

## Dnesni P0 Priority (2026-04-04)

Pokud pokracujeme z aktualniho realneho stavu, dalsi kriticke bloky uz nejsou "nice to have", ale nutny zaklad pro dalsi Creator workflow:

- `GitHub login + remote desk`: prihlaseni, create repo, private clone, fetch/pull/push/sync, publish branch a auth diagnostika tak, aby Creator realne fungoval jako lehci GitHub Desktop pro modpack workspace.
- `VOID ID`: bezpecna jednotna identita pro launcher + backend + web, role, projekty, release registrace a propojeni `Microsoft` / `Discord` / `GitHub` bez dalsiho rustu ad-hoc tokenu.
- `Copilot SDK`: nativni AI host s jasnym scope pickerem, streaming chatem, patch preview, explicitnim apply flow a audit trailem misto volneho chatu bez guardrailu.

## UX Shell a Information Architecture

Dnesni Creator Studio uz ma dobry vizualni zaklad, ale informacni architektura je moc "long page". Finalni smer nema byt dalsi vysoka stranka s deseti kartami pod sebou, ale jasne rozdeleny workflow shell.

Hlavni UX pravidla:

- jeden hlavni ukol = jedna zalozka, ne nekonecny scroll pres cely Creator
- horni cast se selected workspacem a rychlym stavem zustava stale po ruce
- kazda zalozka ma vlastni fokus a vlastni primary actions
- v Creator rezimu se pravy sloupec prepina z bezneho `ContextDock` na `Copilot Desk`
- `Copilot Desk` je dostupny ve vsech zalozkach a nikdy neztraci vazbu na aktualne vybranou workspace
- `Copilot Desk` musi byt kontextovy podle aktualni zalozky, souboru, diffu nebo notes scope
- `Copilot Desk` musi vzdy videt sdileny workspace context: metadata, aktivni branch, dirty stav, vybrany soubor, posledni diff, notes scope, export stav a posledni snapshot
- vedle `Copilot Desk` ma existovat rychly vyjizdeci `Notes / Mind Map Drawer`, ktery lze otevrit z jakekoliv zalozky bez opusteni aktualniho workflow
- `Notes / Mind Map Drawer` vyjizdi zprava "zpoza" `Copilot Desk`, prekryva hlavni creator plochu a slouzi pro rychle planovani, zapis napadu a progression mapu
- quick actions jako logy, screenshoty, workspace folder a session summary patri do `Overview`, ne mezi editor a release flow
- finalni UX ma minimalizovat vnoreny scroll a omezit situace, kdy uzivatel ztrati kontext pri prechodu mezi editaci, planningem a exportem

Finalni uzivatelske workflow zalozky:

- `Overview` - vyber instance, rychly stav workspace, server vazby, session akce, logy, screenshoty, posledni aktivita
- `Metadata` - nazev packu, summary, autori, verze, MC verze, loader, release channel, logo, cover a dalsi assety
- `Mods` - pridavani, update, mazani, kontrola verzi, zavislosti, konfliktni moduly a navaznost na export
- `Files` - prochazeni souboru, structured editor, raw fallback, diff, validace, quick jump
- `Notes` - markdown nebo rich notes, interne wiki, poznamky k balancu a `Quest / Progression Canvas` nebo mind mapa
- `Git` - status, branche, commit, pull, push, diff, konflikty, GitHub vazby
- `Release` - export/import, snapshoty, validace, changelog, QA gate, publish podklady

Implementacni poznamka:

- funkce mohou vznikat po fazich, ale ve finalnim produktu se maji seskupit do techto workflow zalozek, aby uzivatel nescrolloval pres vsechno najednou
- `Copilot Desk` nema byt samostatna "news" karta, ale prava kontextova pracovni plocha Creatoru s chatem, diff preview a AI akcemi nad aktualnim scope
- prepinani mezi zalozkami nesmi resetovat AI kontext workspace; meni se jen aktivni fokus a doplnkove scope informace
- `Notes` workflow ma mit dve vrstvy: plnou zalozku pro spravu a archivaci obsahu a rychly vyjizdeci drawer pro okamzite napady, mapovani progresu a poznamky behem prace jinde

## Navrhovana Architektura

Doporucena nova struktura:

- `src/Models/CreatorStudio/`
- `src/Services/CreatorStudio/CreatorWorkspaceService.cs`
- `src/Services/CreatorStudio/CreatorManifestService.cs`
- `src/Services/CreatorStudio/CreatorAssetsService.cs`
- `src/Services/CreatorStudio/CreatorEditorService.cs`
- `src/Services/CreatorStudio/CreatorNotesService.cs`
- `src/Services/CreatorStudio/CreatorGitService.cs`
- `src/Services/CreatorStudio/CreatorGitHubService.cs`
- `src/Services/CreatorStudio/CreatorGitHubAuthService.cs`
- `src/Services/CreatorStudio/CreatorPackagingService.cs`
- `src/Services/CreatorStudio/CreatorReleaseService.cs`
- `src/Services/CreatorStudio/CreatorCopilotService.cs`
- `src/Services/VoidIdentity/VoidIdAuthService.cs`
- `src/Services/VoidIdentity/VoidIdApiClient.cs`
- `src/Controls/CreatorStudio/`
- `src/ViewModels/CreatorStudio/`

Minimalni domluvane kontrakty:

- `CreatorWorkspace`
- `CreatorManifest`
- `CreatorAsset`
- `CreatorDocument`
- `CreatorCanvasNode`
- `CreatorGitStatus`
- `CreatorGitAuthSession`
- `CreatorExportProfile`
- `CreatorReleaseSnapshot`
- `CreatorCopilotScope`
- `CreatorPatchPreview`
- `VoidIdSession`
- `VoidIdProjectMembership`

## Faze 0 - Foundation, Shell a Contracts

Cil: pripravit datovy model a service vrstvu tak, aby dalsi faze nestavely na nahodnych helper metodach.

Task list:

- [x] Definovat `CreatorShellState` pro `selected tab`, `selected subview`, `selected scope`, `right dock mode`, `dirty indicators` a `last open workspace section`.
- [x] Definovat `CreatorWorkspaceContext` jako sdileny zdroj pravdy pro pravy `Copilot Desk` napric vsemi creator zalozkami.
- [x] Navrhnout finalni IA workflow zalozek `Overview`, `Metadata`, `Mods`, `Files`, `Notes`, `Git`, `Release`.
- [x] Rozdelit soucasny dlouhy Creator page layout na budouci samostatne hosty panelu misto jednoho velkeho stacku v jednom `ScrollViewer`.
- [x] Definovat contract pro creator-only pravy panel, ktery umi prepnout standardni `ContextDock` na `Copilot Desk`.
- [x] Definovat sekundarni pravostranny `Drawer/Canvas` layer pro `Notes` a `Mind Map`, ktery se otevre zpoza `Copilot Desk` a prekryva hlavni creator plochu.
- [x] Zalozit `CreatorStudio` model/service namespace misto dalsiho nafukovani stavajicich partialu.
- [x] Navrhnout `creator_manifest.json` jako hlavni zdroj pravdy pro metadata workspace.
- [x] Definovat standardni workspace strukturu slozek.
- [x] Zavest `CreatorWorkspaceService`, ktery umi nacist workspace, metadata, assety, docs a stav exportu.
- [x] Oddelit soucasny plain-text workbench od budouciho editor hostu.
- [x] Definovat `scope` model pro `single file`, `multi-file`, `folder`, `notes`, `canvas`, `release board`.
- [x] Dopsat rules pro context handoff mezi zalozkami tak, aby `Copilot Desk` drzel stale workspace-level pamet a zaroven prijimal aktivni fokus z konkretni zalozky.
- [x] Pripravit snapshot-before-apply hook pro vetsi zmeny.
- [x] Dopsat lokalni persistence pro selected workspace, recent workspaces a posledni aktivitu.

Aktualni stav implementace slice `2026-03-23`:

- creator shell uz ma samostatny model stavu, sdileny workspace context a persistenci do launcher configu
- Creator Studio uz neni jedna dlouha stranka, ale workflow shell se zalozkami `Overview`, `Metadata`, `Mods`, `Files`, `Notes`, `Git`, `Release`
- pravy sloupec umi v creator rezimu prepnout standardni `ContextDock` na creator-only `Copilot Desk`
- `Notes / Mind Map Drawer` uz existuje jako sekundarni overlay vrstva nad creator shellem
- `CreatorWorkspaceService` drzi pohromade manifest path contract, standardni layout slozek, git signal, snapshot/export summary a aktivni scope
- snapshot-before-apply hook uz je napojeny na workspace-scope metadata apply flow a umi pred vetsim creator zapisem vytvorit snapshot relevantnich workspace casti

Exit criteria:

- [x] existuje jednotny datovy kontrakt pro Creator Workbench
- [x] dalsi faze uz nebudou muset cist stav napric peti ruznymi helpery

## Faze 1 - Bootstrap a Metadata Studio

Cil: z dnesniho custom instance wizardu udelat skutecny vstupni bod pro novy modpack.

Task list:

- [x] Rozsirit wizard o varianty `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git`, `Restore Snapshot`.
- [x] Pridat metadata pole `pack name`, `slug`, `summary`, `authors`, `version`, `MC version`, `loader`, `loader version`, `recommended RAM`, `primary server`, `release channel`.
- [x] Pri vytvoreni instance zapsat `creator_manifest.json`.
- [x] Automaticky vytvorit standardni strukturu slozek `config`, `defaultconfigs`, `scripts`, `kubejs`, `docs`, `notes`, `exports`, `qa`, `quests`.
- [x] Pridat podporu pro templaty modpacku.
- [x] Vybudovat prvni realnou workflow zalozku `Metadata` jako centralni misto pro identitu packu a technicke parametry instance.
- [x] Umoznit pozdejsi editaci techto metadat v samostatnem metadata panelu.
- [x] Napojit metadata na Instance Workspace header a Creator Studio summary.

Aktualni stav implementace slice `2026-03-23`:

- create profile wizard uz pri bootstrapu sbira prvni metadata packu (`pack name`, `slug`, `summary`, `authors`, `version`, `release channel`, `recommended RAM`, `primary server`)
- zalozeni custom instance uz vytvori standardni creator workspace slozky a prvni `creator_manifest.json`
- workflow zalozka `Metadata` uz umi manifest nacist, upravit a ulozit zpet do workspace bez odchodu do externiho editoru
- snapshot-before-apply hook je dnes realne zapojeny aspon pro workspace-scope metadata apply flow; finalni multi-file patch host na ten samy guard navaze pozdeji
- wizard ted umi realny bootstrap `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git` i `Restore Snapshot`; lokalni archive jdou pres existujici installer, git clone pres lokalni `git` proces a snapshot restore pres launcher backup workspace
- template presets uz vytvari curated baseline soubory v `docs`, `notes`, `qa` a `quests`, takze template neni jen kosmeticka volba ve wizardu
- `creator_manifest.json` se po ulozeni propaguje zpet do `ModpackInfo`, Instance Workspace header ted preferuje creator metadata a Creator Studio Overview/clipboard summary uz ukazuji pack identity, release channel a primary server z manifestu

Exit criteria:

- [x] novy modpack se da zalozit jako release-ready workspace
- [x] metadata nejsou rozstrilena po nahodnych souborech

## Faze 2 - Branding a Asset Pipeline

Cil: dostat do launcheru plnohodnotnou spravu loga, coveru a dalsich media assetu.

Task list:

- [x] Pridat upload pro `logo`, `cover`, `square icon`, `wide hero`, `social preview`.
- [x] Dopsat asset storage pravidla a vazbu na `creator_manifest.json`.
- [x] Pridat preview variant primo v launcheru.
- [x] Pridat resize/crop pipeline a zakladni validace rozliseni, pomeru stran a transparency.
- [x] Oznacovat screenshoty jako `official`, `release candidate`, `archive`.
- [x] Pridat export media kitu pro release materialy.
- [x] Rozsirit `Metadata` zalozku o vizualni `Branding` blok, aby textova metadata a obrazky patrily do jednoho workflow.
- [x] Napojit branding na launcher card/header preview.
- [x] Automaticky importovat verejne logo z CF/MR packu pri prvnim generovani manifestu.

Aktualni stav implementace slice `2026-03-25`:

- `CreatorBrandingModels.cs` definuje `CreatorBrandingProfile`, `CreatorAssetMetadata`, `BrandingAssetSlot` a `BrandingAssetRequirement` s doporucenymi rozlisenimi a pomery stran
- `CreatorManifest` uz obsahuje `Branding` profil a `Assets` kolekci pro metadata nahraných assetu
- `CreatorAssetsService` resi upload, validaci (rozliseni, pomer stran, alpha kanal pres SkiaSharp), storage do `assets/branding` a export media kitu
- `CreatorManifestService` pri absenci manifestu u stazenych CF/MR packu vytvari fallback z dostupnych dat (nazev, autor, popis, verze) a pri prvnim generovani manifestu automaticky stahuje verejne logo
- `MainViewModel.CreatorStudio.Branding.cs` pridava upload/replace/remove/export commandy a live preview pro vsechny branding sloty
- tlacitko v metadata tabu se meni na "Vygenerovat z existujiciho" pokud manifest chybi a jde o importovany pack
- branding preview se propaguje do instance workspace hlavicky
- `creator_manifest.json` nově drží i kurátorovaná metadata screenshotů (`official`, `release candidate`, `archive`, favorit), takže se výběr promo galerie nepřepisuje běžným metadata save flow
- `Metadata` tab nově ukazuje featured promo screenshot, rychlé tagovací akce nad galerií a export `media kitu` přibaluje curated screenshoty i alias `featured-screenshot.*`

Exit criteria:

- [x] autor umi nahrat a spravovat identitu modpacku bez exploreru
- [x] assety maji stabilni ulozeni a preview

## Faze 3 - Structured Editors a Diff Host

Cil: nahradit raw editor inteligentnim hostem s parsery, diffem a validaci.

Task list:

- [x] Vytvorit `Editor Host` s rezimy `Structured`, `Raw`, `Split`, `Diff`.
- [x] Pridat parsery pro `json`, `json5`, `toml`, `yaml`, `yml`, `ini`, `properties`.
- [x] Pridat syntax-aware rezim pro `js`, `zs`, `kubejs` a dalsi skripty.
- [x] Pridat sekcni outline, search a quick jump.
- [x] Pridat `diff against last snapshot/export/default`.
- [x] Pridat validacni badges a human-readable vysvetleni chyb.
- [x] Pridat fallback do raw modu tam, kde parser neni spolehlivy.
- [x] Promenit dnesni plain-text Creator Workbench na finalni `Files` zalozku s dvoupanelovym layoutem `strom souboru + editor/diff`.
- [x] Navrhnout `Mods` zalozku jako samostatny workflow nad existujicim content managementem, ne jen schovany odkaz jinam.
- [x] Zachovat soucasny file picker/search a znovu ho pouzit jako vstup do noveho hostu.

Aktualni stav implementace slice `2026-04-04`:

- `Files` zalozka uz neni plain-text box; ma realny editor host s rezimy `Structured`, `Raw`, `Split` a `Diff`
- structured parsovani je napojene pro `json`, `json5`, `toml`, `yaml/yml`, `ini/cfg` a `properties`
- `js`, `zs`, `scripts` a `kubejs` soubory dostaly syntax-aware outline nad funkcemi, hooky a eventy, i kdyz samotna editace zustava raw-first
- levy inspector drzi outline, filtr, focus/quick jump a scalar hodnoty pro structured edit bez rozbiti soucasneho pickeru souboru
- diff umi porovnat aktualni obsah proti `nactene bazi`, `poslednimu snapshotu`, `poslednimu exportu` a u `config/defaultconfigs` i proti default counterpartu
- validace uz vraci lidsky citelne chyby/warningy misto ticheho failu parseru
- raw fallback zustava bezpecny default vsude, kde parser nema spolehlivy roundtrip
- structured save je vedome normalizacni vrstva: u `json5`, `toml`, `yaml`, `ini` a `properties` muze zmenit formatting, poradi nebo komentarove bloky; UI na to ted explicitne upozornuje

Exit criteria:

- [ ] nejcastejsi config a data soubory uz nejsou plain text-only
- [ ] uzivatel vidi co meni a proc je to validni nebo rozbite

Aktualni stav implementace (2026-04-04):

- `Mods` zalozka v Creator Studiu uz drzi kompletni obsahovy workflow v jednom tabu: umi filtrovat nainstalovane mody, pridavat local `.jar`, delat multi-select bulk akce a zapinat/vypinat i odebirat mod obsah bez odskoku do dalsiho manageru.
- Mod workflow uz v creator rezimu necili slepe na `CurrentModpack`; pouziva realne vybrany Creator workspace, takze search/add/remove i list pracuji nad spravnou instanci.
- `.voidpack` export/import uz u modu nepouziva bundlovani `jar` souboru; misto toho zapisuje textovy modlist se zdrojovou identitou a import umi tyhle mod reference znovu stahnout nebo nahlasit jako manualni follow-up.

## Faze 4 - Notes, Wiki a Quest Canvas

Cil: drzet planning, poznamky a progression thinking primo v launcheru.

Task list:

- [ ] Pridat `Notes Workspace` pro markdown nebo rich text notes.
- [ ] Pridat strukturu `design notes`, `balancing notes`, `release notes`, `known issues`, `todo`.
- [ ] Pridat `Quest / Progression Canvas`.
- [ ] Podporit nody pro `questline`, `gate`, `boss`, `dimension`, `recipe tier`, `release blocker`, `idea`.
- [ ] Umoznit odkaz z node na konkretni soubor, commit, export snapshot nebo issue.
- [ ] Umoznit export notes/canvas do `md`, `json`, `png`.
- [ ] Udelat z toho finalni `Notes` zalozku s rezimy `Notes`, `Wiki`, `Canvas`, `Mind Map`.
- [ ] Pridat globalni quick-open tlacitko nebo sipku na `Copilot Desk`, ktere otevre `Notes / Mind Map Drawer` z libovolne creator zalozky.
- [ ] Umoznit, aby drawer otevrel posledni pouzity notes dokument nebo posledni aktivni mind mapu bez ztraty aktualniho creator kontextu.
- [ ] Umoznit pripnout note nebo canvas node ke konkretni zalozce, souboru, diffu nebo release kroku.
- [ ] Dopsat search pres notes i canvas.

Exit criteria:

- [ ] autor umi vest planning a zapisky bez odchodu do Obsidianu/Miro
- [ ] notes/canvas umi odkazovat na realny workbench obsah

## Faze 5 - Git a GitHub Desk

Cil: pridat skutecny repo workflow nad workspacem vcetne GitHub prihlaseni a remote operaci tak, aby Creator realne pokryl bezny flow ala GitHub Desktop.

Task list:

- [x] Mit zakladni lokalni git vrstvu nad `git` CLI pro `status`, `stage`, `unstage`, `commit`, `pull`, `push`, `branch list` a compact historii.
- [ ] Dotahnout `init`, `clone`, `link existing repo` primo z `Git` tabu a bootstrap wizardu.
- [ ] Pridat prihlaseni do GitHubu pres browser/device flow pro public desktop klient.
- [ ] Napojit GitHub API SDK (`Octokit`) pro `create repo`, `fork`, `list repos`, `list branches`, `open PR`, `issues`, `release draft` a remote metadata.
- [ ] Umoznit `Create repository from workspace` vcetne volby ownera, visibility, README, `.gitignore` a remote bootstrapu.
- [ ] Umoznit `Clone private repo`, `Change remote`, `Test auth`, `Publish branch`, `Fetch`, `Pull`, `Push` a `Sync` CTA z jednoho panelu.
- [ ] Dotahnout remote-aware `status`, `fetch`, `pull`, `push`, `checkout branch`, `create branch` a `commit` flow do jednoho konzistentniho UX.
- [ ] Pridat diff viewer mezi working tree a HEAD.
- [ ] Umoznit commit jen vybranych souboru nebo workbench scope.
- [ ] Pridat signal `remote ahead/behind`, `uncommitted changes`, `conflicts`, `dirty files`.
- [ ] Napojit GitHub remote metadata: posledni release, posledni commity, linky na issues/PR.
- [ ] U konfliktu nabidnout citelny resolve flow, ne jen hard fail.
- [ ] Vyhradit tomu samostatnou `Git` zalozku, aby sync nebyl schovany mezi vedlejsimi akcemi.
- [ ] Umoznit otevrit workspace z konkretni branch nebo tagu.
- [ ] Pri prvnim pushi nabidnout bootstrap GitHub Actions workflow pro `.voidpack` / release buildy.
- [ ] Dopsat auth diagnostics a reconnect flow, aby bylo jasne proc `push` nebo `clone` selhal.

Aktualni stav implementace slice `2026-04-04`:

- `CreatorGitService` uz resi zakladni lokalni git vrstvu pres CLI a `Git` tab uz umi zmeny, stage/revert, commit, commit & push, branche a compact historii.
- Chybi ale prihlaseni do GitHubu, create repo flow, private repo clone chooser, skutecny `fetch/sync` dashboard, publish branch, PR/release metadata a auth-aware error states.

Bezpecnostni guardraily:

- default transport ma byt `HTTPS + system credential manager / Git Credential Manager`, ne PAT ulozeny v remote URL nebo `.git/config`
- launcher je public client; zadny `GitHub client secret` nesmi byt zabaleny v desktop appce
- stavajici `SecureStorageService` fallback do plaintextu na non-Windows nestaci pro GitHub tokeny; pred shipem je nutne doplnit realny OS keychain provider pro Linux/macOS
- scope rozdelit minimalne na `read profile`, `repo metadata` a `repo write`; UI musi umet token odpojit/revoke
- auth a sync chyby nesmi padat do ticheho failu; vzdy musi byt videt, jestli chyba byla v loginu, credential helperu, remote autorizaci nebo merge konfliktu

Exit criteria:

- [ ] autor umi se prihlasit do GitHubu, zalozit repo, klonovat i private repo a delat `fetch/pull/push/sync` bez terminalu
- [ ] launcher uz neni jen lokalni editor, ale collaboration hub s auth-aware remote workflow

## Faze 6 - Packaging, Export Profiles a Compatibility

Cil: z jedne instance vyrabet validni buildy pro ruzne cile.

Task list:

- [ ] Zachovat a znovu pouzit stavajici `.voidpack` export/import.
- [ ] Pridat `CurseForge-compatible export`.
- [ ] Pridat `Modrinth .mrpack` export.
- [ ] Pridat `GitHub release bundle` export profile.
- [ ] Pridat export preview `co jde ven`, `co je nove`, `co je odebrane`, `co je rozbite`.
- [ ] Pridat validator pro metadata, manifest, assety, overrides, loader target a velikost souboru.
- [ ] Pridat import flow pro CF/MR pack, repo branch a starsi export snapshot.
- [ ] Postavit z techto akci citelnou `Release` zalozku misto dalsiho dlouheho seznamu tlacitek.
- [ ] Dopsat export profiles jako ulozitelne recipe presety.

Exit criteria:

- [ ] launcher umi vic nez lokalni `.voidpack`
- [ ] export je auditovatelny a validovany pred vydanim

## Faze 7 - Snapshot Ring, Diff Archive a Release History

Cil: udelat z exportu a releasu sledovatelnou historii, ne jednorazovy zip.

Task list:

- [ ] Ukladat posledni exporty jako release snapshoty.
- [ ] Drzet metadata `author`, `version`, `branch`, `commit`, `MC version`, `loader`, `mod count`, `notes`.
- [ ] Pridat diff mezi snapshoty.
- [ ] Pridat diff `working tree vs last export`.
- [ ] Umoznit oznaceni `stable`, `candidate`, `nightly`, `playtest`, `hotfix`.
- [ ] Pridat rychly rollback na predchozi snapshot.
- [ ] Umoznit vytvorit playtest instanci z konkretniho snapshotu.

Exit criteria:

- [ ] autor umi snadno porovnat a vratit buildy
- [ ] release historie je citelna i pro support/QA

## Faze 7A - VOID ID Control Plane (backend + launcher)

Cil: zavest jednotnou bezpecnou identitu pro VOID-CRAFT.EU tak, aby launcher, backend, web a creator release flow sdilely jeden auth a project model.

Task list:

- [ ] Vyjmout auth a project identity logiku z monolitickeho `void-craft.eu-BACKEND/server.js` do samostatneho modulu / service vrstvy `VoidId`.
- [ ] Zavest DB schema a migrace pro `users`, `external_accounts`, `sessions`, `refresh_tokens`, `projects`, `project_members`, `release_registrations`, `tester_codes` a `audit_log`.
- [ ] Postavit `VOID ID` jako `OAuth 2.1 / OIDC` provider: launcher jako public client s `Authorization Code + PKCE`, web jako secure cookie klient, backend jako issuer se `JWKS`.
- [ ] Podepisovat access tokeny asymetricky (`EdDSA` nebo `ES256/RS256`) s `kid`, rotaci klicu a `/.well-known/openid-configuration`.
- [ ] Refresh tokeny delat jako rotating opaque tokeny, v DB ukladat jen hash, detekovat replay a umet odvolat celou session chain.
- [ ] V launcheru pridat `VOID ID` login/logout/link-unlink flow a spravu propojenych uctu `Microsoft`, `Discord`, `GitHub`.
- [ ] V backendu zavest role a claims pro `player`, `tester`, `creator`, `maintainer`, `admin` a vazbu prav na projekty.
- [ ] Pridat project registry: vlastni modpacky, release kanaly, tester pristupy, release registrace a membership management.
- [ ] Navazat Creator `Release` flow na `VOID ID` projekt a release registraci bez nutnosti manualniho backend zasahu.
- [ ] Zastavit dalsi rust ad-hoc auth patternu typu `token` v query stringu a postupne je nahradit bearer/cookie flow s expiraci a audit logem.
- [ ] Pridat rate limiting, IP/device heuristiky, brute-force ochranu, revoke-all-sessions a admin audit trail.
- [ ] Oddelit secrets od repa, zavest rotaci klicu a pripravenost na self-host / domenove presuny bez vazby na jeden hostname.

Aktualni technicka realita `2026-04-04`:

- backend dnes bezi jako `Express + MySQL` monolit a vedle verejnych endpointu uz ma i privilegovane flow chranene ENV tokeny; `VOID ID` ma tohle sjednotit, ne pridat dalsi paralelni auth vrstvu
- launcher dnes umi `Microsoft auth` pro hru, ale nema jednotnou identitu pro platformu, projekty, tester pristupy ani GitHub/Discord linking

Bezpecnostni guardraily:

- launcher nesmi nikdy obsahovat `client secret`; pouzivat jen public-client flow s `PKCE`
- web ma pouzivat `HttpOnly`, `Secure`, `SameSite` session cookies; launcher ma pouzivat bearer access token + refresh token ulozeny v bezpecnem storage
- zadne tokeny v URL, query stringu, logu nebo clipboardu mimo explicitni short-lived device/login kroky
- claims v access tokenu drzet minimalni; citlive projektove pravo dotahovat server-side podle membershipu
- pokud pridame lokalni heslo, musi byt az jako sekundarni vrstva s `Argon2id`; preferovany smer je external identity linking + pozdeji passkeys

Exit criteria:

- [ ] uzivatel se umi prihlasit do `VOID ID` z webu i launcheru a session se umi bezpecne obnovit a odpojit
- [ ] backend umi overit role a projektove membershipy bez ad-hoc tokenu a launcher umi tyto role respektovat v Creator flow
- [ ] GitHub, Discord a Microsoft propojeni funguje jako first-class account linking vrstva

## Faze 8 - Copilot SDK Layer

Cil: AI musi umet nejen radit, ale pracovat nad celym workbench obsahem.

Task list:

- [ ] Postavit lokalni `Copilot Host` sidecar / service nad `.NET` balickem `GitHub.Copilot.SDK`, necpat agent runtime primo do UI threadu launcheru.
- [ ] Overit a diagnostikovat zavislosti `Copilot CLI`, entitlement a login stav jeste pred vytvorenim session.
- [ ] Integrovat `Copilot SDK` do Creator Studio shellu.
- [ ] Zavest streaming session lifecycle: create, cancel, resume, timeout, retry a per-workspace kontext.
- [ ] Pridat `scope picker` pro file/folder/notes/canvas/release board.
- [ ] Pridat AI akce `explain`, `find issue`, `suggest`, `rewrite`, `apply patch`, `rename`, `summarize diff`.
- [ ] Umoznit prime AI upravy configu, skriptu, notes, manifestu a dalsich textovych assetu.
- [ ] Umoznit AI preklad mezi vrstvami: z notes do patche, z diffu do changelogu, z crash logu do fix navrhu.
- [ ] Definovat custom tools jen pro povolene creator operace: file read/write pres patch preview, notes update, manifest apply, diff explain, release draft helper.
- [ ] Vsechny multi-file zmeny vest pres patch preview, diff, opt-in apply a undo.
- [ ] Pred vetsim apply flow vytvorit snapshot.
- [ ] V Creator rezimu prepnout pravy sloupec launcheru na `Copilot Desk`, ktery dedi kontext aktivni zalozky a aktualniho scope.
- [ ] Zajistit, aby `Copilot Desk` mel ve vsech zalozkach trvaly pristup k celemu vybranemu workspace contextu, i kdyz se zrovna pracuje jen s jednim souborem nebo jednou notes sekci.
- [ ] Podporit `Copilot Desk` rezimy `chat`, `patch preview`, `diff explain`, `notes assist`, `release assist`.
- [ ] Napojit `Copilot Desk` na `Notes / Mind Map Drawer`, aby AI umela zakladat poznamky, doplnovat mind mapu a odkazovat na ni bez opusteni aktualni zalozky.
- [ ] Implementovat explicitni permission handler; produkcni integrace nesmi pouzivat `ApproveAll`.
- [ ] Pridat bloklist / redaction pro secrets, max velikost scope, max pocet souboru a guard proti sahani mimo aktivni workspace.
- [ ] Dopsat audit trail `co AI navrhla`, `co bylo aplikovano`, `co bylo odmitnuto`.

Implementacni poznamka `Copilot SDK`:

- dnesni official SDK pocita s `Copilot CLI` jako runtime prereq; launcher musi umet zobrazit `CLI chybi`, `uzivatel neni prihlaseny`, `chybi entitlement` a `offline` stavy misto ticheho selhani
- prvni produkcni slice ma byt `chat + diff explain + patch preview`; prime multi-file apply a commit draft az po overeni audit trailu, undo a snapshot recoveries

Bezpecnostni guardraily:

- zadny volny shell ani arbitrary command runner pro AI by default; vse pres explicitni allowlist toolu
- `Copilot Desk` nesmi cist nebo posilat secrets bez redaction vrstvy a scope limitu
- bezpecny default je `preview`, ne `auto-apply`; hlasite ukazat co se meni, proc a kolika souboru se to dotkne
- AI session musi jit okamzite zastavit a vsechny apply operace musi zanechat audit stopu

Exit criteria:

- [ ] AI umi realne upravit cokoliv relevantniho v workbenchi
- [ ] bezpecny default je preview, ne tichy rewrite

## Faze 9 - Release Ops, QA a Publish Flow

Cil: z Creator Workbenche dodelat konec cesty az k vydani.

Task list:

- [ ] Vytvorit `Release Board`.
- [ ] Pridat stav `version bump`, `snapshot ready`, `diff ready`, `notes ready`, `qa ready`, `publish target selected`.
- [ ] Pridat smoke checklist a manualni QA potvrzeni.
- [ ] Napojit crash log review a known issues list.
- [ ] Pridat generator changelogu, Discord postu, GitHub release notes a tester briefu.
- [ ] Pridat release gate, ktera blokuje publish pri kritickych chybach.
- [ ] Pridat feedback inbox pro playtest.
- [ ] Pridat archiv vydanych buildu s navazanymi notes a exporty.

Exit criteria:

- [ ] autor umi z launcheru projit pripravu releasu od diffu po publish podklady
- [ ] release gate uz neni jen mentalni checklist mimo launcher

## Faze 10 - Hardening, Polish a Release

Cil: dotahnout kvalitu, vykon a bezpecnost na production uroven.

Task list:

- [ ] Otestovat startup dopad vsech novych Creator Studio panelu.
- [ ] Dopsat offline/degraded fallbacky pro notes, git status, GitHub data a AI unavailable stavy.
- [ ] Otestovat keyboard navigation a focus flow.
- [ ] Otestovat kontrast, reduced motion a error stavy.
- [ ] Otestovat recovery po padu pri apply patch flow.
- [ ] Otestovat rollback/export/import na realnych instancich.
- [ ] Otestovat Windows i Linux behavior pro file paths, git binary a clipboard/shell akce.
- [ ] Dopsat release docs a finalni user-facing copy.

Exit criteria:

- [ ] Creator Workbench je stabilni, citelny a release-ready
- [ ] nova vrstva nezpomaluje launcher ani nerozbija hlavni CTA `Hrat`

## Doporucene Implementacni Poradi

Poradi, ktere dava nejvetsi smysl bez zbytecnych reworku:

1. Faze 0 - Foundation a Contracts
2. Faze 1 - Bootstrap a Metadata Studio
3. Faze 2 - Branding a Asset Pipeline
4. Faze 3 - Structured Editors a Diff Host
5. Faze 4 - Notes, Wiki a Quest Canvas
6. Faze 5 - Git a GitHub Desk
7. Faze 6 - Packaging, Export Profiles a Compatibility
8. Faze 7 - Snapshot Ring, Diff Archive a Release History
9. Faze 7A - VOID ID Control Plane
10. Faze 8 - Copilot SDK Layer
11. Faze 9 - Release Ops, QA a Publish Flow
12. Faze 10 - Hardening, Polish a Release

## Co Nedelat

Aby se plan nerozpadl:

- [ ] neprepisovat cele `Creator Studio` UI do dalsiho monolitu
- [ ] nevracet se k jednomu nekonecnemu scroll view pres vsechny workflow sekce
- [ ] nenasazovat `Copilot SDK` driv, nez bude hotovy scope + diff + snapshot model
- [ ] nepouzivat `PermissionHandler.ApproveAll` ani jiny tichy auto-apply rezim v produkcnim `Copilot SDK` hostu
- [ ] neukladat `GitHub` nebo `VOID ID` tokeny do plaintext storage, remote URL nebo logu
- [ ] nerozsirovat backend o dalsi `token=` query auth patterny
- [ ] nedelat CurseForge/Modrinth publish flow bez validacni vrstvy
- [ ] nenechat notes/canvas zit mimo workspace kontrakt
- [ ] neresit AI jako volny chat bez vazby na realny workbench obsah
- [ ] nenechat v Creator rezimu bezet bezny pravy news/community panel jako hlavni pomocny sloupec
- [ ] neschovat notes a mind mapu do jedine hluboke zalozky bez rychleho otevreni odkudkoliv z Creatoru

## Finalni Release Gate

Creator Workbench lze oznacit za hotovy az kdyz:

- [ ] nova instance se da zalozit od nuly nebo z gitu
- [ ] metadata, branding a assety se spravuji primo v launcheru
- [ ] bezne configy a data soubory maji structured editor
- [ ] notes a progression canvas jsou produkcne pouzitelne
- [ ] git sync a diff flow jsou stabilni
- [ ] GitHub login, create repo a private clone/push/pull flow funguji bez terminalu
- [ ] `.voidpack`, CurseForge a Modrinth exporty jsou validni
- [ ] `VOID ID` login/linking a project registry flow jsou stabilni a bezpecne
- [ ] AI umi delat bezpecne prime upravy nad workbenchem
- [ ] release board umi dovest build az k finalnimu vydani
- [ ] produkt je vykonny, pristupny a ma recovery flow pri chybach

## Kratky Start Plan

Pokud se ma zacit hned dalsim praktickym slicem po aktualnim stavu `2026-04-04`, doporucuju:

1. dodelat Fazi 5 do podoby `GitHub login + create repo + private clone + sync CTA` ala GitHub Desktop
2. paralelne postavit Fazi `7A` jako `VOID ID` auth foundation v backendu a launcher `PKCE` login shell
3. rozbehnout Fazi 8 jako `Copilot Host` POC: chat, diagnostics, diff explain a patch preview bez auto-apply
4. navazat audit trail, undo a snapshot recovery pro AI + release apply flow
5. teprve potom dodelat plny release gate, rollback a pokrocile Notes/Canvas workflow

To je dnes nejkratsi cesta, jak dostat Creator z "lokalni editor + workflow shell" na realny collaboration cockpit s identitou, repem a AI vrstvou.
