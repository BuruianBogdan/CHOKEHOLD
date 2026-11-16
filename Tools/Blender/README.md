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

## Piese nucleare (v2)

| Piesă | Dimensiuni (m) | Unde ajunge |
|---|---|---|
| `K17_Reactor_Vessel` | Ø 4 × 4 | `A_Tank_Main`: vasul reactorului din sala A |
| `K17_Control_Rod_Drive` | Ø 3 × 2,1 | decor atârnat din tavan deasupra reactorului |
| `K17_Pressurizer` | Ø 3,5 × 3,4 | `A_Tank_Small` |
| `K17_Coolant_Tank` | Ø 3 × 3,2 | `Pumps_Tank_*` |
| `K17_Coolant_Pump` | 4 × 2,5 × 4 | `A_Generator`, `Pumps_Machine`, `Yard_Generator` |
| `K17_Turbine` | 6 × 3,6 × 8 | `Hangar_Machine` |
| `K17_Control_Panels` | 6 × 2,2 × 3 | `Lab_ServerRack` |
| `K17_Hazmat_Lockers` | 1 × 2,2 × 4 | `Security_Lockers`, cu ușile spre cameră |
| `K17_Shelf` | 1 × 2,2 × 2, segment | `*_Shelf*`, în șir pe lungime |
| `K17_Rubble` | 1 × 1 × 1 | stâncile (`*_Rock*`), întinse pe cutia lor |
| `K17_Sign_Radiation` | 0,5 × 0,6 | decor: lângă uși |
| `K17_Floor_Trefoil` | Ø 3 | decor: pe podea, lângă reactor |
| `K17_Fuel_Pool` | 6 × 4 | decor: bazinul cu grătar din sala reactorului |
| `K17_Beacon` | — | decor: girofar cu lumină rotitoare (`BeaconSpin`) la intrările în site-uri |
| `K17_Emergency_Light` | — | decor: lămpi roșii în tuneluri și în buncăr |
| `K17_Pipe_Run` | 2 m, segment | decor: conducte pe pereții halelor (6 m) și ai sălilor industriale (2,75 m) |
| `K17_Decon_Arch` | ușă de 1,4 × 2,4 | decor: cadre de decontaminare la ușile de lângă spawn-uri |

Sloturile `*Glow` (`ReactorGlow`, `PoolGlow`, `BeaconGlow`, `EmergencyGlow`, `ScreenGlow`) au emisie în Unity.

Decorul îl plasează `BuildNuclearDressing` din `BlacksiteK17MapBuilder.Nuclear.cs` și nu are collider.

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

## Piese v3 (interioare, acoperișuri, exterior)

| Piesă | Unde ajunge |
|---|---|
| `K17_Console` | toate mesele (`Crate_*`): laborator, birouri, camera de control; segmente de 2 m |
| `K17_HVAC` | `Roof_HVAC_*`: unități de ventilație (cover) pe acoperișul laboratorului |
| `K17_Pipe_Big`, `K17_Pipe_Cap`, `K17_Pipe_Collar` | țevile mari (`*_Pipe_*`): tronsoane cu flanșe, iar la capete flanșă oarbă sau trecere prin perete; suporturi spre tavan sau podea |
| `K17_Pipe_Run_End`, `K17_Pipe_Run_Collar` | capetele șirurilor de țevi de pe pereți: cot în perete sau trecere în peretele de colț |
| `K17_Ladder_Section`, `K17_Ladder_Top` | scările de mână spre acoperișuri (componenta `Ladder`) |
| `K17_Downpipe_*`, `K17_Electrical_Box`, `K17_Wall_Vent`, `K17_Wall_Lamp` | fațadele exterioare |
| `K17_Door_Panel`, `K17_Window_Panel`, `K17_Roll_Door` | clădirile pline (turnul de pază, magazia din curte) |
| `K17_Truck`, `K17_Floodlight`, `K17_Razor_Wire` | spawn-uri, curți, coronamentul zidului de incintă |
| `K17_Transformer`, `K17_Guard_Hut` | `Yard_Generator`, `Outpost_Shed` |

Piese refăcute:
- **`K17_Coolant_Pump`** are două pompe centrifuge, cu motor pe același ax, colector comun și aspirație prin podea.
- **`K17_Barrier_High`** e acum un bastion din coșuri de plasă umplute cu pământ.
- **`K17_Rubble`** e acum o stivă de plăci de beton prefabricate.

## Texturi PBR (`k17_textures.py`)

```
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_textures.py
```

Scriptul generează din cod 15 seturi de texturi. Fiecare are 1024 × 1024 pixeli și se repetă fără cusătură. Ies în `Assets/_Project/Art/Textures/K17`, fiecare set în trei fișiere:

| Fișier | Conținut |
|---|---|
| `<Set>_A` | culoarea, aproape neutră: nuanța vine din material |
| `<Set>_N` | normal map |
| `<Set>_M` | R = metalic, A = luciu |

Seturile sunt:
- **beton:** Concrete, ConcreteDark, ConcreteFloor;
- **pentru exterior și acoperișuri:** Asphalt, RoofMembrane, Gravel;
- **finisaje interioare:** Plaster, LabTiles;
- **metale:** PaintedMetal, Steel, CheckerPlate;
- **lemn, materiale textile și cauciuc:** Wood, Hesco, Canvas, Rubber.

`ApplySurfaces` din `BlacksiteK17MapBuilder.Surfaces.cs` leagă fiecare material al hărții de un set de texturi. Tiling-ul se calculează în metri, pentru că UV-urile sunt în metri.
