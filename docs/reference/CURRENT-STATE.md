# VOID-CRAFT Launcher - Aktuální Stav

Snapshot: 2026-04-04

Aktuální release: 3.1.8.1

## Co je dnes reálně v produktu

### Runtime a platforma

- .NET 9
- Avalonia UI 11.3.10
- centrální data launcheru v `Documents/.voidcraft`
- Microsoft login, secure token cache a launch guard proti tichému guest fallbacku

### Hlavní hotové plochy

- `Dashboard`
- `Server Hub`
- `Achievement Hub`
- `Skin Studio`
- `Instance Workspace`
- custom profile flow a správa instancí

### Creator Studio / Creator Workbench

Dnes už je reálné a hotové hlavně toto:

- workflow shell se záložkami `Overview`, `Metadata`, `Mods`, `Files`, `Notes`, `Git`, `Release`
- bootstrap nové instance přes `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git`, `Restore Snapshot`
- `creator_manifest.json` a standardní creator workspace struktura
- metadata editace přímo v launcheru
- Creator workspace selection se obnovuje z perzistentni volby i po restartu launcheru, takze studio drzi posledni zvoleny workspace konzistentneji napric Metadata, Mods i Files
- novy Creator workspace pri zalozeni predvyplni autora z aktualne prihlasene identity, ale autor zustava editovatelny
- branding pipeline pro `logo`, `cover`, `square icon`, `wide hero`, `social preview`
- validace assetů, media kit export a promo screenshot kurátorství
- `Mods` tab má plný in-tab workflow: vetsi katalog pro hledani a pridavani modu, local `.jar` import, multi-select, bulk add/remove, zapínání/vypínání modů a fallback ze striktně kompatibilniho vyhledani do sirsiho katalogu
- `.voidpack` export/import drží mody jako textový modlist manifest místo balení `mods/*.jar`; import se je pokouší znovu stáhnout a hlásí ruční follow-up kusy
- `Files` tab má realny editor host s režimy `Form`, `Editor`, `Porovnání`, parsery pro běžné config formáty, outline/focus workflow a diff proti snapshotu/exportu/default counterpartu
- `Files` layout je zjednoduseny na levy seznam souboru, jeden hlavni editor a jeden pomocny panel s navigaci, formem a kontrolou, aby byl citelny i na vetsich workspacech

To odpovídá realitě po Creator Workbench F0-F2 a následných polish hotfixech.

## Co ještě není dotažené

Největší otevřené mezery vůči roadmapě jsou stále:

- plnohodnotný notes/wiki + quest/progression canvas
- lokální Git workflow a GitHub desk
- CurseForge/Modrinth kompatibilní packaging a release validace
- AI-native patch/apply flow nad workbenchem
- release board, QA gate a publish orchestrace

Poznamka k Files tabu:

- structured save je dnes zamerne normalizacni a neni comment-preserving roundtrip; `json5`, `toml`, `yaml`, `ini` a `properties` proto ukazuji explicitni warning, ze ulozeni muze prepsat formatting nebo komentarove bloky
- copy a popisy ve Files tabu uz maji produkcni wording; technicke fallbacky zustavaji v pozadi, ale hlavni UI mluvi jazykem editoru, ne diagnostiky

Vedle toho zůstává průběžná kvalita:

- accessibility a focus/runtime review
- reduced-motion a kontrast validace v plném end-to-end provozu
- ověření live fallbacků proti backend content feedu a account runtime flow

## Jak číst zbytek dokumentace

Aktivní zdroje pravdy:

- `README.md` - stručný vstup do repozitáře
- `CHANGELOG.md` - co opravdu vyšlo
- `future.md` - produktová roadmapa, ne implementační backlog
- `docs/planning/creator-workbench.md` - nejpřesnější implementační dokument pro Creator Studio
- `docs/reference/CURRENT-STATE.md` - tento soubor

Historické nebo referenční dokumenty:

- `docs/planning/IMPLEMENTATION_PLAN.md` - starý v2/.NET 8 plán, historický kontext
- `docs/planning/LAUNCHER-OBSIDIAN-REDESIGN-BLUEPRINT.md` - redesign blueprint a UX reference

## Praktické pravidlo

Když si dva dokumenty odporují, ber v pořadí:

1. `CHANGELOG.md`
2. kód
3. `docs/planning/creator-workbench.md`
4. `future.md`
5. historické plány