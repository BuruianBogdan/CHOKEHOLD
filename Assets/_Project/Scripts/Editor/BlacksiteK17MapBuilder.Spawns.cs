using ChokeHold.Gameplay;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Spawn-urile si zonele exterioare: T1 Spawn, T1 Ramp, camera de securitate, West Outpost,
    // West Rocks, T2 Spawn, East Yard, North Overlook / North Catwalk.
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

            // T1 Ramp (iesirea principala din spawn) si pasarelele spre mid
            Crate(gCover, -48f, -20f, 2f, 1.2f, 2f);
            HalfWall(gCover, -40f, -26f, 3f, 90f);
            InnerWall(gStructures, "T1Ramp_Wall", -44f, -30f, -44f, -23f, GroundCeiling);
            Crate(gCover, -31f, -16f, 1.4f, 1.4f, 1.4f);

            // Camera de securitate: a treia iesire din spawn (spawn -> securitate -> curte sau T1 Ramp)
            Roof("Roof_Security", -42f, -46f, -30f, -32f, UpperFloorHeight);
            DoorWithLintel("Door_Security_North", -38f, -32f, -36f, -30f, false, InnerWallHeight);
            DoorWithLintel("Door_Security_East", -30f, -40f, -28f, -38f, true, InnerWallHeight);
            DoorWithLintel("Door_Security_West", -44f, -42f, -42f, -40f, true, InnerWallHeight);

            Block(gStructures, "Security_Lockers", -42f, -37f, -41f, -33f, 2.2f);
            Crate(gCover, -36f, -44.5f, 4f, 1f, 1.2f);
            Crate(gCover, -32f, -34f, 2f, 1.1f, 2f);
            Lamp("Light_Security", -36f, low, -39f, ColdLight, 12f, 2.5f);

            // West Outpost
            Block(gStructures, "Outpost_Tower", -70f, -14f, -64f, -8f, 4f);
            SolidBlock(gStructures, "Outpost_Shed", -76f, -6.5f, -72f, -1.5f, 0f, 3f, containerBlueMat);
            Crate(gCover, -60f, -10f, 2f, 1.2f, 2f);
            Crate(gCover, -62f, 0f, 1.2f, 1.2f, 1.2f);
            Crate(gCover, -73f, 2f, 3f, 1.5f, 1.5f);
            HalfWall(gCover, -64f, -16f, 4f, 0f);

            // West Rocks: stanci ca margine naturala si cover pe drumul spre A Control
            Rock(gStructures, "WestRocks_Rock_1", new Vector3(-49f, 0f, 2f), new Vector3(4f, 2.5f, 3f), 25f);
            Rock(gStructures, "WestRocks_Rock_2", new Vector3(-52f, 0f, 10f), new Vector3(3f, 2f, 2.5f), -15f);
            Rock(gStructures, "WestRocks_Rock_3", new Vector3(-44f, 0f, -2f), new Vector3(3f, 2.2f, 3f), 40f);
            Crate(gCover, -50f, 6f, 2f, 1.5f, 2f);
        }

        // ------------------------------------------------------------------
        // Aparatori: spawn, East Yard
        // ------------------------------------------------------------------
        private static void BuildTeam2Side()
        {
            float up = UpperFloorHeight;

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

            // East Yard: cladirea de generator in mijloc face din curte un inel
            Block(gStructures, "Yard_Generator", 58f, 20f, 68f, 28f, 4f);
            Rock(gStructures, "EastYard_Rock", new Vector3(75f, 0f, 16f), new Vector3(3f, 2.2f, 3f), 30f);

            Crate(gCover, 54f, 30f, 2f, 2f, 2f);
            Crate(gCover, 70f, 24f, 2f, 2f, 2f);
            Container(gCover, 73f, 31f, 0f, containerBlueMat);
            HalfWall(gCover, 52f, 14f, 3f, 90f);
            Crate(gCover, 72f, 12f, 1.5f, 1.5f, 1.5f);

            // Balconul anexei, cu scari in curte
            Stairs(gStairs, "Stairs_EastYard", 46f, 18f, 50f, 26f, Vector3.forward, 0f, up);
            CreatePlatform(gUpper, "EastYard_Balcony", 46f, 26f, 50f, 32f, up);
            Railing(gUpper, "EastYard_Balcony_Rail_E", 50f, 26f, 50f, 32f, up);
            Railing(gUpper, "EastYard_Balcony_Rail_N", 46f, 32f, 50f, 32f, up);
        }

        // ------------------------------------------------------------------
        // North Overlook (culoarul de la sol) si North Catwalk (pasarela de deasupra)
        // ------------------------------------------------------------------
        private static void BuildNorthOverlook()
        {
            float up = UpperFloorHeight;
            float wall = GroundCeiling;

            Stairs(gStairs, "Stairs_T2_To_Catwalk", 46f, 52f, 54f, 56f, Vector3.left, 0f, up);

            // North Catwalk: de la rampa T2 pana in A Heaven
            CreatePlatform(gUpper, "North_Catwalk", -24f, 52f, 46f, 56f, up);
            Railing(gUpper, "North_Catwalk_Rail_W", -22f, 52f, 1f, 52f, up);
            Railing(gUpper, "North_Catwalk_Rail_E", 5f, 52f, 46f, 52f, up);

            for (float x = -16f; x <= 40f; x += 14f)
            {
                CreateBox(gUpper, $"North_Catwalk_Pillar_{x:0}", new Vector3(x, GroundCeiling * 0.5f, 52.3f),
                    new Vector3(0.5f, GroundCeiling, 0.5f), pipeMat);
            }

            // Ziduri alternate care rup linia de la sol
            InnerWall(gStructures, "Overlook_Stub_West", -6f, 42f, -6f, 47f, wall);
            InnerWall(gStructures, "Overlook_Stub_Mid", 30f, 47f, 30f, 52f, wall);
            InnerWall(gStructures, "Overlook_Stub_East", 46f, 42f, 46f, 47f, wall);

            // Balcon plin (2 m) pe latura sudica, cu trepte
            SolidBlock(gStairs, "North_Overlook_Balcony", 14f, 42f, 24f, 46f, 0f, 2f, platformMat);
            Stairs(gStairs, "North_Overlook_Steps", 24f, 42f, 30f, 46f, Vector3.left, 0f, 2f);

            Rock(gStructures, "Overlook_Rock", new Vector3(52f, 0f, 44f), new Vector3(3f, 2f, 2.5f), 20f);

            Crate(gCover, -4f, 48f, 2f, 1.2f, 2f);
            HalfWall(gCover, 36f, 48f, 4f, 0f);
            Crate(gCover, 44f, 45f, 1.5f, 1.5f, 1.5f);
            Crate(gCover, 8f, 50f, 1.4f, 1.4f, 1.4f);

            // A Catwalks (zona de la sol dintre North Overlook si site-ul A)
            Crate(gCover, -16f, 46f, 2f, 1.2f, 2f);
        }
    }
}
