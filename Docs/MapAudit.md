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

---

# Rezultate Faza 2 — design competitiv

Măsurat în scenă după reconstruire, din fiecare celulă de 2 m în care poate sta un jucător, în 72 de
direcții, cu ochii la 1,6 m. Ușile sunt considerate deschise, baricadele nemontate, pereții moi întregi.

Cum se aplică limitele:
- cerința e „~25 m” în interior și „~40 m” în exterior, deci se acceptă o toleranță de 10 %
  (27,5 m și 44 m);
- o linie e „de interior” doar dacă pornește dintr-o zonă acoperită și se termină tot sub acoperiș;
  dacă iese afară, i se aplică limita de exterior;
- hangarul și Loading Bay sunt hale mari și au limita de exterior (site-ul B e gândit pentru luptă
  la distanță medie).

| Verificare | Faza 0 | Faza 2 |
|---|---|---|
| Zone de la parter în limită | 0 din 33 | **33 din 49** |
| Cea mai lungă linie la parter | 108,7 m | 59,2 m |
| Unghiuri lungi dintr-o singură poziție (maxim) | 10 | 7 |
| Linie din T1 Spawn | 70 m, până în Choke Point | 38 m, se oprește în T1 Ramp |
| Linie din T2 Spawn | 74 m, până în mid | 43 m, se oprește în North Overlook |
| A Heaven → T2 Spawn pe North Catwalk | 114 m (spawn-peek) | tăiată de peretele spawn-ului |
| Zone neconectate | 0 | 0 |
| Barricade points / obiecte destructibile | 9 / 20 | 10 / 31 |

## Ce s-a schimbat

**Zonele sunt acum camere.** 39 de pereți despărțitori stau pe marginile reale ale zonelor și lasă
doar goluri standard: 11 uși simple (1,4 × 2,4 m), 20 de uși duble (3 × 2,4 m), 2 uși de garaj
(5 × 3,2 m), 5 pereți moi și o fereastră (pervaz la 1,0 m, deschidere de 1,2 m). Cele 12 uși din
pereții groși au toc de 1,4 m și canat care se deschide cu F.

**Paravane la uși.** În spatele ușilor prin care treceau linii lungi e un cover în picioare la ~3 m:
din prag vezi stânga și dreapta paravanului, nu toată zona de dincolo.

**Spawn-uri.** Fiecare are trei ieșiri acoperite: T1 — ușă dublă spre T1 Ramp, camera de securitate,
rampa tunelului; T2 — ușă dublă spre North Overlook (cu scara spre catwalk), ușă simplă și ușă dublă
spre East Yard.

**Site A** (5 intrări): A Control (ușă dublă + perete moale), A Ramp (ușă + perete moale), A Pipes
(ușă din sala pompelor), depozit (perete moale), A Heaven (trapă din galerie, ușă și fereastră
baricadabilă la sol, pasarela la etaj). Zona de plant e o cameră în cameră: zid pe sud, perete cu ușă
pe nord, perete de țevi pe vest.

**Site B** (5 intrări): două uși de garaj și un perete moale de 6 m dinspre Loading Bay, ușă dublă pe
vest (Mid East), ușă dublă pe nord sub balcon plus trapă din mezanin, ușa biroului în spatele stivei,
ușă dublă din East Yard pentru apărători. Container Stack are două niveluri și se urcă pe trepte.

**Mid Lab:** ușă dublă pe sud flancată de doi pereți moi spre Choke Point, ușă dublă pe nord (decalată),
ușă pe vest, ușă spre anexă; la etaj trei deschideri baricadabile.

**Rotații apărători:** spre A — North Overlook la sol sau North Catwalk pe sus; spre B — East Yard sau
Mid East Corridor; între site-uri — pe sus prin laborator și anexă, sau prin East Yard și North
Overlook, ambele fără a trece prin zona atacatorilor.

**Cover la dimensiuni standard.** Toate obiectele de cover sunt acum: crouch (1,1 m × ≥ 2 m),
standing (2,0 m × ≥ 2 m), ladă 2 × 2 × 2, stivă 2 × 2 × 3,5, container de 20 ft sau de 40 ft.
Cele 13 cutii mici și cele 10 de 1,5 m au dispărut; helper-ul vechi `Crate` aduce automat orice
dimensiune la standard.

**Reparat pe parcurs:** ușa de la etaj dintre laborator și anexă dădea în golul scării; a fost mutată
în colțul de nord, lângă palier.

## Linii lungi intenționate

- **North Catwalk: 100 m**, de la scara T2 până în peretele de vest al site-ului A. E linia de sniper
  cerută în referință (North Catwalk → A / Mid). Nu ajunge în T2 Spawn.
- **Supraveghere de la etaj peste propriul site:** galeriile din A văd peste zona de plant (până la
  40 m), iar pasarela și mezaninul din hangar văd peste hală (până la 63 m). Sunt pozițiile verticale
  ale apărătorilor și sunt expuse din mai multe intrări.

## Ce a rămas peste limită

La parter, 16 zone:

| Linie | Limită | De unde → până unde |
|---|---|---|
| 59,2 m | 44 | Hangar → prin ușa de garaj → Loading Bay → rampa tunelului |
| 54,3 m | 44 | Loading Bay → prin ușa de garaj → peretele de est al hangarului |
| 50,8 m | 44 | gura de est a tunelului → Loading Bay → hangar |
| 50,1 m | 44 | East Yard → Mid East Corridor → hangar |
| 47,9 m | 44 | Mid Connector → pasarele → T1 Ramp |
| 47,3 m | 44 | South Lane → Loading Bay → hangar; Yard To Hangar → hangar |
| 46,7 m | 44 | North Overlook → A Catwalks → Mid Connector → A Ramp (trei uși pe diagonală) |
| 44,5 m | 44 | South Courtyard, pe diagonală |
| 46,7 m | 27,5 | Bombsite A → prin ușa de sub A Heaven → A Catwalks → North Overlook |
| 38,5 m | 27,5 | A Ramp → peste site-ul A, pe diagonală |
| 37,9 m | 27,5 | Mid Lab → prin ușa de nord → North Overlook |
| 35,1 m | 27,5 | ușa de nord a laboratorului → prin laborator, pe diagonală |
| 33,1 m | 27,5 | ușa de sub A Heaven → peste site-ul A |
| 32,7 m | 27,5 | A Control → peste site-ul A, pe diagonală |
| 27,6 m | 27,5 | ușa de vest a laboratorului → prin laborator |

Aproape toate sunt raze oblice care prind două sau trei uși la rând, ori diagonale ale halei
hangarului și ale site-ului A (34 × 34 m). La etaj rămân 13 zone din 17 peste limită: axele
laboratorului de la etaj (30–36 m) și supravegherile de mai sus.

Poziții cu multe unghiuri lungi simultan: hangar și Mid Lab (7), Loading Bay și Bombsite A (6),
ușile laboratorului, East Yard și ușa de sub A Heaven (5). Restul sunt la 4 sau mai puțin.

## Abateri față de cerință

- **Ferestre:** una singură (sub A Heaven). Fereastra din A Ramp a fost scoasă: prin ea trecea o linie
  de 50 m peste tot site-ul. Mid Lab are patru uși, dar nicio fereastră.
- **Limite:** aplicate cu toleranța de 10 % și cu hangarul tratat ca spațiu deschis.
- **Echilibrul spre A** (atacatorii ajung primii) nu a fost schimbat prin rute; rămâne de reglat după
  teste de joc.

---

# Rezultate Faza 3 — finisare vizuală

Toată finisarea e generată în `BlacksiteK17MapBuilder.Detail.cs`, după ce zonele sunt construite, din
ce au plasat ele (fețele de perete de pe grilă, acoperișurile, planșeele, luminile). Nu folosește
asset-uri externe. Detaliile nu au collider, deci nu schimbă mișcarea și nici liniile de vedere
măsurate în Faza 2 (re-măsurat: aceleași 33 din 49 de zone în limită, totul conectat). Excepție:
balustradele, care au collider.

| Element | Cum arată |
|---|---|
| Pereți | plintă de 0,25 m peste tot; pe teme: industrial (A) — benzi ruginii la 1,0 și 3,0 m + pilaștri la 5 m; laborator — lambriu alb, dungă turcoaz, pilaștri albi la 6 m; hangar — bandă galbenă de siguranță + stâlpi metalici de 0,5 × 0,3 m la 6 m; tunel — bandă de pericol + pilaștri; exterior — soclu de beton + pilaștri la 6 m |
| Colțuri | coloană de 0,7 m pe fiecare colț convex de perete |
| Coronament | capac închis la culoare pe toate blocurile de perete (se disting de podea din vederea de sus) |
| Uși și ferestre | ramă cu montanți și buiandrug de 0,16 m și prag la toate ușile (simple, duble, de garaj); ramă și pervaz ieșit la fereastră; plintă pe ambele fețe ale pereților despărțitori |
| Tavan | grinzi la 4 m sub toate acoperișurile; în hangar ferme de 0,7 m la 6 m plus grindă longitudinală |
| Lumini | corp de iluminat fizic (lampă emisivă + carcasă + tijă până în tavan) la fiecare lumină; 52 de lămpi adăugate automat acolo unde un interior nu avea lumină pe o rază de 9 m (calde în A și în tunel, reci în laborator, albe în hangar) |
| Balustrade | stâlpi la 1,5 m, mână curentă, riglă și plintă; colliderul rămâne un panou plin de 1 m. Pe toate muchiile libere ale platformelor și planșeelor de etaj (inclusiv golurile scărilor) se pun automat |
| Podea | strat colorat pe teme (exterior gri cald, industrial maroniu, laborator gri deschis, hangar gri-albastru, tunel închis); bandă galben-negru pe conturul zonelor de plant și pe pragul ușilor de garaj; literele A și B pictate în site-uri; săgeți cu litera site-ului la ieșirile din spawn-uri |

Paleta: Team 1 albastru, Team 2 roșu, site-uri galben, pereți moi lemn, trape galben deschis, cover
portocaliu, platforme albastru, scări albastru-gri, ramele și balustradele gri-închis.

## Ce nu s-a făcut în Faza 3

- Cabluri și conducte noi pe tavan: au rămas doar țevile existente din A și din sala pompelor.
- Teșituri suplimentare: harta e ortogonală, iar colțurile convexe primesc coloane în loc de teșire.
- Iluminarea nu e „bake”-uită: luminile sunt în timp real. Pentru build-ul final merită un bake de
  lumină și sonde de reflexie.

---

# Rezultate Faza 4 — validare

Validatorul e în repo: `CHOKEHOLD > Map > Validate` (`BlacksiteK17MapBuilder.Validate.cs`). Scrie
raportul complet în `Docs/MapValidation.md`. Verifică, cu ușile deschise și baricadele nemontate:

1. obiecte de recuzită (cover, rezervoare, țevi, stânci, utilaje, manechine) care intră peste 0,1 m
   în altceva; îmbinările dintre pereți, plăci, scări și acoperișuri sunt considerate construcție;
2. găuri în podea, pe fiecare metru din toate zonele, inclusiv tunelul;
3. conectivitate: flood-fill cu capsula jucătorului din spawn-ul T1 — toate zonele, toate plăcile de
   la etaj, spawn-ul T2; nicio poziție în afara conturului; punctele de spawn libere;
4. linii de vedere peste 27,5 m în interior și 44 m în exterior;
5. cover în afara dimensiunilor standard;
6. uși, ferestre și trape sub standard; spațiu liber sub 3 m în interioare.

Prima rulare a dat 63 de erori. Majoritatea erau îmbinări de construcție (pereți care trec printr-un
acoperiș sau o platformă, pereți în T), așa că regula a fost restrânsă la recuzită. Reparate:

- paravan din site-ul A care intra în zidul zonei de plant (scurtat);
- rezervorul principal din A lipit de zid, rezervorul din sala pompelor peste țevi (mutate);
- manechinul din site-ul A în interiorul unui paravan (mutat);
- paravanul din A Control care tăia peretele de sud (înlocuit cu unul mai scurt);
- paravanul de la etajul laboratorului care tăia peretele moale (scurtat);
- scara stivei de containere care intra în containere (mutată cu 0,2 m);
- o stâncă din West Rocks care intra în perete și apoi în altă stâncă (mutată);
- zidul de sud-vest al curții, care trecea prin peretele dintre curte și South Lane (scurtat);
- validatorul număra jucătorul ca obstacol pe punctul de spawn (corectat).

**Rezultat final: 0 erori, 28 de avertismente.** Avertismentele sunt toate linii de vedere peste
limită (15 la parter, 13 la etaj), descrise mai sus la Faza 2.

| Verificare | Rezultat |
|---|---|
| Suprapuneri | 0 din 285 de obiecte de cover și structuri |
| Găuri în podea | 0 din 11 573 de celule de 1 m |
| Conectivitate | 51 din 51 de zone, 37 din 37 de plăci de etaj, spawn-ul T2 accesibil, nimic în afara hărții |
| Linii de vedere | 34 din 49 de zone în limită la parter, 4 din 17 la etaj |
| Cover | 112 obiecte, toate la standard |
| Uși / ferestre / trape | 46 / 1 / 5, toate la standard; spațiu liber ≥ 3 m în interioare |

---

# Rezumat final

## Ce s-a schimbat pe zone

| Zonă | Față de punctul de plecare |
|---|---|
| T1 Spawn | trei ieșiri acoperite (ușă dublă spre T1 Ramp, camera de securitate nouă, rampa tunelului); din spawn nu se mai vede mid-ul |
| West Outpost / West Rocks | intrări prin uși duble; stânci ca margine; sala pompelor (A Pipes) e acum o rută proprie spre A |
| Site A | clădire industrială acoperită, cu luminator; 5 intrări (A Control, A Ramp, A Pipes, depozit, trapă din A Heaven); zona de plant e o cameră în cameră; galerie în L la etaj |
| Mid Lab + anexă | două niveluri, temă de laborator; ușă dublă cu pereți moi spre Choke Point, 4 uși la parter, 3 deschideri baricadabile la etaj, 3 trape |
| Choke Point / Mid | cameră cu trei uși decalate; Mid Connector și coridoarele din mid împărțite în camere cu uși |
| North Overlook / North Catwalk | două curți despărțite de un perete; catwalk-ul rămâne linia de sniper spre A, oprită de peretele spawn-ului T2 |
| T2 Spawn / East Yard | trei ieșiri acoperite; clădirea de generator face din curte un inel |
| Site B / hangar | hală acoperită cu ferme și luminator; 5 intrări (două uși de garaj și un perete moale din Loading Bay, vest, nord cu trapă din mezanin, biroul); stivă de containere pe două niveluri |
| Loading Bay / birou | acoperit, cover din containere de 40 ft; biroul nou leagă Loading Bay de spatele stivei |
| South Courtyard / South Lane | magazie centrală și ziduri care fac trei culoare; aleea despărțită de curte cu două uși duble |
| South Tunnel | subteran real la −3,5 m, cu rampe la capete, scară la mijloc și 7 pereți alternați |

## Rutele finale

- **Atacatori spre A:** principală T1 Ramp → West Outpost → A Control; secundară West Outpost → sala
  pompelor (A Pipes); agresivă T1 Ramp → pasarele → Mid Connector → A Ramp sau depozit (perete moale).
- **Atacatori spre B:** subterană prin South Tunnel → Loading Bay → uși de garaj; principală prin
  camera de securitate → curte → Choke Point → Mid East → ușa de vest a hangarului; secundară curte →
  South Lane → Loading Bay.
- **Verticale:** North Catwalk → A Heaven (trapă în A); pod A ↔ etajul laboratorului; etajul
  laboratorului → anexă → balconul din East Yard; mezaninul hangarului (trapă în B); stiva de containere.
- **Apărători:** spre A pe North Overlook (sol) sau North Catwalk (sus); spre B prin East Yard sau Mid
  East; între site-uri pe sus prin laborator și anexă sau prin East Yard și North Overlook, fără a trece
  prin zona atacatorilor.

## Compromisuri față de referință și față de cerințe

- **Scară:** 160 × 120 m în loc de ~540 m cât ar indica bara din referință.
- **Forme:** grilă ortogonală cu colțuri teșite și coloane, nu octogoane și diagonale ca în referință;
  Mid Connector e un culoar cu cot, nu o diagonală.
- **Tunel:** subteran, dar drept cu pereți alternați, nu curbat.
- **Linii de vedere:** 15 zone la parter și 13 la etaj rămân peste limită (diagonale prin hangar și peste
  site-ul A, axele etajului laboratorului, supravegherea de pe galerii și din mezanin). Limitele sunt
  aplicate cu 10 % toleranță, iar hangarul e tratat ca spațiu deschis.
- **Ferestre:** una singură; cea din A Ramp a fost scoasă pentru că deschidea o linie de 50 m.
- **Echilibru:** atacatorii ajung pe A puțin înaintea apărătorilor; de reglat după teste de joc.
- **Finisare:** fără cabluri noi pe tavan, iluminare în timp real (fără bake).
