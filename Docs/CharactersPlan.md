# CHOKEHOLD: Plan pentru personaje, arme și animații

Faza 0: analiză și plan. Ramura e `art/characters`.

Scopul: trei personaje riggate și animate (CT_Operator, T_Insurgent_A, T_Insurgent_B), mâinile la persoana întâi (FP arms) pentru fiecare echipă și cinci arme, fiecare cu un model la persoana întâi (FP) și unul la persoana a treia (TP). Totul se generează din scripturi Blender rulate headless și se integrează cu sistemele de joc existente.

---

## 0. Ce se poate obține din cod (de citit înainte de confirmare)

Pipeline-ul existent (`k17_kit.py`, `k17_textures.py`) construiește geometria din primitive și texturile din zgomot procedural. Aceeași abordare merge bine pentru unele părți și mai slab pentru altele:

| Element | Ce rezultă |
|---|---|
| Arme, echipament rigid (cască, vestă, pungi, rucsac, radio, toc, bocanci) | **Bine.** Sunt forme mecanice, ușor de construit din cod. Detaliile se văd ca normal map de la un model high-poly copt (bake) pe unul low-poly. |
| Corpul și hainele (falduri, cusături) | **Acceptabil, stilizat.** Corp anatomic simplificat, haine ca strat deasupra lui cu câteva falduri modelate procedural. Nu ajunge la nivelul referințelor (acelea sunt sculptate manual). |
| Camuflaj, uzură, murdărie | **Bine.** Texturi procedurale coapte în atlas. |
| Skinning | **Bine**, cu reguli de greutăți puse din cod: piesele rigide sunt legate de un singur os, iar umerii, coatele și genunchii au greutăți netezite. |
| Animații | **Funcționale, dar rigide.** Le scriu cadru cu cadru în Python: buclele de locomoție și clipurile de armă merg. Mișcarea organică (alergare, aterizare, moarte) arată mecanic față de motion capture. |

**Recomandarea mea:** construiesc totul din cod, cum cere brief-ul. Scheletul îl fac compatibil cu Unity Humanoid, așa că orice clip Mixamo sau din Asset Store se poate pune peste personaje fără nicio modificare la modele. Raportul final (Faza 7) va arăta clar ce clipuri merită înlocuite.

---

## 1. Referințe

Imaginile primite în chat sunt salvate în repo:

| Fișier | Folosit pentru |
|---|---|
| `Docs/Reference/Characters/CT_Operator.jpg` | CT_Operator (față, spate, centura separată) |
| `Docs/Reference/Characters/T_GasMask.jpg` | T_Insurgent_A (față, spate); punctul de plecare pentru T_Insurgent_B |
| `Docs/Reference/Weapons/AK_Rifle.jpg` | AK-K17 |
| `Docs/Reference/Weapons/M4_Vandal.jpg` | M4A4-M Vandal |
| `Docs/Reference/Weapons/M4_Suppressed.jpg` | M4-SD |
| `Docs/Reference/Weapons/M15T_Breacher.jpg` | M15-T Breacher |
| `Docs/Reference/Weapons/USP_Suppressed.jpg` | P-SD |

**Lipsește `M15T_Base_Photo.png`**, menționat în brief. Lucrez doar după `M15T_Breacher.jpg`.

**Mărci reale de înlocuit.** În referințe apar: logo Mauser, EOTech pe holo, „USP-S”, „RMR”/Trijicon pe red dot, steagul de pe brațul CT.
- Pe modele nu apare niciuna.
- Le înlocuiesc cu emblema K-17 și cu serii inventate (de exemplu `K17-ARS 0417`).
- Holo-ul devine „HV-1 Holographic”, iar red dot-ul devine „RD-3”.
- Patch-ul CT de unitate e un scut stilizat, nu un steag real.

---

## 2. Componentele fiecărui personaj

Prioritatea: **P1** = silueta și lizibilitatea în joc, **P2** = detaliu vizibil la distanță medie, **P3** = detaliu de aproape (poate fi doar textură).

### CT_Operator (vertical slice, primul)

| Componentă | Descriere din referință | Prioritate |
|---|---|---|
| Cască | Cască balistică neagră, tip high-cut, cu șine laterale | P1 |
| Montură NVG | Placă în față cu braț scurt, fără tub NVG | P2 |
| Căști de comunicație | Cupe pe urechi, microfon pe braț | P1 (lățesc silueta capului) |
| Ochelari | Ochelari tactici negri, lentilă închisă | P1 |
| Cagulă | Neagră, acoperă fața sub ochelari | P1 |
| Haină | Cu glugă (gluga stă pe spate, sub cască), camuflaj bleumarin/gri, guler înalt | P1 |
| Pantaloni | Cargo, același camuflaj, buzunare pe coapse | P1 |
| Plate carrier | Negru: placă frontală, 3 pungi de încărcătoare, panou de velcro sus | P1 |
| Radio | Radio pe umărul stâng, cablu spre cască | P2 |
| Patch-uri | „CT” albastru pe brațul stâng, patch de unitate inventat pe umărul drept | P2 (accent albastru) |
| Banderole | Benzi negre pe ambele brațe | P3 |
| Centură | Centură tactică: catarama, cleşte, carabinieră, cordon spiralat, pungă în spate | P2 |
| Toc de pistol | Toc pe coapsa dreaptă, cu două benzi | P1 |
| Genunchiere | Negre, rigide | P2 |
| Mănuși | Complete, negre | P2 |
| Bocanci | Negri, înalți, cu șireturi | P2 |
| Rucsac | Rigid, negru, cu fațete (hard-shell) | P1 (silueta din spate) |

### T_Insurgent_A (după T_GasMask.jpg)

| Componentă | Descriere | Prioritate |
|---|---|---|
| Glugă | Neagră, trasă peste cap | P1 |
| Mască de gaze | Verde-oliv, doi ochelari rotunzi, filtru frontal și filtru lateral | P1 (silueta T inconfundabilă) |
| Shemagh | Vișiniu, în carouri, înfășurat la gât | P1 (accent vișiniu) |
| Banderolă | Vișinie pe brațul drept, gri pe stângul | P1 |
| Haină | Camuflaj verde, mâneci suflecate cu cotiere | P1 |
| Pantaloni | Același camuflaj, cu buzunar pe coapsa stângă | P1 |
| Vestă | Tactică oliv, cu 3 încărcătoare AK curbate la vedere și 2 pungi laterale | P1 |
| Radio | Radio cu antenă lungă pe pieptul stâng | P1 (antena se vede în siluetă) |
| Grenadă | Prinsă pe pieptul drept | P2 |
| Toc de pistol | Pe coapsa dreaptă | P2 |
| Genunchiere și cotiere | | P2 |
| Mănuși | Fără degete, cu degetele goale | P2 |
| Bocanci | Maro, uzați | P2 |
| Rucsac | MOLLE oliv, cu catarame | P1 |
| Scut | Scut balistic mic, în lateral | P3 (opțional, decor; îl las pentru final) |

### T_Insurgent_B (variantă)

| Componentă | Diferența față de A | Prioritate |
|---|---|---|
| Cap | Cagulă neagră cu ochelari de protecție, fără glugă și fără mască de gaze | P1 |
| Eșarfă | Vișinie, legată peste nas și gură | P1 |
| Camuflaj | Maro/nisip pe haină, pantaloni oliv | P1 |
| Echipament | Chest rig ușor cu 4 încărcătoare, fără placă | P1 |
| Spate | Fără rucsac; geantă de umăr în bandulieră, pe stânga | P1 |
| Comun cu A | Banderolă vișinie, toc de pistol, genunchiere, bocanci | – |

**Lizibilitate la 30 m.**
- **CT:** cască lată cu căști de comunicație, siluetă închisă la culoare, albastru pe umeri.
- **T:** cap rotunjit (glugă și mască, respectiv cagulă), vișiniu la gât și pe braț, siluetă verde/maro.

În Faza 7 verific asta cu o captură din Blacksite K-17.

---

## 3. Corpul de bază și scheletul

**Corpul.** E un corp comun, de 1,80 m, cu rădăcina între picioare la Y = 0 și privirea spre +Z (aceleași convenții ca `U()` din `k17_kit.py`).
- **Sub haine:** ascuns aproape în întregime. Ce se acoperă se șterge la export, ca să nu treacă prin haine și să nu consume triunghiuri.
- **Hainele:** sunt carcase făcute din corp (offset de 1-2 cm) și împart scheletul cu el.

**Scheletul** e compatibil cu Unity Humanoid; cele două oase de armă sunt extra:

```
Root (la sol)
└─ Hips
   ├─ Spine ─ Chest ─ UpperChest
   │                  ├─ Neck ─ Head ─ (HeadTop_End)
   │                  ├─ Shoulder_L ─ UpperArm_L ─ LowerArm_L ─ Hand_L ─ Thumb/Index/Middle/Ring/Little 1-2-3_L
   │                  │                                                 └─ weapon_L_IK (punctul mâinii stângi pe armă)
   │                  └─ Shoulder_R ─ UpperArm_R ─ LowerArm_R ─ Hand_R ─ degete 1-2-3_R
   │                                                                    └─ weapon_R (socket-ul armei)
   ├─ UpperLeg_L ─ LowerLeg_L ─ Foot_L ─ Toes_L
   └─ UpperLeg_R ─ LowerLeg_R ─ Foot_R ─ Toes_R
```

**Skinning:**
- maxim 4 influențe pe vertex;
- piesele rigide sunt legate de un singur os: casca de Head, vesta de UpperChest, pungile de Chest, rucsacul de UpperChest, tocul de UpperLeg_R, genunchierele de LowerLeg;
- hainele moi au greutăți calculate după distanța de la os, netezite la umeri, coate, șolduri și genunchi;
- test de deformare (Faza 1): o poză ghemuit, cu ținta în sus și trunchiul răsucit, randată în `Docs/Characters/DeformTest.png`.

---

## 4. Texturi, LOD-uri, hitbox-uri

**Texturi.** Un atlas pe personaj, de 2048 × 2048, cu patru hărți: BaseColor, Normal, Metallic/Smoothness și AO. Se fac prin bake în Cycles, rulat headless.
- **Sursa:** materialele procedurale (camuflaj, țesătură, cauciuc, metal).
- **Detaliul în normal map:** vine din modelul high-poly (cusături, MOLLE, șireturi).
- **Armele:** un atlas de 1024 pe armă.

**LOD-uri:**

| LOD | Țintă |
|---|---|
| LOD0 | ≤ 25k triunghiuri |
| LOD1 | ~50% (decimate) |
| LOD2 | ~20% (decimate și piesele mici scoase) |

Toate sunt pe același schelet.

**Hitbox-uri.** Capsule și cutii legate de oase, pe stratul `Hitbox`. Fiecare are o componentă nouă, `HitZone`, cu un multiplicator:

| Zonă | Oase | Multiplicator |
|---|---|---|
| Cap | Head | ×4 |
| Gât | Neck | ×1 (fără headshot pe gât) |
| Piept | UpperChest, Chest | ×1 |
| Abdomen | Spine, Hips | ×1 |
| Brațe | Upper/LowerArm, Hand | ×0,75 |
| Picioare | Upper/LowerLeg, Foot | ×0,75 |

Ragdoll-ul folosește aceleași collidere, plus `Rigidbody` și `CharacterJoint`, active doar după moarte.

---

## 5. Armele

| Armă (nume în joc) | Echipă | Rol | Piese mobile | Detalii |
|---|---|---|---|---|
| **AK-K17** | T | pușcă | încărcător, manetă de armare, trăgaci, selector | mâner de lemn, șine M-LOK, grip vertical, holo HV-1 și magnifier, frână de gură, pat telescopic |
| **M4A4-M Vandal** | CT | pușcă | încărcător (translucid, cu cartușe), manetă de armare, trăgaci, selector, capac port | frână de gură, lanternă, PEQ (laser), holo HV-1 și magnifier, bandă camuflaj pe pat și încărcător |
| **M4-SD** | CT | pușcă cu amortizor | ca la M4 | amortizor, grip vertical, pat fix |
| **M15-T Breacher** | T | pistol | slide, încărcător, trăgaci, piedică | compensator, red dot RD-3, lanternă |
| **P-SD** | CT | pistol cu amortizor | slide, încărcător, trăgaci | amortizor lung (oțel ars, albăstrui), red dot, lanternă, bandă pe mâner |

**Socket-uri pe fiecare armă** (obiecte goale, exportate în FBX):
- `Muzzle`: flacăra din gura țevii;
- `Eject`: tuburile;
- `Sight`: punctul ochiului la ADS;
- `Grip_R` și `Grip_L`: pentru IK.

**Bugete de triunghiuri:**

| Model | Buget |
|---|---|
| Armă FP | ≤ 15k |
| Armă TP | ≤ 5k (fără interior, fără gravuri; aceleași socket-uri) |
| FP arms | ≤ 10k |

---

## 6. Animațiile

Toate sunt pe loc (in-place), fără root motion; mișcarea o face `FpsController`. Clipurile sunt pe scheletul humanoid și se exportă câte un FBX pe clip din `k17_anims.py`. Numele au forma `<Grup>_<Armă>_<Acțiune>`, de exemplu `Loco_Rifle_Run_FL`.

### Persoana a treia (×2: pușcă și pistol, unde contează)

| Grup | Clipuri | Total |
|---|---|---|
| Locomoție | idle; mers ×8 direcții; alergare ×8; sprint înainte; crouch idle; crouch walk ×8; întoarcere stânga/dreapta | 2 × 28 = 56 |
| Aer | start de săritură, cădere (buclă), aterizare | 2 × 3 = 6 |
| Țintire | aim offset în 9 poze (sus/centru/jos × stânga/centru/dreapta); lean stânga/dreapta | 2 × 11 = 22 |
| Armă | tragere, reîncărcare, reîncărcare din gol, scoatere, punere în toc | 5 × 2 = 10 |
| Interacțiuni | plantare, dezamorsare (buclă), deschidere ușă, urcare pe scară (buclă), montare baricadă | 5 |
| Reacții | hit react față/spate/stânga/dreapta | 4 (moartea se face prin ragdoll) |

Aproximativ **103 clipuri TP**.

### Persoana întâi (pe fiecare armă: 5 arme)

| Clipuri | Total |
|---|---|
| idle, scoatere, punere în toc, tragere, ADS intrare, ADS ieșire, reîncărcare, reîncărcare din gol, inspect, sprint | 10 × 5 = 50 |
| plantare, dezamorsare (FP arms, fără armă) | 2 |

Aproximativ **52 de clipuri FP**.

**Proceduralul rămâne în cod, peste animație.** `HitscanWeapon` are deja sway, bob, recul și ADS. Le păstrez și le aplic peste clipuri, cu ADS-ul aliniat pe socket-ul `Sight`.

---

## 7. Integrarea cu codul existent

**Ce există acum:**
- `TeamMember` (cu echipa), `Health` (`TakeDamage(float)`, evenimentul `Died`), `FpsController` (`IsCrouching`, `IsSprinting`, `IsGrounded`, lean, `ClimbLadder`);
- `HitscanWeapon`: raycast, sway, bob, recul, ADS și statistici direct în componentă;
- `BombController` (`IsPlanting`, `IsDefusing`), `RoundManager`, `Ladder`, `SimpleDoor`, `PlayerInteraction`;
- **nu există `BotController` și nici rețea.** Ceilalți „jucători” sunt dummy-uri capsulă (`TeamMember` + `Health`) puse de generatorul hărții.

**Ce adaug:**

| Componentă nouă | Rol |
|---|---|
| `WeaponData` (ScriptableObject) | modele FP/TP, damage, cadență, încărcător, timp de reîncărcare, recul, sunete, clipuri |
| `HitZone` | multiplicatorul de damage pe collider |
| `CharacterVisuals` | alege modelul după `TeamMember.Team` (T: A sau B după slot), afișează FP arms doar jucătorului local și lasă la el doar umbra corpului |
| `CharacterAnimatorDriver` | citește starea din `FpsController`, `HitscanWeapon`, `BombController`, `Ladder` și scrie parametrii în Animator |
| `Ragdoll` | pornește ragdoll-ul la `Health.Died` și îl oprește la respawn |
| `WeaponSockets` | găsește `Muzzle`, `Eject`, `Sight`, `Grip_*` în modelul armei |

**Ce modific:**
- `HitscanWeapon`: citește statisticile din `WeaponData`, aplică multiplicatorul din `HitZone`, pornește flacăra din `Muzzle` și publică evenimentele `Fired`/`Reloaded`. Câmpurile vechi rămân ca fallback, ca scena actuală să meargă și fără `WeaponData`.
- `Health`: o variantă `TakeDamage(damage, zone)` și direcția loviturii, pentru hit react.
- `TeamMember`: dezactivarea randărilor la moarte nu se mai aplică pe corp, pentru că ragdoll-ul rămâne vizibil.
- `FpsController`: expune `Velocity`, `Lean` și `IsClimbing` (doar citire).
- Dummy-urile din hartă: capsulele devin personaje în idle, cu hitbox-uri, ca ținte de antrenament.

**Animator Controller:**

| Layer | Conținut |
|---|---|
| Base | blend tree 2D (viteză × direcție) cu stări Stand/Crouch, plus Air și Ladder |
| UpperBody (mască) | aim offset 2D și lean, additive |
| Action (mască) | fire, reload, draw, holster, plant, defuse, door, barricade |
| HitReact | additive |

IK-ul se face cu **`com.unity.animation.rigging`** (pachet nou, trebuie adăugat în `manifest.json`):
- Two Bone IK pentru mâna stângă pe `Grip_L`;
- Multi-Aim pentru cap și coloană spre țintă.

---

## 8. Ordinea de lucru și commit-urile

1. **Faza 1:** corp de bază, schelet, skinning și test de deformare. Commit.
2. **Vertical slice CT_Operator:** model, LOD-uri, hitbox-uri, M4A4-M Vandal și P-SD, clipurile TP și FP de bază, integrare în Unity, scena showcase. Commit, apoi ți-l arăt și aștept feedback.
3. **T_Insurgent_A** cu AK-K17 și M15-T Breacher. Commit.
4. **T_Insurgent_B** (refolosește clipurile și armele T). Commit.
5. **M4-SD**, restul animațiilor, verificare, randări, comparație cu referințele și raportul `Docs/CharactersReport.md`. Commit.

Fișierele noi:
- **Scripturile Blender:** `Tools/Blender/k17_characters.py`, `k17_weapons.py`, `k17_anims.py`, cu helper-ele comune într-un modul `k17_common.py` extras din `k17_kit.py`.
- **Ieșirile:**
  - `Assets/_Project/Art/Characters/<Nume>/` (FBX, atlas, materiale);
  - `Assets/_Project/Art/Weapons/<Nume>/`;
  - `Assets/_Project/Art/Animations/`.

---

## 9. Decizii de confirmat

1. **Calitatea modelelor și animațiilor generate din cod** e cea descrisă la secțiunea 0. Scheletul rămâne compatibil cu Mixamo și Asset Store, ca să poți înlocui clipurile mai târziu. Mergem așa?
2. **Pachetul `com.unity.animation.rigging`:** pot să-l adaug în `manifest.json`?
3. **`M15T_Base_Photo.png` lipsește:** lucrez doar după `M15T_Breacher.jpg`?
4. **Dummy-urile capsulă din hartă:** le înlocuiesc cu personaje în idle (CT în site-uri, cum sunt acum)?
5. **Gâtul:** îl las la ×1 (corp), cum am propus, sau vrei headshot și pe gât (×4)?
