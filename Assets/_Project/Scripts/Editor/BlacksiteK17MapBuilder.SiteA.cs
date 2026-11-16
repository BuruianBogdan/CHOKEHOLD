using ChokeHold.Gameplay;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Bombsite A (cladire industriala inchisa), A Control, A Pipes (sala pompelor) si depozitul.
    //
    // Intrari pentru atacatori pe site:
    //   1. A Control  - usa dubla + perete moale (sud)
    //   2. A Ramp     - usa simpla + perete moale + fereastra (est)
    //   3. A Pipes    - usa din sala pompelor (vest)
    //   4. Depozit    - perete moale direct in site (sud-est)
    //   5. A Heaven   - trapa din galerie + usa de la sol (nord-est), pasarela North Catwalk la etaj
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSiteA()
        {
            float up = UpperFloorHeight;
            float low = GroundCeiling - 0.4f;
            float full = InnerWallHeight;

            // ---------------- Scari si platforme ----------------
            // Scara spre A Heaven: 6 m lata, urca pe toata latura de sud a galeriei (fara balustrada in capat).
            Stairs(gStairs, "Stairs_A_Heaven", -36f, 38f, -30f, 46f, Vector3.forward, 0f, up);
            Stairs(gStairs, "Stairs_A_SouthGallery", -52f, 22f, -44f, 26f, Vector3.right, 0f, up);

            // A Platform - coltul nord-vestic, platforma plina la 1,2 m
            SolidBlock(gStairs, "A_Platform", -56f, 44f, -44f, 54f, 0f, 1.2f, platformMat);
            Stairs(gStairs, "A_Platform_Steps", -50f, 38f, -46f, 44f, Vector3.forward, 0f, 1.2f);

            // ---------------- Galerie (etaj) ----------------
            // A Heaven are o trapa: intrare verticala direct deasupra site-ului.
            Slab(gUpper, "A_Heaven", up,
                new[] { new Area("Heaven", -36f, 46f, -24f, 56f) },
                new[] { new Area("Hatch", -30f, 50f, -28f, 52f) },
                platformMat);
            StandardHatch(gUpper, "Hatch_A_Heaven", -30f, 50f, -28f, 52f, up);

            CreatePlatform(gUpper, "A_Gallery_East", -30f, 26f, -24f, 46f, up);
            SolidBlock(gUpper, "A_Gallery_Screen", -27.4f, 29f, -27f, 36f, up, 2.2f, wallMat);   // paravan in fata podului
            CreatePlatform(gUpper, "A_Gallery_South", -44f, 22f, -30f, 26f, up);
            Railing(gUpper, "A_Heaven_Rail_W", -36f, 46f, -36f, 56f, up);
            Railing(gUpper, "A_Gallery_East_Rail", -30f, 26f, -30f, 46f, up);
            Railing(gUpper, "A_Gallery_South_Rail", -44f, 26f, -30f, 26f, up);

            // Pod de la galeria A, peste A Ramp si Mid Connector, pana la usa de vest a laboratorului
            CreatePlatform(gUpper, "Bridge_A_To_Lab_1", -24f, 30f, -12f, 34f, up);
            CreatePlatform(gUpper, "Bridge_A_To_Lab_2", -16f, 22f, -12f, 30f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_N", -24f, 34f, -12f, 34f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_S", -24f, 30f, -16f, 30f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_W", -16f, 22f, -16f, 30f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_End", -16f, 22f, -12f, 22f, up);

            // ---------------- Acoperis ----------------
            Slab(gRoofs, "Roof_SiteA", full + SlabThickness,
                new[]
                {
                    new Area("Site", -58f, 22f, -24f, 56f),
                    new Area("Heaven Door", -26f, 48f, -22f, 56f),
                },
                new[]
                {
                    new Area("Skylight", -44f, 36f, -38f, 42f),
                },
                roofMat);

            // ---------------- Intrari (pereti cu goluri standard) ----------------

            // Sud: A Control -> site. Usa dubla + perete moale.
            Partition("Part_AControl_Site", -46f, 21.75f, -34f, 21.75f, full,
                DoubleGap(4f),
                SoftGap(9.5f, 3f));

            // Est: A Ramp -> site. Perete moale si usa; sus trece podul spre laborator.
            Partition("Part_ARamp_Site", -24.25f, 28f, -24.25f, 36f, full,
                SoftGap(1.25f, 2.5f),
                DoorGap(4f),
                UpperGap(4f, DoorWidth, up));
            HighCover(-27.5f, 29f, -27.5f, 34f);       // paravan in spatele usii din A Ramp

            // Nord-est: A Catwalks -> site, pe sub A Heaven. Usa si fereastra la sol; sus intra North Catwalk.
            Partition("Part_AHeavenDoor", -24.25f, 48f, -24.25f, 56f, full,
                DoorGap(2f).Screened(-1),
                WindowGap(5.5f),
                UpperGap(6f, 4f, up));

            // ---------------- Interior ----------------
            // Peretele de tevi separa culoarul A Pipes (vest) de zona de plant.
            InnerWall(gStructures, "A_Pipes_Wall_South", -50f, 26f, -50f, 36f, 3.5f);

            // Zona de plant e o camera in camera: zid pe sud si perete cu usa pe nord,
            // ca din nicio intrare sa nu se vada tot site-ul.
            InnerWall(gStructures, "A_Wall_South", -50f, 33f, -34f, 33f, GroundCeiling);
            // Peretele care inchide zona de plant spre nord sta lipit sub marginea lui A Heaven
            // (fata de est = muchia galeriei) si urca pana la nivelul ei: zid si galerie formeaza un singur corp.
            Partition("Part_A_NorthMid", -36.25f, 44f, -36.25f, 56f, UpperFloorHeight, DoorGap(10.5f));
            SoftWall(gStructures, "A_Pipes_SoftWall", -50f, 38f, -50f, 42f, 0f, 3.5f);
            Pipe(gStructures, "A_Pipe_West_Low", new Vector3(-57.3f, 0.6f, 32f), new Vector3(-57.3f, 0.6f, 44f), 0.9f);
            Pipe(gStructures, "A_Pipe_West_High", new Vector3(-57.3f, 1.8f, 32f), new Vector3(-57.3f, 1.8f, 44f), 0.7f);
            Pipe(gStructures, "A_Pipe_Overhead_1", new Vector3(-58f, 4.6f, 31f), new Vector3(-36f, 4.6f, 31f), 0.6f);
            Pipe(gStructures, "A_Pipe_Overhead_2", new Vector3(-58f, 5f, 40f), new Vector3(-44f, 5f, 40f), 0.6f);
            Tank(gStructures, "A_Tank_Main", -36f, 30.5f, 4f, 4f);
            Tank(gStructures, "A_Tank_Small", -27f, 42f, 3.5f, 3.4f);
            Block(gStructures, "A_Generator", -46f, 34f, -42f, 38f, 2.5f);

            // Cover: pozitii de plant (generator, lada), de aparare (platforma, sub Heaven) si de intrare.
            HighCover(-46f, 27f, -46f, 32.4f);           // taie banda de sud dinspre usa A Pipes
            HighCover(-44f, 27f, -37f, 27f);           // paravan in spatele usii duble din A Control
            LowCover(-54f, 36f, -54f, 38f);            // culoarul A Pipes
            BigCrate(-41f, 53f);                       // coltul de nord, sub luminator
            LowCover(-51f, 49f, -49f, 49f, 1.2f);      // pe platforma A
            HighCover(-40f, 39f, -40f, 44f);           // inchide vederea prin fereastra din A Ramp

            // Lumini calde, industriale
            Lamp("Light_A_NW", -50f, 7.4f, 48f, WarmLight, 20f);
            Lamp("Light_A_SW", -50f, 7.4f, 30f, WarmLight, 20f);
            Lamp("Light_A_Center", -40f, 7.4f, 38f, WarmLight, 20f);
            Lamp("Light_A_NE", -30f, 7.4f, 50f, WarmLight, 18f);
            Lamp("Light_A_UnderGalleryEast", -27f, low, 34f, WarmLight, 10f, 2.5f);
            Lamp("Light_A_UnderGallerySouth", -37f, low, 24f, WarmLight, 10f, 2.5f);
            Lamp("Light_A_UnderHeaven", -30f, low, 52f, WarmLight, 10f, 2.5f);

            // ---------------- A Control (holul de intrare din sud) ----------------
            Roof("Roof_AControl", -46f, 10f, -34f, 22f, up);

            // Intrarea din West Rocks: o singura usa dubla, pe latura de vest.
            Partition("Part_AControl_South", -46f, 10f, -42f, 10f, full);
            Partition("Part_AControl_West", -46f, 10f, -46f, 14f, full, DoubleGap(2f));
            HighCover(-43f, 10.6f, -43f, 15f);         // paravan in spatele usii, oprit inainte de peretele de sud

            BigCrate(-37f, 15f);
            LowCover(-44.5f, 18f, -41.5f, 18f);
            Lamp("Light_AControl", -40f, low, 16f, WarmLight, 12f, 2.5f);

            // ---------------- A Pipes (sala pompelor) ----------------
            // Ruta proprie: West Outpost -> sala pompelor -> usa din peretele de vest al site-ului.
            Roof("Roof_APipes", -76f, 10f, -62f, 34f, 4.5f);
            DoorCell("Door_APipes_South", -70f, 6f, -68f, 10f, false, full);
            DoorCell("Door_APipes_Site", -62f, 28f, -58f, 30f, true, full);

            Pipe(gStructures, "Pumps_Pipe_Low", new Vector3(-75.3f, 0.6f, 12f), new Vector3(-75.3f, 0.6f, 32f), 0.9f);
            Pipe(gStructures, "Pumps_Pipe_High", new Vector3(-75.3f, 2f, 12f), new Vector3(-75.3f, 2f, 32f), 0.7f);
            Pipe(gStructures, "Pumps_Pipe_Cross", new Vector3(-76f, 3.6f, 22f), new Vector3(-62f, 3.6f, 22f), 0.6f);
            Tank(gStructures, "Pumps_Tank_South", -66f, 15f, 3f, 3.2f);
            Tank(gStructures, "Pumps_Tank_North", -72.8f, 27f, 3f, 3.2f);
            Block(gStructures, "Pumps_Machine", -71f, 19f, -67f, 23f, 2.2f);

            Lamp("Light_APipes_South", -69f, 3.8f, 15f, WarmLight, 14f, 2.5f);
            Lamp("Light_APipes_North", -69f, 3.8f, 29f, WarmLight, 14f, 2.5f);

            // ---------------- Depozit ----------------
            // Leaga A Control de Mid Connector si are un perete moale direct in site.
            Roof("Roof_Depot", -32f, 4f, -24f, 20f, up);
            DoorCell("Door_Depot_East", -24f, 10f, -22f, 12f, true, full);
            DoorCell("Door_Depot_West", -34f, 14f, -32f, 16f, true, full);

            SoftWall(gStructures, "Depot_Breach_SoftWall", -30f, 21f, -28f, 21f, 0f, DoorHeight);
            SolidBlock(gDoors, "Depot_Breach_Lintel", -30f, 20f, -28f, 22f, DoorHeight, full - DoorHeight, wallMat);

            Block(gStructures, "Depot_Shelf_West", -32f, 5f, -31f, 9f, 2.2f);
            Block(gStructures, "Depot_Shelf_East", -25f, 14f, -24f, 19f, 2.2f);
            LowCover(-29f, 7f, -27f, 7f);

            Lamp("Light_Depot", -28f, low, 12f, WarmLight, 12f, 2.5f);

            // ---------------- Gameplay ----------------
            CreateBombSite(gGameplay, "BombSite_A", SiteA);
            CreateDummy(gDummies, "Dummy_Defender_SiteA", new Vector3(-44f, 0f, 41f));
            CreateDummy(gDummies, "Dummy_Defender_AHeaven", new Vector3(-33f, up, 50f));
        }
    }
}
