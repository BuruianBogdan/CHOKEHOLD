# Blacksite K-17 — raport de validare

Generat de `CHOKEHOLD > Map > Validate`. Ușile sunt considerate deschise, baricadele nemontate.

**0 erori, 28 avertismente.**

## 1. Obiecte care intră unele în altele

- ok — 355 obiecte de cover și structuri verificate, nicio suprapunere peste 0.1 m

## 2. Găuri în podea

- ok — 12862 celule de 1 m verificate în 68 zone, nicio gaură

## 3. Conectivitate

- ok — 14528 poziții accesibile din spawn-ul T1
- ok — nicio poziție accesibilă în afara conturului hărții
- ok — 68 din 68 zone accesibile
- ok — 37 din 37 plăci și platforme de etaj accesibile

## 4. Linii de vedere

Limite: 27,5 m în interior și 44 m în exterior (~25 / ~40 m cu 10 % toleranță). O linie care iese de sub acoperiș primește limita de exterior. Hangarul și Loading Bay sunt hale (limită de exterior).

Linie intenționată: North Catwalk, 100.3 m, de la (45, 53) la (-55.3, 53).

- WARNING parter, Bombsite A: 46.7 m > 27.5 m, (-33, 23) → (0, 56)
- WARNING parter, Hangar / Bombsite B: 59.2 m > 44 m, (65, -33) → (11.4, -58)
- WARNING parter, Mid Lab: 37.9 m > 27.5 m, (15, 27) → (-9.3, 56)
- WARNING parter, Loading Bay: 54.3 m > 44 m, (31, -51) → (78, -23.9)
- WARNING parter, A Ramp: 35.4 m > 27.5 m, (-25, 31) → (0, 56)
- WARNING parter, Lab North Door: 35.1 m > 27.5 m, (5, 43) → (-7, 10)
- WARNING parter, Tunnel East Mouth: 50.8 m > 44 m, (21, -53) → (67, -31.5)
- WARNING parter, East Yard: 50.1 m > 44 m, (59, 31) → (30.3, -10)
- WARNING parter, A Heaven Door: 33.1 m > 27.5 m, (-25, 53) → (-36.3, 21.9)
- WARNING parter, Mid Connector: 47.9 m > 44 m, (-13, 9) → (-43.8, -27.7)
- WARNING parter, South Lane: 47.3 m > 44 m, (17, -45) → (59.9, -25)
- WARNING parter, Yard To Hangar: 47.3 m > 44 m, (65, 5) → (45, -37.9)
- WARNING parter, North Overlook: 46.7 m > 44 m, (-1, 55) → (-34, 22)
- WARNING parter, South Courtyard: 44.5 m > 44 m, (15, -15) → (-28, -26.5)
- WARNING parter, Lab West Door: 27.6 m > 27.5 m, (-11, 23) → (14, 34.7)
- WARNING etaj, Hangar North Door: 55.6 m > 27.5 m, (41, 5) → (22, -47.2)
- WARNING etaj, Mid East Bend: 63.2 m > 44 m, (23, 1) → (77.7, -30.6)
- WARNING etaj, Hangar / Bombsite B: 63.2 m > 44 m, (23, 1) → (77.7, -30.6)
- WARNING etaj, Bombsite A: 40.3 m > 27.5 m, (-25, 27) → (-58, 50.1)
- WARNING etaj, Mid East Corridor: 54.8 m > 44 m, (41, 7) → (22.3, -44.5)
- WARNING etaj, A Control: 38.1 m > 27.5 m, (-43, 23) → (-23.9, 56)
- WARNING etaj, Lab South Door: 36.4 m > 27.5 m, (3, 5) → (18.4, 38)
- WARNING etaj, Lab North Door: 36.4 m > 27.5 m, (1, 43) → (16.4, 10)
- WARNING etaj, Mid Lab: 52.5 m > 44 m, (-7, 37) → (6.6, -13.8)
- WARNING etaj, Lab Annex Door: 35.4 m > 27.5 m, (19, 37) → (-10, 16.7)
- WARNING etaj, A Ramp: 35.4 m > 27.5 m, (-25, 35) → (-55.6, 52.7)
- WARNING etaj, Lab Annex: 32.1 m > 27.5 m, (21, 37) → (-10, 28.7)
- WARNING etaj, Lab West Door: 30.0 m > 27.5 m, (-11, 23) → (18, 30.8)
- ok — parter: 49 din 64 zone în limită; etaj: 4 din 17

## 5. Cover

- ok — 120 obiecte de cover, toate pe dimensiunile standard

## 6. Uși, ferestre, trape și spațiu liber

- ok — 51 uși, 6 ferestre și 5 trape, toate la standard
- ok — spațiu liber de minimum 3 m în toate interioarele (în afara ușilor și a scărilor)

## 7. Îmbinări (pereți, coloane, tavane)

- ok — 354 pereți, blocuri și obiecte de cover verificate: nimic oprit sub tavan, nicio fantă între 0.03 și 0.8 m

## 8. Pregătire pentru texturi

- ok — 3998 cutii cu UV la scară reală (1 unitate UV = 1 m) și UV2 pentru lightmap
- ok — 1405 piese din kit-ul modular (`Assets/_Project/Art/Models/Kit`), cu UV în metri
- ok — 53 materiale, câte unul pe tip de suprafață (`Assets/_Project/Art/Materials/Greybox`): înlocuind un material se texturează toate suprafețele de acel tip
- ok — nicio față de sus suprapusă în același plan (fără z-fighting pe podele, capace și plăci)

## 9. Buncăr: timp de ajuns pentru fiecare echipă

Drumul cel mai scurt pe jos (grilă de 1 m, cu scări și tuneluri), până la podeaua buncărului:

| Echipă | Distanță | Timp la sprint (7,5 m/s) | Intră pe |
|---|---|---|---|
| Atacatori | 117 m | 15.7 s | West Link (tunelul de nord) |
| Apărători | 110 m | 14.7 s | North Link (tunelul de est) |

- ok — diferență de 6% între echipe (sub 10%)

## 10. Acoperișuri și camera de control

- ok — 3 scări de mână (Ladder_OutpostTower 4 m, Ladder_LabRoof 8.5 m, Ladder_CourtyardShed 4 m)
- ok — Lab Roof (8.4 m) vede în jos spre: Mid Connector, Mid East Corridor, South Courtyard, North Overlook
- ok — Control Room (8.4 m) vede în jos spre: nicio zonă de la sol (doar acoperișul)
- ok — Outpost Tower Roof (4 m) vede în jos spre: West Outpost, T1 Ramp, A Pipes South Door
- ok — Courtyard Shed Roof (4 m) vede în jos spre: South Courtyard, Choke Point, T1 Walkway South
