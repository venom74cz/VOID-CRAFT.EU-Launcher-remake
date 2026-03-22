# VOID Future Roadmap

Produktova mapa post-release smeru pro launcher. Neni to slib na dalsi patch; je to kuratorovany vyber systemu, ktere muzou z launcheru udelat zivy produkt s vlastnim charakterem, rytmem a duvodem k opakovanemu otevirani.

> [!important] North Star
> Launcher nema byt jen spoustec. Ma byt vstupni portal do celeho herniho ekosystemu: do progresu, komunitnich momentu, navratovych loopu i release komunikace.

> [!tip] Jak s roadmapou pracovat
> Ber to jako produktovy kompas. Priorita A a B jsou realisticke smery s nejvyssim dopadem, zatimco D a cast C jsou druha vlna az po stabilnim technickem zakladu.

> [!warning] Release guardrail
> Kazda budouci feature musi obhajit startup vykon, cistotu dashboardu, offline/degraded stavy a primou vazbu na hlavni CTA `Hrat`.

---

## Co tenhle dokument resi

Hlavni blueprint resi, jak launcher navrhnout a dodat tak, aby byl moderni, cisty a implementovatelny.

Tenhle soubor jde o vrstvu dal. Resi:

- co dlouhodobe zveda hodnotu launcheru
- co podporuje identitu hrace, skupiny a komunity
- co dava smysl jako produktova vrstva, ne jen dekorace

> [!quote]
> Cil neni levna gamifikace.
> Cil je launcher, ktery ma rytmus, kontext a duvod, proc ho chtit otevirat i mimo samotny launch hry.

---

## Jak roadmapu cist

Kazdy napad je psany stejnym stylem, aby slo rychle poznat, co je produktova hodnota a co uz implementacni narocnost:

- `Popis` - co feature znamena z pohledu hrace
- `Mozna implementace` - realisticke zpusoby, jak to postavit
- `Proc to dava smysl` - proc by to melo smysl pro produkt a UX

> [!info]
> Ne vsechny feature jsou vhodne pro kazdy launcher.
> Ale vetsina z nich jde prizpusobit pro ruzne typy projektu: game hub, komunitni launcher, event launcher, launcher pro modded obsah nebo obecnou desktop appku kolem hry.

---

## Produktove mantinely

Kazda future feature by mela splnit aspon 2 z techto bodu:

- zkracuje cestu do hry nebo dava duvod launcher otevrit
- posiluje identitu hrace, skupiny nebo komunity
- propojuje launcher s realnym progressem, obsahem nebo aktivitou
- funguje jako obsahova vrstva, ne jen jako dekorace
- da se rozumne provozovat, analyzovat a ladit
- nevyzaduje nonstop manualni administraci

Kazda future feature by naopak nemela:

- pusobit pay-to-win
- spamovat uzivatele notifikacemi
- rozbijet focus na hlavni CTA `Hrat`
- vytvaret dark patterns nebo FOMO za kazdou cenu

> [!warning]
> **Startup performance jako mantinel.**
> Launcher nesmí být těžší na cold start než hra samotná.
> Každý nový panel, live data feed nebo widget musí projít základním perf testem před integrací do hlavního flow.

---

## Priorita A - Core features

> [!success]
> Tohle jsou feature, ktere maji nejvetsi sanci zvednout hodnotu launcheru skoro v jakemkoliv projektu.

### 1. Seasonal Journey Track

> [!success]
> **Popis**  
> Sezonalni nebo etapovy progres celeho obdobi. Ne battle pass, ale prehledna vrstva, ktera spojuje ukoly, eventy, exploration a komunitni aktivitu do jedne cesty.
>
> **Mozna implementace**
> - Kazde obdobi ma vlastni tema, milestone vrstvy a vizualni identitu.
> - Launcher ukazuje osobni progres, globalni progres a nejblizsi unlock.
> - Rewardy mohou byt hlavne kosmeticke, statusove nebo obsahove.
>
> **Proc to dava smysl**
> - Dava launcheru dlouhodoby rytmus.
> - Zveda navratovost i mimo update dny.
> - Umi propojit hru, obsah a dashboard do jednoho systemu.

### 2. Mastery Paths

> [!success]
> **Popis**  
> Dlouhodobe specializacni cesty podle stylu hrani. Hrac se muze profilovat jako builder, explorer, strategist, collector, creator nebo support player.
>
> **Mozna implementace**
> - Hrac si cestu bud rucne vybere, nebo se mu sklada podle realneho chovani.
> - Kazda cesta ma vlastni milestone badge, titulky a vizualni styl.
> - Launcher ukazuje progres, signature achievementy a dalsi logicky krok.
>
> **Proc to dava smysl**
> - Achievementy pak nejsou jen ploche checklisty.
> - Pomaha orientaci v sirokem mnozstvi obsahu.
> - Posiluje identitu hrace i bez tvrdych kompetitivnich zebricku.

### 3. Chronicle Timeline

> [!success]
> **Popis**  
> Osobni timeline zalozena na skutecnem progresu a udalostech. Cilem je, aby launcher neukazoval jen data, ale i pribeh toho, co hrac zazil.
>
> **Mozna implementace**
> - Timeline taha data z achievementu, questu, eventu, unlocku a dulezitych session momentu.
> - Kazdy zapis ma kratky text, datum a idealne vazbu na konkretni aktivitu nebo obsah.
> - Vybrane body lze pripnout jako osobni highlighty.
>
> **Proc to dava smysl**
> - Obycejnou historii meni na osobni narativ.
> - Zveda emotional value launcheru.
> - Dobre funguje i bez agresivni gamifikace.

### 4. Discovery Codex

> [!success]
> **Popis**  
> Atlas objevu, zaznamu a zajimavych stop v obsahu. Muze jit o lokace, challenge, buildy, postavy, guide entry nebo cokoliv, co ma smysl archivovat a objevovat.
>
> **Mozna implementace**
> - Kazdy zaznam muze obsahovat obrazek, autora, kratky popis, typ obsahu a stav `discovered / undiscovered`.
> - Soucasti muze byt `featured entry of the week` nebo kuratorsky vyber.
> - Codex muze navazovat na achievementy, questy nebo exploration.
>
> **Proc to dava smysl**
> - Dava launcheru silnou obsahovou vrstvu.
> - Podporuje exploration i dlouhodoby archiv.
> - Funguje pro ruzne typy projektu, nejen pro jeden konkretni typ obsahu.

### 5. Smart Onboarding Missions

> [!success]
> **Popis**  
> Onboarding ve forme jasne progres cesty misto textove zdi. Novy hrac dostane konkretni kroky, ktere ho nenasilne provedou prvnimi minutami nebo hodinami.
>
> **Mozna implementace**
> - Krokovy flow muze zahrnovat login, instalaci, prvni launch, nastaveni, prvni session a prvni doporuceny obsah.
> - Kazdy krok muze mit mini reward, help card nebo kratky contextual tip.
> - Launcher muze rozeznat, kde se hrac zasekl, a nabidnout odpovidajici pomoc.
>
> **Proc to dava smysl**
> - Snizuje zahlceni.
> - Zvysuje retenci novych hracu.
> - Resi UX problem bez nutnosti psat dlouhe manualy.

### 6. Memory Lane / Return Recap

> [!success]
> **Popis**  
> Kdyz se hrac vrati po delsi dobe, launcher mu da kvalitni recap toho, co se zmenilo a na co navazat.
>
> **Mozna implementace**
> - Recap muze ukazat posledni session, nove zmeny, aktivni eventy a doporuceny dalsi krok.
> - U timeline lze vytahnout posledni dulezity moment pred pauzou.
> - Pri delsi neaktivite se muze dashboard prepnout do navratoveho rezimu.
>
> **Proc to dava smysl**
> - Je to silny retention system bez grindu.
> - Pomaha hracum znovu navazat bez chaosu.
> - Dela z launcheru uzitecneho pruvodce, ne jen spoustec.

### 7. Patch Impact Summary

> [!success]
> **Popis**  
> Misto klasickych patch notes launcher vysvetli, co update znamena konkretne pro daneho hrace.
>
> **Mozna implementace**
> - Panel muze rict `pribyly nove challenge`, `zmenil se tvuj oblibeny obsah`, `tahle zmena se tyka tveho progresu`.
> - Zdrojem muze byt kombinace release metadata, tagu a jednoducheho rule engine.
> - Pro navratove hrace muze byt dostupny zkraceny recap `co je pro tebe dulezite`.
>
> **Proc to dava smysl**
> - Changelog dostane realnou hodnotu.
> - Hrac se rychleji zorientuje po update.
> - Je to premium UX s rozumnou implementacni slozitosti.

### 8. Performance Advisor

> [!success]
> **Popis**  
> Osobni performance a stability vrstva. Launcher pomaha hraci pochopit, proc ma problemy, a nabidne rozumne kroky.
>
> **Mozna implementace**
> - Muze pracovat s crash logy, RAM nastavenim, profily kvality, problemovymi moduly nebo startup health checkem.
> - Nabidne doporuceni typu `snizit RAM`, `prepnout preset`, `vypnout narocne doplnky`.
> - Vse musi byt vysvetlene lidsky a bez technickeho overloadu.
>
> **Proc to dava smysl**
> - Resi realny problem skoro kazdeho vetsiho launcheru.
> - Zveda duveru v produkt.
> - Ma prakticky dopad i bez flashy prezentace.

### 9. Version Story / Changelog Card

> [!success]
> **Popis**  
> Vizuální "release card" pro každou verzi launcheru samotného — odlišná od Patch Impact Summary, která řeší herní obsah. Místo technického changelogu uživatel vidí přehlednou kartu: co se změnilo v UI, co přibylo v dashboardu, co se opravilo.
>
> **Mozna implementace**
> - Karta se zobrazí při prvním spuštění po aktualizaci launcheru jako neinvazivní overlay nebo panel.
> - Každá verze má krátký název / motiv (ne jen číslo), 3–5 bullet bodů a vizuální diff klíčových změn.
> - Archiv všech verzí dostupný v nastavení.
>
> **Proc to dava smysl**
> - Buduje důvěru — uživatel vidí, že produkt žije a zlepšuje se.
> - Nízká implementační složitost, vysoký UX dopad.
> - Dělá launcher transparentním produktem, ne black boxem.

---

## Priorita B - Social a community systems

> [!info]
> Tady jsou systemy, ktere nejvic pomahaji navratum, social loopu a pocitu, ze launcher zije.

### 10. Community Milestones

> [!info]
> **Popis**  
> Globalni cile pro celou komunitu, sezonu nebo event. Hrac vidi, ze jeho aktivita je soucasti vetsiho pohybu.
>
> **Mozna implementace**
> - Cile mohou byt pocet challenge, quest count, discovery progress, creator activity nebo event body.
> - Launcher zobrazi globalni progress bary, highlight poslednich posunu a nejvetsi push momenty.
> - Pri splneni se odemkne globalni reward, story card nebo specialni featured obsah.
>
> **Proc to dava smysl**
> - Buduje pocit spolucasti.
> - Dobre se napojuje na eventy a release dny.
> - Dashboard pak pusobi zive, ne staticky.

### 11. Group / Guild Identity Layer

> [!info]
> **Popis**  
> Social meta-vrstva pro party, gildy, squady nebo male skupiny. Launcher pak nepracuje jen s jednotlivcem, ale i se skupinovou identitou.
>
> **Mozna implementace**
> - Skupina ma vlastni summary kartu s aktivitou, poslednimi uspechy a cilovymi milestone.
> - Muze existovat skupinovy prestige level nebo status vrstva.
> - Feed muze ukazovat posledni odemcene uspechy, challenge nebo highlights.
>
> **Proc to dava smysl**
> - Posiluje navrat cele skupiny, ne jen jednotlivce.
> - Zveda hodnotu social loopu.
> - Funguje napric ruznymi typy launcheru a komunit.

### 12. Daily and Weekly Operations

> [!info]
> **Popis**  
> Kratsi rotujici aktivity s jasnym cilem. Nemaji pusobit jako druha prace, ale jako lehky motivator otevrit launcher a neco dokoncit.
>
> **Mozna implementace**
> - Operace mohou byt navsteva obsahu, dokonceni ukolu, aktivita s partou nebo lehka creator challenge.
> - Kazdy ukol by mel byt kratky, srozumitelny a dobrovolny.
> - Vyplata muze byt hlavne kosmeticka, reputacni nebo progressova.
>
> **Proc to dava smysl**
> - Udrzuje rytmus mezi velkymi eventy.
> - Dava dashboardu aktualni obsah.
> - Funguje dobre, pokud se drzi lehke a neagresivni formy.

### 13. Social Proof Cards

> [!info]
> **Popis**  
> Male dashboard karty, ktere ukazuji, ze komunita opravdu zije.
>
> **Mozna implementace**
> - Karty typu `dnes bylo aktivnich 47 hracu`, `nekdo z tve skupiny byl online pred 16 minutami`, `pribyl novy spotlight`.
> - Obsah se taha z bezpecnych, jednoduse vysvetlitelnych dat.
> - Lze omezit na par nejrelevantnejsich karet, aby dashboard nepusobil zahlcene.
>
> **Proc to dava smysl**
> - Launcher pusobi zive.
> - Hrac vidi social kontext i bez otevreni Discordu nebo webu.
> - Nema to velkou implementacni slozitost proti UX hodnote.

### 14. Creator Quests

> [!info]
> **Popis**  
> Track pro kreativni a komunitni obsah. Nejen herni progres, ale i to, co komunita tvori kolem produktu.
>
> **Mozna implementace**
> - Ukoly mohou motivovat ke screenshotum, videum, guide entry, showcase postum nebo event highlightum.
> - Validace muze byt castecne manualni, castecne pres submit flow v launcheru.
> - Nejlepsi prispevky mohou skoncit ve spotlightu nebo v discovery codexu.
>
> **Proc to dava smysl**
> - Podporuje organicky obsah komunity.
> - Propojuje launcher s galerii, social vrstvou a eventy.
> - Dava hodnotu i hracum, kteri nejsou ciste grind oriented.

### 15. Spotlight System

> [!info]
> **Popis**  
> Kuratorska vrstva, ktera pravidelne vytahne to nejzajimavejsi z komunity nebo aktualniho obdobi.
>
> **Mozna implementace**
> - Spotlight muze byt `hrac tydne`, `build tydne`, `creator post tydne`, `guide tydne`, `clip tydne`.
> - Obsah muze schvalovat moderator nebo editorial panel.
> - Launcher zobrazi spotlight jako premium hero blok, ne jen maly feed item.
>
> **Proc to dava smysl**
> - Zveda kvalitu dashboardu.
> - Pomaha kvalitnimu obsahu ziskat viditelnost.
> - Dela z launcheru editorial produkt, ne jen utility appku.

### 16. Reputation for Helpful Actions

> [!info]
> **Popis**  
> Soft reputace za uzitecne chovani. Ne rank za grind, ale prestiz za fair play, pomoc a prinos komunite.
>
> **Mozna implementace**
> - Body mohou jit za pomoc novackum, event participation, tvorbu navodu nebo dlouhodobou ferovou aktivitu.
> - Reputace by mela byt omezena proti farmeni a nemela by byt hlavni kompetitivni metou.
> - Odemyka spis soft benefity typu badge, frame nebo highlight status.
>
> **Proc to dava smysl**
> - Odmenuje chovani, ktere dela komunitu lepsi.
> - Vyhyba se pay-to-win i grind pasti.
> - Muze zvednout kvalitu social prostoru kolem launcheru.

### 17. Shared Goal Board

> [!info]
> **Popis**  
> Spolecna nastenka cilu pro partu, projekt nebo skupinu pratel. Hodi se pro pripravovane eventy, dlouhodobe challenge i kreativni projekty.
>
> **Mozna implementace**
> - Skupina si muze pripnout kratke cile, terminy a stav rozpracovanosti.
> - Ke kazdemu cili lze pripojit odpovedne hrace, obrazek nebo progress note.
> - Dashboard ukaze, co je hotovo, co ceka a kdo se naposledy pohnul.
>
> **Proc to dava smysl**
> - Posunuje social vrstvu z pasivniho feedu do aktivni spoluprace.
> - Dava skupinam duvod launcher otevrit i mimo samotne hrani.
> - Funguje pro casual party i organizovane komunity.

### 18. Event Ops Center

> [!info]
> **Popis**  
> Specialni rezim launcheru pro eventy, sezony a velke release dny.
>
> **Mozna implementace**
> - Obsahuje countdown, live feed, event pravidla, quick join flow a highlight odmen.
> - Pri aktivnim eventu se muze zviditelnit na homepage bez toho, aby schovalo hlavni `Hrat`.
> - Po eventu muze prepnout do recap rezimu s vysledky a nejlepsimi momenty.
>
> **Proc to dava smysl**
> - Dela z launcheru vstupni portal do eventu.
> - Zlepsuje orientaci v den releasu.
> - Dobre se kombinuje s milestones, spotlightem i navratovym recapem.

### 18A. Discord Identity Bridge

> [!info]
> **Popis**  
> Volitelne propojeni launcher uctu s Discord identitou. Neni to nahrada za Microsoft login do hry, ale community a role vrstva nad launcherem. Uzivatel pak muze mit v launcheru realny social kontext: role, squadu, creator status, event access nebo support historii.
>
> **Mozna implementace**
> - OAuth link v sekci uctu: `Microsoft = herni identita`, `Discord = komunitni identita`.
> - Po propojeni lze tahat avatar, display name, guild membership, role, creator/mod status a napojit je na dashboard, news feed a event panely.
> - Discord login muze odemknout personalizovane karty typu `announcementy pro tvoji roli`, `eventy pro tvoji skupinu`, `creator challenge`, `support kanal pro tvuj problem`, `rychle otevrit spravny channel`.
> - Dobre funguje i pro one-click akce: join oficialni Discord, predvyplneny bug report, RSVP na event, claim creator reward, sync squad badge do launcher profilu.
> - Scope musi byt uzky, transparentni a volitelny. Discord auth nesmi byt podminkou pro samotne hrani.
>
> **Proc to dava smysl**
> - Zmensuje propast mezi launcherem a realnou komunitou.
> - Dava smysl pro onboarding, support, creator ecosystem i event ops.
> - Umi personalizovat obsah bez agresivni gamifikace.
> - Pro projekt s aktivnim Discordem je to mnohem silnejsi use case nez jen dalsi social iconka.

---

## Priorita C - Utility a personalizace

> [!tip]
> Tahle vrstva dela z launcheru chytry osobni dashboard a ne jen hezky seznam tlacitek.

### 19. Adaptive Play Recommendations

> [!tip]
> **Popis**  
> Launcher doporucuje dalsi krok podle stavu hrace. Musi to byt chytre a uzitecne, ne AI spam panel.
>
> **Mozna implementace**
> - Doporuveni vychazi z progresu, delky neaktivity, otevrenych cilu nebo aktivniho obdobi.
> - Panel muze nabidnout 2 az 3 konkretni kroky misto dlouheho seznamu.
> - Kazde doporuceni by melo umet vysvetlit, proc se zobrazuje.
>
> **Proc to dava smysl**
> - Pomaha hraci neztratit smer.
> - Z dashboardu dela osobni rozcestnik.
> - Je to premium feeling bez nutnosti tezke AI vrstvy.

### 20. Personal Goal Board

> [!tip]
> **Popis**  
> Uzivatel si muze pripnout vlastni osobni cile a mit je stale na ocich.
>
> **Mozna implementace**
> - Cile mohou byt rucne vytvorene nebo napojene na systemove milestone.
> - Ke kazdemu cili lze pripsat poznamku, prioritu nebo orientacni deadline.
> - Dashboard muze ukazat jen 3 az 5 aktivnich cilu, aby zustal cisty.
>
> **Proc to dava smysl**
> - Kombinuje systemove ukoly s osobni motivaci.
> - Dela z launcheru osobni dashboard.
> - Funguje i bez velke backendove slozitosti.

### 21. Collections and Sets

> [!tip]
> **Popis**  
> Sberatelska vrstva nad achievementy. Misto jednotlivych checkmarku se buduje pocit, ze hrac sklada cele tematicke sady.
>
> **Mozna implementace**
> - Kolekce mohou byt spojene s discovery, challenge, content typy nebo seasonal badge.
> - Kazda sada ma progres, rarity a pripadny specialni reward za kompletaci.
> - Na dashboardu staci ukazovat par rozdelanych kolekci, ne vsechny najednou.
>
> **Proc to dava smysl**
> - Dava achievementum lepsi strukturu.
> - Podporuje exploration i dlouhodoby completionism.
> - Dobre funguje v seasonal i evergreen vrstve.

### 22. Prestige Reset Without Punishment

> [!tip]
> **Popis**  
> Prestige pro dlouhodobe hrace bez trestu. Nic se nebere zpet, jen se pridava ceremonialni vrstva identity.
>
> **Mozna implementace**
> - Po dokonceni velke mastery cesty se odemkne dalsi uroven badge, frame nebo title.
> - System muze pouzivat vizualni signatury misto hernich vyhod.
> - V timeline se muze zapisovat, kdy hrac dosahl noveho prestige levelu.
>
> **Proc to dava smysl**
> - Dava dlouhodobym hracum dalsi horizont.
> - Nevytvari frustraci z resetu.
> - Drzi se ciste kosmeticke a identitni vrstvy.

### 23. Companion Knowledge Layer

> [!tip]
> **Popis**  
> Pomocna vrstva pro orientaci v obsahu, systemech a problemech. Ne chatbot na homepage, ale chytre umistena pomoc — kontextova, tichá a neinvazivní.
>
> **Mozna implementace**
> - Contextual help cards podle toho, kde se hrac nachazi v flow.
> - Kuratorske odpovedi na caste otazky typu `kde zacit`, `co delat dal`, `proc mi to pada`.
> - Pozdeji muze byt cast odpovedi AI-assisted, ale jen nad kvalitnim kuratorskym zakladem.
>
> **Proc to dava smysl**
> - Snizuje frustraci novacku i navratovych hracu.
> - Zveda praktickou hodnotu launcheru.
> - Je to rozumnejsi use case nez obecny AI chat — Companion je kontextova pomoc, ne volny chatbot.

> [!note]
> **Poznamka k odliseni od AI chatu:**
> Companion Knowledge Layer neni AI chatbot na homepage. Je to kuratovana, kontextova help vrstva. Obecny AI chat bez jasneho use case patri do sekce "co nedelat moc brzo" — Companion je naopak konkretni, cileny a spravny use case.

### 24. Quick Resume / Session Handoff

> [!tip]
> **Popis**  
> Launcher si pamatuje, kde hrac minule skoncil, a nabidne jemne navazani na posledni session.
>
> **Mozna implementace**
> - Muze ukazat posledni hrany profil, posledni cil, posledni aktivitu nebo posledni otevreny task.
> - Pri dalsim otevreni nabidne `pokracovat tam, kde jsi skoncil`.
> - V navratovem rezimu se da spojit s recap kartou a doporucenym dalsim krokem.
>
> **Proc to dava smysl**
> - Zkracuje cestu z launcheru do akce.
> - Pusobi velmi osobne a premiove.
> - Je to lehka feature s dobrym UX dopadem.

### 25. Smart Notification Digest

> [!tip]
> **Popis**  
> Misto spam notifikaci dostane hrac chytry souhrn toho, co je pro nej relevantni.
>
> **Mozna implementace**
> - Digest muze byt denni, tydenni nebo session-based.
> - Obsahuje jen dulezite zmeny: aktivni event, update, skupinovy pohyb, novy spotlight nebo pripomenuti cile.
> - Hrac si muze nastavit typy obsahu a frekvenci.
>
> **Proc to dava smysl**
> - Zveda relevanci notifikaci.
> - Snizuje pocit, ze launcher spammuje.
> - Pomaha udrzet navratovy loop bez tlaku.

### 26. Global Search / CMD+K

> [!tip]
> **Popis**  
> Rychlý univerzální vyhledávač pro celý launcher. Uživatel může jednou klávesovou zkratkou najít cokoliv — profil, event, nastavení, quest, codex entry, skupinu nebo obsah.
>
> **Mozna implementace**
> - Spouštění přes `CMD+K` / `CTRL+K` nebo search ikonu v hlavní navigaci.
> - Výsledky rozdělené do kategorií: Akce, Obsah, Nastavení, Hráči, Eventy.
> - Podpora fuzzy search a rychlých akcí (např. `přejít na poslední session`, `otevřít nastavení grafiky`).
>
> **Proc to dava smysl**
> - Zlatý standard moderních desktop a web aplikací (Linear, Figma, Notion, GitHub).
> - Jakmile launcher roste, orientace bez search je frustrující.
> - Nízká bariéra použití, vysoký comfort dopad pro pokročilé uživatele.

### 27. Dashboard Personalizace

> [!tip]
> **Popis**  
> Uživatel si může přeuspořádat nebo schovat widgety na hlavním dashboardu podle svých preferencí.
>
> **Mozna implementace**
> - Drag & drop pro přeskládání karet a panelů.
> - Toggle pro skrytí widgetů, které uživatel aktivně nepoužívá (např. Creator Quests pro hráče, který netvoří obsah).
> - Launcher si pamatuje rozvržení per-profil nebo globálně.
> - Možnost rychlého resetu na výchozí layout.
>
> **Proc to dava smysl**
> - Standard v top launcherech a platformách (Steam, Epic, Xbox App).
> - Respektuje různé typy uživatelů bez nutnosti tvořit separátní profily.
> - Dává launcheru pocit osobního nástroje, ne generického produktu.

### 27A. Creator Studio Copilot SDK Layer

> [!tip]
> **Popis**  
> Creator Studio dostane task-oriented copilot vrstvu postavenou nad vybranou pracovni instanci. Ne obecny chat pro vsechno, ale asistenta, ktery rozumi modpacku, assetum, changelogu, screenshotum, logum a release workflow. Cilem je z Creator Studia udelat skutecny authoring workspace, ne jen utility panel.
>
> **Mozna implementace**
> - Copilot panel bezi nad `selected instance` kontextem: vidi metadata modpacku, obsah `mods/`, `config/`, screenshoty, crash logy, linked servery a posledni zmeny ve workspace.
> - Pomoci `Copilot SDK` muze asistovat s ukoly typu `vysvetli co se zmenilo mezi dvema buildy`, `navrhni patch summary`, `vytvor draft Discord announcementu`, `zkontroluj release checklist`, `shrni crash log pro support`, `navrhni popis modpacku nebo spotlight card`.
> - Creator Studio muze byt napojene i na GitHub repo modpacku nebo konfigurace: `fetch`, `pull`, prehled poslednich commitu, branch context a signal, ze nekdo pushnul zmeny, ktere si muzes stahnout do pracovni instance.
> - Diky tomu jde delat lehkou kolaboraci bez odchodu z launcheru: otevrit branch/workspace, stahnout update od dalsiho autora, porovnat lokalni zmeny proti repu a navazat na uz rozpracovanou verzi.
> - Silny use case je preklad mezi technickou a obsahovou vrstvou: z lokalnich zmen udela lidsky citelny changelog, Creator Quest submit, guide draft nebo release card pro launcher.
> - Akce by mely byt kuratorovane a bezpecne: navrhy, diff summary, exporty, metadata, validace assetu a checklisty. Ne volne autonomni prepisovani cele instance bez kontroly.
> - Pozdeji lze pridat specializovane rezimy `release copilot`, `support copilot`, `creator post copilot`, `modpack audit`, `repo sync copilot`, kazdy s jasnym scope a prompt kontraktem.
>
> **Proc to dava smysl**
> - Navazuje to primo na realny smer, kde se Creator Studio meni na modpack-authoring workspace.
> - Zkracuje cas mezi `mam rozdelanou instanci` a `mam pripraveny release / post / support summary`.
> - Repo sync vrstva z Creator Studia dela realny collaboration hub, ne solo authoring panel.
> - Je to mnohem presvedcivejsi AI use case nez obecny chat na homepage.
> - Dobre se propojuje s Patch Impact Summary, Version Story, Creator Quests i Spotlight systemem.

### 27B. Export Snapshot Ring a Modlist Diff Archive

> [!tip]
> **Popis**  
> Kazdy export modpacku se ulozi jako porovnatelny snapshot verze, aby slo snadno videt, co se mezi buildy zmenilo. Cilem neni jen `vyrobit zip`, ale budovat malou historii releasu primo v launcheru. Minimalni varianta muze drzet poslednich 5 exportu na instanci.
>
> **Mozna implementace**
> - Pri kazdem exportu se ulozi metadata snapshotu: datum, autor, verze, branch, commit hash, MC verze, loader, mod count, changelog note a otisk exportovaneho modlistu.
> - Launcher drzi `last 5 exports` per-instance, starsi snapshoty rotuje nebo archivuje mimo hot workspace.
> - Nad exporty jde jednim klikem vygenerovat diff: `pridane mody`, `odebrane mody`, `updatnute verze`, `zmenene configy`, `zmenene assety`, `zmena loaderu` nebo `zmena manifestu`.
> - Diff panel muze rovnou generovat lidsky citelny summary pro patch notes, GitHub release body, Discord announcement nebo interni QA check.
> - Dobre funguje i porovnani `working instance vs posledni export`, aby autor pred releasem hned videl, co jeste neni v oficialni verzi.
>
> **Proc to dava smysl**
> - Exporty prestanou byt jednorazovy artifact a stanou se z nich auditovatelne verze.
> - Diff modlistu je jeden z nejpraktictejsich podkladu pro release notes, support i kontrolu regresi.
> - Retence poslednich 5 exportu drzi historii lehkou, ale porad dost bohatou na porovnani.
> - Vyborne to navazuje na Creator Studio, GitHub collaboration i Copilot vrstvu nad changelogy.

### 27C. GitHub Review Inbox a Release Gate

> [!tip]
> **Popis**  
> Creator Studio muze mit vlastni review inbox nad repem a exporty: co je nove na vetvi, co ceká na pull, co jeste nema release note, co nema snapshot diff a co neni pripravene k vydani. Je to operacni panel pro kolaboraci, ne plna nahrada GitHub webu.
>
> **Mozna implementace**
> - Panel ukazuje posledni commity, otevrene release checklisty, rozdil proti `main` nebo `release` vetvi a stav lokalni pracovni instance.
> - Launcher umi zvyraznit `nekdo pushnul update`, `mas lokalni zmeny`, `chybi export snapshot`, `chybi changelog`, `neni hotovy diff`.
> - Jednotlive gate mohou byt jednoduche a prakticke: validni export, ulozeny snapshot, vygenerovany modlist diff, vyplnena poznamka k releasu, volitelne quick smoke-test potvrzeni.
> - Vhodne je i lehke napojeni na Issues / PR odkazy nebo interni task identifikatory, aby autor videl kontext prace bez loveni po zalozkach.
>
> **Proc to dava smysl**
> - Drzi release disciplinu bez zbytecne enterprise slozitosti.
> - Pro maly tym nebo community autory je to velmi silny productivity boost.
> - Spojuje repo workflow, exporty a release komunikaci do jednoho mista.

### 27D. Rollback a Playtest Channels z Export Snapshotu

> [!tip]
> **Popis**  
> Export snapshoty nejsou jen historie, ale i prakticky zdroj pro rychly rollback a testovaci vetve. Autor si muze jednim klikem obnovit starsi verzi, vytvorit `preview` instanci nebo poslat testerum presny build, ke kteremu se da vratit.
>
> **Mozna implementace**
> - Kazdy export lze oznacit jako `stable`, `candidate`, `nightly`, `hotfix` nebo `playtest`.
> - Launcher umi z export snapshotu vytvorit novou testovaci instanci bez zasahu do hlavni pracovni verze.
> - Pri problemu po releasu jde rychle porovnat `aktualni vs posledni stable` a pripadne udelat rollback na posledni dobry export.
> - Support a QA muze pracovat nad presnym export ID / hash, takze bugy nejsou navazane jen na vagne `nejaka starsi verze`.
>
> **Proc to dava smysl**
> - Vyrazne to snizuje riziko pri castejsich updatech a spolupraci vice autoru.
> - Usnadnuje playtest workflow bez rucniho kopirovani instanci.
> - Z Creator Studia dela mnohem dospelejsi release workspace.

---

## Priorita D - Experimental premium ideas

> [!warning]
> Tohle jsou odvaznejsi smery. Nemusi prijit brzo, ale davaji premium vizi a smer do budoucna.

### 28. Live Activity View

> [!warning]
> **Popis**  
> Zivy prehled toho, co se prave deje. Nemusi to byt nutne mapa, muze to byt i chytry activity layer.
>
> **Mozna implementace**
> - View muze ukazovat event zony, aktivni highlights, popularni obsah, skupinove aktivity nebo featured body.
> - Lze prepinat vrstvy podle typu obsahu.
> - Pro prvni verzi staci kuratorovany nebo polo-dynamicky prehled.
>
> **Proc to dava smysl**
> - Silne propojuje launcher s realnou aktivitou.
> - Umi vytvorit wow efekt bez zbytecneho chaosu.
> - Dava dashboardu pocit "live produktu".

### 29. User Showcase Pipeline

> [!warning]
> **Popis**  
> System pro submit a vystaveni nejlepsiho user-generated obsahu primo v launcheru.
>
> **Mozna implementace**
> - Uzivatel nebo moderator prida showcase s obrazkem, kratkym popisem, autorem a tagy.
> - Homepage nebo spotlight blok ukazuje carousel vybranych prispevku.
> - Pozdeji lze pridat submit flow s moderaci a kuratorskym schvalenim.
>
> **Proc to dava smysl**
> - Podporuje kreativitu a komunitni tvorbu.
> - Dava launcheru editorial kvalitu.
> - Funguje i jako marketingovy material pro projekt.

### 30. Mentor / Group Finder Layer

> [!warning]
> **Popis**  
> Dobrovolna social vrstva pro novacky, partaky a hledani lidi na spolecnou hru nebo obsah.
>
> **Mozna implementace**
> - Hrac si muze zvolit `hraju solo`, `hledam partu`, `chci pomoct` nebo `muzu pomahat`.
> - Podle toho launcher ukaze relevantni cards, tips a social flow.
> - Pozdeji lze pridat mentor program nebo lehkou reputacni vrstvu.
>
> **Proc to dava smysl**
> - Pomaha novackum najit sve misto.
> - Posiluje social onboarding.
> - Muze snizit odpad hracu po prvnich dnech.

### 31. Progress Route Composer

> [!warning]
> **Popis**  
> Pokrocila vrstva, ktera sklada doporucenou cestu produktem nebo obsahem pro ruzne typy hracu.
>
> **Mozna implementace**
> - Route typy: novacek, navratovy hrac, builder, grinder, creator, low-end setup.
> - Trasy mohou byt kuratorovane jako serie kroku, ne nutne AI generovane.
> - Route muze kombinovat challenge, help cards a doporucene nastaveni.
>
> **Proc to dava smysl**
> - Ve velkem mnozstvi obsahu obrovsky pomaha s orientaci.
> - Zveda pocit, ze launcher opravdu rozumi potrebam hrace.
> - Je to silne i bez komplikovaneho backendu.

### 32. Narrative Capsules

> [!warning]
> **Popis**  
> Atmosfericke kapsle pro sezony, release nebo velke eventy. Ne jen seznam zmen, ale vstupni editorial do nove etapy.
>
> **Mozna implementace**
> - Kapsle muze obsahovat intro text, vizualni motiv, hlavni novinky a doporucene fokus body.
> - Zobrazi se pri prvnim otevreni po velkem release nebo jako hero panel na homepage.
> - Pozdeji muze mit i audio, motion nebo mini gallery vrstvu.
>
> **Proc to dava smysl**
> - Dela z launcheru skutecny portal do noveho obdobi.
> - Zveda emotional value releasu.
> - Pomaha sjednotit vizualni smer a storytelling.

### 33. Discovery Radar

> [!warning]
> **Popis**  
> Objevovaci feed, ktery ukazuje zajimavy obsah relevantni pro konkretniho hrace.
>
> **Mozna implementace**
> - Feed muze kombinovat nove discovery entry, spotlight prispevky, event content a personalizovane hinty.
> - Relevance muze byt jednoducha: podle stylu hrani, otevrenych cilu nebo predchozi aktivity.
> - Pro prvni verzi staci kuratorovany radar s lehkou personalizaci.
>
> **Proc to dava smysl**
> - Podporuje exploration bez hard push notifikaci.
> - Dava launcheru obsahovou hloubku.
> - Pomaha hraci objevovat veci, ktere by jinak minul.

---

## Priorita E - Infrastruktura a kvalita

> [!note]
> Tyhle veci nejsou viditelné uživateli jako feature — ale přímo určují, jestli launcher působí jako solidní produkt nebo jako beta. Musí být řešeny průběžně, ne jako afterthought.

### 34. Accessibility (a11y)

> [!note]
> **Popis**  
> Přístupnost jako first-class citizen, ne bonus. Launcher musí fungovat pro uživatele s různými potřebami.
>
> **Mozna implementace**
> - WCAG 2.1 AA jako baseline standard pro kontrast, focus states a čitelnost.
> - Color blind mody (deuteranopia, protanopia, tritanopia) pro všechny barevně kódované UI prvky.
> - Keyboard navigation pro celý launcher bez nutnosti myši.
> - Font scaling respektující systémové nastavení.
> - Screen reader kompatibilita pro klíčové flow (onboarding, hlavní dashboard, nastavení).
>
> **Proc to dava smysl**
> - U globálního launcheru je a11y regulatorní i etický standard.
> - Špatná přístupnost vylučuje část uživatelské základny bez důvodu.
> - Keyboard navigation a dobrý kontrast zlepšují UX pro všechny, nejen pro uživatele s potřebami.

### 35. Offline / Degraded State Design

> [!note]
> **Popis**  
> Launcher musí mít jasně definované chování pro každý stav připojení — plné offline, částečné připojení, server error.
>
> **Mozna implementace**
> - Každý live data panel (Community Milestones, Social Proof Cards, Activity View) musí mít explicitně navržený offline fallback.
> - Launcher musí umožnit spuštění hry i bez připojení, pokud to logika produktu dovolí.
> - Chybové stavy musí být lidsky vysvětlené, ne technické error kódy.
> - Skeleton loading states pro všechny dynamické panely — žádné bílé prázdné plochy.
>
> **Proc to dava smysl**
> - Spousta launcherů padá na hubu právě tady.
> - Dobře navržené error stavy jsou součástí premium UX, ne jen technický detail.
> - Uživatel, který se nemůže připojit, nesmí dostat broken UI.

### 36. Lokalizace / i18n Pipeline

> [!note]
> **Popis**  
> Globální launcher potřebuje lokalizaci jako součást architektury, ne jako pozdější patch.
>
> **Mozna implementace**
> - Všechny UI stringy procházejí lokalizační vrstvou od prvního dne — žádné hardcoded texty.
> - Podpora RTL jazyků (arabština, hebrejština) v základním layout systému.
> - Date, time a number formatting podle locale uživatele.
> - Lokalizační pipeline s podporou community překladů nebo profesionálních překladatelů.
>
> **Proc to dava smysl**
> - I/18n přidaný dodatečně je drahý a bolestivý technický dluh.
> - Globální launcher bez lokalizace vylučuje velké části světové uživatelské základny.
> - Community překlady mohou být silný engagement nástroj sám o sobě.

### 37. Privacy Controls a Data Transparency

> [!note]
> **Popis**  
> Uživatel musí vědět, co launcher sbírá, a mít možnost to ovlivnit.
>
> **Mozna implementace**
> - Dedikovaná sekce "Co o mně launcher ví" — přehled sbíraných dat srozumitelnou češtinou/angličtinou, ne právním jazykem.
> - Granulární opt-out z telemetrie, analytiky a personalizace.
> - GDPR compliance jako baseline pro EU uživatele; CCPA pro US.
> - Možnost exportu nebo smazání vlastních dat.
>
> **Proc to dava smysl**
> - GDPR je regulatorní požadavek pro EU trh, ne optional feature.
> - Transparentnost buduje důvěru a snižuje support load.
> - Uživatelé dnes privacy controls aktivně hledají a očekávají.

### 38. Motion Language a Design System

> [!note]
> **Popis**  
> Konzistentní motion systém a design language pro celý launcher — ne "víc efektů", ale jasně definovaný charakter pohybu a vizuálního feedback.
>
> **Mozna implementace**
> - Definovaná sada transition typů: entrance animace, exit, micro-feedback (button press, toggle, loading).
> - Pohyb musí respektovat `prefers-reduced-motion` systémové nastavení.
> - Design tokens pro barvy, spacing, typografii a animační timing — jeden zdroj pravdy.
> - Skeleton loading states jako součást design systému, ne jako ad-hoc řešení per-panel.
> - Error a empty states jako first-class součást komponent knihovny.
>
> **Proc to dava smysl**
> - Bez systému se motion stane chaotickým a produkt působí nekonzistentně.
> - Design tokens umožňují theming, seasonal skin nebo dark/light mode bez přepisování komponent.
> - Reduced motion podpora je zároveň accessibility požadavek.

---

## Top set pro nejvetsi dopad

> [!success]
> Pokud bych mel z celeho seznamu vybrat nejsilnejsi kombinaci pro identitu, retenci a odliseni launcheru, vzal bych:
>
> 1. Seasonal Journey Track
> 2. Chronicle Timeline
> 3. Smart Onboarding Missions
> 4. Memory Lane / Return Recap
> 5. Discovery Codex
> 6. Patch Impact Summary
> 7. Community Milestones
> 8. Companion Knowledge Layer
> 9. Global Search / CMD+K
> 10. Offline & Error State Design
> 11. Motion Language a Design System

Tohle dohromady vytvari:

- jasny duvod launcher otevirat opakovane
- silnejsi pocit identity a smeru
- lepsi orientaci pro nove i navratove hrace
- mix emotional a prakticke hodnoty
- solidni technicky zaklad, ktery drzi spolu pri rustu produktu

---

## Co nedelat prilis brzy

> [!warning]
> Tyhle veci mohou byt cool, ale maji horsi pomer hodnota vs. slozitost, pokud prijdou prilis brzy.

- komplexni economy meta jen pro launcher
- prehnane agresivni daily streaky
- spammy social rankingy bez kontextu
- obecny AI chat na homepage bez jasneho use case (≠ Companion Knowledge Layer, ktery je kontextovy a kuratorovany)
- velke mnozstvi kosmetickych unlocku bez silneho jadra produktu
- plne automaticke doporucovaci systemy bez kvalitnich dat a kuratorske vrstvy
- live data panely bez dobre navrženych offline a error stavu

---

## Doporuceny backlog format

> [!example]
> Pokud budeme chtit tenhle dokument pozdeji prevest na backlog, doporucuju pridavat ke kazde feature tyhle tagy:
>
> - `Impact: High / Medium / Low`
> - `Complexity: High / Medium / Low`
> - `Project-specific: Yes / Partial / No`
> - `Needs backend: Yes / No`
> - `Needs live data: Yes / No`
> - `Needs moderation: Yes / No`
> - `Seasonal candidate: Yes / No`
> - `Accessibility dependency: Yes / No`
> - `Needs i18n: Yes / No`

Prakticky zapis pak muze vypadat takhle:

```md
### Nazev feature

> [!example]
> **Popis**
> Jedna az dve vety, co je pointa feature.
>
> **Mozna implementace**
> - Konkretni varianta A
> - Konkretni varianta B
>
> **Proc to dava smysl**
> - Hlavni produktova hodnota
> - Hlavni UX nebo community hodnota
```

---

## Zaver

Pokud ma byt launcher fakt top, musi jit za hranici:

- instalace
- update
- seznam profilu, modu nebo serveru
- tlacitko `Hrat`

> [!quote]
> Nejsilnejsi smer neni `vic efektu`.
> Nejsilnejsi smer je udelat z launcheru zivy portal do celeho herniho ekosystemu,
> propojit realny progres s premium desktop UX
> a dat hraci pocit identity, smeru a navaznosti.

To je presne ten rozdil mezi hezkym launcherem a platformou, ktera ma vlastni charakter.

> [!note]
> **Technicka vrstva je stejne dulezita jako feature vrstva.**
> Launcher s 30 featurami a spatnym motion systemem, rozbitym offline stavem nebo bez a11y pusobi jako beta.
> Launcher s 10 featurami a solidnim technickym zakladem pusobi jako produkt.