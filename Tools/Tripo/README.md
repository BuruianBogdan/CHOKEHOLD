# Modele din Tripo

Aici intră modelele generate în Tripo Studio. Pune fișierele descărcate în `Inbox`, iar ele trec prin pipeline-ul proiectului:
- scară reală și orientare;
- buget de triunghiuri și LOD-uri;
- schelet K17, animații și hitbox-uri, la personaje;
- collidere și așezare din generator, la recuzită.

După integrare, fișierul original se mută în `Source` cu numele personajului (de exemplu `Source/CT_Operator.zip`). `Tools/Blender/k17_tripo.py` refăce personajul oricând din el.

| Folder | Ce pui acolo |
|---|---|
| `Inbox/Characters` | personaje întregi |
| `Inbox/Props` | recuzită pentru hartă, câte un obiect pe fișier |
| `Inbox/Weapons` | arme, câte o arhivă pe armă |
| `Source` | se umple singur, nu pui nimic aici |

## Cum generezi

- **Plan:** generează doar cu Pro activ. Ce faci pe Free e public și necomercial.
- **Model:** HD Model, cu texturi. Rig-ul, animațiile și retopo-ul lor nu sunt necesare.
- **Export:** GLB, de preferat, sau FBX cu texturile incluse. Dacă vine o arhivă `.zip`, o pui așa cum e.
- **Nume:** exact numele din tabelele de mai jos, de exemplu `T_Insurgent_A.glb`. Dacă ai pornit de la o imagine, pune-o lângă model cu sufixul `_ref`, de exemplu `T_Insurgent_A_ref.png`.

### Personaje

- **Poză:** în picioare, A-pose sau T-pose, brațele depărtate de corp.
- **Mâini:** goale, fără armă în mână. Altfel arma se lipește de degete.
- **Echipament:** fără arme agățate pe corp. Pistolul din toc e în regulă.
- **Imagine:** cât mai din față, pe fundal simplu.

| Fișier | Pornind de la | Ce este |
|---|---|---|
| `T_Insurgent_A` | `Docs/Reference/Characters/T_GasMask.jpg` | teroristul cu mască de gaze |
| `T_Insurgent_B` | aceeași imagine, altă variantă | al doilea terorist, de exemplu cu cagulă în loc de mască, altă vestă sau culoare |
| `CT_Operator` (opțional) | `Docs/Reference/Characters/CT_Operator.jpg` | înlocuiește CT-ul făcut din cod |

### Recuzită

- **Obiect:** unul singur, fără podea, perete sau alte obiecte în jur.
- **Stil:** realist, industrial, uzat, fără logo-uri sau mărci reale.
- **Dimensiuni:** cele din tabel sunt aproximative. Le folosesc la scalare, nu trebuie să le respecți în Tripo.

| Fișier | Ce este | Dimensiuni (L × l × Î, m) | Unde pe hartă |
|---|---|---|---|
| `Console_Control` | pupitru de comandă cu ecrane și butoane | 2,0 × 0,9 × 1,2 | camera de control, laborator |
| `Rack_Server` | dulap de servere cu lumini | 0,6 × 1,0 × 2,0 | laborator, camera de control |
| `Cabinet_Electrical` | tablou electric metalic, ușă cu avertizare | 1,2 × 0,4 × 2,0 | coridoare, generator |
| `Valve_Manifold` | grup de țevi cu robinete-roată | 2,0 × 0,6 × 1,5 | zona reactorului, pereți cu țevi |
| `Tank_Pressure` | rezervor vertical sub presiune | Ø 1,2 × 2,5 | exterior, zona reactorului |
| `Pump_Coolant` | pompă industrială cu motor | 1,8 × 0,9 × 1,2 | zona reactorului |
| `Generator_Diesel` | generator diesel pe cadru | 3,0 × 1,2 × 1,8 | exterior, B |
| `Drum_Hazard` | butoi metalic de 200 l, galben, cu simbol radioactiv | Ø 0,6 × 0,9 | peste tot, cover jos |
| `Pallet_Drums` | palet cu 4 butoaie legate | 1,2 × 1,2 × 1,0 | depozit, exterior |
| `Locker_Row` | șir de 4 dulapuri metalice | 1,6 × 0,5 × 1,9 | vestiar, securitate |
| `Workbench_Lab` | masă de laborator cu aparatură | 2,0 × 0,8 × 0,95 | laborator |
| `Crate_Military` | ladă militară de lemn sau metal | 1,2 × 0,8 × 0,8 | spawn-uri, cover |
| `Cart_Tools` | cărucior cu scule | 0,9 × 0,5 × 1,0 | atelier, hangar |
| `Cable_Drum` | tambur mare cu cablu | Ø 1,4 × 0,9 | exterior |
| `Barrier_Concrete` | parapet de beton (jersey) | 2,0 × 0,6 × 0,8 | exterior, cover |

Poți adăuga și altceva. Dă-i un nume clar în engleză și scrie-mi dimensiunea aproximativă.

## Arme

Pune câte o arhivă `.zip` pe armă în `Inbox/Weapons`, exportată ca la personaje (FBX cu texturi). Un singur mesh e în regulă: `Tools/Blender/k17_tripo_weapons.py` desprinde singur încărcătorul (și închizătorul la pistol), deschide sticla la red dot și holo și pune socket-urile.

După integrare, arhiva se mută în `Source/Weapons/<Id>.zip`. Armele de acum:

| Id | Armă | Echipă |
|---|---|---|
| `AK_K17` | AK cu red dot și mâner vertical | T |
| `Galil_K` | Galil cu red dot | T |
| `M4A1_S` | M4A1-S cu holo și amortizor | CT |
| `M4A4` | M4A4 cu ACOG | CT |
| `SSG_K` | lunetistă ușoară | ambele |
| `AW_K17` | lunetistă grea | ambele |
| `P17_K` | pistolul de start T | T |
| `Knife_Karambit` | cuțitul T | T |
| `Knife_Combat` | cuțitul CT | CT |

O armă nouă are nevoie de un rând în `WEAPONS` din script (lungimea reală, unde sunt mânerul, țeava și ochiul de vizare) și de o intrare în `WeaponDataBuilder` (preț, daune, cadență). Dacă arhiva are un nume ciudat sau e o dublură, spune-mi ce armă e.
