using ChokeHold.Gameplay;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Spawn-urile si zonele exterioare: T1 Spawn, T1 Ramp, camera de securitate, West Outpost,
    // West Rocks, T2 Spawn, East Yard, North Overlook / North Catwalk.
    //
    // Fiecare spawn are trei iesiri acoperite si nicio linie de vedere directa in mid:
    //   T1: usa dubla spre T1 Ramp, camera de securitate (spre curte), rampa tunelului.
    //   T2: usa dubla spre North Overlook (si scara spre catwalk), doua usi spre East Yard.
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSpawnsAndOuterZones()
        {
            BuildTeam1Side();
            BuildTeam2Side();
            BuildNorthOverlook();
        }

        // ------------------------------------------------------------------
        // Atacatori: spawn, T1 Ramp, camera de securitate, West Outpost, West Rocks
        // ------------------------------------------------------------------
        private static void BuildTeam1Side()
        {
            float low = GroundCeiling - 0.4f;
            float full = InnerWallHeight;

            Vector3[] spawnPoints =
            {
                new Vector3(-60f, 0.1f, -44f),
                PlayerStart,
                new Vector3(-52f, 0.1f, -44f),
                new Vector3(-58f, 0.1f, -38f),
                new Vector3(-54f, 0.1f, -38f),
            };

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                CreateSpawnPoint(gSpawnsT1, $"Spawn_T1_{i + 1}", spawnPoints[i], 0f, Team.Attackers);
            }

            // Camionul cu care au venit atacatorii, parcat langa zidul de vest (cover in spawn).
            Block(gCover, "T1_Truck", -68f, -44f, -65.5f, -37.5f, 3f);

            // Iesirea principala din spawn: usa dubla spre T1 Ramp.
            Partition("Part_T1Spawn_North", -60f, -28f, -44f, -28f, full, DoubleGap(13f));

            // T1 Ramp: nod cu trei iesiri (West Outpost, pasarelele spre mid, camera de securitate)
            InnerWall(gStructures, "T1Ramp_Wall", -44f, -30f, -44f, -23f, GroundCeiling);
            Partition("Part_Outpost_South", -60f, -22f, -54f, -22f, full, DoubleGap(3f));
            Partition("Part_Outpost_East", -54f, -22f, -54f, -16f, full);
            Partition("Part_T1Ramp_Walkway", -36f, -22f, -36f, -16f, full, DoubleGap(3f));
            // Inchide fanta de 1,5 m dintre capatul peretelui de pasarela si coloana de colt a cladirii de la est (z = -22)
            Partition("Part_T1Ramp_Walkway_Link", -35.75f, -21.74f, -34.25f, -21.74f, full);
            Partition("Part_Walkway_Courtyard", -28f, -22f, -28f, -14f, full, DoorGap(2f));
            Partition("Part_Walkway_Courtyard_Corner", -28f, -14f, -26f, -14f, full);

            HighCover(-49.5f, -24f, -44.5f, -24f);     // paravan in fata usii din spawn
            HighCover(-40f, -21.5f, -40f, -16.5f);     // paravan in fata usii spre pasarele
            HighCover(-39.5f, -27f, -34.5f, -27f);     // paravan in fata usii camerei de securitate
            BigCrate(-31f, -19f);
            HighCover(-24f, -22.5f, -24f, -17.5f);     // paravan in curte, in fata usii dinspre pasarele

            // Camera de securitate: a treia iesire din spawn (spawn -> securitate -> curte sau T1 Ramp)
            Roof("Roof_Security", -42f, -46f, -30f, -32f, UpperFloorHeight);
            DoorCell("Door_Security_North", -38f, -32f, -36f, -30f, false, full);
            DoorCell("Door_Security_East", -30f, -40f, -28f, -38f, true, full);
            DoorCell("Door_Security_West", -44f, -42f, -42f, -40f, true, full);

            Block(gStructures, "Security_Lockers", -42f, -37f, -41f, -33f, 2.2f);
            Crate(gCover, -36f, -44.5f, 4f, 1f, 1.2f);
            LowCover(-33f, -34f, -31f, -34f);
            HighCover(-39f, -42.5f, -39f, -39.5f);     // paravan in spatele usii dinspre spawn
            Lamp("Light_Security", -36f, low, -39f, ColdLight, 12f, 2.5f);

            // West Outpost
            Partition("Part_Outpost_Rocks", -56f, -4f, -56f, 6f, full, DoubleGap(5f));
            Partition("Part_Outpost_Rocks_East", -56.22f, -4.29f, -52.72f, -4.29f, full);   // bratul spre est, adaugat de mana

            Block(gStructures, "Outpost_Tower", -70f, -14f, -64f, -8f, 4f);
            SolidBlock(gStructures, "Outpost_Shed", -76f, -6.5f, -72f, -1.5f, 0f, 3f, containerBlueMat);
            HighCover(-60f, -14f, -56f, -14f);         // paravan in fata usii dinspre T1 Ramp
            BigCrate(-65f, -1f);
            HighCover(-72.5f, 1.5f, -69.5f, 1.5f);
            LowCover(-66f, -17f, -62f, -17f);

            // West Rocks: lada ca cover pe drumul spre A Control
            BigCrate(-50f, 6f);
        }

        // ------------------------------------------------------------------
        // Aparatori: spawn, East Yard
        // ------------------------------------------------------------------
        private static void BuildTeam2Side()
        {
            float up = UpperFloorHeight;
            float full = InnerWallHeight;

            Vector3[] spawnPoints =
            {
                new Vector3(64f, 0.1f, 50f),
                new Vector3(68f, 0.1f, 50f),
                new Vector3(72f, 0.1f, 50f),
                new Vector3(66f, 0.1f, 44f),
                new Vector3(70f, 0.1f, 44f),
            };

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                CreateSpawnPoint(gSpawnsT2, $"Spawn_T2_{i + 1}", spawnPoints[i], 225f, Team.Defenders);
            }

            // Iesirile din spawn. Peretele de vest e pe toata inaltimea: opreste si linia
            // de pe North Catwalk inainte de spawn.
            Partition("Part_T2Spawn_West", 56f, 42f, 56f, 56f, full, DoubleGap(3.5f));
            Partition("Part_T2Spawn_South", 56f, 38f, 78f, 38f, full, DoorGap(2.5f), DoubleGap(15.5f));
            HighCover(60f, 43f, 60f, 48f);             // paravan in spatele usii spre North Overlook
            HighCover(56.5f, 41f, 61.5f, 41f);           // paravan in spatele usii simple spre East Yard

            // East Yard: cladirea de generator in mijloc face din curte un inel
            Block(gStructures, "Yard_Generator", 58f, 20f, 68f, 28f, 4f);
            BigCrate(76.5f, 17f);          // taie linia lunga din T2 Spawn spre sudul curtii

            HighCover(52.5f, 26f, 52.5f, 33f);         // paravan in fata usii anexei
            HighCover(61f, 6f, 66f, 6f);               // paravan in fata usii spre hangar
            BigCrate(70f, 24f);
            Container(gCover, 70f, 31f, 0f, containerBlueMat);
            HighCover(52f, 12f, 52f, 17f);
            BigCrate(72f, 12f);

            // Balconul anexei, cu scari in curte
            Stairs(gStairs, "Stairs_EastYard", 46f, 18f, 50f, 26f, Vector3.forward, 0f, up);
            CreatePlatform(gUpper, "EastYard_Balcony", 46f, 26f, 50f, 32f, up);
            SolidBlock(gUpper, "EastYard_Balcony_Screen", 49.6f, 26f, 50f, 32f, up, 2.2f, wallMat);
            Railing(gUpper, "EastYard_Balcony_Rail_N", 46f, 32f, 50f, 32f, up);
        }

        // ------------------------------------------------------------------
        // North Overlook (culoarul de la sol) si North Catwalk (pasarela de deasupra)
        // ------------------------------------------------------------------
        private static void BuildNorthOverlook()
        {
            float up = UpperFloorHeight;
            float wall = GroundCeiling;
            float full = InnerWallHeight;

            Stairs(gStairs, "Stairs_T2_To_Catwalk", 46f, 52f, 54f, 56f, Vector3.left, 0f, up);

            // North Catwalk: de la scara T2 pana in A Heaven. Linie lunga intentionata (vezi MapAudit.md).
            CreatePlatform(gUpper, "North_Catwalk", -24f, 52f, 46f, 56f, up);
            Railing(gUpper, "North_Catwalk_Rail_W", -22f, 52f, 1f, 52f, up);
            Railing(gUpper, "North_Catwalk_Rail_E", 5f, 52f, 46f, 52f, up);

            for (float x = -16f; x <= 40f; x += 14f)
            {
                CreateBox(gUpper, $"North_Catwalk_Pillar_{x:0}", new Vector3(x, GroundCeiling * 0.5f, 52.3f),
                    new Vector3(0.5f, GroundCeiling, 0.5f), pipeMat);
            }

            // Culoarul de la sol e impartit in doua curti de un perete cu usa dubla;
            // peretele se opreste la catwalk, care trece liber pe deasupra.
            Partition("Part_Overlook_Cross", 22f, 42f, 22f, 56f, full,
                DoubleGap(7.5f),
                UpperGap(12f, 4f, up, full - up));

            // Spre A Catwalks: usa dubla la sol, gol pentru pasarela sus.
            Partition("Part_Overlook_ACatwalks", -10f, 42f, -10f, 56f, full,
                DoubleGap(4f),
                UpperGap(12f, 4f, up, full - up));

            // Ziduri alternate care rup linia de la sol in fiecare curte
            InnerWall(gStructures, "Overlook_Stub_West", -8f, 42f, -8f, 47f, wall);
            InnerWall(gStructures, "Overlook_Stub_Mid", 30f, 47f, 30f, 52f, wall);
            InnerWall(gStructures, "Overlook_Stub_East", 46f, 42f, 46f, 48.5f, wall);

            // Balcon plin (2 m) pe latura sudica, cu trepte
            SolidBlock(gStairs, "North_Overlook_Balcony", 14f, 42f, 20f, 46f, 0f, 2f, platformMat);
            Stairs(gStairs, "North_Overlook_Steps", 8f, 42f, 14f, 46f, Vector3.right, 0f, 2f);


            LowCover(-1f, 51f, 1f, 51f);
            HighCover(18f, 47.5f, 18f, 51.5f);         // paravan in fata usii din zidul dintre curti
            LowCover(34f, 48f, 38f, 48f);
            BigCrate(44f, 45f);
            BigCrate(10f, 50f);

            // A Catwalks (zona de la sol dintre North Overlook si site-ul A)
            HighCover(-14f, 43.5f, -14f, 48.5f);       // paravan in fata usii spre North Overlook
        }
    }
}
