# 🎨 Creator Workbench — UX Redesign Blueprint

> **Cíl:** Přetvořit Creator Workbench z funkčně nabitého, ale nepřehledného prostředí
> na **čistý, intuitivní workspace**, kde se autor modpacku okamžitě zorientuje,
> nic nehledá a práce mu plyne bez tření.
>
> *Funkce zůstávají. Mění se způsob, jakým je autor vidí a používá.*

## Stav Implementace k 2026-04-04

Legenda:
- `[x]` hotovo v kódu
- `[-]` částečně hotovo, základ existuje, ale blueprint ještě není dotažený do cílového stavu
- `[ ]` zatím chybí

### Přehled podle implementačního pořadí

- [x] Shell + workspace header: 2řádkový header, identity, status badges, quick actions, persistent right dock a Notes drawer existují.
- [x] Tab přejmenování + routing: `Metadata` je přejmenované na `Identity`, shell už běží přes `Overview / Identity / Mods / Files / Notes / Git / Release`.
- [x] Overview cleanup: timeline aktivity, quick links, recent workspaces a projektové staty jsou reálně nasazené.
- [x] Identity split: `Profile` + `Branding` sub-taby existují, live preview a asset checklist fungují.
- [-] Mods workflow: vyhledávání přes CurseForge + Modrinth, batch akce, infinite browse a ruční výběr verze před instalací už jsou hotové; stále chybí plný attention board, detail panel modu a dependency graf.
- [x] Files polish: quick filter, structured/raw/diff editor, validační badges, breadcrumb a inline problémy jsou hotové.
- [-] Release pipeline: pipeline step bar, validace, export karty, changelog editor, historie a per-card export tlačítka existují; stále chybí quick diff mezi releasy, rollback flow a plný QA gate workflow.
- [-] Copilot Desk: creator-only dock, shared workspace context a rychlé akce existují; stále chybí skutečný chat-first AI workspace a adaptivní tab-specific action palette podle blueprintu.
- [-] Notes režimy: `Docs`, `Wiki`, `Canvas`, `Mind Map` i Notes drawer existují; stále chybí plnohodnotná editace canvasu, linking flow a export `PNG/JSON` přímo z UI.
- [x] Git polish: changes list, stage/revert, commit, commit & push, branch list a compact history existují.
- [-] Keyboard shortcuts: `Ctrl+1..7`, `Ctrl+N`, `Ctrl+S`, `Ctrl+Shift+S`, `Ctrl+K`, `Ctrl+E`, `Ctrl+D`, `Escape` jsou napojené; stále chybí kompletní sada jako `Ctrl+P`, `Ctrl+G`, `Ctrl+Shift+G` a plný quick-open flow.
- [ ] Responsive breakpoints: plné chování pro `>1400 / 1000-1400 / <1000 px` ještě není dotažené.

### Důležité otevřené mezery proti blueprintu

- [ ] Mods: sekce `Pozornost`, detail panel modu, závislosti a konflikty jako první-class workflow.
- [ ] Identity / Branding: drag & drop asset sloty a výrazněji produktový branding flow podle finálního návrhu.
- [ ] Git / GitHub auth: přihlášení, create repo, private clone, fetch/pull/push/sync a publish branch flow ala GitHub Desktop.
- [ ] Release: QA Gate, playtest build flow, feedback inbox, rollback a diff proti minulému releasu.
- [ ] VOID ID: bezpečný account layer pro launcher + backend + web, linked accounts, role a projektové členství.
- [ ] Copilot Desk: opravdový chat-first pracovní panel místo převážně informačního panelu.
- [ ] Copilot SDK host: streaming session, scope picker, patch preview, explicitní apply a audit trail.
- [ ] Notes: interaktivní Canvas / Mind Map editor místo read-only přehledu dat.
- [ ] Kontextová menu, drag & drop workflow a accessibility pass.

### Dnešní P0 priority navázané na redesign

- [ ] `GitHub login + remote desk`: teď je nutné doplnit reálné přihlášení, create repo, private clone a sync flow, aby `Git` tab fungoval jako lehčí GitHub Desktop a ne jen lokální status panel.
- [ ] `VOID ID`: redesign už musí počítat s jednotnou identitou pro launcher, backend i web, protože bez ní nebude fungovat project registry, role, tester access ani bezpečné release workflow.
- [ ] `Copilot SDK`: pravý `Copilot Desk` musí být postavený nad skutečným SDK hostem se streamingem, preview-only apply a guardraily, ne jen nad statickými akcemi.

---

## 📐 Designové Principy

| # | Princip | Pravidlo |
|---|---------|----------|
| 1 | **Jeden úkol = jeden pohled** | Žádný tab nemá řešit víc než jednu jasnou činnost. |
| 2 | **Progressive disclosure** | Zobraz minimum, odhal zbytek až na požádání. |
| 3 | **Vizuální hierarchie** | Každá plocha má max 1 primární akci, 2–3 sekundární, zbytek v menu. |
| 4 | **Konzistentní layout** | Všechny taby sdílejí stejný grid: `Sidebar │ Main │ Right Dock`. |
| 5 | **Nulový scroll-hell** | Žádná záložka nesmí vyžadovat scrollování přes 3+ obrazovky obsahu. |
| 6 | **Context stále po ruce** | Header workspace + Copilot Desk jsou fixní, nikdy nemizí. |
| 7 | **Rychlé přepínání** | Klávesové zkratky pro taby, drawer, scope. Žádné vnořené modály. |

---

## 🏗️ Globální Layout Shell

```
┌─────────────────────────────────────────────────────────────────────┐
│  ▸ Workspace Header  (fixní, vždy viditelný)                       │
│    [Logo] Pack Name · v1.2.0 · stable · MC 1.21.4 NeoForge         │
│    ● Git: main ↑2  │  ◉ 3 dirty files  │  📦 Last export: 2h ago  │
├─────┬───────────────────────────────────────────┬───────────────────┤
│     │                                           │                   │
│  T  │          MAIN CONTENT AREA                │   RIGHT DOCK      │
│  A  │                                           │                   │
│  B  │   (mění se podle aktivního tabu)          │   Copilot Desk    │
│     │                                           │   nebo            │
│  B  │                                           │   Context Dock    │
│  A  │                                           │                   │
│  R  │                                           │   ─────────────   │
│     │                                           │   Notes Drawer    │
│     │                                           │   (overlay)       │
│     │                                           │                   │
└─────┴───────────────────────────────────────────┴───────────────────┘
```

### Workspace Header — Redesign

**Současný problém:** Header ukazuje příliš mnoho textových polí v řadě, chybí vizuální scan-path.

**Nový návrh:**

```
┌──────────────────────────────────────────────────────────────────┐
│  [48px Logo]   VOID-BOX                                          │
│               v1.2.0 stable · MC 1.21.4 · NeoForge 21.4.62      │
│                                                                  │
│  ● main ↑2    ◉ 3 dirty    📦 2h ago    🔔 0 issues    [⚡ ▾]   │
└──────────────────────────────────────────────────────────────────┘
```

- **Řádek 1:** Logo + název + verze + channel + tech stack — identity na první pohled
- **Řádek 2:** Status badges jako pill chips — git, dirty, export, issues
- **`[⚡ ▾]`:** Quick Actions dropdown — screenshot, open folder, copy summary, session log

**Klíčová změna:** Všechny session akce (screenshot, logy, folder, clipboard) se přesouvají z Overview tabu do header quick actions. Jsou potřeba odkudkoliv, ne jen z Overview.

---

## 📑 Redesign Záložek

### Nový Tab Bar

```
┌──────────┬──────────┬────────┬────────┬────────┬───────┬──────────┐
│ Overview │ Identity │  Mods  │ Files  │ Notes  │  Git  │ Release  │
└──────────┴──────────┴────────┴────────┴────────┴───────┴──────────┘
     0           1        2        3        4        5        6
```

> **Přejmenování:** `Metadata` → `Identity`
> Důvod: "Metadata" zní technicky a abstraktně. "Identity" jasně říká
> "tady definuješ kdo tvůj pack je a jak vypadá".

Klávesové zkratky: `Ctrl+1` až `Ctrl+7` pro přímý skok.

---

### Tab 0 — Overview

**Účel:** Rozcestník. Rychlý přehled a navigace, ne práce.

**Současný problém:**
- Mixuje workspace picker, session akce a pack summary do jedné stěny karet
- Session akce (screenshoty, logy) tady nemají smysl — patří do headeru

**Nový layout:**

```
┌─────────────────────────────────────────────────┐
│                 OVERVIEW                         │
│                                                  │
│  ┌──────────────────┐  ┌──────────────────────┐  │
│  │ 🔵 Workspace     │  │ 📊 Stav Projektu     │  │
│  │                  │  │                      │  │
│  │ [Výběr instance] │  │  Mody:    147        │  │
│  │ Path: ...        │  │  Soubory: 2,340      │  │
│  │ MC 1.21.4        │  │  Notes:   12         │  │
│  │ NeoForge 21.4.62 │  │  Snapshots: 5        │  │
│  │                  │  │  Exports: 3          │  │
│  └──────────────────┘  └──────────────────────┘  │
│                                                  │
│  ┌──────────────────────────────────────────────┐│
│  │ 🕐 Poslední Aktivita                        ││
│  │                                              ││
│  │  • 14:32  Editován server.properties         ││
│  │  • 14:20  Přidán mod Create 0.5.1f           ││
│  │  • 13:55  Commit: "balance update"           ││
│  │  • 13:40  Export .voidpack v1.1.9            ││
│  └──────────────────────────────────────────────┘│
│                                                  │
│  ┌──────────────┐ ┌──────────────┐ ┌───────────┐│
│  │ ⚡ Rychlé    │ │ 🔗 Odkazy    │ │ 📋 Recent ││
│  │   Akce       │ │              │ │ Workspaces││
│  │              │ │ GitHub repo  │ │           ││
│  │ Nová inst.   │ │ CurseForge   │ │ VOID-BOX  ││
│  │ Import pack  │ │ Modrinth     │ │ TestPack  ││
│  │ Clone Git    │ │ Discord      │ │ ATM10-dev ││
│  └──────────────┘ └──────────────┘ └───────────┘│
└─────────────────────────────────────────────────┘
```

**Co se mění:**
- ❌ Odstraněn blok session akcí (→ header quick actions)
- ❌ Odstraněn pack summary (→ header + Identity tab)
- ✅ Přidána timeline poslední aktivity
- ✅ Přidány rychlé akce pro bootstrap nové instance
- ✅ Přidán blok odkazů (GitHub, CF, MR, Discord) z manifestu
- ✅ Recent workspaces zůstávají

---

### Tab 1 — Identity *(dříve Metadata)*

**Účel:** Vše o identitě packu na jednom místě — text i vizuál.

**Současný problém:**
- Metadata tab je enormní: textová metadata + branding + screenshoty + launcher preview
- Uživatel scrolluje přes 4+ obrazovky obsahu
- Branding sekce je vizuálně odříznutá od kontextu

**Nový layout — dvě sub-záložky:**

```
┌─────────────────────────────────────────────────┐
│  IDENTITY                                        │
│  ┌────────────┬──────────────┐                   │
│  │ 📝 Profile │ 🎨 Branding  │    (sub-tabs)    │
│  └────────────┴──────────────┘                   │
└─────────────────────────────────────────────────┘
```

#### Sub-tab: Profile

```
┌─────────────────────────┬───────────────────────┐
│                         │                       │
│  Pack Name   [_______]  │   👁️ LIVE PREVIEW     │
│  Slug        [_______]  │                       │
│  Version     [_______]  │   ┌─────────────────┐ │
│  Summary     [_______]  │   │  Launcher Card   │ │
│              [_______]  │   │  ┌───┐           │ │
│  Authors     [_______]  │   │  │ L │ VOID-BOX  │ │
│                         │   │  │ O │ v1.2.0    │ │
│  ─── Technické ───      │   │  │ G │ stable    │ │
│  MC Version  [1.21.4 ▾] │   │  │ O │           │ │
│  Loader      [Neo.. ▾]  │   │  └───┘           │ │
│  Loader Ver  [21.4. ▾]  │   └─────────────────┘ │
│  Rec. RAM    [6 GB   ▾] │                       │
│                         │   ┌─────────────────┐ │
│  ─── Distribuce ───     │   │  Instance Header │ │
│  Release Ch. [stable ▾] │   │  ...             │ │
│  Primary Srv [_______ ] │   └─────────────────┘ │
│                         │                       │
│  ─── Odkazy ───         │                       │
│  Web         [_______]  │                       │
│  Discord     [_______]  │                       │
│  GitHub      [_______]  │                       │
│  Support     [_______]  │                       │
│                         │                       │
│       [ 💾 Uložit ]     │                       │
└─────────────────────────┴───────────────────────┘
```

- **Levý sloupec:** Formulář — logicky seskupený do sekcí (identita, tech, distribuce, odkazy)
- **Pravý sloupec:** Live preview — jak bude pack vypadat v launcheru, aktualizuje se real-time
- **Nulový scroll** — vše se vejde na jednu obrazovku

#### Sub-tab: Branding

```
┌───────────────────────────────────────────────────┐
│                                                   │
│  ┌───────┐  ┌───────────────────────────────────┐ │
│  │       │  │ Asset Sloty                       │ │
│  │ LOGO  │  │                                   │ │
│  │       │  │  ☑ Logo        256×256  ✓ ok      │ │
│  │ drop  │  │  ☐ Cover       1920×1080  —       │ │
│  │ here  │  │  ☑ Square Icon 128×128  ✓ ok      │ │
│  │       │  │  ☐ Wide Hero   1280×400  —        │ │
│  └───────┘  │  ☐ Social Prev 1200×630  —        │ │
│             │                                   │ │
│  Accent:    │  [ ⬆ Upload ]  [ 📦 Export Kit ]  │ │
│  [#1BD96A]  └───────────────────────────────────┘ │
│                                                   │
│  ─── Promo Galerie ────────────────────────────── │
│                                                   │
│  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐       │
│  │ ★⭐ │ │ 📸  │ │ 📸  │ │ 📸  │ │  +  │       │
│  │feat.│ │ RC  │ │offic│ │arch │ │ add │       │
│  └─────┘ └─────┘ └─────┘ └─────┘ └─────┘       │
│                                                   │
│  Tag: [Official ▾]   [ ⭐ Featured ]  [ 🗑 ]     │
└───────────────────────────────────────────────────┘
```

**Co se mění:**
- ❌ Jeden obří tab → dva fokusované sub-taby
- ✅ Logo upload se stává drag & drop zonou, ne malým tlačítkem
- ✅ Asset sloty mají jasný checklist styl — vidíš co chybí
- ✅ Promo galerie je horizontální strip, ne vertikální seznam
- ✅ Tagging screenshotů je inline, ne v separátním dialogu

---

### Tab 2 — Mods

**Účel:** Správa mod obsahu — přidávání, odebírání, aktualizace, konflikty.

**Současný problém:**
- Mod management existuje, ale UX je list-heavy bez vizuální hierarchie
- Chybí jasný scan-path: co je aktuální, co potřebuje pozornost

**Nový layout:**

```
┌──────────────────────────────────────────────────┐
│  MODS                                 [147 modů] │
│                                                  │
│  🔍 [________________] [Filtr ▾] [+ Přidat ▾]   │
│                                                  │
│  ─── Pozornost (3) ─────────────────────────     │
│  ⚠ Create 0.5.1f     → 0.5.1g dostupný  [⬆]    │
│  ⚠ JEI 19.8.2        → 19.8.5 dostupný  [⬆]    │
│  ❌ IrisShaders       konflikt s Sodium   [?]    │
│                                                  │
│  ─── Všechny mody ──────────────────────────     │
│  ┌────────────────────────────────────────────┐  │
│  │ ☑ Applied Energistics 2   15.2.1   CF  ✓  │  │
│  │ ☑ Architectury            13.0.8   MR  ✓  │  │
│  │ ☑ Create                  0.5.1f   CF  ⚠  │  │
│  │ ☐ Optifine (disabled)     HD U I7  —   —  │  │
│  │ ...                                        │  │
│  └────────────────────────────────────────────┘  │
│                                                  │
│  ─── Akce ───                                    │
│  [ ⬆ Update All ]  [ 🗑 Bulk Remove ]            │
│  [ 📋 Export Modlist ]  [ 📂 Add Local .jar ]     │
└──────────────────────────────────────────────────┘
```

**Co se mění:**
- ✅ **Sekce "Pozornost"** — nahoře vždy vidíš co potřebuje řešení (updaty, konflikty)
- ✅ Čistý search + filter bar
- ✅ Bulk akce dole, ne roztroušené mezi mody
- ✅ Status ikony: ✓ ok, ⚠ update, ❌ konflikt, — disabled

**Kategorie a seskupení modů:**

| Kategorie | Příklad modů | Ikona |
|-----------|-------------|-------|
| ⚙️ Tech | Create, AE2, Mekanism | ozubené kolo |
| ✨ Magic | Ars Nouveau, Botania | hvězdička |
| 🔧 Utility | JEI, WAILA, Jade | klíč |
| 🎯 QoL | Mouse Tweaks, Inventory Sorter | terčík |
| 📚 Library | Architectury, Cloth Config | kniha |
| 🌍 World | Biomes O'Plenty, Terralith | globus |
| 🎨 Visual | Shaders, Resource Packs loader | paleta |

- Filtry: `[All ▾]` `[Tech ▾]` `[Magic ▾]` `[Needs Attention ▾]` `[Disabled ▾]`
- Mody se dají řadit: `Název` · `Datum přidání` · `Velikost` · `Zdroj` · `Stav`
- Grouping toggle: `Flat list` / `By category` / `By source`

**Mod Detail Panel (klik na mod):**

```
┌──────────────────────────────────────────────────┐
│  📦 Create  v0.5.1f                    [✕ Zavřít]│
│  ─────────────────────────────────────────────    │
│  Zdroj: CurseForge │ Autor: simibubi             │
│  Přidáno: 2026-03-15 │ Velikost: 12.4 MB         │
│                                                  │
│  ─── Závislosti (3) ────────────────────         │
│  ☑ Flywheel         0.6.11     ✓ nainstalován    │
│  ☑ Registrate       1.3.3      ✓ nainstalován    │
│  ☐ Create Compat    —          ✕ volitelný       │
│                                                  │
│  ─── Závisí na tomto modu (5) ──────────         │
│  Create: Steam & Rails │ Create: Enchantment ... │
│                                                  │
│  ─── Akce ───                                    │
│  [ ⬆ Update ] [ 🔧 Config ] [ 📊 Changelog ]    │
│  [ ☐ Disable ] [ 🗑 Odebrat ]                    │
└──────────────────────────────────────────────────┘
```

**Dependency Graf (toggle v detail panelu):**

```
  Create ──→ Flywheel
     │──→ Registrate
     │──→ Forge/NeoForge
     │
  Create: Steam & Rails ──→ Create
  Create: Enchantment ──→ Create
```

- Vizuální dependency strom ukazuje co na čem závisí
- Červeně zvýrazněné chybějící závislosti
- Klik na mod v grafu otevře jeho detail

---

### Tab 3 — Files

**Účel:** Procházení a editace workspace souborů.

**Současný problém:**
- Editor host už funguje (Structured/Raw/Split/Diff), ale navigace ve stromu
  souborů může být nepřehledná u velkých workspace
- Chybí jasný "co právě edituji" kontext

**Nový layout:**

```
┌──────────────┬───────────────────────────────────┐
│  📁 STROM    │  📄 EDITOR                        │
│              │                                   │
│  🔍 [_____] │  server.properties                 │
│              │  config/ · Structured · ✓ Valid    │
│  ▾ config/   │  ─────────────────────────────     │
│    ▸ create/ │                                   │
│    ▸ jei/    │  # Server Properties              │
│    server.pr │  ┌────────────────────────────┐   │
│  ▸ kubejs/   │  │ Key           │ Value      │   │
│  ▸ scripts/  │  │───────────────│────────────│   │
│  ▸ default.. │  │ server-port   │ 25565      │   │
│  ▸ docs/     │  │ motd          │ VOID-CRAFT │   │
│  ▸ notes/    │  │ max-players   │ 20         │   │
│              │  │ online-mode   │ true       │   │
│              │  └────────────────────────────┘   │
│              │                                   │
│              │  [Structured ▾] [Diff ▾] [💾]     │
└──────────────┴───────────────────────────────────┘
```

**Vylepšení navigačního stromu:**

| Prvek | Popis |
|-------|-------|
| **Quick filter** | Hledání přímo ve stromu — píšeš a strom se filtruje |
| **Breadcrumb** | Nad editorem: `config / create / server.properties` |
| **Režim badge** | Structured / Raw / Diff — malý pill vedle názvu souboru |
| **Validace badge** | ✓ Valid / ⚠ Warnings / ❌ Errors — hned vidíš stav |
| **Dirty indicator** | Tečka u názvu souboru pokud má neuložené změny |
| **Diff source picker** | Dropdown: vs Snapshot / vs Export / vs Default |

**Co se mění:**
- ✅ Breadcrumb path pro orientaci v hloubce
- ✅ Validační badge přímo u názvu souboru
- ✅ Quick filter ve stromu místo hledání v celém listu
- ✅ Jasné vizuální odlišení režimů editoru

---

### Tab 4 — Notes

**Účel:** Plánování, poznámky, progression thinking — vše bez odchodu z launcheru.

**Nový layout — tři sub-režimy:**

```
┌─────────────────────────────────────────────────────┐
│  NOTES                                               │
│  ┌────────┬────────┬──────────┬────────────┐         │
│  │ 📝 Docs│ 📚 Wiki│ 🗺️ Canvas│ 🧠 Mind Map│ (režimy)│
│  └────────┴────────┴──────────┴────────────┘         │
└─────────────────────────────────────────────────────┘
```

#### Režim: Docs

```
┌──────────────────┬──────────────────────────────────┐
│  📋 Dokumenty    │  📝 Editor                        │
│                  │                                   │
│  ▸ Design Notes  │  # Balance Notes                  │
│  ▸ Balance Notes │                                   │
│  ▸ Release Notes │  ## Tier 3 Recipes                │
│  ▸ Known Issues  │                                   │
│  ▸ TODO          │  Create steel blend je příliš     │
│  ────────────    │  levný. Zvýšit cost z 4→8.        │
│  [+ Nový]        │                                   │
│                  │  ## Dimension Gating               │
│                  │  Nether locked za boss #2.         │
│                  │                                   │
│                  │  🔗 Linked: boss_config.json       │
│                  │  🔗 Linked: Quest: "Nether Gate"   │
└──────────────────┴──────────────────────────────────┘
```

#### Režim: Canvas / Mind Map

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│    [Questline: Getting Started]                      │
│         │                                            │
│    ┌────┴────┐                                       │
│    ▼         ▼                                       │
│  [Craft     [Find                                    │
│   Bench]     Iron]                                   │
│    │         │                                       │
│    └────┬────┘                                       │
│         ▼                                            │
│    [Gate: Nether] ──── 🔗 dimension_config.json      │
│         │                                            │
│         ▼                                            │
│    [Boss: Wither] ──── 🔗 boss_loot.json             │
│         │                                            │
│         ▼                                            │
│    [Tier 3 Recipes] ── 🔗 kubejs/recipes.js          │
│                                                      │
│  Typy nodů:                                          │
│  🟢 Questline  🔴 Gate  💀 Boss  🌐 Dimension        │
│  ⚗️ Recipe Tier  🚫 Blocker  💡 Idea                  │
│                                                      │
│  [+ Node]  [🔗 Link to File]  [📤 Export PNG/JSON]   │
└──────────────────────────────────────────────────────┘
```

**Co se mění:**
- ✅ Čtyři jasné režimy místo jednoho neurčitého "Notes" prostoru
- ✅ Docs mají sidebar pro navigaci mezi dokumenty
- ✅ Canvas nody mají vizuální typy s barvami a ikonami
- ✅ Linking na soubory, commity, exporty přímo z nodu/dokumentu
- ✅ Export do MD, JSON, PNG

**Notes Drawer (Quick Access):**
- Přístupný z jakéhokoliv tabu přes `Ctrl+N` nebo tlačítko v Right Dock
- Vyjíždí zprava přes hlavní obsah
- Otevře poslední dokument nebo mind mapu
- Umožňuje rychlý zápis poznámky bez opuštění aktuálního workflow

---

### Tab 5 — Git

**Účel:** Verzování a synchronizace workspace.

**Nový layout:**

```
┌───────────────────────────────────────────────────┐
│  GIT                                               │
│                                                    │
│  ● main  ↑2 ↓0  │  Last commit: 3h ago            │
│  Remote: github.com/voidivide/void-box             │
│                                                    │
│  ┌─────────────────────────────────────────────┐   │
│  │  📋 Changes (3)                      [Stage All]│
│  │                                              │   │
│  │  M  config/create/server.json          [S]   │   │
│  │  M  kubejs/recipes/steel.js            [S]   │   │
│  │  A  notes/balance-v2.md                [S]   │   │
│  └─────────────────────────────────────────────┘   │
│                                                    │
│  Commit: [________________________________]        │
│          [ ✅ Commit ]  [ Commit & Push ]           │
│                                                    │
│  ┌─────────────────────────────────────────────┐   │
│  │  📜 Poslední Commity                        │   │
│  │                                              │   │
│  │  abc1234  balance update           3h ago    │   │
│  │  def5678  add Create 0.5.1f        5h ago    │   │
│  │  ghi9012  initial workspace setup  1d ago    │   │
│  └─────────────────────────────────────────────┘   │
│                                                    │
│  ─── Akce ───                                      │
│  [ 🔄 Pull ]  [ ⬆ Push ]  [ 🌿 Branch ▾ ]         │
│  [ 🔀 Merge ]  [ 📊 Diff vs HEAD ]                 │
└───────────────────────────────────────────────────┘
```

**Co se mění:**
- ✅ Změny nahoře s jasnými stage tlačítky (`[S]`)
- ✅ Commit message + akce jsou viditelné bez scrollu
- ✅ Historie commitů je kompaktní, ne dominantní
- ✅ Branch operace v dropdown, ne v separátním dialogu
- ✅ Conflict resolve flow (pokud nastane) nahradí hlavní plochu jasným merge editorem

---

### Tab 6 — Release

**Účel:** Od buildu k vydání — export, validace, changelog, publish příprava.

**Současný problém:**
- Release flow je lineární seznam tlačítek bez vizuálního průběhu
- Není jasné kde v procesu jsem a co ještě zbývá

**Nový layout — Release Pipeline:**

```
┌──────────────────────────────────────────────────────┐
│  RELEASE                                              │
│                                                       │
│  ─── Pipeline ──────────────────────────────────────  │
│                                                       │
│  [✅ Version]→[✅ Snapshot]→[⏳ Validate]→[○ Notes]→[○ Publish]│
│                                                       │
│  Aktuální krok: Validate                              │
│                                                       │
│  ┌────────────────────────────────────────────────┐   │
│  │  🔍 Validace                                   │   │
│  │                                                │   │
│  │  ✅ Metadata kompletní                         │   │
│  │  ✅ Logo nastaveno                             │   │
│  │  ✅ 147 modů — žádné konflikty                 │   │
│  │  ⚠️ Cover chybí (doporučeno)                   │   │
│  │  ✅ Loader target: NeoForge 21.4.62            │   │
│  │  ✅ Velikost: 234 MB                           │   │
│  │                                                │   │
│  │  [ ▸ Pokračovat ]                              │   │
│  └────────────────────────────────────────────────┘   │
│                                                       │
│  ─── Export Profily ──────────────────────────────    │
│                                                       │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐        │
│  │ .voidpack  │ │ CurseForge │ │ .mrpack    │        │
│  │ ✅ ready   │ │ ⚠️ cover   │ │ ✅ ready   │        │
│  │ [Export]   │ │ [Export]   │ │ [Export]   │        │
│  └────────────┘ └────────────┘ └────────────┘        │
│                                                       │
│  ┌────────────┐                                       │
│  │ GitHub     │                                       │
│  │ ○ not set  │                                       │
│  │ [Setup]    │                                       │
│  └────────────┘                                       │
│                                                       │
│  ─── Historie Releasů ──────────────────────────     │
│                                                       │
│  v1.2.0  stable    2026-04-01  147 mods  📦 .voidpack│
│  v1.1.9  playtest  2026-03-28  145 mods  📦 .mrpack  │
│  v1.1.8  stable    2026-03-20  143 mods  📦 all      │
│                                                       │
│  [Diff v1.2.0 vs v1.1.9]  [Rollback ▾]               │
└──────────────────────────────────────────────────────┘
```

**Co se mění:**
- ✅ **Pipeline vizualizace** — progress bar ukazuje kde jsi v procesu
- ✅ Validace jako checklist, ne jako modální dialog
- ✅ Export profily jako karty vedle sebe — jasný status každého cíle
- ✅ Release historie s quick diff a rollback
- ✅ Changelog generátor je součástí pipeline kroku "Notes"

**Pipeline krok: Notes / Changelog Generator:**

```
┌────────────────────────────────────────────────────┐
│  📝 Release Notes & Changelog                      │
│                                                    │
│  ┌──────────────────┬─────────────────────────────┐│
│  │ 🤖 Auto-generate │ ✏️ Ruční editace             ││
│  └──────────────────┴─────────────────────────────┘│
│                                                    │
│  Zdroj:  [Diff vs v1.1.9 ▾]  [Commit historie ▾]  │
│                                                    │
│  ## v1.2.0 — Balance Update                        │
│                                                    │
│  ### 🆕 Přidáno                                    │
│  - Create 0.5.1f                                   │
│  - 3 nové KubeJS recepty pro steel tier            │
│                                                    │
│  ### 🔧 Změněno                                    │
│  - Nether gate posunut za boss #2                  │
│  - Steel blend cost zvýšen z 4 na 8                │
│                                                    │
│  ### 🗑️ Odebráno                                   │
│  - Optifine (nahrazen Iris + Sodium)               │
│                                                    │
│  ─── Export formáty ───                            │
│  [📋 Markdown] [💬 Discord Post] [🐙 GitHub Notes] │
│  [📧 Tester Brief] [📄 Launcher Changelog]         │
└────────────────────────────────────────────────────┘
```

**Pipeline krok: QA Gate:**

```
┌────────────────────────────────────────────────────┐
│  🔒 QA Gate                                        │
│                                                    │
│  ─── Automatické kontroly ───                      │
│  ✅ Manifest validní                               │
│  ✅ Všechny mody kompatibilní s MC 1.21.4          │
│  ✅ Žádné duplikátní mod ID                        │
│  ✅ Export profil splňuje CF/MR požadavky           │
│  ⚠️ 2 mody bez explicitní licence (info)           │
│                                                    │
│  ─── Manuální smoke test ───                       │
│  ☐ Hra se spustí bez crash                         │
│  ☐ Hlavní quest linka funguje                      │
│  ☐ Nether/End dimension load ok                    │
│  ☐ Performance > 40 FPS na doporučeném HW          │
│  ☐ Known issues zapsány                            │
│                                                    │
│  ─── Playtest ───                                  │
│  [ ▶ Spustit Playtest Build ]                      │
│  Vytvoří izolovanou kopii pro testování.            │
│  Posledný playtest: v1.2.0-rc.1, 2h ago            │
│                                                    │
│  ─── Feedback ───                                  │
│  [ 📥 Feedback Inbox (3 nové) ]                    │
│  Crash reporty, bug reporty od testerů.            │
│                                                    │
│  [ ✅ Schválit release ] [ ❌ Blokovat ]            │
└────────────────────────────────────────────────────┘
```

---

## 🎛️ Right Dock — Copilot Desk Redesign

**Současný problém:**
- Copilot Desk ukazuje příliš mnoho status informací
- Chybí jasná interakční plocha — vypadá spíš jako info panel než pracovní desk

**Nový layout:**

```
┌─────────────────────┐
│  COPILOT DESK       │
│                     │
│  ┌─────────────────┐│
│  │ 💬 Chat         ││
│  │                 ││
│  │ "Vysvětli tuto  ││
│  │  config změnu"  ││
│  │                 ││
│  │ [____________]  ││
│  │ [Odeslat]       ││
│  └─────────────────┘│
│                     │
│  ─── Kontext ───    │
│  📍 Tab: Files      │
│  📄 server.props    │
│  🌿 main ↑2        │
│  ◉ 3 dirty          │
│                     │
│  ─── Akce ───       │
│  [Explain]          │
│  [Find Issue]       │
│  [Suggest Fix]      │
│  [Apply Patch]      │
│  [Summarize Diff]   │
│                     │
│  ─── Quick ───      │
│  [📝 Notes Drawer]  │
│  [🔀 Context Dock]  │
└─────────────────────┘
```

**Klíčové změny:**
- ✅ **Chat je primární** — nahoře, ne schovaný
- ✅ Kontext je kompaktní — jen 4 řádky, ne celý manifest dump
- ✅ AI akce jsou kontextové podle aktivního tabu
- ✅ Quick switch na Notes Drawer a Context Dock dole

**Kontextová adaptace AI akcí:**

| Aktivní Tab | AI Akce |
|-------------|---------|
| Identity | Generate summary, Translate, Improve description |
| Mods | Find conflicts, Suggest alternatives, Explain mod |
| Files | Explain config, Find issue, Rewrite, Apply patch |
| Notes | Expand note, Generate quest tree, Convert to changelog |
| Git | Explain diff, Generate commit message, Review changes |
| Release | Generate changelog, Write Discord post, Validate |

---

## 🎨 Vizuální Systém

### Barevné Kódování Stavů

| Stav | Barva | Použití |
|------|-------|---------|
| ✅ OK / Hotovo | `#1BD96A` (green) | Validace ok, soubor uložen, export ready |
| ⚠️ Pozornost | `#F5A623` (amber) | Update dostupný, doporučení, warning |
| ❌ Chyba | `#E5534B` (red) | Konflikt, validační error, failed |
| ○ Čeká | `#5B6078` (dimmed) | Nest nastaven, neaktivní krok |
| ⏳ Probíhá | `#0066FF` (blue) | Aktuální krok, loading |
| 📌 Připnuto | `#BD93F9` (purple) | Favorit, featured, pinned |

### Typografie a Spacing

| Element | Styl |
|---------|------|
| Tab headers | 13px Semi-Bold, `#E2E8F0` |
| Section headers | 12px Bold Uppercase, `#8892B0`, letter-spacing 0.5px |
| Body text | 13px Regular, `#E2E8F0` |
| Muted labels | 12px Regular, `#5B6078` |
| Status badges | 11px Medium, pill shape, 4px padding |
| Card spacing | 12px gap between cards, 16px padding inside |
| Section spacing | 24px between major sections |

### Ikony

Konzistentní sada ikon pro celý Workbench:

```
📁 Složka     📄 Soubor     📝 Editace    💾 Uložit
🔍 Hledat     ⚡ Quick Act  🌿 Branch     📦 Export
🔗 Odkaz      📊 Stats      📋 Seznam     🗑️ Smazat
⬆️ Upload     ⬇️ Download   🔄 Refresh    ⚙️ Config
```

---

## ⌨️ Klávesové Zkratky

| Zkratka | Akce |
|---------|------|
| `Ctrl+1` … `Ctrl+7` | Přepnutí tabu |
| `Ctrl+N` | Otevřít Notes Drawer |
| `Ctrl+Shift+N` | Nová poznámka |
| `Ctrl+S` | Uložit aktuální soubor / manifest |
| `Ctrl+Shift+S` | Uložit vše |
| `Ctrl+P` | Quick open soubor (strom) |
| `Ctrl+G` | Git commit dialog |
| `Ctrl+Shift+G` | Git push |
| `Ctrl+E` | Quick export (.voidpack) |
| `Ctrl+D` | Diff aktuálního souboru |
| `Ctrl+/` | Focus Copilot Desk chat |
| `Escape` | Zavřít drawer / overlay |

---

## 📐 Responzivní Chování

| Šířka okna | Chování |
|------------|---------|
| **> 1400px** | Plný layout: Tab bar + Main + Right Dock |
| **1000–1400px** | Right Dock se sbalí do overlay panelu (toggle tlačítko) |
| **< 1000px** | Tab bar přejde na ikony, Right Dock jako drawer |

---

## 🔄 Migrace ze Současného Stavu

### Co zůstává beze změny
- Celá service vrstva (`CreatorWorkspaceService`, `CreatorManifestService`, atd.)
- Datové modely a kontrakty
- `creator_manifest.json` schema
- Editor Host mechanika (Structured/Raw/Split/Diff)
- Snapshot a export pipeline

### Co se mění v UI
| Oblast | Změna |
|--------|-------|
| **Workspace Header** | Kompaktnější, status badges, quick actions dropdown |
| **Tab: Metadata** | Přejmenován na Identity, rozdělen na Profile + Branding sub-taby |
| **Tab: Overview** | Odstraněny session akce (→ header), přidána timeline a quick links |
| **Tab: Mods** | Přidána sekce "Pozornost" nahoře pro updaty a konflikty |
| **Tab: Files** | Přidán breadcrumb, validační badge, quick filter ve stromu |
| **Tab: Notes** | Čtyři sub-režimy: Docs, Wiki, Canvas, Mind Map |
| **Tab: Git** | Čistší stage/commit flow, kompaktní historie |
| **Tab: Release** | Pipeline vizualizace, export jako karty, release historie |
| **Copilot Desk** | Chat first, kompaktní kontext, kontextové akce |

### Implementační pořadí redesignu

1. **Shell + Header** — nový header layout, quick actions, status badges
2. **Tab přejmenování + routing** — Metadata → Identity, keyboard shortcuts
3. **Overview cleanup** — timeline, quick links, odstranění session bloků
4. **Identity split** — Profile + Branding sub-taby
5. **Mods "Pozornost"** — attention section nahoře
6. **Files polish** — breadcrumb, filter, badges
7. **Release pipeline** — vizuální progress, export karty
8. **Copilot Desk** — chat-first layout, kontextové akce
9. **Notes sub-režimy** — Docs / Wiki / Canvas / Mind Map
10. **Git polish** — stage flow, kompaktní historie
11. **Keyboard shortcuts** — globální binding
12. **Responsive** — breakpointy a sbalování

---

## 🔍 Globální Workspace Search

**Command Palette styl — `Ctrl+Shift+P` nebo `Ctrl+K`:**

```
┌──────────────────────────────────────────────────┐
│  🔍 [search across everything____________]       │
│                                                  │
│  ─── Soubory ───                                 │
│  📄 config/create/server.json          Files     │
│  📄 kubejs/recipes/steel.js            Files     │
│                                                  │
│  ─── Mody ───                                    │
│  📦 Create v0.5.1f                     Mods      │
│  📦 Create: Steam & Rails              Mods      │
│                                                  │
│  ─── Poznámky ───                                │
│  📝 Balance Notes > Tier 3 Recipes     Notes     │
│  📝 Known Issues > Create compat       Notes     │
│                                                  │
│  ─── Git ───                                     │
│  🌿 commit: "steel balance update"     Git       │
│                                                  │
│  ─── Manifest ───                                │
│  ⚙️ primary-server: void-craft.eu      Identity  │
└──────────────────────────────────────────────────┘
```

- Hledá napříč: soubory, mody, notes, canvas nody, git commity, manifest
- Výsledky jsou groupované podle tabu / zdroje
- Enter otevře přímo na správném tabu s focus na výsledek
- Klávesová zkratka: `Ctrl+K` (quick search) nebo `Ctrl+Shift+F` (full search)

---

## 🆕 Empty States a Onboarding

Každý tab musí mít **příjemný empty state**, který říká co dělat dál — ne prázdnou plochu.

| Tab | Empty State | CTA |
|-----|------------|-----|
| **Overview** (žádný workspace) | Ilustrace + "Vyber workspace nebo vytvoř nový" | `[+ Nová instance]` `[Import pack]` `[Clone Git]` |
| **Identity** (chybí manifest) | "Tvůj pack ještě nemá identitu" | `[Vygenerovat manifest]` |
| **Mods** (prázdná instance) | "Zatím žádné mody" | `[+ Hledat mody]` `[📂 Přidat .jar]` |
| **Files** (žádný vybraný soubor) | Strom vlevo, vpravo: "Vyber soubor pro editaci" | Strom souborů je vždy viditelný |
| **Notes** (žádné dokumenty) | "Začni psát poznámky k vývoji packu" | `[+ Design Notes]` `[+ TODO]` `[+ Balance Notes]` |
| **Git** (neinicializováno) | "Workspace zatím není verzovaný ani připojený ke GitHubu" | `[Přihlásit GitHub]` `[Init repo]` `[Clone]` `[Link existující]` |
| **Release** (žádný export) | "Připrav svůj první build" | `[Začít release pipeline]` |

**First-Time Onboarding Flow:**

```
┌──────────────────────────────────────────────────┐
│  👋 Vítej v Creator Workbench!                    │
│                                                  │
│  1. ▶ Vyber nebo vytvoř modpack workspace        │
│  2. 📝 Nastav identitu a branding                │
│  3. 📦 Přidej mody a uprav konfiguraci           │
│  4. 🌿 Přihlas GitHub a připoj repozitář         │
│  5. 🚀 Připrav první release                     │
│                                                  │
│  [ Začít ]  [ Přeskočit — znám to ]              │
└──────────────────────────────────────────────────┘
```

- Zobrazí se jen při prvním otevření Creator Workbench
- Každý krok odkáže na správný tab
- Po dokončení nebo přeskočení se už neukáže

---

## ⚠️ Error, Offline a Degraded States

Workbench musí fungovat i když nejde všechno.

| Situace | Chování | Indikace |
|---------|---------|----------|
| **Žádný internet** | Mods: skryj search, ukaž jen local. Git: skryj push/pull. Copilot: "AI nedostupná" | 🔴 pill badge v headeru |
| **Git není nainstalován** | Git tab: empty state s linkem na instalaci | ⚠️ "Git binary nenalezen" |
| **Copilot nedostupný** | Right dock: zůstane context info, chat disabled | Dimmed chat + info box |
| **Manifest poškozen** | Identity: warning banner + nabídka regenerace | ❌ červený banner nahoře |
| **Soubor nelze parsovat** | Files: automatický fallback do Raw režimu | ⚠️ badge "Parse error → Raw" |
| **Git conflict** | Git tab: nahradí normální view conflict resolve UI | 🔴 červený status v headeru |
| **Export selže** | Release: error detail + retry + fallback tipy | ❌ karta exportu zčervená |
| **Snapshot full** | Overview: warning o stáří / velikosti snapshotů | ⚠️ info v Overview |

**Pravidla pro error handling:**
- Nikdy tichý fail — vždy ukaž co se stalo a co s tím
- Error toast s `[Detaily]` a `[Opakovat]` tlačítky
- Degraded mode > úplné zablokování funkce
- Restore/recovery akce vždy na dosah (max 1 klik)

---

## 🔔 Notifikace a Undo Systém

### Toast Notifikace

```
┌────────────────────────────────────────┐
│  ✅ Export .voidpack dokončen           │
│     234 MB · 147 modů · v1.2.0        │
│     [ Otevřít složku ]  [ ✕ ]         │
└────────────────────────────────────────┘
```

| Typ | Barva | Trvání | Příklad |
|-----|-------|--------|---------|
| ✅ Success | green | 5s auto-hide | Export hotov, commit odeslán |
| ⚠️ Warning | amber | 10s | Soubor má validační warningy |
| ❌ Error | red | persistent | Export selhal, git conflict |
| ℹ️ Info | blue | 5s | Nový update modu dostupný |

- Toasty se zobrazují vpravo dole nad Copilot Desk
- Max 3 najednou, starší se archivují do notification center
- Notification center: `🔔` ikona v headeru s badge počtem

### Undo Systém

| Akce | Undo možnost |
|------|-------------|
| Editace souboru | `Ctrl+Z` klasický undo v editoru |
| Smazání modu | Toast s `[↩ Vrátit]` po dobu 10s |
| Commit | Revert commit v Git tabu |
| AI Apply Patch | Snapshot + `[↩ Revert]` v diff preview |
| Export | Export je read-only, undo nepotřeba |
| Manifest editace | `[↩ Vrátit změny]` tlačítko vedle Save |
| Notes smazání | Toast s `[↩ Obnovit]`, 30s window |

- **Velké operace** (AI multi-file patch, bulk mod remove): vždy automatický snapshot před apply
- **Snapshot restore**: dostupný z Overview → Snapshots

---

## 📎 Context Menu a Drag & Drop

### Pravý klik — kontextové akce:

| Kde | Akce v menu |
|-----|------------|
| **Strom souborů** | Otevřít · Otevřít v Raw · Diff · Kopírovat path · Přejmenovat · Smazat |
| **Mod v seznamu** | Update · Config · Changelog · Disable/Enable · Odebrat · Ukázat závislosti |
| **Note v sidebaru** | Otevřít · Přejmenovat · Duplikovat · Exportovat · Smazat |
| **Canvas node** | Editovat · Link na soubor · Změnit typ · Odpojit · Smazat |
| **Git change** | Stage · Unstage · Diff · Revert · Otevřít soubor |
| **Release snapshot** | Diff · Rollback · Vytvořit playtest · Stáhnout · Smazat |

### Drag & Drop:

| Co | Kam | Efekt |
|----|-----|-------|
| Obrázek z Exploreru | Branding drop zone | Upload jako asset (logo/cover/...) |
| `.jar` soubor | Mods tab | Přidat jako lokální mod |
| Soubor ze stromu | Canvas node | Vytvořit link z nodu na soubor |
| Soubor ze stromu | Notes editor | Vložit `🔗 link` na soubor |
| Mod z listu | Jiný mod | Zobrazit dependency vztah |
| Tab index | Tab bar | Přeuspořádat pořadí tabů (persistent) |

---

## ♿ Accessibility (A11y)

| Oblast | Požadavek |
|--------|-----------|
| **Kontrast** | Min 4.5:1 pro text, 3:1 pro UI elementy (WCAG AA) |
| **Focus ring** | Viditelný focus indicator pro keyboard navigaci |
| **Tab order** | Logický tab order: Header → Tab bar → Main → Right dock |
| **Screen reader** | ARIA labels pro všechny interaktivní elementy |
| **Reduced motion** | Respektovat OS preference — žádné animace pokud disabled |
| **Zoom** | Funkční layout při 125% a 150% system DPI |
| **Klávesnice** | Vše dostupné bez myši (taby, akce, drawer, dialogy) |

---

## ✅ Definition of Done

Creator Workbench UX Redesign je hotový, když:

- [ ] Žádný tab nevyžaduje scroll přes víc než 1.5 obrazovky
- [ ] Každý tab má jasnou primární akci viditelnou bez scrollu
- [ ] Header ukazuje identitu a stav workspace na první pohled
- [ ] Přepínání mezi taby neruší kontext (Copilot Desk + header zůstávají)
- [ ] Notes drawer je dostupný z jakéhokoliv tabu přes `Ctrl+N`
- [ ] Všechny stavy (ok, warning, error, pending) mají konzistentní barvy
- [ ] Keyboard shortcuts fungují pro core workflow
- [ ] Responsive layout funguje od 1000px šířky
- [ ] Žádná existující funkce nebyla odstraněna, jen reorganizována
