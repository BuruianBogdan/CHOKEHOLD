# Raport personaje și arme — Blacksite K-17

Stare: **CT_Operator terminat ca vertical slice** (model, rig, arme, animații TP, mâini FP, integrare, showcase). Urmează T_Insurgent_A, T_Insurgent_B și AK-K17, pe același pipeline.

Tot ce e mai jos se generează din cod. Blender rulează headless, iar Unity folosește meniurile din `CHOKEHOLD → Characters`. Nimic nu e editat de mână.

## 1. Cum se reface totul

```
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_textures.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_characters.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_weapons.py
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_anims.py -- Rifle
D:/Blender/blender.exe -b --factory-startup --python Tools/Blender/k17_anims.py -- Pistol
```

Apoi, în Unity, în ordine:

1. **Build Prefabs** — personajele: materiale, LOD, hitbox-uri, ragdoll, rig IK;
2. **Build Weapon Prefabs** — armele FP și TP cu socket-uri;
3. **Build Weapon Data** — asset-urile `WeaponData`;
4. **Build Animator** — controller-ul TP;
5. **Build FP Arms** — mâinile FP, layer-ul FirstPerson și legarea de jucător;
6. **Build Showcase Scene**;
7. **Render Showcase** — capturile din `Docs/Characters`.

## 2. Personaje

| Model | LOD0 | LOD1 | LOD2 | Buget LOD0 | Materiale |
|---|---|---|---|---|---|
| CT_Operator | 8 857 | 4 420 | 1 720 | ≤ 25 000 | Skin, Nylon, CT_Camo, Rubber, Plastic, Glass, PatchBlue, PatchGrey |
| FPArms_CT | 906 | — | — | ≤ 10 000 | Nylon, CT_Camo |

- **Schelet:** 55 de oase, compatibil Humanoid și Mixamo, plus `weapon_R` și `weapon_L_IK`. Înălțimea e 1,80 m, rădăcina la Y = 0, fața spre +Z.
- **Echipament CT:** cască cu șine și suport NVG, căști cu microfon, ochelari, balaclava, vestă cu port-încărcătoare, stație radio cu antenă, rucsac rigid, centură cu pungă, clește și carabinieră, toc pe coapsă, genunchiere, cotiere, mănuși întregi, petice pe umeri (fără mărci reale).
- **Pielea de sub gât** se scoate la ținutele care acoperă tot corpul. Nu se vede și ar străpunge hainele. Așa LOD0 a scăzut de la 17 430 la 8 857 triunghiuri.
- **UV-uri:** proiecție pe cub de 1 m. Camuflajul se repetă la 0,5 m, ca pe uniforma din referință.
- **Greutăți:** bone heat legase buzunarele pantalonilor de degetele mâinilor, care în A-pose stau lângă coapse. Când mâinile se mișcau, plăci întregi zburau după ele. Acum greutățile brațelor se scot de pe pantaloni, bocanci, partea de jos a vestei și corp. Mănușile merg pe falanga cea mai apropiată, ca degetele să se îndoaie.

## 3. Arme

| Armă | FP | TP | Piese animabile | Socket-uri |
|---|---|---|---|---|
| M4A4-M Vandal | 3 480 | 824 | Magazine, Bolt, ChargingHandle, Trigger, Selector, DustCover | Muzzle, Eject, Sight, Grip_R, Grip_L |
| M4-SD | 3 016 | 680 | la fel | la fel |
| M15-T Breacher | 1 304 | 344 | Magazine, Slide, Trigger | la fel |
| P-SD | 1 564 | 428 | Magazine, Slide, Trigger | la fel |
| AK-K17 | — | — | urmează cu teroriștii | — |

Bugetele sunt de cel mult 15 000 de triunghiuri pentru FP și 5 000 pentru TP.

- **Magnifier-ul** de pe puștile M4 stă rabatat lateral pe montura flip-to-side. Ochirea se face prin holo, iar tubul plin nu mai acoperă imaginea.
- **Reticule:** holo-ul are punct și inel de repere, iar red dot-ul are punct. Ambele sunt emisive, doar pe modelul FP.

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
| Arme | `WeaponData` cu prefab-urile FP și TP, statistici, offset hip și distanța ochiului la ADS |
| Ținte de antrenament | personajul CT idle cu P-SD, în loc de capsule |

Verificat în Play mode:
- 6 ținte și jucătorul au Animator activ, clipul corect, arma TP, 17 hitbox-uri și ragdoll în repaus;
- jucătorul are M4A4-M Vandal cu toate socket-urile, mâinile FP pe layer-ul FirstPerson și camera overlay în stack.

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

Toate sunt în `Docs/Characters/CT_Operator`:

| Ce | Fișiere |
|---|---|
| Model în Unity | `CT_Operator_Unity_Front / Back / Side / Head` |
| Poze din clipuri | `CT_Operator_Pose_*`: aim, run, crouch, reload, lean, plant, hit, pistol |
| LOD-uri | `CT_Operator_LOD0 / LOD1 / LOD2` |
| Mâini FP | `FP_<armă>_Hip`, `FP_<armă>_ADS` pentru Vandal, M4-SD și P-SD |
| Blender (A-pose, deformare) | `CT_Operator_Front / Back / Side / Head / Deform` |
| Stand de arme | `Docs/Characters/Weapons_Unity.png` |

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
| T_Insurgent_A, T_Insurgent_B, AK-K17, FPArms_T | următorul pas, pe același pipeline |
| Captura în hartă la 30 m, T vs CT | după teroriști |
| Texturi de 2048 pentru personaje | seturile procedurale sunt de 1024; ridicarea rezoluției e o opțiune din `k17_textures.py` |
| Animații FP ca clipuri | sunt procedurale în cod: cu IK pe socket-uri, mâinile stau pe orice armă fără clipuri separate |
| Calitatea animațiilor TP | poze-cheie generate din cod, rigide față de motion capture |
| Ochire cu magnifier-ul | rabatat lateral; o comutare 1× / 3× ar cere a doua poziție ADS |

## 10. Sugestii pentru calitate de producție

- **Animații TP:** Mixamo, gratuit. Scheletul K17 e compatibil Humanoid, deci clipurile Mixamo se pot retargeta direct pe CT. Sunt utile pachetele „Rifle” și „Pistol” pentru locomoție, aim, reload și moarte.
- **Asset Store:** un pachet de animații FPS (de exemplu „FPS Animation Pack” sau „Low Poly FPS Pack”) dă clipuri FP pentru mâini. Ele pot înlocui animațiile procedurale, iar IK-ul pe socket-uri rămâne pentru mâna stângă.
- **Detaliu de model:** un retopo pe un sculpt, sau un pachet de echipament militar modular, ar aduce cute și barete. Scheletul, socket-urile și pipeline-ul Unity rămân aceleași.
