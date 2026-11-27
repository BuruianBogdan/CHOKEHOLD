# Tripo, runda 2: terminale, dispozitive, grenade și înlocuiri pe hartă

Ai 3900 de puncte. Piesele sunt grupate pe priorități: începi cu **P1**, apoi treci la P2 și P3 cât îți ajung punctele. După primul model vezi cât ți-a scăzut din puncte și împarți restul. Lasă cam 20% pentru regenerări, pentru că unele ies prost din prima.

Procedura e aceeași ca la runda 1 (vezi `Props_Prompts.md`):
- Text to 3D, **HD Model** cu texturi, fără rig.
- Exporți GLB sau FBX/zip cu numele din titlu și îl pui în `Inbox/Props`.
- Pentru grenade și grappling hook folosești `Inbox/Weapons`.
- Fiecare prompt are sub 1000 de caractere.

**Ecranele terminalelor** le cer negre și goale, ca sticlă. În Unity pun peste ele un ecran real, pe care desenez UI-ul live: Protocol 21, licitația, datoriile, rețetele Fusion și starea jammer-ului. Dacă Tripo desenează ceva pe ecran, nu contează, îl acopăr.

---

## P1 — Obiectivul și terminalele The House (ideea principală a jocului)

### Signal_Jammer — 1,2 × 1,2 × 1,6 m (obiectivul rundei)
```
A single military signal jammer node, the hero objective of a tactical shooter, heavy rugged steel base on four short legs with bolted armor plates, a central upright equipment column with stacked electronic modules, cooling fins and thick armored cables, three folding directional antenna panels and two whip antennas on top, a recessed flat black glass status screen on the front, a big guarded activation switch under a flip-up cover, small red and green indicator lamps, warning stripes on the base. Olive drab and dark grey paint, chipped edges, dust and grime, used in the field. Realistic PBR materials, metal and rubber. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### House_Terminal_Kiosk — 0,8 × 0,6 × 1,9 m (terminalul The House, în picioare)
```
A single tall freestanding secure data terminal kiosk inside a secret military facility, armored dark gunmetal steel cabinet with angled front, one large flat black glass touchscreen recessed behind a protective steel frame, a rugged metal keypad, a card slot and a small receipt printer slot below the screen, a thin strip of cold white light along the top edge, small camera dome on top, ventilation slots on the sides, heavy bolted base plate. Clean, expensive and slightly sinister, a corporate machine in a military bunker. Realistic PBR materials, brushed and painted metal, glass. Isolated single object, no floor, no background, no text, no logos, no brand names. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### House_Terminal_Wall — 0,9 × 0,25 × 1,1 m (variantă de perete)
```
A single wall-mounted secure military terminal panel, thick armored steel housing that bolts onto a wall, a wide flat black glass screen in the center behind a protective frame with corner bolts, a rugged metal keypad and a card reader on the right side, two small status lamps, a protective hinged hood over the top, armored conduit pipe going up out of the top. Dark gunmetal and black paint, light scratches and wear. Realistic PBR materials, metal and glass. Isolated single object, flat back side for mounting on a wall, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop, real-world proportions.
```

### House_Terminal_Field — 0,6 × 0,45 × 0,3 m (terminal portabil, valiză)
```
A single rugged military field terminal built into an open hard-shell carry case, the lid stands open showing a flat black glass screen inside the lid, the base holds a sealed rubber keyboard, a trackball, rows of connectors and a coiled cable, the case is olive drab polymer with metal latches, corner protectors and a carry handle, foam padding visible around the electronics. Used, dusty, scratched. Realistic PBR materials, plastic, rubber and metal. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Fusion_Station — 1,8 × 0,9 × 1,3 m (Resource Fusion: rețete și sloturi)
```
A single industrial fabrication workstation from a military black site, heavy steel table with a flat black glass screen standing on an arm at the back, in front of it four empty contribution slots: square metal receptacles with sliding armored lids and green ring lights, a central sealed assembly chamber with a thick glass window and a mechanical clamp arm inside, cable trays and pipes underneath, warning stripes on the edges. Dark grey and olive paint, scuffed, oily, used. Realistic PBR materials, metal, glass, rubber. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Escrow_Locker — 1,2 × 0,6 × 2,0 m (Weapon Escrow, depui arma la The House)
```
A single armored weapon escrow locker bank from a secret military facility, tall steel cabinet with three vertical locker doors, each door has a narrow reinforced glass window, a heavy rotary lock and a small status lamp, the middle door is slightly open showing an empty padded rifle rack inside, a flat black glass screen and a card reader built into the frame above the doors. Dark gunmetal steel, worn paint on edges, scratches near the handles. Realistic PBR materials, metal and glass. Isolated single object, no floor, no background, no weapons, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Supply_Crate_Smuggling — 1,1 × 0,7 × 0,6 m (Supply Smuggling, lada clandestină)
```
A single clandestine military supply crate, heavy reinforced case made of dark olive composite with metal corner guards, side handles and thick latches, a small electronic lock panel with a keypad on the front, a short telescopic beacon antenna with a round amber beacon light on top of the lid, a parachute harness strap wrapped around it, stenciled warning stripes, mud and scratches. Rugged, smuggled, used. Realistic PBR materials, composite, metal and rubber. Isolated single object, lid closed, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Alarm_Beacon — 0,3 × 0,3 × 0,45 m (Pressure State, lumina roșie de alarmă)
```
A single industrial rotating alarm beacon light with a siren horn, mounted on a small steel wall bracket, a red glass dome with a mirror reflector inside, a black metal horn speaker below it, a steel protective wire cage around the dome, cable going into a junction box. Worn, slightly dusty, military facility. Realistic PBR materials, glass, metal and plastic. Isolated single object, flat back side for mounting on a wall, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop.
```

### Door_Control_Panel — 0,3 × 0,12 × 0,45 m (cititor de card și comanda ușilor)
```
A single wall-mounted security door control panel from a military facility, rugged steel housing, a small flat black glass display at the top, a card reader slot, a metal keypad with large buttons, a big red emergency lock button under a flip cover, two status lamps, armored cable conduit going down out of the bottom. Dark grey paint, scratched around the keypad, slightly grimy. Realistic PBR materials, metal and glass. Isolated single object, flat back side for mounting on a wall, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop.
```

---

## P1 — Dispozitivele de echipă (Resource Fusion)

Docul spune că dispozitivul de echipă e unul dintre Jammer, Tactical Screen, Reinforced Lock și Emergency Scanner. **Tactical_Screen** e dispozitivul care afișează lucruri: harta, pozițiile marcate și timer-ul.

### Tactical_Screen — 0,9 × 0,5 × 1,5 m (ecran tactic pe trepied)
```
A single deployable military tactical display, a large flat black glass screen in a rugged rubber-armored frame with handles on both sides, mounted on a folding heavy-duty steel tripod with spiked feet, a battery pack and a small antenna clipped to the back, cables running down one tripod leg, a sun hood over the top of the screen. Olive drab and black, dusty, used in the field. Realistic PBR materials, metal, rubber and glass. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, game-ready prop, real-world proportions.
```

### Reinforced_Lock — 0,5 × 0,25 × 0,6 m (se prinde pe o ușă și o blochează)
```
A single heavy portable door reinforcement lock device, a thick steel brace bar with hydraulic pistons on both ends, a central armored control box with a small flat black glass display and a keypad, magnetic mounting pads, yellow and black warning stripes, red status lamp. Rugged military engineering gear, scratched and dusty. Realistic PBR materials, metal and rubber. Isolated single object, flat back side for mounting on a door, no door, no background, no text, no logos. Clean closed geometry, game-ready prop.
```

### Emergency_Scanner — 0,5 × 0,5 × 1,1 m (scanner care dezvăluie inamicii)
```
A single portable military area scanner, a short folding tripod holding a rotating sensor head with a round radar dish and a cluster of camera lenses, a battery case at the base with a carry handle, a small flat black glass display on the side, blue status lights around the sensor head, cables tied with straps. Olive and dark grey, worn and dusty. Realistic PBR materials, metal, plastic and glass. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

---

## P1 — Grenade și echipament (le pregătesc ca arme FP și TP)

### Grenade_Frag — Ø 6 × 10 cm
```
A single military fragmentation hand grenade, round segmented steel body painted olive drab, a spoon lever along the side, a steel fuse assembly on top with a ring pull pin and a safety clip, slight paint wear on the edges. Realistic PBR materials, metal. Isolated single object, no hand, no background, no text, no logos. Clean closed geometry, game-ready prop for a first-person shooter, real-world proportions.
```

### Grenade_Smoke — Ø 6 × 15 cm
```
A single military smoke grenade, a smooth cylindrical sheet metal canister painted grey with a light grey band, small round emission holes on the top and bottom, a spoon lever and a ring pull pin on top, slight dents and paint wear. Realistic PBR materials, metal. Isolated single object, no hand, no smoke, no background, no text, no logos. Clean closed geometry, game-ready prop for a first-person shooter, real-world proportions.
```

### Grenade_Flash — Ø 5 × 13 cm
```
A single military stun grenade flashbang, a black cylindrical steel body with many rectangular vent ports cut along its sides, a spoon lever and a ring pull pin on top, a blue band at the base, slightly worn black paint showing metal on the edges. Realistic PBR materials, metal. Isolated single object, no hand, no background, no text, no logos. Clean closed geometry, game-ready prop for a first-person shooter, real-world proportions.
```

### Molotov — Ø 8 × 28 cm
```
A single improvised incendiary bottle, a dark green glass bottle half filled with amber fuel, a dirty torn cloth rag stuffed into the neck and tied with wire, the rag hanging down the side, some fuel stains on the glass and cloth. Realistic PBR materials, glass, liquid and fabric. Isolated single object, no fire, no hand, no background, no label, no text. Clean closed geometry, game-ready prop for a first-person shooter, real-world proportions.
```

### Grenade_Needle — Ø 7 × 11 cm (grenada noastră cu pini)
```
A single futuristic military needle grenade, a compact ribbed steel sphere slightly flattened, its surface covered with many small hexagonal launch ports, each port holding the tip of a thin silver needle, a glowing cyan ring band around the middle, a spoon lever and a ring pull pin on top, dark gunmetal finish with light wear. Grounded near-future tech, realistic, not cartoon. Realistic PBR materials, metal. Isolated single object, no hand, no background, no text, no logos. Clean closed geometry, game-ready prop for a first-person shooter.
```

### Grapple_Launcher — 0,45 × 0,1 × 0,25 m (urcat rapid pe clădiri)
```
A single handheld military grappling hook launcher, a compact pistol grip with a trigger and a short rail frame, a fat motorized winch drum on top wound with black synthetic rope, a short wide barrel with a folded four-pronged steel grappling hook loaded in the muzzle, a battery pack under the barrel, a sling attachment point. Matte black and olive polymer with worn steel parts. Realistic PBR materials, metal, polymer and rope. Isolated single object, no hand, no background, no text, no logos. Clean closed geometry, game-ready weapon for a first-person shooter, real-world proportions.
```

### Breach_Charge — 0,4 × 0,08 × 0,25 m
```
A single military breaching charge for doors and wooden walls, a flat rectangular explosive pack wrapped in olive tape, a detonator box with a small red lamp and a short antenna on top, adhesive pads on the back, coiled detonation cord along the edge. Used and dusty. Realistic PBR materials, fabric tape, plastic and metal. Isolated single object, flat back side, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop.
```

### Signal_Scrambler — 0,15 × 0,1 × 0,2 m (utilitar aruncat)
```
A single throwable military signal scrambler device, a small rugged box shape with rounded rubber bumpers on the corners, three short stubby antennas on top, a ring of blinking blue LEDs, a folding steel spike on the bottom to stick into the ground. Matte dark grey with olive rubber, scratched. Realistic PBR materials, metal, rubber and plastic. Isolated single object, no hand, no background, no text, no logos. Clean closed geometry, game-ready prop for a first-person shooter.
```

---

## P2 — Înlocuiri pe hartă (cutiile simple, barierele, ușile)

### Wood_Wall_Breakable — 2,0 × 0,15 × 2,6 m (peretele de lemn care se sparge)
```
A single temporary wooden partition wall panel from a military warehouse, vertical rough pine planks nailed to a frame of horizontal wooden beams, some planks with knots and cracks, stenciled warning stripes on the lower part, a few bent nails and metal corner brackets, dusty and slightly weathered. Realistic PBR materials, wood and metal. Isolated single object, flat and rectangular, both sides finished, no floor, no background, no text, no logos. Clean closed geometry, game-ready prop.
```
O să-l sparg în bucăți în Blender și o să păstrez și varianta întreagă. Nu cere Tripo să-l spargă.

### Crate_Wood_Large — 1,2 × 1,2 × 1,2 m
```
A single large military wooden supply crate, thick pine planks with dark reinforced wooden edges and steel corner brackets, rope handles on two sides, stenciled arrow and hazard symbols, scuffs, dirt and water stains, lid nailed shut. Realistic PBR materials, wood and metal. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Crate_Stack_Military — 1,8 × 1,2 × 1,6 m
```
A stack of military supply cases as one object: two long olive drab plastic ammunition crates on the bottom, one medium metal ammo can and a smaller green hard case on top, held together with yellow ratchet straps, latches and handles on every case, worn edges, dust. Realistic PBR materials, plastic, metal and fabric. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Container_Shipping — 6,0 × 2,4 × 2,6 m
```
A single 20 foot steel shipping container, corrugated walls painted faded military green, rust streaks and chipped paint, double cargo doors with locking bars and handles on one end, closed, corner castings, a few dents. Realistic PBR materials, painted and rusty metal. Isolated single object, no floor, no background, no text, no logos, no brand names. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Barrier_Concrete_High — 2,0 × 0,6 × 1,8 m (high cover)
```
A single tall precast concrete T-wall barrier segment used at military checkpoints, wide flat base and a tall thick wall, two steel lifting loops on top, weathered grey concrete with stains, chipped corners, a faded yellow and black hazard band near the bottom. Realistic PBR materials, concrete and metal. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Barrier_Hesco — 1,0 × 1,0 × 1,2 m (sandbag de curte)
```
A single military defensive barrier cell, a square wire mesh basket lined with thick beige geotextile fabric and filled with packed sand and gravel visible at the top, slightly bulging sides, dirt and dust. Realistic PBR materials, metal mesh, fabric and sand. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Door_Security — 1,2 × 0,2 × 2,3 m (ușa grea, cu toc)
```
A single heavy armored security door with its steel frame, thick grey painted steel door leaf with a small reinforced glass window with wire mesh, a heavy lever handle and a push bar, hinges on one side, a rubber seal, a yellow and black hazard stripe at the bottom, scratches and grime near the handle. Realistic PBR materials, metal and glass. Isolated single object, door closed, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop, real-world proportions.
```

### Door_Rollup — 3,0 × 0,3 × 3,0 m (ușa rulantă de hangar)
```
A single industrial roll-up shutter door with its housing, horizontal corrugated steel slats, a steel roll housing box at the top, side guide rails, a bottom bar with a pull handle, faded grey paint, rust at the bottom, dents. Realistic PBR materials, metal. Isolated single object, door fully closed, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop, real-world proportions.
```

### Truck_Military — 7,5 × 2,5 × 3,2 m
```
A single military cargo truck parked, six wheels, a flat-nosed cab with a canvas-covered cargo bed at the back, olive drab paint with mud on the lower half, a spare tire, jerry cans on the side, wire mesh over the headlights. Realistic PBR materials, metal, canvas and rubber. Isolated single object, no driver, no floor, no background, no text, no logos, no brand names. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Floodlight_Tower — 1,5 × 1,5 × 6,0 m
```
A single mobile floodlight tower, a small towed trailer base with a diesel generator box and four stabilizer legs, a tall telescopic mast with four rectangular floodlight panels at the top, a cable spiralling down the mast. Yellow paint faded and dirty, rust spots. Realistic PBR materials, metal, glass and rubber. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Generator_Diesel — 2,2 × 1,0 × 1,4 m
```
A single industrial diesel generator unit, an enclosed steel box with ventilation louvres, access panels with latches, a control panel with gauges and switches behind a hinged cover, an exhaust pipe on top, a fuel tank base frame with lifting points, thick power cables coming out of one side. Olive drab paint, oil stains, scratches. Realistic PBR materials, metal and rubber. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Antenna_Rooftop — 1,5 × 1,5 × 3,0 m (pe acoperișuri)
```
A single rooftop military communications mast, a steel lattice tower section on a heavy square ballast frame with concrete blocks, a round satellite dish and two panel antennas mounted on it, a junction box and cables running down, a red warning light on top. Weathered galvanized steel, rust streaks. Realistic PBR materials, metal and concrete. Isolated single object, no roof, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

---

## P3 — Detalii, dacă îți mai rămân puncte

### Pipe_Run_Wall — 3,0 × 0,4 × 1,0 m
```
A single section of industrial wall piping, three parallel steel pipes of different diameters running horizontally, with flanges, a big valve wheel, a pressure gauge, wall brackets and a small junction box, yellow pipe markings, rust and grime. Realistic PBR materials, metal. Isolated single object, flat back side for mounting on a wall, no wall, no background, no text, no logos. Clean closed geometry, game-ready prop.
```

### Reactor_Coolant_Pump — 1,6 × 1,2 × 1,8 m (zona reactorului)
```
A single heavy nuclear plant coolant pump assembly, a large vertical cylindrical electric motor on top of a thick pump casing, big inlet and outlet pipes with bolted flanges cut short, a steel skid base, warning stripes and radiation symbols, grey and yellow paint, grime. Realistic PBR materials, metal. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

### Cable_Drum — Ø 1,2 × 0,8 m
```
A single large wooden industrial cable drum lying on its side, thick black power cable wound around it, wooden flanges with bolts, dirty and weathered. Realistic PBR materials, wood and rubber. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, game-ready prop, real-world proportions.
```

### Fuel_Drum_Pallet — 1,2 × 1,2 × 1,0 m
```
A wooden pallet with four steel fuel drums on it as one object, drums painted dark blue and olive with dents and rust, a yellow spill containment tray under them, one drum with a hand pump on top. Realistic PBR materials, metal and wood. Isolated single object, no floor, no background, no text, no logos. Clean closed geometry, flat bottom, game-ready prop, real-world proportions.
```

---

## Texturile de pe pereți și podea (nu se fac în Tripo)

Tripo face obiecte 3D, nu texturi care se repetă pe pereți și pe podea. Pentru astea folosim biblioteci gratuite CC0, bune și pentru jocuri comerciale: **ambientCG** și **Poly Haven**. Nu costă puncte.

Am nevoie de ok-ul tău ca să le descarc. Pentru fiecare suprafață iau un set PBR la 2K (culoare, normal, roughness și AO):

| Unde pe hartă | Material |
|---|---|
| Pereții de beton din curte și ai clădirilor | beton turnat cu urme de cofraj, beton vopsit uzat |
| Pereții interiori (laborator, birouri) | panouri de beton vopsite, faianță industrială |
| Podeaua din interior | beton șlefuit pătat, epoxy industrial, tablă striată la depozit |
| Curtea, drumurile | asfalt crăpat, pietriș, beton de exterior |
| Hangar și depozit | tablă cutată, plăci metalice cu nituri |
| Acoperișuri | membrană bituminoasă, pietriș de acoperiș |
| Reactor | plăci de oțel, grătare metalice |
| Peretele de lemn | scânduri de pin brut |

Pe lângă ele adaug **decal-uri** pentru pete, scurgeri, dungi de avertizare și marcaje pe podea, ca suprafețele să nu arate repetitive.
