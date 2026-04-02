# VOID-CRAFT Launcher 3.1.7

## Discord Announcement

```md
## VOID-CRAFT Launcher 3.1.7 je venku

Tohle je cileny hotfix po 3.1.6.
Resi hlavne neprijemny regression, kdy launcher mohl v nekterych stavech spustit hru jako `Guest`, i kdyz byl v UI aktivni Microsoft ucet.

### Co opravuje

- **Spravny ucet pri startu hry**
  Launcher si pred spustenim znovu overi session z aktivniho uctu a Microsoft launch uz nepusti do ticheho guest fallbacku.

- **Spolehlivejsi obnova ulozeneho uctu**
  Auto-login a account recovery ted drzi spravne MSAL mapovani i pri vice ulozenych uctech nebo starsi token cache.

- **Optimalizace odpovidaji UI**
  Nastaveni optimalizacnich flagu a GC v detailu instance ted odpovida tomu, co se skutecne pouzije pri launchi.

- **Stabilnejsi release notifikace**
  Discord release webhook v CI uz nespadne potichu na prilis dlouhem changelogu; release text se zkrati na bezpecnou delku a chyba webhooku je videt.

### Co ted

Pokud jsi v 3.1.6 narazil na start hry jako `Guest` i pri prihlasenem uctu, prejdi na **3.1.7**.
Je to maly patch, ale opravuje presne ten typ chyby, ktery kazi duveru v launcher.
```

## Short Variant

```md
## VOID-CRAFT Launcher 3.1.7 je venku

Hotfix po 3.1.6 resi hlavne regression, kdy se hra mohla spustit jako `Guest` i pri aktivnim Microsoft uctu.

Hlavni body:
- opraveny launch session recovery z aktivniho uctu
- stabilnejsi MSAL account recovery
- optimalizacni flagy v detailu instance sedi s realnym runtime stavem
- tvrdsi a bezpecnejsi Discord release webhook v CI

Pokud te 3.1.6 nekdy pustila do hry jako Guest, 3.1.7 je ten hotfix.
```

## Notes

- Text je psany tak, aby sel rovnou vlozit do Discordu.
- Tohle je hotfix release, ne dalsi velky feature drop.
- Dava smysl komunikacne navazat na 3.1.6 jako opravu dulezite regresni chyby.