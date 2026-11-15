using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Hangar / Bombsite B, Container Stack, Loading Bay si biroul din coltul sud-estic.
    //
    // Intrari pentru atacatori pe site:
    //   1. Loading Bay  - doua usi de garaj + perete moale (sud)
    //   2. Mid East     - usa dubla pe vest
    //   3. Mezanin      - usa dubla pe nord la sol + trapa din mezanin (vertical)
    //   4. Birou        - usa din spatele Container Stack-ului (sud-est)
    // Aparatorii vin din East Yard (usa dubla pe nord-est).
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSiteB()
        {
            float up = UpperFloorHeight;
            float wall = GroundCeiling;
            float low = GroundCeiling - 0.4f;
            float full = InnerWallHeight;

            // ---------------- Scari si etaj ----------------
            Stairs(gStairs, "Stairs_Hangar_West", 22f, -42f, 26f, -34f, Vector3.forward, 0f, up);
            Stairs(gStairs, "Stairs_Hangar_North", 50f, -2f, 58f, 2f, Vector3.left, 0f, up);

            // Pasarela pe vest, mezanin pe nord (cu trapa), balcon spre Mid East Corridor
            CreatePlatform(gUpper, "Hangar_Catwalk_West", 22f, -34f, 26f, 2f, up);
            Slab(gUpper, "Hangar_Mezzanine_North", up,
                new[] { new Area("Mezzanine", 26f, -2f, 50f, 2f) },
                new[] { new Area("Hatch", 34f, -2f, 36f, 0f) },
                platformMat);
            StandardHatch(gUpper, "Hatch_Hangar_Mezzanine", 34f, -2f, 36f, 0f, up);
            CreatePlatform(gUpper, "Hangar_North_Balcony", 38f, 2f, 44f, 8f, up);
            Railing(gUpper, "Hangar_Catwalk_West_Rail", 26f, -34f, 26f, -2f, up);
            Railing(gUpper, "Hangar_Mezzanine_Rail", 26f, -2f, 50f, -2f, up);
            Parapet(gUpper, "Hangar_North_Balcony_Parapet", 38f, 8f, 44f, 8f, up);

            // Acoperis peste hangar si Loading Bay, cu luminator deasupra site-ului
            Slab(gRoofs, "Roof_Hangar", full + SlabThickness,
                new[]
                {
                    new Area("Hangar", 22f, -40f, 78f, 2f),
                    new Area("Loading Bay", 22f, -56f, 60f, -38f),
                    new Area("North Door", 38f, 2f, 44f, 8f),
                },
                new[]
                {
                    new Area("Skylight", 42f, -22f, 48f, -16f),
                },
                roofMat);

            // ---------------- Intrari (pereti cu goluri standard) ----------------

            // Vest: Mid East Bend -> hangar, pe sub pasarela.
            Partition("Part_Hangar_West", 22f, -4f, 22f, 2f, full, DoubleGap(3f, true));
            Partition("Part_Hangar_West_Corner", 22f, 2f, 24f, 2f, full);

            // Nord: Mid East Corridor -> hangar, pe sub balcon.
            Partition("Part_Hangar_North", 38f, 6f, 44f, 6f, wall, DoubleGap(3f, true).Screened(1));

            // Nord-est: East Yard -> hangar (ruta aparatorilor).
            Partition("Part_Hangar_Yard", 58f, 2f, 66f, 2f, full, DoubleGap(5.5f));

            // Sud: Loading Bay -> hangar. Doua usi de garaj si un perete moale intre ele.
            Partition("Part_Hangar_LoadingBay", 26f, -38f, 60f, -38f, full,
                GarageGap(6.5f, 5f),
                SoftGap(17f, 6f),
                GarageGap(26.5f, 5f));
            Partition("Part_Hangar_LoadingBay_Corner", 60f, -40f, 60f, -38f, full);

            // South Lane -> Loading Bay.
            Partition("Part_Lane_LoadingBay", 22f, -46f, 22f, -38f, full, DoubleGap(2f));

            // ---------------- Hangar: structura ----------------
            // Utilaj si container de 40 ft pe vest, zid sub mezanin, rand de containere pe est:
            // site-ul ramane deschis in mijloc, dar fara linii pe toata lungimea hangarului.
            Block(gStructures, "Hangar_Machine", 28f, -18f, 34f, -10f, wall);
            InnerWall(gStructures, "Hangar_Wall_North", 34f, -6f, 50f, -6f, wall);
            HighCover(22.5f, -10f, 26f, -10f);                    // taie culoarul de sub pasarela de vest
            Container40(35f, -27f, 0f, containerBlueMat);

            Container(gCover, 59f, -14f, 0f, containerRedMat);
            Container(gCover, 59f, -22f, 0f, containerBlueMat);
            Container(gCover, 59f, -22f, 0f, containerRedMat, 2.6f);
            Container(gCover, 59f, -31f, 0f, containerBlueMat);

            // Cover pe site: pozitii de plant si de post-plant
            Container(gCover, 40f, -18f, 90f, containerRedMat);
            Container(gCover, 46f, -25f, 0f, containerBlueMat);   // cu containerul rosu formeaza un L in mijlocul site-ului
            CrateStack(48f, -12f);
            Container(gCover, 52f, -4f, 90f, containerRedMat);
            HighCover(27f, -6f, 27f, 1.5f);                     // paravan in spatele usii de vest
            BigCrate(62f, -27f);                                  // culoarul dintre randul de containere si stiva
            HighCover(61f, -4f, 66f, -4f);                        // paravan in spatele usii dinspre East Yard
            LowCover(39f, -34f, 42f, -34f);
            Container(gCover, 52.5f, -32f, 90f, containerBlueMat);   // inchide axa usii de garaj din est
            HighCover(63f, -37f, 67f, -37f);                      // paravan in fata usii biroului, in hangar

            // ---------------- Container Stack (est): stiva pe doua niveluri, pe care se urca ----------------
            float[] stackX = { 66.3f, 68.9f, 71.5f, 74.1f };

            foreach (float x in stackX)
            {
                Container(gCover, x, -20f, 0f, x > 70f ? containerBlueMat : containerRedMat);
            }

            Container(gCover, 71.5f, -20f, 0f, containerRedMat, 2.6f);
            Container(gCover, 74.1f, -20f, 0f, containerBlueMat, 2.6f);
            Stairs(gStairs, "Stairs_ContainerStack", 66f, -16.8f, 70f, -11.8f, Vector3.back, 0f, 2.6f);
            Container(gCover, 70f, -32f, 90f, containerBlueMat);

            // Lumini puternice, albe
            float[] lampX = { 31f, 45f, 59f, 72f };
            float[] lampZ = { -33f, -20f, -7f };

            foreach (float x in lampX)
            {
                foreach (float z in lampZ)
                {
                    Lamp($"Light_Hangar_{x:0}_{-z:0}", x, 7.4f, z, WhiteLight, 20f, 3.5f);
                }
            }

            Lamp("Light_Hangar_UnderMezzanine", 38f, low, 0f, WhiteLight, 12f, 2.5f);
            Lamp("Light_Hangar_UnderCatwalk", 24f, low, -18f, WhiteLight, 10f, 2.5f);

            // ---------------- Loading Bay ----------------
            Container40(36f, -53f, 90f, containerBlueMat);
            HighCover(57f, -51f, 57f, -47f);                      // paravan in fata usii biroului
            Container(gCover, 50f, -47f, 0f, containerRedMat);
            Container(gCover, 50f, -47f, 0f, containerBlueMat, 2.6f);
            Container(gCover, 32.5f, -44f, 90f, containerRedMat);    // paravan in fata usii de garaj din vest
            LowCover(43f, -44f, 46f, -44f);
            HighCover(49.5f, -42f, 55.5f, -42f);                  // paravane in spatele usilor de garaj si a usii din South Lane
            HighCover(26.5f, -47f, 26.5f, -42.5f);

            Lamp("Light_LoadingBay_West", 30f, 7.4f, -48f, WhiteLight, 20f, 3.5f);
            Lamp("Light_LoadingBay_Mid", 43f, 7.4f, -47f, WhiteLight, 20f, 3.5f);
            Lamp("Light_LoadingBay_East", 55f, 7.4f, -47f, WhiteLight, 20f, 3.5f);

            // ---------------- Birou (coltul sud-estic) ----------------
            // Leaga Loading Bay de latura estica a hangarului, in spatele Container Stack-ului.
            Roof("Roof_LoadingOffice", 62f, -54f, 76f, -42f, up);
            DoorCell("Door_Office_West", 60f, -50f, 62f, -48f, true, full);
            DoorCell("Door_Office_North", 64f, -42f, 66f, -40f, false, full);

            Block(gStructures, "Office_Shelf", 75f, -53f, 76f, -47f, 2.2f);
            Crate(gCover, 70f, -45f, 4f, 1f, 1.2f);
            LowCover(66f, -52f, 68f, -52f);

            Lamp("Light_Office", 69f, low, -48f, WhiteLight, 12f, 2.5f);

            // ---------------- Gameplay ----------------
            CreateBombSite(gGameplay, "BombSite_B", SiteB);
            CreateDummy(gDummies, "Dummy_Defender_SiteB", new Vector3(44f, 0f, -9f));
        }
    }
}
