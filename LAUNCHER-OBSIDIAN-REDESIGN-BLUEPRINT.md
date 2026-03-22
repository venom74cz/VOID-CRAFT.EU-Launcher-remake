# VOID-CRAFT Launcher - Obsidian Redesign Blueprint

> [!IMPORTANT]
> Tenhle soubor je zaroven:
> - produktovy a UX blueprint
> - architekturni smer pro redesign launcheru
> - zivy realizacni tracker, ktery budeme postupne odskrtavat

## Rychla orientace

### Legenda stavu

- 🟩 hotovo
- 🟨 rozpracovano
- ⬜ nehotovo
- ⛔ blokovano nebo odlozeno

### Jak tenhle dokument cist

- casti nahore vysvetluji vizualni a produktovy smer
- stred dokumentu mapuje featury do konkretnich UI ploch
- konec dokumentu obsahuje jasny checklist implementace, ktery budeme prubezne aktualizovat

### Doporucene poradi cteni

1. Cil a aktualni stav
2. Nova produktova vize
3. Hlavni obrazovky a design system
4. Explicitni coverage featur
5. Realizacni tracker na konci dokumentu

## Cil

Navrhnout novy launcher UI od nuly tak, aby vizualne odpovidal nebo prekonaval web VOID-CRAFT.EU, ale pritom zustal realne implementovatelny nad existujici Avalonia architekturou.

Tenhle dokument je postaveny na realnem cteni projektu `VoidCraftLauncher`, ne na odhadu.

## Co je v projektu ted

Po precteni launcheru je aktualni stav tento:

- `src/App.axaml` uz ma cast obsidian palety, ale funguje spis jako jeden velky inline style sheet nez jako skutecny design system.
- `src/Views/MainWindow.axaml` je jedno monoliticke okno s velkym mnozstvim layoutu, overlayu, modalu a view switchingu v jednom souboru.
- `src/Themes/` je prazdne.
- `src/Controls/` je prazdne.
- `MainViewModel` je uz rozdelen do partial souboru, takze business logika je zachranitelna a neni nutne ji cele zahodit.
- Soucasny layout je porad dost "Modrinth desktop clone" a ne vlastni premium launcher experience pro VOID-CRAFT.

Z toho plyne jedna dulezita vec:

**Nedelat dalsi redesign jako dalsi vrstvu uvnitr soucasneho `MainWindow.axaml`.**

Pokud to udelame timhle zpusobem, vznikne dalsi vizualni vrstva nad starym layoutem a projekt se bude hure rozsirovat o skin manager, achievementy, toast system, crash center a dalsi featury.

## Nova produktova vize

Nova verze launcheru nebude vypadat jako seznam karet s tlacitkem Play. Bude to pusobit jako:

- command center pro vstup do sveta VOID-CRAFT
- premium desktop aplikace s vlastni identitou
- zivy panel nad serverem, modpackem, profilem a progressem
- platforma pripravena na dalsi featury, ne jen prebarveny launcher

Navrhovany stylovy nazev smeru:

**Obsidian Command Deck**

To znamena:

- ultra dark zaklad
- vrstveny glassmorphism bez lacinosti
- silne purple pulse akcenty
- crystal teal pouze jako sekundarni signal
- obsah jako dominantni vrstva, ne dekorace
- animace s vahou a smerem, ne random efekty

## Hlavni UX principy

### 1. Launcher musi byt zazitek, ne formular

Uzivatel po otevreni musi okamzite citit:

- co je jeho hlavni modpack
- jestli je server online
- na jaky server muze okamzite naskocit
- co je noveho
- jak rychle hrat
- kde resit profil, skin, achievementy a nastaveni

### 2. Jedno dominantni centrum pozornosti

Kazdy screen musi mit jednu hlavni akci:

- Dashboard: Hrat / pokracovat
- Discover: Prozkoumat a nainstalovat
- Instance detail: Spravovat konkretni pack
- Settings: Udelat systemove zmeny
- Skin studio: Ulozit a nahrat skin

### 3. Vedlejsi informace musi zit, ale nerusit

Server status, changelog, account stav, toast notifikace a launch progres maji byt porad dostupne, ale nesmi prehlusit hlavni flow.

### 4. Kazda nova feature musi mit predem misto v layoutu

Design se musi pripravit uz ted pro:

- toast system
- crash reporter
- backup prompt
- skin manager
- achievementy
- live overlay generator
- websocket server status
- server quick connect a server bindings
- Discord announcements a YouTube content feed
- theme engine
- lokalizaci

## Navrh nove informacni architektury

## Shell layout

Novy launcher doporucuju postavit jako 5vrstvy shell:

### Vrstva 1 - Navigation Rail

Uzky levy rail, ale bohatsi nez dnes:

- logo VOID-CRAFT nahoře
- hlavni navigace: Dashboard, Discover, Instances, Social, Settings
- utility tlacitka dole: update, logs, account, theme
- aktivni stav reseny glow linkou a jemnym podsvicenim celeho slotu

### Vrstva 2 - Command Header

Horni horizontalni command deck pres hlavni obsah:

- velky nazev aktualni sekce
- contextual subtitle
- quick actions podle view
- live server pill
- global search / command input
- session status: prihlasen, stahuje se, hra bezi, update dostupny

To je rozdil proti dnesku: header nebude jen title bar, ale ridici centrum cele aplikace.

### Vrstva 3 - Adaptive Content Canvas

Stredni hlavni plocha, ktera meni strukturu podle view:

- Dashboard: hero + content blocks
- Discover: search + editorial results grid
- Instance detail: workspace tabs + sticky actions
- Settings: sections + lab cards

### Vrstva 4 - Context Dock

Pravy panel zustane, ale zmeni funkci. Nebude to "nahodne info vedle". Bude to zivy kontextovy dock:

- kdyz je vybrana instance, ukazuje instance telemetry
- kdyz nic nehraje, ukazuje server/community/changelog
- kdyz je aktivni server binding, ukazuje quick connect kartu s navazanym modpackem
- kdyz je dostupny content feed, ukazuje posledni Discord announcementy a media highlights
- kdyz bezi instalace, dock se prepne na live job console
- kdyz dojde crash, dock se zmeni na diagnostics panel

### Vrstva 5 - Overlay Layer

Sem patri vsechny premium interakce:

- toasty
- modal sheets
- command palette
- backup prompt
- crash report drawer
- login flow
- create profile wizard

## Navrh hlavnich obrazovek

## 1. Dashboard

Tohle ma byt novy domov launcheru. Ne seznam karet, ale hlavni command page.

Struktura:

- Hero block s VOID-BOX2 jako dominantnim packem
- Primarni CTA `Hrat`
- Sekundarni CTA `Pokračovat v konfiguraci`, `Otevrit slozku`, `Zkontrolovat update`
- Live status chipy: verze packu, stav serveru, pocet hracu, uptime, posledni update
- Pod hero sekce:
  - `Moje instance`
  - `Novinky a media`
  - `Doporucene / nove`
  - `Posledni aktivita`
  - `Novinky z launcheru`

Vizuální smer:

- velke cinematic pozadi s rozostrenym artworkem packu
- foreground glass card s hlubokym purple glow
- progress stavy integrovane primo do hero bloku, ne jako maly overlay dole na karticce

Sekce `Novinky a media` ma byt jeden z highlight bloku Dashboardu:

- posledni Discord announcementy
- posledni YouTube videa
- volitelne YouTube posty nebo community updates, pokud budou dostupne pres backend nebo sdileny feed
- vizualne jako editorial cards, ne obycejny textovy list

## 2. Discover

Nechci klasicky list. Chci "content browser".

Struktura:

- editorial search header
- source switcher jako segment control
- featured strip nahore
- adaptivni grid s kartami, ktere maji:
  - artwork
  - jmeno a autor
  - badges
  - quality score / popularita / update freshness
  - hover actions

Kazda karta ma mit 3 stavy:

- idle
- hover preview
- install progress

Pri hoveru:

- jemny camera lift
- reveal metadat
- mikropohyb overlaye
- install CTA se rozsviti jako aktivni call to action

## 3. Instance Detail Workspace

Misto soucasneho klasickeho tab view navrhuju workspace styl:

- sticky header s pack identitou
- vlevo detail a akce
- vpravo health/status panel
- obsah v sekcich nebo tabs podle modu

Sekce:

- Overview
- Content
- Gallery
- Performance
- Saves and Backups
- Advanced

Tohle je idealni misto pro backup prompt, crash history, options presets, potato mode a pozdeji export/import.

## 4. Skin Studio

Skin manager nesmi byt jen modal s upload buttonem. Ma to byt plnohodnotna studio obrazovka nebo velky sheet.

Rozlozeni:

- vlevo velky preview panel
- uprostred knihovna skinu
- vpravo properties panel

Obsah:

- 2D preview front/back
- pozdeji priprava na pseudo 3D preview
- classic/slim toggle
- import z URL, file, current account
- upload na Mojang
- cape selector

Designovy detail:

- preview panel muze mit pomaly floating glow grid v pozadi
- selected skin tile musi mit premium gallery feel, ne default list selection

## 5. Achievement Hub

Achievementy si zaslouzi vlastni emotional layer. Nemaji byt jen seznam odznaku.

Struktura:

- top summary: odemceno X z Y
- progress ring / tier ladder
- grid achievement cards s raritou
- unlock history timeline

Odemcene karty:

- lehky glow podle tieru
- datum unlocku
- reward feeling

Zamcene karty:

- frost glass styl
- hint text
- progress indikace tam, kde jde merit

## 6. Settings Lab

Nastaveni musi pusobit jako technicky cockpit, ne formular.

Rozdeleni:

- Performance
- Java and Runtime
- Interface
- Accounts
- Data and Storage
- Experimental

Kazda sekce jako velka card cluster kompozice, ne prosty stack kontrol.

## 7. Server Hub a Quick Connect

Tohle je nova feature vrstva nad klasickym seznamem serveru. Nema to byt jen ulozeny seznam IP adres.

Cil:

- uzivatel vidi servery, ktere chce hrat
- kazdy server muze byt navazany na konkretni modpack
- klik na `Hrat server` spusti spravny modpack a po startu hned pripoji hrace na server
- hlavni server VOID-CRAFT bude hardcoded, pripnuty a nesmazatelny

Navrhovana datova struktura:

- `Name`
- `Host`
- `Port`
- `IconUrl` nebo lokalni ikonka
- `LinkedModpackId` nebo `LinkedModpackName`
- `IsHardcoded`
- `IsFavorite`
- `LastPlayedAt`
- `AutoJoinMode`

Produktove pravidla:

- VOID-CRAFT server je vzdy prvni, hardcoded a zvyrazneny
- custom servery si uzivatel muze pridavat, upravovat a mazat
- pokud server nema navazany modpack, launcher nabidne jeho vyber
- pokud navazany modpack neni nainstalovany, launcher nabidne instalaci nebo zmenu vazby
- pokud je modpack nainstalovany, tlacitko `Hrat` provede launch + auto-connect flow

UI umisteni:

- Dashboard: `Server Quick Connect` sekce hned pod hlavnim hero blokem nebo vedle nej
- Context Dock: kompaktni karta s hardcoded VOID-CRAFT serverem a dalsimi pripnutymi servery
- Instance Workspace: nova sekce nebo tab `Servers`, kde pujde spravovat vazby server -> modpack
- Settings Lab: globalni sprava server listu a auto-join preferenci

Interakcni model:

- primarni karta VOID-CRAFT: online stav, pocet hracu, verze, linked modpack, tlacitko `Hrat na serveru`
- custom server karta: status, linked modpack badge, edit menu, tlacitko `Hrat`
- pri kliknuti na `Hrat`:
  - overit linked modpack
  - spustit nebo aktualizovat linked modpack
  - po startu hry predat server connect instrukci

Technicka poznamka:

- preferovana implementace je pres launch argument nebo quick-play mechanismus tam, kde to konkretni Minecraft/mod loader verze podpori
- fallback je vlastni kompatibilni mechanika pro starsi instance, aby chovani bylo jednotne i u starsich packu
- feature musi byt navrzena tak, aby hardcoded VOID-CRAFT flow fungoval jako first-class path, ne jen jako dalsi custom server

## 8. News and Media Feed

Tohle ma byt launcherova verze toho, co mate na webu jako novinky a social content, ale podane premium desktop formou.

Cil:

- dostat do launcheru zive novinky bez otevirani webu
- ukazat Discord announcementy jako hlavni community signal
- ukazat YouTube videa a dalsi obsah v editorial kartach
- pouzit stejne nebo kompatibilni zdroje dat jako web, aby se obsah neduplikoval rucne

Overeny existujici zdroje z webu:

- Discord channel messages endpoint: `/api/discord/channel/{CHANNEL_ID}/messages`
- YouTube video feed: YouTube RSS kanal prevedeny do citelneho feedu
- YouTube posty nebo community posty: zatim brat jako volitelnou vrstvu, idealne az pres backend-normalized feed

Produktove pravidlo:

- launcher ma primarne tahat stejne zdroje jako web
- Discord announcementy jsou priorita
- YouTube videa jsou druha vrstva feedu
- YouTube posty jen pokud bude stabilni backend endpoint nebo jiny spolehlivy zdroj

UI umisteni:

- Dashboard: hlavni blok `Novinky a media`
- Context Dock: kompaktni highlights s 1 az 3 poslednimi polozkami
- volitelne detailni `News Hub` sheet nebo view, pokud bude obsah bohaty

Typy karet:

- `Discord Announcement Card`
  - avatar autora nebo icona kanalu
  - title nebo vytazeny headline
  - cas publikace
  - kratky excerpt
  - badge typu oznameni

- `YouTube Video Card`
  - thumbnail
  - title
  - published at
  - CTA `Prehrat` nebo `Otevrit`

- `Community Post Card`
  - mensi editorial card s textovym excerptem
  - pouze pokud bude spolehlive podporena v API vrstve

Interakcni model:

- klik na Discord announcement otevre detail nebo web/Discord link
- klik na video otevre video v browseru
- feed se nacita asynchronne po startu launcheru
- pri chybe se content blok schova do fallback state misto hlasite chyby v UI

Technicka poznamka:

- Discord announcementy lze navazat na existujici endpoint, ktery uz pouziva web
- YouTube videa lze sdilet pres stejny feed jako web
- YouTube posty jsou nejistota, takze je lepsi je v trackeru vest jako `optional if stable source exists`

## Design system

## Barevny system

Zakladni smer zustava obsidian, ale je potreba jej formalizovat do tokenu.

### Core tokeny

- `BgCanvas`: `#07070b`
- `BgShell`: `#0d0d14`
- `BgPanel`: `#141420`
- `BgElevated`: `#1b1b29`
- `BgInteractive`: `#242437`
- `StrokeSoft`: `#2d2d42`
- `StrokeStrong`: `#3a3a55`
- `TextPrimary`: `#f5f7ff`
- `TextSecondary`: `#bfc4e6`
- `TextMuted`: `#8086ab`
- `PrimaryA`: `#5b4ecc`
- `PrimaryB`: `#7c6fff`
- `PrimaryGlow`: `#a89cff`
- `AccentTeal`: `#00d4aa`
- `Success`: `#4bffb0`
- `Warning`: `#ffb347`
- `Danger`: `#ff4b6b`

### Preview paleta

> [!NOTE]
> Tohle je zjednodusena barevna reference pro rychly preview v Markdownu. Pokud renderer nepodpori inline styly, porad zustavaji citelne hex hodnoty.

| Token | Preview | Hex | Pouziti |
| --- | --- | --- | --- |
| BgCanvas | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#07070b;border:1px solid #3a3a55;"></span> | `#07070b` | hlavni canvas |
| BgPanel | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#141420;border:1px solid #3a3a55;"></span> | `#141420` | hlavni panely |
| PrimaryA | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#5b4ecc;border:1px solid #a89cff;"></span> | `#5b4ecc` | zacatek gradientu |
| PrimaryB | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#7c6fff;border:1px solid #a89cff;"></span> | `#7c6fff` | hlavni CTA |
| AccentTeal | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#00d4aa;border:1px solid #bfc4e6;"></span> | `#00d4aa` | sekundarni akce |
| Warning | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#ffb347;border:1px solid #3a3a55;"></span> | `#ffb347` | upozorneni |
| Danger | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#ff4b6b;border:1px solid #3a3a55;"></span> | `#ff4b6b` | chyby, destruktivni akce |
| Success | <span style="display:inline-block;width:18px;height:18px;border-radius:999px;background:#4bffb0;border:1px solid #3a3a55;"></span> | `#4bffb0` | uspech |

### Material pravidla

- hlavni panely: temer nepruhledne
- elevated panely: jemna translucence a highlight na hrane
- overlaye: heavy blur + dark veil
- aktivni CTA: gradient, ne flat fill

## Typografie

Pro Avalonia zustat u Inter, ale pracovat s ni ambiciozneji:

- Display: 28-36 px, ExtraBold
- Section title: 18-22 px, Bold
- Card title: 14-16 px, SemiBold/Bold
- Meta text: 11-12 px
- Micro labels: 10 px, tracking navic

Web-grade dojem nevznika jen fontem, ale rytmem, velikostmi a kontrastem.

## Radius a spacing

- shell cards: 20 px
- medium cards: 16 px
- controls: 12 px
- chips: 999 px pill
- primary spacing scale: 4 / 8 / 12 / 16 / 24 / 32 / 48

## Ikonografie

Emoji v produkcnim redesignu odstranit z primarni navigace a klicovych CTA.

Doporuceni:

- prejit na konzistentni icon set jako Fluent Symbols nebo Lucide asset export
- emoji nechat maximalne pro sekundarni playful detail v achievementech nebo news

## Motion system

Animace musi byt system, ne nahodny doplnek.

### Zakladni pravidla motion

- hover: 120-180 ms
- panel transition: 220-280 ms
- modal/sheet reveal: 280-360 ms
- launch/install progress emphasis: opakovany pulz nebo flowing gradient

### Kde animace opravdu pouzit

- boot launcheru: stagger reveal shellu
- prepinani view: slide + fade, ne instant skok
- hero card: ambient pulse na glow vrstve
- live server status: jemna breathing animace
- install state: proudici progress beam
- toasty: slide-in z horniho rohu s jemnym fade-out

### Kde animace neprehanet

- comboboxy
- formulare
- dlouhe listy vysledku
- vse, co by zpomalovalo launcher

## System quality gates

Tohle je cast, ktera rozhoduje, jestli bude launcher opravdu pusobit jako technologicky top produkt a ne jen jako hezky vizual.

### 1. Data aggregation a content contracts

Nejvetsi logicke riziko je tahat ruzne feedy kazdy jinym zpusobem primo z launcheru.

Pravidlo:

- launcher nema byt klient peti ruznych social API formatu
- idealni stav je jeden backend-normalized content endpoint pro launcher i web
- Discord, YouTube videa a pripadne dalsi feedy maji mit sjednoceny datovy kontrakt

Minimalni cil:

- `ContentFeedItem`
  - `Id`
  - `Source`
  - `Type`
  - `Title`
  - `Excerpt`
  - `ImageUrl`
  - `PublishedAt`
  - `Author`
  - `TargetUrl`
  - `Priority`

Proc je to dulezite:

- web i launcher pak sdili stejny obsahovy model
- zmensi se riziko rozbiti pri zmene externich feedu
- UI vrstva zustane cista a nebude resit parsing ruznych zdroju

### 2. Offline-first a cache strategie

Launcher neni jen online dashboard. Musi fungovat dobre i pri pomalem nebo zadnem internetu.

Pravidlo:

- content feed, server status i social bloky musi mit cache a fallback state
- launcher se nesmi jevit jako rozbity jen proto, ze feed endpoint kratce neodpovida

Minimalni UX pravidla:

- pri startu ukazat posledni uspesne nacteny obsah z cache
- soucasne na pozadi zkusit refresh
- pri chybe zobrazit stale hezky fallback state
- pri offline rezimu preferovat `stale while revalidate` chovani

### 3. Accessibility a ovladani

Pokud to ma byt opravdu top, nesmi to byt jen o glow efektech.

Pravidlo:

- vsechna hlavni flow musi jit ovladat klavesnici
- focus states musi byt vizualne silne a konzistentni
- kontrast textu a akcnich prvku musi byt bezpecny i na tmavem pozadi
- reduced motion rezim musi umet omezit nepodstatne animace
- hit targety maji byt dost velke i pro desktop rychle klikani

Minimalni standard:

- zretelny focus ring
- minimalni aktivni plocha 40x40
- reduced motion feature flag
- neukryvat dulezite informace pouze do hover stavu

### 4. Performance budget

Design muze byt bohaty, ale nesmi z launcheru udelat tezky browser-like produkt.

Pravidlo:

- shell musi byt citelny a stabilni do 1 az 2 sekund od startu na beznem PC
- tezke karty, obrazky a feedy se nacitaji az po vykresleni zakladniho shellu
- animace a glow efekty nesmi zhorsovat scroll a hover plynulost

Prakticke dopady:

- lazy load obrazku a media thumbnailek
- skeletony misto skakani layoutu
- max pocet soucasne aktivnich ambient animaci
- feed bloky nacitat asynchronne mimo kritickou startup cestu

### 5. Server auto-connect compatibility gate

Tohle je druhe nejvetsi logicke riziko po feedech.

Pravidlo:

- `Play server` flow se nesmi brat jako samozrejmost pro vsechny packy bez overeni kompatibility
- musi existovat jasna strategie pro moderni verze, starsi verze a mod loader special cases

Nutne stavy flow:

- linked modpack exists
- linked modpack installed
- linked modpack launching
- launch success
- auto-connect injected
- auto-connect unsupported fallback

UX pravidlo:

- pokud auto-connect nejde spolehlive, launcher to rekne otevrene a nabidne fallback, ne tichy fail

### 6. Observability a diagnostika

Top produkt potrebuje i top diagnostiku.

Pravidlo:

- social feed loading, server binding launch flow, crash reporting i update flow musi byt logovany strukturovane
- diagnostika ma umet rict, jestli problem vznikl v UI, feed endpointu, launcher service nebo game launchi

Tohle je dulezite hlavne pro:

- Discord a YouTube feed
- server quick connect
- auto-connect po launchi
- WebSocket fallbacky

## Featury z roadmapy a kde budou zit v UI

## Toast system

Umisteni:

- pravy horni overlay stack

Chovani:

- success, warning, error, info
- hover pause
- click pro detail nebo dismiss

## Crash reporter

Umisteni:

- po padu se neotvira jen modal
- prijde toast a zaroven diagnostics drawer zprava

Drawer obsah:

- kratka diagnoza
- mclo.gs URL
- posledni crash patterns
- rychle akce: kopirovat, otevrit log, zkusit backup, restartovat

## Backup prompt

Nemel by to byt obycejny messagebox.

Navrh:

- middle sheet pred update flow
- jasne ukaze co zalohuje
- ukaze cilovou cestu
- ma 3 akce: zalohovat a pokracovat, preskocit, zrusit

## Skin manager

Patri do global utility vrstvy, ale vstup do nej bude i z account docku.

## Achievementy

Patri jako samostatna hlavni sekce v navigation railu.

## Overlay generator

Patri do `Creator Tools` sekce nebo do Settings Lab pod kategorii `Streaming`.

## Server status

Navenek se projevi v live chipu v command headeru a v context docku.

Implementace je polling-first, s cache a fallback signalizaci. Dokument uz neslibuje neexistujici WebSocket vrstvu, dokud pro ni nebude realny kodovy zaklad.

## Server quick connect a server bindings

Patri do Dashboardu, Context Docku a do instance-level spravy vazeb.

Pokryva:

- hardcoded VOID-CRAFT server
- vlastni seznam serveru uzivatele
- vazbu serveru na konkretni modpack
- launch + auto-connect flow po kliknuti na `Hrat`

## Discord announcements a YouTube content feed

Patri do Dashboardu a Context Docku jako `Novinky a media`.

Pokryva:

- Discord oznameni tahana z API
- YouTube videa tahana ze sdileneho feedu jako na webu
- volitelne YouTube posty, pokud bude stabilni zdroj dat
- editorial prezentaci obsahu ve stylu launcheru, ne obycejny list

## Explicitni coverage vsech schvalenych featur

Ano, po tomhle doplneni je v dokumentu pokryta kazda schvalena feature z `implementation_plan.md` i `implementation_plan2.md`.

### Core improvements

- Paralelni stahovani modu: patri do install pipeline a live job console v context docku. Neni to vizualni feature, ale blueprint s ni pocita jako soucast `Performance Foundation` vrstvy a progress UX.
- Toast system: pokryto jako `ToastHost` v overlay layeru.
- Crash reporter: pokryto jako diagnostics drawer + toast flow po padu hry.
- Backup prompt pred updatem: pokryto jako pre-update sheet a zaroven jako sekce `Saves and Backups` v instance workspace.

### Skin editor a management

- Skin Manager UI: pokryto jako samostatne `Skin Studio`.
- Lokalni kolekce skinu: pokryto jako knihovna skin tiles v centralnim panelu studia.
- Mojang API integrace: pokryto v pravem properties panelu jako account actions pro upload a sync.
- Cape Manager: pokryto v properties panelu Skin Studia.
- 2D/3D preview: 2D preview je explicitne uvedene, pseudo-3D priprava taky.

### Gamifikace a social

- Achievement system: pokryto jako `Achievement Hub` s unlock historií, progress a raritou.
- Twitch/YouTube stream overlay: pokryto jako `Creator Tools` nebo `Streaming` sekce v Settings Lab.
- Polling-first server status: pokryto jako live signal v command headeru a context docku, s cache vrstvou a prostorem pro pozdejsi rozsireni bez produktoveho slibu na WebSocket.
- Server quick connect a server bindings: pokryto jako `Server Hub`, `Dashboard quick connect`, `Context Dock server card` a `launch plus auto-connect` flow.
- Discord announcementy a YouTube content feed: pokryto jako `Novinky a media` blok na Dashboardu a kompaktni highlights v Context Docku.

### UX a polish

- Live Modpack Preview Cards: pokryto v Dashboard a Discover views jako preview-rich karty s hover stavy, badge systemem a progress stavy.
- Theme Engine: pokryto v `Settings Lab` a `Themes/` architekture pres tokeny, dynamic themes a future custom theme surface.
- Lokalizace CZ/EN: pokryto jako first-launch language picker a `Interface` sekce v Settings Lab.
- Modpack Export/Import: pokryto jako soucast `Instance Workspace`, hlavne sekce `Saves and Backups` a `Advanced`.

### Architektura a infra

- Refactor MainViewModel: pokryto v architekturni casti, ale je potreba ho brat jako samostatnou realizacni fazi, ne jen design detail.
- DI kontejner: patri do bootstrap vrstvy aplikace mezi `App.axaml.cs` a services composition.
- Strukturovane logovani pres Serilog: patri do diagnostics a crash tooling vrstvy.
- Secure credential storage: patri do `Accounts` a `Data and Storage` sekci Settings Lab, plus do auth infrastructure.

### Explicitni coverage z druheho planu

- NavigationService: pokryto shell routingem a command header orchestration.
- AuthViewModel: pokryto v account docku, login flow a auth modalech.
- LaunchViewModel: pokryto v hero play flow, live job console a instance actions.
- BrowserViewModel: pokryto jako Discover content browser.
- SettingsViewModel: pokryto jako Settings Lab.
- DashboardViewModel: pokryto jako novy Home/Dashboard surface.
- Obsidian theme redesign: pokryto v design systemu, tokenech, material rules, motion systemu a shell layoutu.

### Co je dulezite vedet

Ten dokument neni nizkourovnovy implementacni checklist po metodach. Je to produktovy a UX blueprint.

To znamena:

- vsechny feature z planu tam ted jsou
- u UX featur je popsane kde a jak budou zit v produktu
- u infra featur je popsane do ktere systemove vrstvy patri
- detaily typu konkretni API volani, konkretni ViewModel split a build order porad zustavaji v implementacnim planu

Jinak receno: `implementation_plan.md` rika **co postavit v kodu**, tenhle blueprint rika **jak a kde to bude zit v novem launcheru**.

## Doporucena implementacni architektura

## Co zmenit v kodu jako prvni

Nejdulzitejsi je rozdelit UI vrstvu, ne business logiku.

### Souborova strategie

Udelat tyto nove oblasti:

- `src/Themes/ObsidianTokens.axaml`
- `src/Themes/ObsidianControls.axaml`
- `src/Themes/ObsidianMotion.axaml`
- `src/Controls/AppShell.axaml`
- `src/Controls/NavRail.axaml`
- `src/Controls/CommandHeader.axaml`
- `src/Controls/ContextDock.axaml`
- `src/Controls/HeroPanel.axaml`
- `src/Controls/ModpackCard.axaml`
- `src/Controls/ToastHost.axaml`
- `src/Views/DashboardView.axaml`
- `src/Views/DiscoverView.axaml`
- `src/Views/InstanceWorkspaceView.axaml`
- `src/Views/SettingsLabView.axaml`
- `src/Views/SkinStudioView.axaml`
- `src/Views/AchievementsView.axaml`

### Co ponechat

- `MainViewModel.*` logiku lze z velke casti ponechat
- launch, auth, install, update flow neni nutne prepisovat kvuli designu

### Co prepsat

- `MainWindow.axaml` na shell orchestrator
- `App.axaml` na import skutecnych theme dictionaries
- code-behind omezit na minimum

## Faze realizace

## Faze 0 - Foundation

- zavadet theme tokeny a resource dictionaries
- odstranit hardcoded barvy z hlavniho layoutu
- zavest custom control vrstvu

## Faze 1 - Shell and Dashboard

- novy shell
- novy dashboard
- novy context dock
- novy command header

Tohle je nejdulezitejsi cast. Jakmile funguje shell, zbytek uz jen osidlujeme.

## Faze 2 - Discover and Instance Workspace

- novy browse experience
- novy detail modpacku
- sticky actions
- premium progress states

## Faze 3 - Overlays and Core UX polish

- toast host
- login sheet redesign
- create profile wizard redesign
- backup prompt
- crash drawer

## Faze 4 - Feature surfaces

- Skin Studio
- Achievement Hub
- Streaming tools
- Theme switching surface

## Faze 5 - Final polish

- motion tuning
- icon system
- loading skeletons
- empty states
- responsive states pro mensi sirky okna

## Prakticka doporuceni pro implementaci

### Nedelat

- nelepit dalsi styly primo do `MainWindow.axaml`
- nepridavat dalsi emoji-based navigaci
- nedrzet vsechny modaly ve stejnem souboru navzdy
- nemichat visual states a business logiku

### Delat

- vsechno stavet na reusable controls
- tokenizovat barvy, radiusy, spacing, elevation a motion
- oddelit shell od jednotlivych obrazovek
- pripravit misto pro dalsi featury driv, nez se zacnou implementovat

## Definition of done pro redesign

Redesign je hotovy, az kdyz plati vsechno tohle:

- launcher ma vlastni identitu a nepusobi jako cizi template
- dashboard ma silny wow efekt a zaroven je okamzite srozumitelny
- discover, detail a settings maji konzistentni system
- account, server, news a launch state jsou integrovane do shellu, ne nahodne vedle
- toasty, crash flow a backup flow pusobi jako premium soucast produktu
- skin manager a achievementy maji predem pripraveny prostor v IA
- theme se da dal rozsirovat bez dalsiho nafukovani `MainWindow.axaml`

## Muj doporuceny dalsi krok

Pokud chces opravdu "absolutni technologicky top" a ne jen dalsi facelift, dalsi implementacni krok ma byt tenhle:

1. Prestavet `MainWindow.axaml` na shell
2. Vytahnout theme do `Themes/`
3. Udelat novy Dashboard a Context Dock jako prvni production slice

To je nejcistejsi cesta, jak dostat launcher na uroven webu a pripravi ho to na vsechny schvalene featury.

---

## Realizacni tracker

> [!IMPORTANT]
> Tohle je hlavni checklist, ktery budeme prubezne odskrtavat podle realneho postupu v kodu.

### Audit reality check - 2026-03-22

> [!WARNING]
> Tracker byl po realnem auditu kodu opraven podle skutecneho stavu implementace.
> To znamena: co je jen mock surface, placeholder, nepouzita infrastruktura nebo nenapojeny flow,
> uz neni vedeno jako hotove.

- Build aktualne prochazi (`dotnet build --no-restore`) s 1 existujicim warningem v `PotatoModsViewModel.cs`.
- `MainWindow.axaml` uz neni monoliticky shell a stare `.bak` / `.new` artefakty byly odstraneny ze source stromu.
- `InstanceWorkspaceView` uz ma realny export/import, snapshoty konfigurace a vazby server -> modpack. Backup prompt sheet je napojeny do delete/reinstall flow a ochranné snapshoty se ukládají mimo mazanou instanci.
- `NewsView` je realne napojeny na `SocialFeedService` / `FilteredNewsFeed` a backend-normalized content feed s fallbackem na legacy Discord/YouTube zdroje.
- `AchievementsView` uz renderuje realny launcher-side achievement kontrakt z backend season/player stats a pri vypadku umi spadnout na lokalni cache snapshot, takze M4 uz neni blokovana mock daty.
- `ServerHubView` je realne napojeny na `Servers`, `QuickConnectCommand` a custom server CRUD/persistenci.
- `SkinStudioView` uz neni placeholder: ukazuje realnou account/UUID identitu, avatar preview a navazuje na existujici externi skin tools. `LocalizationView` uz stoji na realne `.resx` / `ResourceManager` infrastrukture s runtime switchem jazyka a perzistenci volby v configu. `ThemeSwitcherView` je napojeny na realne runtime prepinani a perzistenci vyberu motivu. `CreateProfileWizard` uz bezi jako samostatny wizard control s validaci kroku, review stavem a uklada custom instance do stejneho launcher workspace jako zbytek runtime flow.
- `NavigationService` uz ridi hlavni navigacni commandy; `CurrentMainView` zustava jako view-state synchronizace z routing service do shellu.
- `CardClickBehavior` uz nahradil zbyvajici pointer-driven card navigaci v Dashboard a Library surfaces.
- `ThemeEngine` je napojeny do bootstrapu i UI akci a prepina runtime tokeny bez restartu aplikace.
- `EmptyState` a `LoadingSkeleton` reusable controls uz jsou nasazene v `NewsView`, `ServerHubView` a workflow kolem instance workspace, ale rollout jeste neni plosny.
- `ServerStatusService` je polling-only; deklarovany WebSocket + fallback zatim v kodu neni.
- Duplicitni polling pres `UpdateServerStatus()` byl zrusen; aktualni stav serveru ridi `ServerStatusService`.

### Milestone 0 - Foundation 🟩

- [x] Vytvorit `Themes/` resource dictionaries (`ObsidianTokens`, `ObsidianControls`, `ObsidianMotion`)
- [x] Presunout hardcoded barvy z `App.axaml` a `MainWindow.axaml` do tokenu
- [x] Zavest zakladni reusable controls vrstvu v `Controls/`
- [x] Pripravit shell-level layout bez rozbiti existujici logiky
- [x] Dopsat design system pravidla pro focus states, reduced motion a minimalni interactive hit area

### Milestone 1 - Shell and Dashboard 🟩

- [x] Prestavet `MainWindow.axaml` na shell orchestrator
- [x] Vytvorit `NavRail`
- [x] Vytvorit `CommandHeader` (integrovano do NavRail a ContextDock)
- [x] Vytvorit `ContextDock`
- [x] Vytvorit `DashboardView`
- [x] Navrhnout hero sekci pro VOID-BOX2
- [x] Navrhnout blok `Novinky a media` na Dashboardu (pripraveno v ContextDock)
- [x] Presunout aktualni pravy panel do noveho kontextoveho docku

### Milestone 2 - Discover and Instance Workspace 🟨

- [x] Vytvorit `DiscoverView`
- [x] Predelat browse cards na preview-rich content browser
- [x] Vytvorit `InstanceWorkspaceView`
- [x] Rozdelit instance detail na `Overview`, `Content`, `Gallery`, `Performance`, `Saves and Backups`, `Advanced`
- [x] Pridat `Servers` sekci nebo tab pro spravu vazeb server -> modpack
- [x] Pripravit misto pro export/import a backup historii

### Milestone 3 - Core UX overlays 🟨

- [x] Pridat `ToastHost`
- [x] Zapojit Toast system do overlay layeru
- [x] Navrhnout backup prompt sheet
- [x] Navrhnout crash diagnostics drawer
- [x] Predelat login flow do premium sheet/modalu
- [x] Predelat create profile flow do wizardu

### Milestone 4 - Feature surfaces 🟩

- [x] Vytvorit `SkinStudioView`
- [x] Vytvorit `AchievementsView`
- [x] Vytvorit `Server Hub` nebo `Server Quick Connect` surface
- [x] Vytvorit `News and Media` surface nebo dashboard module
- [x] Pridat `Streaming` / `Creator Tools` surface
- [x] Pripravit surface pro theme switching
- [x] Pripravit surface pro lokalizaci CZ/EN

### Milestone 5 - Backend and infra napojeni 🟨

- [x] Zapojit paralelni stahovani do install UX
- [x] Zapojit Crash Reporter do realneho post-exit flow
- [x] Dopsat observability a produktove finalizovat polling-first server status flow
- [x] Pridat datovy model pro servery a bindings na modpacky
- [x] Pridat hardcoded VOID-CRAFT server jako default pinned entry
- [x] Napojit `Play server` flow na launch linked modpacku
- [x] Napojit auto-connect po startu hry
- [x] Napojit Discord announcements feed ze sdileneho API endpointu
- [x] Napojit YouTube video feed stejnym nebo kompatibilnim zpusobem jako web
- [x] Rozhodnout, jestli YouTube posty maji stabilni zdroj; pokud ne, nechat jako optional backlog
- [x] Sjednotit Discord a YouTube content do backend-normalized feed kontraktu
- [x] Pridat cache vrstvu pro social feedy a server status
- [x] Dopsat compatibility matrix pro server auto-connect podle verze a launcher flow
- [x] Pripravit Theme Engine na runtime prepinani
- [x] Pripravit Export/Import flow v instance workspace
- [x] Pripravit secure storage surface pro ucty a citliva data

### Milestone 6 - Architektura 🟨

- [x] Dokoncit rozdeleni `MainViewModel` na orchestrator a samostatne oblasti
- [x] Zavest `NavigationService`
- [x] Zavest DI registraci service vrstvy
- [x] Presunout logging na strukturovane logovani
- [x] Omezit code-behind na minimum
- [x] Zavest observability pro feed loading, quick connect a fallback scenare

### Milestone 7 - Final polish 🟨

- [x] Vyladit motion timing napric launcherem
- [x] Sjednotit icon system
- [x] Dodelat loading skeletons
- [x] Dodelat empty states
- [x] Dodelat responsive chovani pro mensi sirky okna
- [ ] Otestovat keyboard navigation a focus flow napric shell layoutem
- [ ] Otestovat kontrast a reduced motion rezim
- [ ] Otestovat startup performance a async loading budget
- [ ] Overit, ze launcher vizualne dosahuje nebo prekonava web

### Done checklist

- [x] Launcher ma vlastni silnou identitu
- [x] Dashboard pusobi premium a je okamzite srozumitelny
- [x] Discover, Instance Workspace a Settings maji jednotny system
- [x] Toast, crash a backup flow jsou integrovane a citelne
- [x] Server quick connect funguje spolehlive pro VOID-CRAFT i custom servery
- [x] Novinky z Discordu a YouTube jsou v launcheru prezentovane stejne kvalitne jako na webu nebo lepe
- [x] Skin Studio a Achievement Hub jsou produkcne pripravene
- [x] Theme a localization surfaces jsou pripraveny pro rozsirovani
- [ ] Feedy, quick connect a social content funguji i pri docasnem offline nebo chybnem endpointu elegantne
- [ ] Launcher je pristupny, plynuly a nepusobi tezce ani pri bohatem UI
- [x] Architektura UI se uz nevraci k monolitickemu `MainWindow.axaml`

### Konkretni otevrene nedostatky po auditu

- [x] Odstranit placeholder texty `Pripravujeme`, `Brzy`, `v pristi verzi`, `v budouci aktualizaci` z produkcnich surfaces
- [x] Napojit `NewsView` na realny `FilteredNewsFeed` a `RefreshNewsFeedCommand`
- [x] Napojit `ServerHubView` na `Servers`, `QuickConnectCommand` a `CopyServerIpCommand`
- [x] Dopsat custom server CRUD flow a odstranit disabled `+ Pridat server`
- [x] Dopsat realne backupy, export/import a server bindings v `InstanceWorkspaceView`
- [x] Zapojit `GetAutoConnectArgs()` do skutecneho launch flow hry
- [x] Zrusit stary polling `UpdateServerStatus()` nebo ho sloucit se `ServerStatusService`
- [x] Upravit tracker a produktovy text tak, aby sliboval jen polling-first server status
- [x] Nahradit hardcoded/mock data v `AchievementsView` realnym backend nebo lokalnim datovym kontraktem
- [x] Zapojit `ThemeEngine` do bootstrapu a UI akci pro realne runtime prepinani motivu
- [x] Zavest skutecnou lokalizacni infrastrukturu (`resx`/resource manager/runtime switch)
- [x] Prevest hlavni navigaci na `NavigationService`, ne na prime `CurrentMainView = ...`
- [x] Nahradit zbyvajici pointer code-behind pouzitim `CardClickBehavior`
- [x] Nasadit `EmptyState` a `LoadingSkeleton` do realnych view misto jednorazovych inline variant
- [x] Dovest icon system i mimo `NavRail` do plne tokenizovanych CTA a feature surfaces
- [x] Odstranit artefakty `MainWindow.axaml.bak` a `MainWindow.axaml.new` ze source stromu

### Follow-up blok po runtime review launcheru

- [x] Presunout vyber pracovni instance ze `Skin Studio` do `Creator Studio` a pretvorit `Streaming Tools` na skutecny modpack-authoring workspace
- [x] Nechat `Skin Studio` ciste account-bound: velky render postavy, historie skinu z NameMC a akce nad identitou bez instance pickeru
- [x] Opravit NameMC parsing tak, aby se historie skinu nacitala pro realne ucty a nepadala na krehkem HTML selectu
- [x] Napojit Voidium achievements na realne ranky a progres misto hrubych placeholder thresholdu 10/50/100h
- [ ] Dostat `Server Hub` a dashboard na jednotnou pravdu: pocet serveru musi zahrnovat pinned VOID-CRAFT i auto-discovered/community polozky
- [ ] Zobrazovat u serveru verzi a pocet modu z navazaneho modpacku nebo jeho lokalnich metadat, ne placeholder `400+`
- [ ] Zrusit vizualni duplicity v pinned server kartach a nechat player-count/status jen jednou a citelne
- [ ] Dotahnout `News` tak, aby Discord zpravy a video preview byly konzistentne viditelne i pri fallback feedu
- [ ] Udelat `Dashboard` obsahove odlisny od `Library`: home ma byt stavovy a operacni prehled, ne druha knihovna instanci
- [ ] Opravit `ToastHost` vizual tak, aby toasty mely nepruhledne surfaces, jasnou severity a nepusobily jako pruhledny overlay

### Dodatek po dalsim runtime review - 2026-03-22

- [x] Pretvorit `Creator Studio` z viditelneho Discord RPC panelu na realny instance workbench s vyberem upravitelnych souboru, editorem obsahu a ulozenim zpet do instance
- [x] Odstranit z `Creator Studio` viditelnou sekci `Discord Rich Presence`; Discord integrace muze bezet na pozadi, ale uz netvori hlavni product surface teto obrazovky
- [x] Vycistit `Skin Studio` wording: odstranit texty typu `Account snapshot`, zrusit navazani na `Creator Studio` a prepsat guest/offline stavy na realne labels pro ucet nebo neprihlasenou relaci
- [x] Rozsirit `NameMC` fallback parsing a candidate URL sady tak, aby se historie skinu nacitala i pri canonical `.1` profile route a nevisela jen na jednom HTML anchor patternu
- [x] Pridat do launcheru latest oficialni `Minecraft.net` clanek: backend endpoint `/api/feed/minecraft-official`, napojeni do `SocialFeedService`, filtr v `NewsView` a featured kartu na `DashboardView`
- [x] Zrychlit startup `News` surface: hydratovat feed z lokalni cache hned po startu launcheru a teprve potom delat background refresh proti backendu / externim zdrojum
- [x] Izolovat feed latency po zdrojich: pridat timeouty pro Discord / YouTube / unified feed a nenechat jeden pomaly endpoint blokovat cely dashboard
- [x] Pridat launcher-side fallback na `Minecraft.net/articles`, aby official news dokazaly prezit prazdny nebo pomaly backend endpoint
- [x] Pridat do `Server Hub` okamzity launch feedback pro `Quick Connect` (toast plus in-view progress banner), aby uzivatel nevidel jen tiche spusteni modpacku bez stavu
- [x] Rozsirit `Quick Connect` o best-effort auto-connect pripravu: zapsat server do `servers.dat`, doplnit legacy `--server/--port` argumenty a pro kompatibilni verze i quick-play multiplayer argumenty
- [x] Vratit shell page transition i do realneho runtime: host view v `MainWindow` ted reaguje na `CurrentMainView` fade/slide animaci a respektuje reduced-motion tridu
- [x] Opravit semantiku `Achievement Hub`: presunout 75 % a 100 % badge z `mastery` do `season progress` a odstranit slaby `online-now` badge, ktery nemel produkcni hodnotu
- [ ] Udelat runtime validaci nad realnym Microsoft uctem a potvrdit, ze `NameMC` historie vraci ocekavanou verejnou timeline i po case
- [ ] Overit end-to-end proti bezicimu backendu, ze featured `Minecraft.net` clanek a sjednoceny news feed padaji korektne i pri docasnem vypadu oficialniho zdroje

### Dodatek - Production text a UX polish - 2026-03-22

- [x] Vymenit vsechny changelog/developer-facing texty v XAML za produkcni uzivatelsky copy (SkinStudioView, StreamingToolsView, DashboardView, ServerHubView, ThemeSwitcherView, InstanceWorkspaceView)
- [x] Vymenit developer texty v Strings.resx a Strings.en.resx: Achievement Hub zdroj, Localization sekce (subtitle, runtime status, infrastruktura, diagnostika)
- [x] Opravit scroll cutoff ve StreamingToolsView a LocalizationView (bottom padding 32 → 60, shodne s ostatnimi view)
- [x] Pridat try-catch do SkinStudioService.FetchSkinHistoryAsync pro jednotlive URL pokusy, aby 404/403 od NameMC nezastavil cely fetch pipeline
- [x] Pridat skip NameMC fetch pro offline ucty (ActiveAccount.Type != Microsoft) s odpovídajici prazdnou zprávou
- [x] Pridat dynamicke SkinHistoryEmptyTitle/SkinHistoryEmptySubtitle, ktere rozlisuji offline vs Microsoft ucet
- [x] Vycistit StreamingSessionStatus a CreatorWorkbenchStatus od zbytecneho developer wordingu
- [x] Opravit Achievement Hub progress labely tak, aby `online/offline` wording pouzival jen `online-now` badge a ne quest / team / podium pravidla
- [x] Nahradit wording `Offline snapshot` a snapshot-based meta texty v Achievement Hub produkcnim copy (`Momentalne offline`, `data z ...`)
- [x] Pridat do Creator Studio vyhledavani v workbench souborech podle cesty a kategorie
- [x] Omezit vysku seznamu souboru a editoru v Creator Studio, aby dlouhe config sady neroztahovaly celou stranku

### Release gate audit - 2026-03-22

- [x] Opravit build blocker launcheru: SDK compile glob bral i `obj-validation` a `obj-verify`, coz rozbijelo `dotnet build` duplicitnimi assembly atributy
- [x] Overit, ze `dotnet build VoidCraftLauncher.csproj` pro `Debug` i `Release` po oprave prochazi bez chyb
- [x] Udelat zakladni startup smoke test `dotnet run`, bez okamziteho padu shellu nebo stack trace v konzoli
- [x] Po runtime fix passu znovu overit `dotnet build VoidCraftLauncher.csproj` pro `Debug` i `Release`, aby feed fallbacky, shell transition a quick-connect zmeny nezavedly novy build regression
- [ ] Manualne potvrdit keyboard navigation, focus flow, kontrast a reduced motion na realnem GUI
- [ ] Manualne potvrdit, ze dashboard vizualne a kvalitativne dosahuje release baru proti webu
- [ ] Potvrdit end-to-end proti realnemu Microsoft uctu, ze `Skin Studio` vraci verejnou NameMC historii stabilne i mimo lokalni fallbacky
- [ ] Potvrdit end-to-end proti bezicimu backendu a realne aplikaci, ze `Quick Connect`, social feed fallbacky a `minecraft-official` feed drzi produkcni chovani i pri docasnem vypadku zdroje

> [!warning]
> Aktualni stav po tomhle auditu: **neni poctive oznacitelny jako ready na vydani**.
> Kod je po build fixu i navazujicim runtime fix passu znovu sestavitelny a hlavni reportovane problemy jsou implementacne pokryte.
> Release gate ale zustava otevreny, dokud neprobehne realna GUI a end-to-end validace `Skin Studio`, `Quick Connect`, official news fallbacku a accessibility / motion scenaru vypsanych vyse.