using ChokeHold.Gameplay;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Bombsite A (cladire industriala inchisa), A Control, A Pipes (sala pompelor) si depozitul.
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSiteA()
        {
            float up = UpperFloorHeight;
            float low = GroundCeiling - 0.4f;

            // ---------------- Site-ul propriu-zis ----------------

            // Scari spre galerie
            Stairs(gStairs, "Stairs_A_Heaven", -34f, 38f, -30f, 46f, Vector3.forward, 0f, up);
            Stairs(gStairs, "Stairs_A_SouthGallery", -52f, 22f, -44f, 26f, Vector3.right, 0f, up);

            // A Platform - coltul nord-vestic, platforma plina la 1,2 m
            SolidBlock(gStairs, "A_Platform", -56f, 44f, -44f, 54f, 0f, 1.2f, platformMat);
            Stairs(gStairs, "A_Platform_Steps", -50f, 38f, -46f, 44f, Vector3.forward, 0f, 1.2f);

            // Galerie in forma de L deasupra site-ului: A Heaven + latura de est + latura de sud
            CreatePlatform(gUpper, "A_Heaven", -36f, 46f, -24f, 56f, up);
            CreatePlatform(gUpper, "A_Gallery_East", -30f, 26f, -24f, 46f, up);
            CreatePlatform(gUpper, "A_Gallery_South", -44f, 22f, -30f, 26f, up);
            Railing(gUpper, "A_Heaven_Rail_W", -36f, 46f, -36f, 56f, up);
            Railing(gUpper, "A_Heaven_Rail_S", -36f, 46f, -34f, 46f, up);
            Railing(gUpper, "A_Gallery_East_Rail", -30f, 26f, -30f, 46f, up);
            Railing(gUpper, "A_Gallery_South_Rail", -44f, 26f, -30f, 26f, up);

            // Pod de la galeria A, peste A Ramp si Mid Connector, pana la usa de vest a laboratorului
            CreatePlatform(gUpper, "Bridge_A_To_Lab_1", -24f, 30f, -12f, 34f, up);
            CreatePlatform(gUpper, "Bridge_A_To_Lab_2", -16f, 22f, -12f, 30f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_N", -24f, 34f, -12f, 34f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_S", -24f, 30f, -16f, 30f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_W", -16f, 22f, -16f, 30f, up);
            Railing(gUpper, "Bridge_A_To_Lab_Rail_End", -16f, 22f, -12f, 22f, up);

            // Acoperis complet, cu luminator deasupra zonei de plant
            Slab(gRoofs, "Roof_SiteA", InnerWallHeight + SlabThickness,
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

            // Interior: perete de tevi pe vest, rezervoare, generator in mijloc
            InnerWall(gStructures, "A_Pipes_Wall_South", -50f, 32f, -50f, 36f, 3.5f);
            SoftWall(gStructures, "A_Pipes_SoftWall", -50f, 38f, -50f, 42f, 0f, 3.5f);
            Pipe(gStructures, "A_Pipe_West_Low", new Vector3(-57.3f, 0.6f, 32f), new Vector3(-57.3f, 0.6f, 44f), 0.9f);
            Pipe(gStructures, "A_Pipe_West_High", new Vector3(-57.3f, 1.8f, 32f), new Vector3(-57.3f, 1.8f, 44f), 0.7f);
            Pipe(gStructures, "A_Pipe_Overhead_1", new Vector3(-58f, 4.6f, 31f), new Vector3(-36f, 4.6f, 31f), 0.6f);
            Pipe(gStructures, "A_Pipe_Overhead_2", new Vector3(-58f, 5f, 40f), new Vector3(-44f, 5f, 40f), 0.6f);
            Tank(gStructures, "A_Tank_Main", -36f, 31f, 4f, 4f);
            Tank(gStructures, "A_Tank_Small", -27f, 42f, 3.5f, 3.4f);
            Block(gStructures, "A_Generator", -46f, 34f, -42f, 38f, 2.5f);

            Crate(gCover, -42f, 30f, 2f, 2f, 2f);
            Crate(gCover, -54f, 37f, 1.4f, 1.4f, 1.4f);
            Crate(gCover, -40f, 52f, 1.5f, 1.5f, 1.5f);
            Crate(gCover, -50f, 49f, 1.2f, 1.2f, 1.2f, 1.2f); // pe platforma A
            HalfWall(gCover, -40f, 42f, 3f, 0f);

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
            Crate(gCover, -38f, 16f, 1.5f, 1.5f, 1.5f);
            HalfWall(gCover, -43f, 20f, 3f, 0f);
            Lamp("Light_AControl", -40f, low, 16f, WarmLight, 12f, 2.5f);

            // ---------------- A Pipes (sala pompelor) ----------------
            // Ruta proprie: West Outpost -> sala pompelor -> usa din peretele de vest al site-ului.
            Roof("Roof_APipes", -76f, 10f, -62f, 34f, 4.5f);
            DoorWithLintel("Door_APipes_South", -70f, 6f, -68f, 10f, false, InnerWallHeight);
            DoorWithLintel("Door_APipes_Site", -62f, 28f, -58f, 30f, true, InnerWallHeight);

            Pipe(gStructures, "Pumps_Pipe_Low", new Vector3(-75.3f, 0.6f, 12f), new Vector3(-75.3f, 0.6f, 32f), 0.9f);
            Pipe(gStructures, "Pumps_Pipe_High", new Vector3(-75.3f, 2f, 12f), new Vector3(-75.3f, 2f, 32f), 0.7f);
            Pipe(gStructures, "Pumps_Pipe_Cross", new Vector3(-76f, 3.6f, 22f), new Vector3(-62f, 3.6f, 22f), 0.6f);
            Tank(gStructures, "Pumps_Tank_South", -66f, 15f, 3f, 3.2f);
            Tank(gStructures, "Pumps_Tank_North", -71f, 27f, 3f, 3.2f);
            Block(gStructures, "Pumps_Machine", -68f, 19f, -64f, 23f, 2.2f);

            Lamp("Light_APipes_South", -69f, 3.8f, 15f, WarmLight, 14f, 2.5f);
            Lamp("Light_APipes_North", -69f, 3.8f, 29f, WarmLight, 14f, 2.5f);

            // ---------------- Depozit ----------------
            // Leaga A Control de Mid Connector si are un perete moale direct in site.
            Roof("Roof_Depot", -32f, 4f, -24f, 20f, up);
            DoorWithLintel("Door_Depot_East", -24f, 10f, -22f, 12f, true, InnerWallHeight);
            DoorWithLintel("Door_Depot_West", -34f, 14f, -32f, 16f, true, InnerWallHeight);

            SoftWall(gStructures, "Depot_Breach_SoftWall", -30f, 21f, -28f, 21f, 0f, 2.5f);
            Lintel(gDoors, "Depot_Breach_Lintel", -30f, 20f, -28f, 22f, InnerWallHeight);

            Block(gStructures, "Depot_Shelf_West", -32f, 5f, -31f, 9f, 2.2f);
            Block(gStructures, "Depot_Shelf_East", -25f, 14f, -24f, 19f, 2.2f);
            Crate(gCover, -28f, 7f, 2f, 1.1f, 2f);

            Lamp("Light_Depot", -28f, low, 12f, WarmLight, 12f, 2.5f);

            // ---------------- Gameplay ----------------
            CreateBombSite(gGameplay, "BombSite_A", SiteA);
            CreateDummy(gDummies, "Dummy_Defender_SiteA", new Vector3(-40f, 0f, 37f));
            CreateDummy(gDummies, "Dummy_Defender_AHeaven", new Vector3(-28f, up, 52f));
        }
    }
}
