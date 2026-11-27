using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Mid Lab (parter + etaj), anexa laboratorului, Mid Connector, Choke Point si coridoarele din mid.
    //
    // Mid Lab controleaza informatia: 4 intrari la parter (sud dubla cu pereti moi spre Choke Point,
    // nord dubla, vest, anexa), plus etajul legat de A si de North Catwalk.
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildMid()
        {
            float up = UpperFloorHeight;
            float wall = GroundCeiling;
            float low = GroundCeiling - 0.4f;
            float full = InnerWallHeight;
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

            // Intrarea de sud (spre Choke Point): usa dubla flancata de pereti moi.
            Partition("Part_LabSouth", 0f, 9.75f, 6f, 9.75f, wall,
                SoftGap(0.75f, 1.5f, wall),
                DoubleGap(3f, true),
                SoftGap(5.25f, 1.5f, wall));

            // Intrarea de nord (spre North Overlook): usa dubla, decalata fata de cea de sud.
            Partition("Part_LabNorth", 0f, 38.25f, 6f, 38.25f, wall,
                DoubleGap(4.4f, true));
            HighCover(2f, 46.5f, 7f, 46.5f);              // paravan in fata usii de nord, in North Overlook

            DoorCell("Door_LabWest", -12f, 22f, -10f, 24f, true, wall);

            // Rack de servere in axa usilor: taie linia sud-nord prin laborator.
            Block(gStructures, "Lab_ServerRack", -2f, 20f, 4f, 23f, 2.2f);
            Block(gStructures, "Lab_Pillar", 4.875f, 23f, 6.875f, 25f, wall);   // 1 m pana la Lab_SoftWall_X8: trece un om

            // Mese de laborator (cover de crouch)
            Crate(gCover, -3f, 15f, 4f, 1f, 1.2f);
            Crate(gCover, 13f, 14f, 4f, 1f, 1.2f);
            Crate(gCover, 7f, 33f, 4f, 1f, 1.2f);
            HighCover(11f, 35f, 11f, 38f);                // paravan pe axa usii spre anexa
            Crate(gCover, -6f, 33f, 1.2f, 1f, 4f);

            // ---------------- Mid Lab: etaj ----------------
            // Planseu cu goluri pentru scari si trape, balcoane peste usi.
            Slab(gUpper, "Lab_Floor2", up,
                new[]
                {
                    new Area("Lab", -10f, 10f, 18f, 38f),
                    new Area("South Balcony", 0f, 4f, 6f, 10f),      // deasupra Choke Point
                    new Area("North Balcony", 0f, 38f, 6f, 44f),     // spre North Catwalk
                    new Area("West Door", -12f, 22f, -10f, 24f),     // spre podul din A
                    new Area("Annex Door", 18f, 36f, 20f, 38f),
                },
                new[]
                {
                    new Area("Stairs SW", -10f, 12f, -6f, 20f),
                    new Area("Stairs NE", 14f, 28f, 18f, 36f),
                    new Area("Hatch SE", 10f, 14f, 12f, 16f),
                    new Area("Hatch NW", -4f, 30f, -2f, 32f),
                });

            StandardHatch(gUpper, "Hatch_Lab_SE", 10f, 14f, 12f, 16f, up);
            StandardHatch(gUpper, "Hatch_Lab_NW", -4f, 30f, -2f, 32f, up);
            Parapet(gUpper, "Lab_SouthBalcony_Parapet", 0f, 4f, 6f, 4f, up);
            Railing(gUpper, "Lab_Stairs_SW_Rail", -6f, 12f, -6f, 20f, up);
            Railing(gUpper, "Lab_Stairs_NE_Rail", 14f, 28f, 14f, 36f, up);

            SoftWall(gUpper, "Lab2_SoftWall", 4f, 12f, 4f, 26f, up, 3f);
            LowCover(11f, 24f, 13f, 24f, up);
            BigCrate(-6f, 26f, up);
            HighCover(0.5f, 30f, 5.5f, 30f, up);          // taie axa nord-sud de la etaj
            HighCover(0.5f, 13f, 3.4f, 13f, up);
            HighCover(9f, 20f, 9f, 24f, up);
            HighCover(12f, 36f, 12f, 38f, up);            // taie axa usii spre anexa, la etaj
            Crate(gUpper, 8f, 34f, 3f, 1f, 1.2f, up);

            Barricade(gBarricades, "Barricade_Lab2_WestDoor", -11f, 23f, up, 2f, 3f, false);
            Barricade(gBarricades, "Barricade_Lab2_AnnexDoor", 19f, 37f, up, 2f, 3f, false);
            Barricade(gBarricades, "Barricade_Lab2_NorthBalcony", 3f, 40f, up, 6f, 3.6924f, true);   // pana sub tavanul etajului

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
                    new Area("Annex Door", 18f, 36f, 20f, 38f),
                },
                new[] { new Area("Control Room Stairs", 14f, 14f, 18f, 24f) },   // scara spre camera de control
                roofMat);

            // Lumini reci, de laborator
            Lamp("Light_Lab_SW", -2f, low, 18f, ColdLight, 14f, 2.5f);
            Lamp("Light_Lab_SE", 13f, low, 18f, ColdLight, 12f, 2.5f);
            Lamp("Light_Lab_North", 4f, low, 33f, ColdLight, 14f, 2.5f);
            Lamp("Light_Lab2_South", 4f, high, 17f, ColdLight, 16f, 2.5f);
            Lamp("Light_Lab2_North", 4f, high, 31f, ColdLight, 16f, 2.5f);

            // ---------------- Lab Annex ----------------
            Stairs(gStairs, "Stairs_Annex", 32f, 22f, 40f, 26f, Vector3.right, 0f, up);

            Crate(gCover, 32f, 34.58f, 6f, 1f, 1.2f);   // lipita de capatul Annex_SoftWall (fara gol de 0,4 m)
            HighCover(22.5f, 35.5f, 22.5f, 38f);          // paravan pe axa usii spre laborator
            Crate(gCover, 24f, 28f, 1.2f, 1f, 3f);
            SoftWall(gStructures, "Annex_SoftWall", 30f, 28f, 30f, 34f, 0f, wall);

            DoorCell("Door_LabAnnex", 18f, 36f, 20f, 38f, true, wall);
            DoorCell("Door_AnnexEast", 42f, 28f, 46f, 30f, true, wall);

            // Trecerea de sud (4 m lata) se inchide cu un perete pe toata inaltimea si o usa baricadabila.
            Partition("Part_AnnexSouth", 26f, 21.75f, 30f, 21.75f, full,
                DoorGap(2f, false, true).Screened(1));

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

            StandardHatch(gUpper, "Hatch_Annex", 24f, 34f, 26f, 36f, up);
            Railing(gUpper, "Annex_Stairs_Rail", 32f, 26f, 40f, 26f, up);
            HighCover(30f, 29f, 30f, 33f, up);
            HighCover(22.5f, 35.5f, 22.5f, 38f, up);           // taie linia laborator -> anexa -> East Yard

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

            // ---------------- Choke Point: camera centrala cu trei usi decalate ----------------
            Partition("Part_Choke_West", -4f, -6f, -4f, 2f, full, DoorGap(6.5f).Screened(-1));
            Partition("Part_Choke_East", 10f, -4f, 10f, 8f, full, DoorGap(2f).Screened(1));
            Partition("Part_Choke_South", -4f, -14f, 10f, -14f, full, DoubleGap(12f));

            BigCrate(-1f, -9f);
            HighCover(0.5f, 0f, 5.5f, 0f);                // paravan in fata usii de sud a laboratorului

            // ---------------- Mid Connector (aleea dintre A si laborator) ----------------
            Partition("Part_Connector_Mid", -22f, 18f, -12f, 18f, full, DoorGap(8f).Screened(-1));
            Partition("Part_Connector_North", -22f, 40f, -12f, 40f, full, DoubleGap(7f).Screened(-1));
            Partition("Part_MidWest_Walkway", -22f, -6f, -22f, 0f, full, DoorGap(4.5f).Screened(1));
            Partition("Part_MidWest_Walkway_Corner", -22f, -6f, -18f, -6f, full);

            BigCrate(-19f, 8f);
            BigCrate(-20f, 28f);

            // ---------------- Mid East (intre Choke Point, hangar si East Yard) ----------------
            Partition("Part_MidEast_Yard", 46f, 6f, 46f, 16f, full, DoubleGap(7.5f));
            Partition("Part_MidEast_Bend", 20f, 6f, 24f, 6f, full, DoorGap(2f));
            HighCover(20.01f, 8.4f, 22.91f, 8.4f);      // paravanul usii, lipit de Part_MidEast_Bend_Corner (fara fanta de 0,6 m)
            Partition("Part_MidEast_Bend_Corner", 20f, 6f, 20f, 8f, full);

            BigCrate(16f, 2f);
            LowCover(35f, 8f, 38f, 8f);
            HighCover(42f, 10.5f, 42f, 13.5f);
            Block(gStructures, "MidEast_Pillar", 27f, 10f, 29f, 12f, wall);

            // ---------------- Gameplay ----------------
            CreateDummy(gDummies, "Dummy_Defender_MidLab", new Vector3(12f, 0f, 20f));
            CreateDummy(gDummies, "Dummy_Defender_LabUpper", new Vector3(-2f, up, 20f));
        }
    }
}
