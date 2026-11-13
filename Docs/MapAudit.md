# Blacksite K-17 — Audit hartă (Faza 0)

Stare auditată: harta generată de `BlacksiteK17MapBuilder.cs` (160 × 120 m, parter + etaj la 4 m),
commit-ul de bază al branch-ului `map/polish-pass`.

Cifrele de mai jos sunt **măsurate în scena din Unity** cu raycast-uri (ochi la 1,6 m, 48 de direcții
din fiecare celulă de 2 m), nu estimate. Ușile erau închise, baricadele scoase.

Referințe folosite:
- `Docs/Reference/Chokehold_Reference.webp` — tactical overview-ul (copiat din imaginea primită la început).
- `Docs/Reference/Current_State.png` **nu există în repo**; am folosit `Docs/BlacksiteK17_*.png`.

---

## a) Găuri, void, ieșiri din hartă

| Verificare | Rezultat |
|---|---|
| Găuri în podea (raycast pe fiecare metru, toate zonele) | **0** |
| Perete exterior | continuu, 11 m, minimum 2 m grosime pe tot conturul — nu se poate ieși |
| Vedere în afara hărții | doar cerul; nu există orizont sau „margine” vizibilă |
| Zone walkable neconectate | **niciuna** — din spawn-ul T1 se ajunge în toate zonele și pe toate platformele de la etaj |

Probleme reale găsite:

1. **Interioare fără tavan.** Acoperire măsurată: Bombsite A 28 %, A Control 14 %, Hangar 11 %,
   Loading Bay 0 %, South Tunnel 74 % (capetele `x -54…-42` și `x 20…24` sunt descoperite).
   Doar Mid Lab și Lab Annex au acoperiș, iar fâșiile de deasupra ușilor lor (balcoanele) sunt deschise.
2. **Buzunare sub scări.** Toate cele 9 scări sunt rampe fără închidere dedesubt: spațiu liber de
   0,2–1,4 m în Bombsite A, North Overlook, East Yard, Hangar, Mid Lab, Lab Annex. Sunt ascunzători
   și fante de tras de tip „pixel peek”.
3. **Balconul North Overlook** (platformă la 2 m) lasă 1,6 m dedesubt — nici trecere, nici perete.
4. **Masă moartă.** 31 % din suprafața hărții (calculat pe grila de 2 m, cu tot cu conturul exterior)
   este umplutură solidă fără rol: colțul NV
   (`x -80…-58, z 22…60`), blocul dintre West Outpost și A, blocul dintre T1 Ramp și curte
   (`x -44…-28, z -52…-22`), fâșia dintre South Lane și tunel, colțul SE.
5. **Uși și treceri sub standard.** Golurile de ușă au 2 m lățime pe 2,5 m (ținta: 1,4 × 2,4 cu toc);
   Annex South Passage are 2 m lățime pe 6 m lungime (sub minimul de 2,5 m pentru coridor).
6. **Nu există nicio fereastră** pe toată harta.

## b) Zone din referință care lipsesc sau sunt incomplete

| Zonă | Stare |
|---|---|
| A Pipes | există doar ca fâșie interioară cu două țevi; în referință e un culoar propriu care ocolește site-ul pe vest și sud-vest |
| A Catwalks | există zona de la sol și o pasarelă; nu e o rețea de catwalk-uri în interiorul lui A |
| A Platform | o singură placă ridicată la 1,2 m, fără structură |
| South Tunnel | la nivelul solului, drept; în referință e „underground route”, curbat |
| Container Stack | containere răzlețe pe latura de est a hangarului; nu e o stivă pe două niveluri pe care se poate urca |
| Loading Bay | podea goală cu 3 containere; fără rampe de încărcare, uși de garaj sau docuri |
| West Outpost | o baracă și un turn-bloc; fără clădire jucabilă |
| T1 Ramp / T2 Ramp | sunt scări-rampă simple; nu au caracter de „rampă” de acces cu cover |
| Mid Lab | camere despărțite, dar fără ferestre spre Choke Point sau Mid Connector |
| Lifturi / scări marcate în referință | lipsesc: liftul East Yard ↔ Mid, scara din tunel, scările din jurul Mid Lab |
| East Yard, North Overlook | spații deschise mărginite doar de umplutură, fără margini „construite” (garduri, stânci) |

## c) Linii de vedere și unghiuri

Limite cerute: 25 m în interior, 40 m în exterior. **Toate cele 33 de zone de la parter le depășesc.**
Cele mai grave:

| Lungime | De la → până la | Ce traversează |
|---|---|---|
| 108,7 m | East Yard (77, 21) → T1 Walkway North (−28, −7) | diagonală prin Mid East Corridor, Mid East Bend, Choke, Mid West Corridor |
| 107,3 m | South Tunnel (−53, −55) → Loading Bay (54, −55) | tot tunelul plus Loading Bay; stâlpii adăugați nu o taie |
| 104,3 m | North Overlook (49, 53) → Bombsite A (−55, 53) | pe sub catwalk, prin A Heaven Door, peste tot site-ul A |
| 101,0 m | South Lane (3, −45) → North Overlook (3, 56) | axa nord–sud: curte → Choke → ușa de sud a laboratorului → laborator → ușa de nord |
| 91,0 m | West Outpost (−75, −17) → South Courtyard (16, −17) | T1 Ramp și curtea, pe est–vest |
| 87,8 m | Hangar (65, −9) → Mid West Corridor (−22, 2) | hangarul și tot mid-ul, pe est–vest |
| 74,0 m | **T2 Spawn** (71, 53) → Mid East / Hangar (34, −11) | din spawn direct în mid |
| 70,0 m | **T1 Spawn** (−65, −33) → Choke (−4, 2) | din spawn direct în mid |

La etaj: **114 m de la A Heaven (−35, 55) până în T2 Spawn (78, 40)** — spawn-peek direct din site-ul A
în spawn-ul apărătorilor, pe North Catwalk. Linia North Catwalk → A este voită, dar nu trebuie să
ajungă în spawn.

Unghiuri de verificat dintr-o singură poziție (culoare mai lungi de 25 m, ținta: maximum 2–3):

| Poziție | Culoare lungi |
|---|---|
| Hangar (37, −35) | 10 |
| Choke Point (7, −3) | 9 |
| South Courtyard (5, −19) | 8 |
| A Control, Bombsite A, A Heaven Door, East Yard, Lab South Door, Loading Bay, North Overlook, Yard To Hangar | 7 fiecare |

Doar Lab Annex, Lab North Door și interiorul Mid Lab sunt aproape de țintă.

## d) Cover

Din 48 de „crates”:
- **13** sunt sub 1,5 m înălțime și sub 2 m lățime — nu ascund un jucător;
- **18** au înălțime de crouch (1,0–1,2 m) și lățime bună;
- **10** au 1,5 m — nici crouch, nici standing (capul rămâne afară);
- **7** sunt standing cover (≥ 1,9 m).

În plus: 8 „half wall”-uri de 1,2 m × 0,5 m grosime (peste standardul de crouch de 1,0–1,1 m),
18 containere de 20 ft, niciun container de 40 ft, nicio stivă pe care se poate urca.
Plasarea e punctuală (o cutie pe zonă), nu grupată pe roluri; niciun obiect nu e gândit ca
poziție de plant sau de post-plant.

## e) Diferențe de layout față de referință

1. **Scara.** Referința indică ~540 m lățime după bara de scară; harta are 160 m. Păstrat intenționat:
   la 540 m o rundă ar dura minute doar de mers.
2. **Forme.** Referința are contururi octogonale și diagonale (spawn-uri, hangar, Mid Connector);
   harta e ortogonală, cu 11 colțuri teșite adăugate ulterior.
3. **Mid Connector** e în referință o diagonală SV–NE; aici e un culoar nord–sud de 44 m plus un cot.
4. **Hangar:** în referință e un octogon cu Loading Bay inclus în partea de sud și Container Stack pe
   est; aici e un dreptunghi de 56 × 42 m cu Loading Bay lipit dedesubt.
5. **Echilibrul rutelor.** T1 → A: 86 m; T2 → A: 106 m. Atacatorii ajung primii pe A cu ~3 s.
   Poziția spawn-urilor e cea din referință, deci corecția trebuie făcută prin rute, nu prin mutare.
6. **Intrări pe site-uri.**
   - A: A Control (12 m lățime), A Ramp (8 m), A Heaven Door la sol (8 m), plus catwalk și pod la etaj.
     Sunt prea late și niciuna nu e prin perete moale spre exterior; nu există hatch din A Heaven.
   - B: ușa de vest (6 m), ușa de nord (6 m), Yard To Hangar (8 m) și **toată latura de sud deschisă
     spre Loading Bay (38 m)**. Niciun perete moale, niciun hatch.
7. **Rotații apărători.** A ↔ B fără zona atacatorilor există (North Overlook → East Yard, sau pe sus
   prin laborator și anexă), dar ambele sunt expuse liniilor de peste 100 m de mai sus.

---

# Plan pe faze

Un commit pe fază, pe branch-ul `map/polish-pass`. Builder-ul se împarte de la Faza 1 în clase
`partial`: `MapBuilder.Core`, `.Layout`, `.SiteA`, `.Mid`, `.SiteB`, `.Spawns`, `.Detail`.

## Faza 1 — Completare și închidere
- Acoperișuri complete: clădirea A (cu luminator deasupra site-ului), A Control, Hangar (cu grinzi),
  Loading Bay, tot tunelul; planșee peste fâșiile de deasupra ușilor.
- Închidere sub toate scările și sub balconul North Overlook.
- Masa moartă devine camere jucabile, câte una pe gol:
  camera de securitate între T1 Ramp și curte, sala pompelor între West Outpost și A (devine A Pipes
  ca rută proprie), depozit între South Lane și tunel, birou în colțul SE al hangarului.
- South Tunnel devine **subteran real** (−3,5 m), cu rampe la ambele capete și scara din mijloc spre
  South Lane, ca în referință. Pentru asta podeaua trece de la o singură placă la plăci pe zone.
- Margini construite la exterior: ziduri joase, garduri, stânci în West Outpost, West Rocks,
  East Yard, North Overlook.

## Faza 2 — Design competitiv
- Fiecare linie din tabelul (c) e tăiată sub 25 m / 40 m; rămâne documentată doar North Catwalk → A
  și → Mid, oprită înainte de T2 Spawn.
- Spawn-uri: 3 ieșiri acoperite fiecare, fără vedere directă în mid.
- Uși de 1,4 × 2,4 m și uși duble de 3 m în locul golurilor de 6–12 m; ferestre cu pervaz la 1,0 m.
- Site A: 4 intrări (A Control, A Ramp, A Pipes, hatch din A Heaven), pereți moi spre A Control și
  A Ramp, ferestre baricadabile.
- Site B: latura de sud închisă cu două uși de garaj și un perete moale; Container Stack pe două
  niveluri, pe care se urcă; un hatch din mezanin.
- Mid Lab: 4 uși/ferestre și perete moale spre Choke Point.
- Cover refăcut pe standard (crouch 1,0–1,1 × ≥ 2 m; standing 1,9–2,2 × ≥ 2 m; crate 2 × 2 × 2;
  container 20 și 40 ft), grupat pe roluri; cele 13 cutii mici și cele 10 de 1,5 m dispar.
- Echilibru A: rută mai scurtă pentru apărători pe North Catwalk și una mai lungă pentru atacatori.

## Faza 3 — Finisare vizuală
- Helpere `DetailedWall`, `DoorFrame`, `WindowFrame`, `Stairs` (trepte de 0,2 m), `Railing` complet.
- Plintă, trim, pilaștri la 4–6 m, coronament pe exterior; panouri diferite pe zone
  (industrial A, laborator Mid, hangar metalic B, beton/stâncă afară).
- Grinzi, conducte și cabluri pe tavan, lămpi ca obiecte; marcaje pe podea.
- Paletă pe zone și iluminare pe zone, fără colțuri complet negre.

## Faza 4 — Validare
- `CHOKEHOLD/Map/Validate`: suprapuneri, găuri în podea, conectivitate, linii de vedere peste limite,
  cover și uși/ferestre sub standard. Pornește de la sonda folosită pentru acest audit.
- Reparat tot ce raportează, imagini noi în `Docs/`, acest fișier actualizat.

## Decizii de confirmat înainte de Faza 1
1. **Tunel subteran real** (recomandat) sau rămâne la sol, dar curbat și acoperit?
2. **Scara rămâne 160 × 120 m** (recomandat)?
3. **Forme:** păstrez grila ortogonală cu colțuri teșite (recomandat, validatorul și grila de 2 m rămân
   simple) sau trec hangarul și spawn-urile pe octogoane adevărate?

---

# Rezultate Faza 1 — completare și închidere

Măsurat din nou în scenă după reconstruire, cu ușile considerate deschise.

| Verificare | Înainte | După |
|---|---|---|
| Zone walkable | 35 | 51 (cu camerele noi și ușile lor) |
| Zone neconectate | 0 | 0 — toate cele 51, plus tunelul și toate platformele de la etaj |
| Acoperire tavan Bombsite A | 28 % | 73 % |
| Acoperire tavan A Control | 14 % | 90 % |
| Acoperire tavan Hangar | 11 % | 79 % |
| Acoperire tavan Loading Bay | 0 % | 87 % |
| Acoperire tavan South Tunnel | 74 % | 94 % |
| Celule cu spațiu liber sub 3 m sub scări / balcoane | 200 | 8 |

Procentele de tavan nu ajung la 100 % din două motive: luminatoarele lăsate intenționat deasupra
zonelor de plant (A și B) și faptul că sonda nu numără celulele ocupate de obiecte. Cele 8 celule
rămase cu spațiu sub 3 m sunt primele trepte ale scărilor (0,2 m), nu buzunare.

Ce s-a schimbat:

- **Builder împărțit pe zone**: `Build`, `Layout`, `SiteA`, `Mid`, `SiteB`, `Spawns`, `South`, plus
  fișierul de bază cu primitivele.
- **South Tunnel subteran real** la −3,5 m: rampe din spawn-ul T1 și din Loading Bay, scară din
  South Lane la mijloc, șicană din patru pereți alternați, harta plină deasupra. Podeaua e acum pe
  plăci, cu șanțul tunelului decupat.
- **Scări cu trepte reale** (0,2 m) în locul celor 9 rampe, pline dedesubt; platforma A și balconul
  North Overlook sunt blocuri pline.
- **Acoperișuri**: clădirea A și hangarul cu Loading Bay (fiecare cu luminator), A Control, balcoanele
  și golurile de ușă ale laboratorului și anexei, toate camerele noi.
- **Lumini pe zone** în toate interioarele acoperite: calde în A, reci în laborator, albe în hangar.
- **Patru camere jucabile în locul masei moarte**:
  - *A Pipes* (sala pompelor): rută proprie West Outpost → latura de vest a site-ului A;
  - *Depozit*: leagă A Control de Mid Connector și are perete moale direct în site-ul A;
  - *Camera de securitate*: a treia ieșire din spawn-ul T1, spre curte sau T1 Ramp;
  - *Birou*: leagă Loading Bay de latura de est a hangarului, în spatele Container Stack-ului.
- **Margini exterioare**: stânci în West Rocks, East Yard și North Overlook.
- Imagine nouă: `Docs/BlacksiteK17_TopDown_Underground.png`.

Abateri față de plan:

- Depozitul e între A Control și Mid Connector, nu între South Lane și tunel: fâșia de acolo are doar
  6 m și, odată cu tunelul subteran, e ocupată de scara lui.
- Ușile noi folosesc încă golul de 2 m; trec la 1,4 × 2,4 m cu toc în Faza 2, împreună cu restul.

Rămas pentru fazele următoare: toate liniile de vedere din tabelul (c) (neschimbate de Faza 1),
cover-ul, intrările pe site-uri, finisajele și validatorul din repo.
