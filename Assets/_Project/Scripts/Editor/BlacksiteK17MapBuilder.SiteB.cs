using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Hangar / Bombsite B, Container Stack, Loading Bay si biroul din coltul sud-estic.
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSiteB()
        {
            float up = UpperFloorHeight;
            float wall = GroundCeiling;
            float low = GroundCeiling - 0.4f;

            // ---------------- Hangar ----------------
            Stairs(gStairs, "Stairs_Hangar_West", 22f, -42f, 26f, -34f, Vector3.forward, 0f, up);
            Stairs(gStairs, "Stairs_Hangar_North", 50f, -2f, 58f, 2f, Vector3.left, 0f, up);

            // Pasarela pe vest, mezanin pe nord, balcon spre Mid East Corridor
            CreatePlatform(gUpper, "Hangar_Catwalk_West", 22f, -34f, 26f, 2f, up);
            CreatePlatform(gUpper, "Hangar_Mezzanine_North", 26f, -2f, 50f, 2f, up);
            CreatePlatform(gUpper, "Hangar_North_Balcony", 38f, 2f, 44f, 8f, up);
            Railing(gUpper, "Hangar_Catwalk_West_Rail", 26f, -34f, 26f, -2f, up);
            Railing(gUpper, "Hangar_Mezzanine_Rail", 26f, -2f, 50f, -2f, up);
            Parapet(gUpper, "Hangar_North_Balcony_Parapet", 38f, 8f, 44f, 8f, up);

            // Acoperis peste hangar si Loading Bay, cu luminator deasupra site-ului
            Slab(gRoofs, "Roof_Hangar", InnerWallHeight + SlabThickness,
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

            // Utilaj pe vest, zid sub mezanin, rand de containere care separa B de culoarul din est
            Block(gStructures, "Hangar_Machine", 28f, -18f, 34f, -10f, wall);
            InnerWall(gStructures, "Hangar_Wall_North_1", 28f, -6f, 36f, -6f, wall);
            InnerWall(gStructures, "Hangar_Wall_North_2", 44f, -6f, 48f, -6f, wall);
            Container(gCover, 59f, -14f, 0f, containerRedMat);
            Container(gCover, 59f, -22f, 0f, containerBlueMat);
            Container(gCover, 59f, -22f, 0f, containerRedMat, 2.6f);
            Container(gCover, 59f, -31f, 0f, containerBlueMat);

            // Cover pe site
            Container(gCover, 40f, -18f, 90f, containerRedMat);
            Crate(gCover, 50f, -24f, 2f, 2f, 2f);
            Crate(gCover, 48f, -12f, 2f, 2f, 2f);
            Crate(gCover, 48f, -12f, 1.4f, 1.4f, 1.4f, 2f);
            Container(gCover, 31f, -30f, 0f, containerBlueMat);
            Container(gCover, 52f, -4f, 90f, containerRedMat);
            Crate(gCover, 34f, -4f, 1.5f, 1.5f, 1.5f);
            Crate(gCover, 40f, -34f, 2f, 1.2f, 2f);
            Crate(gCover, 56f, -37f, 2f, 2f, 2f);

            // Container Stack - latura estica a hangarului
            Container(gCover, 67f, -10f, 0f, containerRedMat);
            Container(gCover, 67f, -10f, 0f, containerBlueMat, 2.6f);
            Container(gCover, 73f, -18f, 0f, containerBlueMat);
            Container(gCover, 67f, -26f, 0f, containerBlueMat);
            Container(gCover, 73f, -30f, 0f, containerRedMat);
            Container(gCover, 73f, -30f, 0f, containerRedMat, 2.6f);

            Barricade(gBarricades, "Barricade_HangarNorthDoor", 41f, 4f, 0f, 6f, wall, true);
            Barricade(gBarricades, "Barricade_HangarWestDoor", 23f, -1f, 0f, 6f, wall, false);

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
            Container(gCover, 32f, -48f, 90f, containerBlueMat);
            Container(gCover, 46f, -52f, 90f, containerRedMat);
            Container(gCover, 46f, -52f, 90f, containerBlueMat, 2.6f);
            Crate(gCover, 40f, -44f, 2f, 1.2f, 2f);
            Crate(gCover, 54f, -46f, 1.5f, 1.5f, 1.5f);

            Lamp("Light_LoadingBay_West", 30f, 7.4f, -48f, WhiteLight, 20f, 3.5f);
            Lamp("Light_LoadingBay_Mid", 43f, 7.4f, -47f, WhiteLight, 20f, 3.5f);
            Lamp("Light_LoadingBay_East", 55f, 7.4f, -47f, WhiteLight, 20f, 3.5f);

            // ---------------- Birou (coltul sud-estic) ----------------
            // Leaga Loading Bay de latura estica a hangarului, in spatele Container Stack-ului.
            Roof("Roof_LoadingOffice", 62f, -54f, 76f, -42f, up);
            DoorWithLintel("Door_Office_West", 60f, -50f, 62f, -48f, true, InnerWallHeight);
            DoorWithLintel("Door_Office_North", 64f, -42f, 66f, -40f, false, InnerWallHeight);

            Block(gStructures, "Office_Shelf", 75f, -53f, 76f, -47f, 2.2f);
            Crate(gCover, 70f, -45f, 4f, 1f, 1.2f);
            Crate(gCover, 67f, -52f, 2f, 1.1f, 2f);

            Lamp("Light_Office", 69f, low, -48f, WhiteLight, 12f, 2.5f);

            // ---------------- Gameplay ----------------
            CreateBombSite(gGameplay, "BombSite_B", SiteB);
            CreateDummy(gDummies, "Dummy_Defender_SiteB", new Vector3(44f, 0f, -8f));
        }
    }
}
