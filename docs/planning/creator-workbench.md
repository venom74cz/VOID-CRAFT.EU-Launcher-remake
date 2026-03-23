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
- [ ] lokalni Git workflow a GitHub desk
- [ ] CurseForge/Modrinth kompatibilni packaging a release validace
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
- `src/Services/CreatorStudio/CreatorPackagingService.cs`
- `src/Services/CreatorStudio/CreatorReleaseService.cs`
- `src/Services/CreatorStudio/CreatorCopilotService.cs`
- `src/Controls/CreatorStudio/`
- `src/ViewModels/CreatorStudio/`

Minimalni domluvane kontrakty:

- `CreatorWorkspace`
- `CreatorManifest`
- `CreatorAsset`
- `CreatorDocument`
- `CreatorCanvasNode`
- `CreatorGitStatus`
- `CreatorExportProfile`
- `CreatorReleaseSnapshot`
- `CreatorCopilotScope`
- `CreatorPatchPreview`

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

- [ ] Pridat upload pro `logo`, `cover`, `square icon`, `wide hero`, `social preview`.
- [ ] Dopsat asset storage pravidla a vazbu na `creator_manifest.json`.
- [ ] Pridat preview variant primo v launcheru.
- [ ] Pridat resize/crop pipeline a zakladni validace rozliseni, pomeru stran a transparency.
- [ ] Oznacovat screenshoty jako `official`, `release candidate`, `archive`.
- [ ] Pridat export media kitu pro release materialy.
- [ ] Rozsirit `Metadata` zalozku o vizualni `Branding` blok, aby textova metadata a obrazky patrily do jednoho workflow.
- [ ] Napojit branding na launcher card/header preview.

Exit criteria:

- [ ] autor umi nahrat a spravovat identitu modpacku bez exploreru
- [ ] assety maji stabilni ulozeni a preview

## Faze 3 - Structured Editors a Diff Host

Cil: nahradit raw editor inteligentnim hostem s parsery, diffem a validaci.

Task list:

- [ ] Vytvorit `Editor Host` s rezimy `Structured`, `Raw`, `Split`, `Diff`.
- [ ] Pridat parsery pro `json`, `json5`, `toml`, `yaml`, `yml`, `ini`, `properties`.
- [ ] Pridat syntax-aware rezim pro `js`, `zs`, `kubejs` a dalsi skripty.
- [ ] Pridat sekcni outline, search a quick jump.
- [ ] Pridat `diff against last snapshot/export/default`.
- [ ] Pridat validacni badges a human-readable vysvetleni chyb.
- [ ] Pridat fallback do raw modu tam, kde parser neni spolehlivy.
- [ ] Promenit dnesni plain-text Creator Workbench na finalni `Files` zalozku s dvoupanelovym layoutem `strom souboru + editor/diff`.
- [ ] Navrhnout `Mods` zalozku jako samostatny workflow nad existujicim content managementem, ne jen schovany odkaz jinam.
- [ ] Zachovat soucasny file picker/search a znovu ho pouzit jako vstup do noveho hostu.

Exit criteria:

- [ ] nejcastejsi config a data soubory uz nejsou plain text-only
- [ ] uzivatel vidi co meni a proc je to validni nebo rozbite

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

Cil: pridat skutecny repo workflow nad workspacem.

Task list:

- [ ] Pridat lokalni git integraci `init`, `clone`, `link existing repo`.
- [ ] Pridat `status`, `fetch`, `pull`, `push`, `checkout branch`, `create branch`, `commit`.
- [ ] Pridat diff viewer mezi working tree a HEAD.
- [ ] Umoznit commit jen vybranych souboru nebo workbench scope.
- [ ] Pridat signal `remote ahead/behind`, `uncommitted changes`, `conflicts`, `dirty files`.
- [ ] Napojit GitHub remote metadata: posledni release, posledni commity, linky na issues/PR.
- [ ] U konfliktu nabidnout citelny resolve flow, ne jen hard fail.
- [ ] Vyhradit tomu samostatnou `Git` zalozku, aby sync nebyl schovany mezi vedlejsimi akcemi.
- [ ] Umoznit otevrit workspace z konkretni branch nebo tagu.

Exit criteria:

- [ ] autor umi stahnout/pushnout update z launcheru
- [ ] launcher uz neni jen lokalni editor, ale collaboration hub

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

## Faze 8 - Copilot SDK Layer

Cil: AI musi umet nejen radit, ale pracovat nad celym workbench obsahem.

Task list:

- [ ] Integrovat `Copilot SDK` do Creator Studio shellu.
- [ ] Pridat `scope picker` pro file/folder/notes/canvas/release board.
- [ ] Pridat AI akce `explain`, `find issue`, `suggest`, `rewrite`, `apply patch`, `rename`, `summarize diff`.
- [ ] Umoznit prime AI upravy configu, skriptu, notes, manifestu a dalsich textovych assetu.
- [ ] Umoznit AI preklad mezi vrstvami: z notes do patche, z diffu do changelogu, z crash logu do fix navrhu.
- [ ] Vsechny multi-file zmeny vest pres patch preview, diff, opt-in apply a undo.
- [ ] Pred vetsim apply flow vytvorit snapshot.
- [ ] V Creator rezimu prepnout pravy sloupec launcheru na `Copilot Desk`, ktery dedi kontext aktivni zalozky a aktualniho scope.
- [ ] Zajistit, aby `Copilot Desk` mel ve vsech zalozkach trvaly pristup k celemu vybranemu workspace contextu, i kdyz se zrovna pracuje jen s jednim souborem nebo jednou notes sekci.
- [ ] Podporit `Copilot Desk` rezimy `chat`, `patch preview`, `diff explain`, `notes assist`, `release assist`.
- [ ] Napojit `Copilot Desk` na `Notes / Mind Map Drawer`, aby AI umela zakladat poznamky, doplnovat mind mapu a odkazovat na ni bez opusteni aktualni zalozky.
- [ ] Dopsat audit trail `co AI navrhla`, `co bylo aplikovano`, `co bylo odmitnuto`.

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
9. Faze 8 - Copilot SDK Layer
10. Faze 9 - Release Ops, QA a Publish Flow
11. Faze 10 - Hardening, Polish a Release

## Co Nedelat

Aby se plan nerozpadl:

- [ ] neprepisovat cele `Creator Studio` UI do dalsiho monolitu
- [ ] nevracet se k jednomu nekonecnemu scroll view pres vsechny workflow sekce
- [ ] nenasazovat `Copilot SDK` driv, nez bude hotovy scope + diff + snapshot model
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
- [ ] `.voidpack`, CurseForge a Modrinth exporty jsou validni
- [ ] AI umi delat bezpecne prime upravy nad workbenchem
- [ ] release board umi dovest build az k finalnimu vydani
- [ ] produkt je vykonny, pristupny a ma recovery flow pri chybach

## Kratky Start Plan

Pokud se ma zacit hned dalsim praktickym slicem, doporucuju:

1. udelat Fazi 0
2. rozdelit Creator do workflow zalozek a pripravit creator-only pravy dock contract
3. rozsireni bootstrapu o metadata + `creator_manifest.json`
4. logo/cover upload
5. editor host s prvnim structured parserem pro `json`/`toml`
6. notes workspace

To je nejkratsi cesta, jak z dnesniho Creator Studia udelat viditelne silnejsi produkt bez odkladu do "nekdy potom".
