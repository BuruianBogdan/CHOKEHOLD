# Grenada cu pini (Needle Grenade)

Utilitar nou, pe lângă smoke, flash și molotov. Când explodează, aruncă un nor de ace fine în jur. Acele care te nimeresc te orbesc pe partea din care au venit, adică cam jumătate din ecran. Cealaltă parte rămâne curată, așa că poți juca mai departe, doar că ești „chior” pe o parte.

## Cum se joacă

| Proprietate | Valoare de pornire |
|---|---|
| Preț | 300 |
| Câte poți căra | 1 |
| Fitil | 1,4 s (ca la flash) |
| Raza acelor | 9 m. Acele sunt hitscan, cu linie de vedere de la grenadă la cap sau torace. |
| Ace aruncate | 24, în toate direcțiile, cu puțină împrăștiere |
| Damage | 4 pe ac, maxim 20. Nu omoară, doar orbește. |
| Durata orbirii | 2,5 s la un ac, crește la 4,5 s la 6 ace sau mai multe. Ultimele 1,2 s se retrage treptat. |
| Friendly fire | Orbește și coechipierii, ca flash-ul, dar fără damage. |

- **Partea orbită** e partea camerei din care au venit acele. Dacă vin din dreapta, se întunecă dreapta. Dacă vin din față, se întunecă partea ochiului mai apropiat de grenadă. Dacă vin din spate, efectul e doar slab, pe margini.
- **Contra:** dacă te întorci cu spatele la grenadă sau stai după un perete, nu te nimerește. Un scut sau o cască grea ar putea opri câteva ace (idee pentru mai târziu).
- **Diferența față de flash:** flash-ul te orbește complet, dar scurt. Acele te orbesc pe jumătate, dar mai mult timp. Le folosești când vrei ca inamicul să nu vadă o parte a camerei, de exemplu când intri pe ușa din stânga lui.

## Cum arată efectul (după schița ta)

- Partea orbită nu e un dreptunghi negru. Granița e curbată: pe linia ochilor, zona vizibilă e mai lată, iar sus și jos întunericul intră mai mult spre centru. Așa e și câmpul real al ochiului, umbrit de nas.
- **Crucea rămâne vizibilă.** Granița trece pe lângă centru, nu peste el.
- Marginea e estompată pe 8–10% din ecran și tremură ușor, ca o pleoapă care clipește. Lângă graniță imaginea e puțin încețoșată și desaturată, apoi trece în negru-roșiatic, nu negru plat.
- La impact: un flash roșu scurt pe partea aceea, câteva fire subțiri, ca niște capilare, care pornesc din margine, și un șuierat în urechea din partea aceea, cu sunetul înfundat.
- La final, întunericul se retrage spre marginea ecranului, nu se stinge dintr-o dată.

## Implementare în Unity (plan)

- `NeedleGrenade` reutilizează aruncarea și fitilul de la flash. La explozie aruncă raze spre cap și torace pentru fiecare jucător din rază și numără acele care au nimerit.
- `NeedleBlindEffect` stă pe camera FP. Primește partea (`side = ±1`), câte ace au nimerit și ora impactului.
- Un shader full-screen URP calculează masca pe fiecare pixel. Coordonatele `x` și `y` sunt ecranul, de la 0 la 1; `x` e oglindit după parte:
  - `limita(y) = 0,52 + 0,10 · (1 − (2y − 1)²)` dă granița curbată, mai lată pe mijloc;
  - `mască = smoothstep(limita, limita + 0,09, x + zgomot · 0,01)`.
- Durata și forța vin din numărul de ace. La final, `limita` crește spre 1, ca întunericul să se retragă spre margine.
- Pe rețea, serverul decide câte ace au nimerit și din ce parte. Clientul doar desenează efectul.
