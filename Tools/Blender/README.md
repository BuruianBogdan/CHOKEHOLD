# Kit modular Blacksite K-17 (Blender)

`k17_kit.py` generează din cod piesele kitului, le exportă în FBX pentru Unity și randează imagini de previzualizare.

## Rulare

Din rădăcina repo-ului (Blender 5.x):

```
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_kit.py
```

Opțiuni după `--`:

- `--no-preview` sare peste randări;
- `--no-blend` nu salvează `K17_Kit.blend`.

Scriptul produce:

| Ce | Unde |
|---|---|
| Piesele FBX | `Assets/_Project/Art/Models/Kit/K17_*.fbx` |
| Previzualizări | `Docs/Kit/K17_Kit_*.png` |
| Fișier Blender, pentru inspecție sau retușuri | `Tools/Blender/K17_Kit.blend` |

După rulare, în Unity: **CHOKEHOLD → Map → Build Blacksite K-17**.

## Piese (v1)

| Piesă | Dimensiuni (m) | Înlocuiește în generator |
|---|---|---|
| `K17_Crate_Big` | 2 × 2 × 2 | `BigCrate_*` |
| `K17_Crate_Stack` | 2 × 3,5 × 2 | `CrateStack_*` |
| `K17_Barrier_Low` | 2 × 1,1 × 0,6, segment | `LowCover_*`, în șir pe lungime |
| `K17_Barrier_High` | 2 × 2 × 0,8, segment | `HighCover_*`, în șir pe lungime |
| `K17_Container_20` | 2,6 × 2,6 × 6 | `Container_*`, își păstrează culoarea |
| `K17_Container_40` | 2,6 × 2,6 × 12 | `Container40_*` |
| `K17_Door_Leaf` | 0,15 × 2,4 × 1,4, pivot în balama | canatul ușilor (`SimpleDoor`) |
| `K17_Pillar_Bunker` | 1,2 × 5,5 × 1,2 | `Bunker_Pillar_*` |
| `K17_Barrel` | Ø 0,6 × 0,9 | decor, încă neplasat |
| `K17_Pallet` | 1,2 × 0,14 × 1,0 | decor, încă neplasat |

## Reguli

- **Coordonate.** În script se folosesc coordonatele Unity: x la dreapta, y în sus, z înainte, în metri. Conversia spre Blender e făcută de funcția `U()`.
- **Pivot.** Fiecare piesă are pivotul la bază, în centrul amprentei. Excepție face ușa, cu pivotul în balamaua de pe +Z.
- **Doar partea vizuală.** O piesă înlocuiește doar ce se vede. Colliderul, numele și poziția cutiei din generator rămân la fel, deci layout-ul și validatorul nu se schimbă. Dacă lipsește un FBX, rămâne cutia gri.
- **UV-uri.** Sunt proiectate pe cutie, cu 1 unitate UV = 1 m. UV2 pentru lightmap îl generează Unity la import (`KitModelImporter`).
- **Materiale.** Sloturile au nume fixe: `Wood`, `WoodDark`, `Concrete`, `ConcreteDark`, `Steel`, `DoorPaint`, `Glass`, `BarrelPaint`, `Hazard`, `Container`.
  - Unity le leagă de `Assets/_Project/Art/Materials/Kit/K17_<slot>.mat`.
  - Aceste materiale se creează o singură dată; după aceea le poți texturiza liber, iar generatorul nu le mai suprascrie.
  - Slotul `Container` primește materialul roșu sau albastru al containerului.
- **Piese noi.** O piesă nouă înseamnă o funcție `piece_*` în script, adăugată în `PIECES`, plus o regulă în `ApplyKit` din `BlacksiteK17MapBuilder.Kit.cs`.
