# VOID-CRAFT Launcher 3.1.9

## Discord Announcement

```md
## VOID-CRAFT Launcher 3.1.9 je venku

Tohle je Creator Studio release, ktery dotahuje publish flow z "funguje nekdy" na pouzitelny workflow od exportu az po GitHub release a VOID Launcher list.

### Co opravuje

- **Jedno misto pro auth**
  GitHub a VOID ID uz nejsou rozhazene po nekolika creator panelech. Publish flow vychazi z jednotneho auth/context mista.

- **Spolehlivejsi session restore**
  VOID ID a GitHub session se po restartu nerozsypou kvuli soubeznym zapisum a VOID ID token se pred registry publish znovu overi nebo refreshne.

- **Publish uz nelaguje launcher**
  Creator git operace uz nebezi jako fake async na UI threadu. Stage/publish flow je realne asynchronni a scoped commit nepada na Windows limit dlouhych argumentu.

- **GitHub workflow opravdu dojede do releasu**
  Upload flow vytvori a pushne release tag, workflow si umi zapsat assety a kdyz release zustal jako draft, automaticky ho publikuje.

- **Branding konecne jde ven s releasem**
  Verejne logo importovaneho packu se umi samo propsat do `assets/branding` a branding assety jdou ted do `.voidpack`, do repa/tagu i do raw URL pro VOID Registry.

- **Bezpecnejsi export**
  Lokální `.voidpack` export uz nepadne jen proto, ze cilovy archiv uz existuje.

### Co ted

Pokud delas pack ve Creator Studiu, prejdi na **3.1.9**.
Je to presne ta verze, ktera narovnava release flow kolem `.voidpack`, GitHub releasu a registry publish.
```

## Short Variant

```md
## VOID-CRAFT Launcher 3.1.9 je venku

Creator Studio dostalo velky publish hardening.

Hlavni body:
- GitHub + VOID ID auth na jednom miste
- spolehlivejsi restore session a refresh VOID ID tokenu pred registry publish
- real async git flow bez lagu pri stage/publish
- auto-publish draft GitHub releasu
- branding assety jdou konecne do `.voidpack`, repa i registry

Pokud stavis pack pres Creator Studio, 3.1.9 je release, ktery ma smysl nasadit.
```

## Notes

- Text je psany tak, aby sel rovnou vlozit do Discordu.
- Komunikacne jde o Creator Studio publish hardening release, ne o dalsi velky shell redesign.
- Dava smysl navazat na 3.1.8.1 jako dalsi konkretni produkcni hotfix pro authoring a release flow.