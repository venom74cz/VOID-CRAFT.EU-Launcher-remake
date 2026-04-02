@ -1,66 +0,0 @@
# VOID-CRAFT Launcher 3.1.0

## Discord Announcement

```md
## VOID-CRAFT Launcher 3.1.0 je venku

Verze **3.1.0** dotahuje z launcheru mnohem realnejsi modpack authoring workspace.
Nejde o dalsi kosmeticky patch, ale o prvni silnou Creator Studio fazi postavenou tak, aby se v launcheru dalo zalozit a rozjet novy pack bez zbytecneho prepinani do dalsich aplikaci.

### Co je nove

- **Creator Studio workflow shell**
  Creator uz neni jedna dlouha stranka. Ma vlastni workflow zalozky `Overview`, `Metadata`, `Mods`, `Files`, `Notes`, `Git` a `Release`.

- **creator_manifest.json jako source of truth**
  Metadata packu se ted drzi v jednom jasnem kontraktu a launcher je umi nacist, upravit i ulozit primo v Creator Studiu.

- **Bootstrap noveho modpacku**
  Wizard ted umi realne varianty `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git` a `Restore Snapshot`.

- **Template a workspace baseline**
  Template presety nejsou jen volba v UI. Pri zalozeni umi pripravit `docs`, `notes`, `qa` a `quests` baseline soubory pro dalsi praci.

- **Copilot Desk a Notes Drawer**
  Creator rezim ma vlastni pravy pracovny sloupec a rychly notes drawer pro handoff, planning a dalsi workflow navazujici na aktualni workspace.

### Co se zlepsilo technicky

- centralizovany creator workspace context a shell state
- metadata sync zpet do launcheroveho modelu a headeru instance
- snapshot-before-apply guard pro vetsi creator zapis
- srovnana release metadata, changelog a README na jeden aktualni release

### Proc je to dulezite

Cilem je, aby launcher nebyl jen misto pro hrani a spravu instanci, ale postupne i **modpack workspace od bootstrapu po release**.
Verze 3.1.0 je prvni krok, kde je tenhle smer v Creator Studiu videt v realnem produkcnim flow.

### Co ted

Pokud launcher uz mate, doporucuju prejit na **3.1.0**.
Nejvic me ted zajima feedback na Creator Studio workflow, bootstrap flow nove instance a to, jestli metadata shell davaji smysl i pri realne praci na packu.
```

## Short Variant

```md
## VOID-CRAFT Launcher 3.1.0 je venku

Nova verze posouva **Creator Studio** z utility panelu na realnejsi modpack workspace.

Hlavni novinky:
- workflow shell pro Creator Studio
- `creator_manifest.json` metadata flow
- bootstrap varianty `Blank`, `Template`, `Import CF`, `Import MR`, `Clone Git`, `Restore Snapshot`
- realne template baseline soubory
- Copilot Desk a Notes Drawer

Feedback na creator workflow sem s nim.
```

## Notes

- Text je zamerne psany tak, aby sel rovnou vlozit do Discordu.
- Tahle verze komunikacne navazuje na release 3.0.0 a soustredi se na Creator Studio F0/F1 slice.