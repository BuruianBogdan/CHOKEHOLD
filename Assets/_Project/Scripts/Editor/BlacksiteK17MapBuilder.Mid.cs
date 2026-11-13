using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Mid Lab (parter + etaj), anexa laboratorului, Mid Connector, Choke Point si coridoarele din mid.
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildMid()
        {
            float up = UpperFloorHeight;
            float wall = GroundCeiling;
            float low = GroundCeiling - 0.4f;
            float high = 2f * UpperFloorHeight - 0.6f;
            float roofTop = 2f * UpperFloorHeight + SlabThickness;

            // ---------------- Mid Lab: parter ----------------
            Stairs(gStairs, "Stairs_Lab_SouthWest", -10f, 12f, -6f, 20f, Vector3.forward, 0f, up);
            Stairs(gStairs, "Stairs_Lab_NorthEast", 14f, 28f, 18f, 36f, Vector3.forward, 0f, up);

            // Camere despartite de pereti; cei "moi" (culoare de lemn) se sparg cu gloante.
            InnerWall(gStructures, "Lab_Wall_Z28_West", -10f, 28f, -2f, 28f, wall);
            InnerWall(gStructures, "Lab_Wall_Z28_East", 4f, 28f, 10f, 28f, wall);
            SoftWall(gStructures, "Lab_SoftWall_Z28", 10f, 28f, 14f, 28f, 0f, wall);
            InnerWall(gStructures, "Lab_Wall_X8_South", 8f, 10f, 8f, 18f, wall);
            SoftWall(gStructures, "Lab_SoftWall_X8", 8f, 22f, 8f, 28f, 0f, wall);

            // Mese de laborator si consola centrala
            Crate(gCover, -3f, 15f, 4f, 1f, 1.2f);
            Crate(gCover, 13f, 14f, 4f, 1f, 1.2f);
            Crate(gCover, 9f, 34f, 4f, 1f, 1.2f);
            Crate(gCover, -6f, 33f, 1.2f, 1f, 4f);
            Crate(gCover, 3f, 20f, 2f, 1.2f, 2f);

            DoorWithLintel("Door_LabWest", -12f, 22f, -10f, 24f, true, wall);
            Barricade(gBarricades, "Barricade_LabSouthDoor", 3f, 7f, 0f, 6f, wall, true);
            Barricade(gBarricades, "Barricade_LabNorthDoor", 3f, 40f, 0f, 6f, wall, true);

            // ---------------- Mid Lab: etaj ----------------
            // Planseu cu goluri pentru scari si trape, balcoane peste usi.
            Slab(gUpper, "Lab_Floor2", up,
                new[]
                {
                    new Area("Lab", -10f, 10f, 18f, 38f),
                    new Area("South Balcony", 0f, 4f, 6f, 10f),      // deasupra Choke Point
                    new Area("North Balcony", 0f, 38f, 6f, 44f),     // spre North Catwalk
                    new Area("West Door", -12f, 22f, -10f, 24f),     // spre podul din A
                    new Area("Annex Door", 18f, 32f, 20f, 34f),
                },
                new[]
                {
                    new Area("Stairs SW", -10f, 12f, -6f, 20f),
                    new Area("Stairs NE", 14f, 28f, 18f, 36f),
                    new Area("Hatch SE", 10f, 14f, 12f, 16f),
                    new Area("Hatch NW", -4f, 30f, -2f, 32f),
                });

            Hatch(gUpper, "Hatch_Lab_SE", 10f, 14f, 12f, 16f, up);
            Hatch(gUpper, "Hatch_Lab_NW", -4f, 30f, -2f, 32f, up);
            Parapet(gUpper, "Lab_SouthBalcony_Parapet", 0f, 4f, 6f, 4f, up);
            Railing(gUpper, "Lab_Stairs_SW_Rail", -6f, 12f, -6f, 20f, up);
            Railing(gUpper, "Lab_Stairs_NE_Rail", 14f, 28f, 14f, 36f, up);

            SoftWall(gUpper, "Lab2_SoftWall", 4f, 12f, 4f, 26f, up, 3f);
            Crate(gUpper, 12f, 24f, 2f, 1.2f, 2f, up);
            Crate(gUpper, -6f, 26f, 1.4f, 1.4f, 1.4f, up);
            Crate(gUpper, 8f, 34f, 3f, 1f, 1.2f, up);

            Barricade(gBarricades, "Barricade_Lab2_WestDoor", -11f, 23f, up, 2f, 3f, false);
            Barricade(gBarricades, "Barricade_Lab2_AnnexDoor", 19f, 33f, up, 2f, 3f, false);
            Barricade(gBarricades, "Barricade_Lab2_NorthBalcony", 3f, 40f, up, 6f, 3f, true);

            // Pod de la balconul de nord la North Catwalk
            CreatePlatform(gUpper, "Bridge_Catwalk_To_Lab", 1f, 44f, 5f, 52f, up);
            Railing(gUpper, "Bridge_Catwalk_To_Lab_Rail_W", 1f, 44f, 1f, 52f, up);
            Railing(gUpper, "Bridge_Catwalk_To_Lab_Rail_E", 5f, 44f, 5f, 52f, up);

            // Acoperis peste laborator si peste balcoane / golurile de usa de la etaj
            Slab(gRoofs, "Roof_MidLab", roofTop,
                new[]
                {
                    new Area("Lab", -10f, 10f, 18f, 38f),
                    new Area("South Balcony", 0f, 4f, 6f, 10f),
                    new Area("North Balcony", 0f, 38f, 6f, 44f),
                    new Area("West Door", -12f, 22f, -10f, 24f),
                    new Area("Annex Door", 18f, 32f, 20f, 34f),
                },
                new Area[0],
                roofMat);

            // Lumini reci, de laborator
            Lamp("Light_Lab_SW", -2f, low, 18f, ColdLight, 14f, 2.5f);
            Lamp("Light_Lab_SE", 13f, low, 18f, ColdLight, 12f, 2.5f);
            Lamp("Light_Lab_North", 4f, low, 33f, ColdLight, 14f, 2.5f);
            Lamp("Light_Lab2_South", 4f, high, 17f, ColdLight, 16f, 2.5f);
            Lamp("Light_Lab2_North", 4f, high, 31f, ColdLight, 16f, 2.5f);

            // ---------------- Lab Annex ----------------
            Stairs(gStairs, "Stairs_Annex", 32f, 22f, 40f, 26f, Vector3.right, 0f, up);

            Crate(gCover, 32f, 35f, 6f, 1f, 1.2f);
            Crate(gCover, 24f, 28f, 1.2f, 1f, 3f);
            SoftWall(gStructures, "Annex_SoftWall", 30f, 28f, 30f, 34f, 0f, wall);

            DoorWithLintel("Door_LabAnnex", 18f, 32f, 20f, 34f, true, wall);
            DoorWithLintel("Door_AnnexEast", 42f, 28f, 46f, 30f, true, wall);
            Lintel(gDoors, "Lintel_AnnexSouth", 30f, 16f, 32f, 22f, InnerWallHeight);
            Barricade(gBarricades, "Barricade_AnnexSouth", 31f, 19f, 0f, 2f, 2.5f, true);

            Slab(gUpper, "Annex_Floor2", up,
                new[]
                {
                    new Area("Annex", 20f, 22f, 42f, 38f),
                    new Area("East Door", 42f, 28f, 46f, 30f),
                },
                new[]
                {
                    new Area("Stairs", 32f, 22f, 40f, 26f),
                    new Area("Hatch", 24f, 34f, 26f, 36f),
                });

            Hatch(gUpper, "Hatch_Annex", 24f, 34f, 26f, 36f, up);
            Railing(gUpper, "Annex_Stairs_Rail", 32f, 26f, 40f, 26f, up);
            Crate(gUpper, 30f, 31f, 2f, 1.2f, 2f, up);

            Slab(gRoofs, "Roof_LabAnnex", roofTop,
                new[]
                {
                    new Area("Annex", 20f, 22f, 42f, 38f),
                    new Area("East Door", 42f, 28f, 46f, 30f),
                },
                new Area[0],
                roofMat);

            Lamp("Light_Annex", 31f, low, 30f, ColdLight, 14f, 2.5f);
            Lamp("Light_Annex2", 31f, high, 30f, ColdLight, 16f, 2.5f);

            // ---------------- Mid Connector / Choke / coridoarele din mid ----------------
            Crate(gCover, -17f, 16f, 1.2f, 1.2f, 1.2f);
            Crate(gCover, -15f, 37f, 1.4f, 1.4f, 1.4f);
            Crate(gCover, 8f, -10f, 1.2f, 1.2f, 1.2f);
            Crate(gCover, 16f, 4f, 1.4f, 1.4f, 1.4f);
            Crate(gCover, 36f, 8f, 2f, 1.2f, 2f);
            HalfWall(gCover, 42f, 12f, 3f, 90f);
            Block(gStructures, "MidEast_Pillar", 27f, 10f, 29f, 12f, wall);

            // ---------------- Gameplay ----------------
            CreateDummy(gDummies, "Dummy_Defender_MidLab", new Vector3(12f, 0f, 20f));
            CreateDummy(gDummies, "Dummy_Defender_LabUpper", new Vector3(-2f, up, 20f));
        }
    }
}
