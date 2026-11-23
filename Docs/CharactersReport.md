# Raport personaje și arme — Blacksite K-17

Stare: **CT și T din Tripo integrate, cu 9 arme din Tripo**, inventar CS (principală, pistol, cuțit), bani, meniu de cumpărare, lunete și cuțit. Personajele și armele făcute din cod au rămas ca rezervă (P-SD, M15-T Breacher și puștile vechi M4).

Modelele din Tripo trec prin scripturi Blender headless, iar restul se generează din cod. Unity folosește meniurile din `CHOKEHOLD → Characters`. Nimic nu e editat de mână.

## 1. Cum se reface totul

```
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_textures.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_tripo.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_tripo_weapons.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_characters.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_weapons.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_anims.py -- Rifle
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_anims.py -- Pistol
```

Apoi, în Unity, în ordine:

1. **Build Prefabs** — personajele: materiale, LOD, hitbox-uri, ragdoll, rig IK;
2. **Build Weapon Prefabs** — armele FP și TP cu socket-uri;
3. **Build Weapon Data** — asset-urile `WeaponData`, catalogul de arme, inventarul, banii și meniul de cumpărare pe jucător, armele țintelor;
4. **Build Animator** — controller-ul TP;
5. **Build FP Arms** — mâinile FP, layer-ul FirstPerson și legarea de jucător;
6. **Build Showcase Scene**;
7. **Render Showcase** — capturile din `Docs/Characters`.

## 2. Personaje

| Model | LOD0 | LOD1 | LOD2 | Sursă | Materiale |
|---|---|---|---|---|---|
| CT_Operator | ~22 000 | ~11 000 | ~4 400 | Tripo (`k17_tripo.py`) | `CT_Operator_Body` (culoare, normale, metalic + luciu) |
| T_Insurgent_A | ~22 000 | ~11 000 | ~4 400 | Tripo (`k17_tripo.py`) | `T_Insurgent_A_Body` |
| FPArms_CT, FPArms_T | — | — | — | brațele tăiate din modelul Tripo | ca la personaj |
| CT_Operator_Code | 8 857 | 4 420 | 1 720 | cod (`k17_characters.py -- CT_Operator_Code`), rămas ca referință | Skin, Nylon, CT_Camo, Rubber, Plastic, Glass, PatchBlue, PatchGrey |

Notele de mai jos (echipament, piele, UV-uri) sunt despre CT-ul făcut din cod. Scheletul e același la toate personajele.

- **Schelet:** 55 de oase, compatibil Humanoid și Mixamo, plus `weapon_R` și `weapon_L_IK`. Înălțimea e 1,80 m, rădăcina la Y = 0, fața spre +Z.
- **Echipament CT:** cască cu șine și suport NVG, căști cu microfon, ochelari, balaclava, vestă cu port-încărcătoare, stație radio cu antenă, rucsac rigid, centură cu pungă, clește și carabinieră, toc pe coapsă, genunchiere, cotiere, mănuși întregi, petice pe umeri (fără mărci reale).
- **Pielea de sub gât** se scoate la ținutele care acoperă tot corpul. Nu se vede și ar străpunge hainele. Așa LOD0 a scăzut de la 17 430 la 8 857 triunghiuri.
- **UV-uri:** proiecție pe cub de 1 m. Camuflajul se repetă la 0,5 m, ca pe uniforma din referință.
- **Greutăți:** bone heat legase buzunarele pantalonilor de degetele mâinilor, care în A-pose stau lângă coapse. Când mâinile se mișcau, plăci întregi zburau după ele. Acum greutățile brațelor se scot de pe pantaloni, bocanci, partea de jos a vestei și corp. Mănușile merg pe falanga cea mai apropiată, ca degetele să se îndoaie.

## 3. Arme

### Armele din Tripo (`k17_tripo_weapons.py`)

| Armă | Echipă | Preț | Daune | Cadență | Încărcător | Ochire | FP | TP |
|---|---|---|---|---|---|---|---|---|
| Karambit | T (start) | — | 34 / 65, ×3 în spate | — | — | — | ~8 000 | ~2 000 |
| Cuțit de luptă | CT (start) | — | 34 / 65, ×3 în spate | — | — | — | ~8 000 | ~2 000 |
| P17-K | T (start) | $200 | 30 | semi | 20 / 120 | cătare | 13 998 | ~4 500 |
| Galil-K | T | $1 800 | 30 | 11 / s | 35 / 90 | red dot | 13 974 | ~4 500 |
| AK-K17 | T | $2 700 | 36 | 10 / s | 30 / 90 | red dot, mâner vertical | 13 969 | ~4 500 |
| M4A1-S | CT | $2 900 | 38 | 10 / s | 20 / 80 | holo, amortizor | 14 064 | ~4 500 |
| M4A4 | CT | $3 100 | 33 | 11 / s | 30 / 90 | ACOG (zoom), mâner vertical | 13 992 | ~4 500 |
| SSG-K | ambele | $1 700 | 88 | 0,8 / s | 10 / 90 | lunetă, 2 trepte de zoom | 13 997 | ~4 500 |
| AW-K17 | ambele | $4 750 | 115 | 0,68 / s | 5 / 30 | lunetă, 2 trepte de zoom | 13 997 | ~4 500 |

Daunele sunt pe piept; capul ia ×4 (orice armă ucide dintr-un glonț în cap), iar membrele ×0,75. Cuțitul dă aceleași daune pe orice zonă.

- **Piese animabile:** încărcătorul la toate armele de foc și închizătorul la P17-K, desprinse din mesh-ul Tripo după coordonate.
- **Opticile:** la red dot și holo sticla e deschisă de-a lungul axei optice, cu un reticul emisiv (punct la red dot, punct și inel la holo). ACOG-ul și lunetele rămân închise; click dreapta trece la ecranul de lunetă (cerc negru, reticul, zoom și sensibilitate mai mică).
- **Dubluri:** în Inbox erau două arhive identice („(1)”), pentru Galil și M4A4. Le-am păstrat o singură dată.

### Armele făcute din cod (rezervă)

| Armă | FP | TP | Folosire acum |
|---|---|---|---|
| P-SD | 1 564 | 428 | pistolul de start CT, până vine al doilea pistol din Tripo |
| M15-T Breacher | 1 304 | 344 | pistol de cumpărat pentru T ($700) |
| M4A4-M Vandal, M4-SD | 3 480 / 3 016 | 824 / 680 | nu se mai cumpără (le-au înlocuit M4A1-S și M4A4) |

## 4. Animații

**Persoana a treia:** 109 clipuri in-place. Sunt 57 pentru pușcă și 52 pentru pistol.

| Grup | Clipuri |
|---|---|
| Locomoție | Idle, Walk ×8 direcții, Run ×8, Sprint, CrouchIdle, CrouchWalk ×8, TurnL/R |
| Aer | JumpStart, Fall, Land |
| Aim offset | 3 × 3 (sus / centru / jos × stânga / centru / dreapta), LeanL/R |
| Armă | Fire, Reload, ReloadEmpty, Draw, Holster |
| Reacții | HitF/B/L/R |
| Acțiuni (doar pușcă) | Plant, Defuse, Door, Ladder, Barricade |
| Moarte | ragdoll fizic, fără clip |

**Animator Controller-ul** e generat din cod și are patru straturi:
- **Base:** blend tree 2D pentru stat în picioare și ghemuit, plus sprint, aer și scară;
- **UpperBody:** aim offset 2D;
- **Lean;**
- **Actions:** triggere.

Totul e dublat pe pușcă și pistol prin `WeaponKind`.

**IK la persoana a treia** folosește `com.unity.animation.rigging` 6.6.0, inclus în editor:
- mâna stângă stă pe `Grip_L` printr-un TwoBoneIK, cu ținta sub `WeaponMount`;
- capul urmărește direcția privirii printr-un MultiAim cu greutate 0,55 și limită de ±50°.

**Persoana întâi** e procedurală (`FirstPersonAnimator`), peste sway, bob, recul și ADS din `HitscanWeapon`:
- **Draw:** arma vine de jos, rotită.
- **Fire:** țeava urcă, iar închizătorul sau piedica merge înapoi.
- **Reload:** arma se înclină și se ridică, mâna stângă ia încărcătorul, îl coboară, apoi pune altul. La reîncărcarea pe gol se trage și închizătorul.
- **Inspect:** tasta Y, flancul stâng apoi partea de sus.
- **Sprint:** arma de-a curmezișul.
- **Plantare și dezamorsare:** arma coborâtă din cadru.
- **ADS:** socket-ul `Sight` ajunge pe axa camerei, la distanța ochiului din `WeaponData`.

Mâinile FP au umerii ficși față de cameră și IK analitic pe două oase spre `Grip_R` și `Grip_L`. Degetele se strâng procedural. Arma și mâinile se desenează cu o cameră overlay pe layer-ul FirstPerson, așa că nu intră în pereți și nu fac umbre. Umbra jucătorului vine de la modelul complet, randat doar ca umbră.

## 5. Integrare în joc

| Element | Cum |
|---|---|
| Model după echipă | `CharacterVisuals`: CT pentru apărători; pentru atacatori, Insurgent A sau B, stabil după nume |
| Jucătorul local | vede doar mâinile FP și umbra propriului corp |
| Hitbox-uri | 17 pe oase: cap ×4, trunchi ×1, membre ×0,75 (`HitZone`) |
| Ragdoll | corpuri cinematice cât timp personajul trăiește, `Ragdoll.SetAlive(false)` la moarte |
| Arme | `WeaponData` cu prefab-urile FP și TP, statistici, preț, recompensă pe kill, tipul ochirii, offset hip și distanța ochiului la ADS |
| Ținte de antrenament | personajul CT idle cu M4A4 sau M4A1-S (după nume), în loc de capsule |

### Inventar, bani și cumpărare (ca în CS)

| Ce | Cum |
|---|---|
| Sloturi | `PlayerLoadout`: [1] armă principală, [2] pistol, [3] cuțit. Rotița mouse-ului schimbă armele doar dacă săritura pe rotiță e oprită din setări. Fiecare slot își ține muniția |
| La start | cuțitul și pistolul echipei (T: Karambit și P17-K, CT: cuțitul de luptă și P-SD) |
| Între runde | supraviețuitorul își păstrează armele, cu muniția plină; după moarte rămân doar cuțitul și pistolul de start |
| Bani | `PlayerEconomy`: $800 la start, maxim $16 000; kill $300 (cuțitul $1 500, AW-K17 $100); rundă câștigată $3 250, pierdută $1 400 + $500 pentru fiecare pierdere la rând (maxim $3 400); plantarea dă $300 atacatorilor |
| Meniul de cumpărare | `BuyMenu`, tasta B: coloane Pistoale / Puști / Lunetiste, doar armele echipei; prețul e roșu când nu ajung banii și „IN INVENTAR” când arma e deja luată. Arma cumpărată intră direct în mână |
| Când se poate cumpăra | în pregătire și în primele 20 s ale rundei, la cel mult 16 m de spawn-ul echipei |
| Lunetă și ACOG | click dreapta trece prin treptele de zoom; arma și mâinile dispar, apare ecranul lunetei; lunetistele ies din lunetă după foc și revin după armare |
| Cuțit | click stânga taie (34), click dreapta înjunghie (65); din spate daunele se triplează (o înjunghiere ucide) |
| Viteză | fiecare armă are viteza ei de mers, ca proporție din alergarea cu cuțitul: pistoale 1, SSG-K 1,04, Galil-K 0,96, M4A1-S și M4A4 0,92, AK-K17 0,884, AW-K17 0,84 (cu luneta pusă, lunetistele merg la 72%) |

### Mișcare și țintire

Săriturile și strafe-ul folosesc fizica pe viteză a shooterelor competitive clasice. Restul mișcării e tactic.

| Ce | Cum |
|---|---|
| Alergare | 5,2 m/s cu cuțitul, înmulțit cu viteza armei. Tastele merg direct, fără netezire |
| Frecare și accelerare | frecare 4 (sub 30% din viteză oprește ca la 30%), accelerare la sol 5: viteza maximă în ~0,2 s |
| Counter-strafe | tasta opusă te aduce sub pragul de precizie (34% din viteză) în ~90 ms; doar ridicând tasta durează ~270 ms |
| Săritura | 1,14 m, 0,64 s în aer, gravitație 20,3 m/s². Sare doar la apăsare (ținută nu sare iar). Rotița mouse-ului sare și ea (pornit implicit) |
| Air strafe și bunny hop | în aer, A / D împreună cu rotirea mouse-ului cresc viteza (în test: 5,2 → 6,65 → 7,82 m/s). Peste 1,7 × viteza maximă, săritura taie viteza. După aterizare, oboseala încetinește pe sol și scade următoarea săritură, deci doar săritura imediată păstrează viteza |
| Duck-jump | Ctrl în aer ridică picioarele cu 0,4 m (urci pe obstacole mai înalte) |
| Sprint | Shift doar mergând înainte, la sol, ×1,3. Ochirea și focul îl opresc; din sprint arma trage după 0,15 s |
| Crouch | Ctrl ținut sau C comutat, viteză ×0,45 |
| Lean | Q / E ținut, 15° și 0,32 m, limitat de pereți |
| Precizie | după viteza reală, nu după taste: oprit (sau după counter-strafe) primul glonț e precis, alergând dispersia crește până la ×2,5–3, în aer mult mai mult, ghemuit e mai mică; în rafală crește cu fiecare glonț |
| Recul | fiecare glonț din rafală urcă mai mult decât cel dinainte, până la un maxim per armă (AK-K17 5,75°, M4A1-S 4,25°); lateral reculul merge într-o parte și o schimbă din când în când. Reculul scade continuu și revine complet la ~1 s după rafală. Glonțul pleacă exact unde e privirea |
| Sensibilitate | grade pe count = sensibilitate × 0,022 (scara clasică, poți copia valoarea din alt joc). Implicit 2,0; se schimbă din meniul de setări (Esc), cu raport separat pentru lunetă și ACOG |

Verificat în Play mode cu intrare simulată (`MovementProbe`, local): vitezele, sprintul doar înainte (Shift pe loc nu mai pornește alergarea), counter-strafe-ul, câștigul de viteză la bunny hop, înălțimea săriturii și duck-jump-ul. Reculul unei rafale de AK-K17 urcă la 5,75° și revine la 0 după o secundă.

Verificat în Play mode (`WeaponsProbe`, local):
- la start T are P17-K și Karambit, $800, iar meniul se deschide în zona de spawn;
- cumpărarea AK-K17 scade banii corect și pune arma în slotul principal, în mână;
- toate cele 13 arme se echipează cu model, socket-uri, muniție și viteză; daunele reale pe țintă (picior / piept / cap) ies ca în tabel, iar cuțitul dă 34 / 65 / 100 în spate;
- la red dot și holo reticulul e în centrul ecranului la ADS; ecranele de lunetă și ACOG apar cu zoom;
- țintele CT țin M4A4 / M4A1-S la persoana a treia, cu mâna stângă pe armă.

## 6. Scena de prezentare

`Assets/_Project/Scenes/CharacterShowcase.unity` are personajele pe podium, armele pe stand și un rig FP separat.

Meniul de debug se deschide și se închide cu Tab. Din el poți:
- alege personajul și arma;
- trece la vederile față, spate, profil și cap;
- porni rotirea pe podium;
- rula orice clip;
- folosi Animator Controller-ul cu parametri din slidere și butoane pentru triggere;
- forța LOD-ul;
- porni ragdoll-ul;
- trece în vederea FP, cu sau fără ADS.

Orbita camerei se face cu click dreapta, iar zoom-ul cu rotița.

## 7. Capturi

Sunt în `Docs/Characters/<Personaj>` (`CT_Operator`, `T_Insurgent_A`):

| Ce | Fișiere |
|---|---|
| Model în Unity | `<Personaj>_Unity_Front / Back / Side / Head` |
| Poze din clipuri | `<Personaj>_Pose_*`: aim, run, crouch, reload, lean, plant, hit, pistol |
| LOD-uri | `<Personaj>_LOD0 / LOD1 / LOD2` |
| Mâini FP | `FP_<armă>_Hip` și `FP_<armă>_ADS`: T cu AK-K17, Galil-K, P17-K, Karambit; CT cu M4A1-S, M4A4, P-SD, cuțitul de luptă |
| Stand de arme | `Docs/Characters/Weapons_Unity.png` |
| Armele din Tripo (Blender) | `Docs/Characters/Weapons/<Id>_Side.png` și `<Id>_Sight.png` (vederea prin ochire) |

## 8. Comparație cu referința (`Docs/Reference/Characters/CT_Operator.jpg`)

Se potrivesc:
- silueta și proporțiile;
- camuflajul bleumarin;
- casca cu șine, căști și NVG;
- ochelarii și balaclava;
- vesta cu trei port-încărcătoare și stația radio;
- rucsacul rigid;
- centura cu pungă la spate, clește și carabinieră;
- tocul pe coapsa dreaptă;
- genunchierele și cotierele;
- mănușile;
- peticele pe umeri.

Diferențe:
- **Stil:** modelul e stilizat, din volume generate. Nu are cute de material, cusături sau barete MOLLE modelate.
- **Petice:** sunt simple. Referința are drapel și inscripția „CT”, iar aici sunt doar culori, fără text sau mărci.
- **Rucsacul:** e o cutie rotunjită, nu carcasa fațetată din referință.
- **Texturi:** sunt de 1024 × 1024, procedurale, nu 2048 pictate.

## 9. Ce nu s-a putut face (încă) și abateri de la plan

| Ce | De ce / ce urmează |
|---|---|
| Al doilea pistol din Tripo | a venit un singur pistol (P17-K, dat T-ului). CT-ul folosește încă P-SD făcut din cod; un pistol CT generat în Tripo intră pe același drum |
| Animații de armare și scoatere per armă | sunt procedurale, comune pe tipuri (pușcă, lunetistă, pistol, cuțit) |
| Sunete per armă | `WeaponData` are câmpurile de sunet, dar încă nu sunt sunete noi |
| Captura în hartă la 30 m, T vs CT | de făcut |
| Texturi de 2048 pentru personaje | seturile procedurale sunt de 1024; ridicarea rezoluției e o opțiune din `k17_textures.py` |
| Animații FP ca clipuri | sunt procedurale în cod: cu IK pe socket-uri, mâinile stau pe orice armă fără clipuri separate |
| Calitatea animațiilor TP | poze-cheie generate din cod, rigide față de motion capture |
| Ochire cu magnifier-ul | rabatat lateral; o comutare 1× / 3× ar cere a doua poziție ADS |

## 10. Sugestii pentru calitate de producție

- **Animații TP:** Mixamo, gratuit. Scheletul K17 e compatibil Humanoid, deci clipurile Mixamo se pot retargeta direct pe CT. Sunt utile pachetele „Rifle” și „Pistol” pentru locomoție, aim, reload și moarte.
- **Asset Store:** un pachet de animații FPS (de exemplu „FPS Animation Pack” sau „Low Poly FPS Pack”) dă clipuri FP pentru mâini. Ele pot înlocui animațiile procedurale, iar IK-ul pe socket-uri rămâne pentru mâna stângă.
- **Detaliu de model:** un retopo pe un sculpt, sau un pachet de echipament militar modular, ar aduce cute și barete. Scheletul, socket-urile și pipeline-ul Unity rămân aceleași.
