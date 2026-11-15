using UnityEngine;

namespace ChokeHold.EditorTools
{
    // South Courtyard, South Lane si South Tunnel (subteran).
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSouth()
        {
            float wall = GroundCeiling;
            float full = InnerWallHeight;

            // ---------------- South Courtyard ----------------
            // Magazie in centru + ziduri care impart curtea in trei culoare.
            Block(gStructures, "Courtyard_Shed", -15f, -32f, -5f, -24f, 4f);
            InnerWall(gStructures, "Courtyard_Wall_East", 7f, -34f, 7f, -26f, wall);
            InnerWall(gStructures, "Courtyard_Wall_SouthWest", -20f, -37.75f, -20f, -34f, wall);
            InnerWall(gStructures, "Courtyard_Wall_North", -14f, -18f, -6f, -18f, wall);
            HighCover(-3f, -18f, -3f, -14.5f);         // taie banda de nord a curtii

            Container(gCover, -2f, -22f, 90f, containerRedMat);
            BigCrate(3f, -34f);
            BigCrate(13f, -25f);
            InnerWall(gStructures, "Courtyard_Screen_Choke", 5f, -19f, 11f, -19f, wall);   // paravan in fata usii din Choke
            HighCover(-18f, -34f, -14f, -34f);         // paravane in fata usilor dinspre South Lane
            HighCover(9f, -33f, 13f, -33f);

            CreateDummy(gDummies, "Dummy_Defender_Courtyard", new Vector3(0f, 0f, -28f));

            // ---------------- South Lane ----------------
            // Curtea si aleea sunt despartite de un zid cu doua usi duble, decalate fata de usa din Choke.
            Partition("Part_Courtyard_Lane", -22f, -38f, 16f, -38f, full, DoubleGap(6f), DoubleGap(33f));
            Partition("Part_Courtyard_Lane_Corner", -22f, -40f, -22f, -38f, full);

            // Trei ziduri alternate (sud, nord, sud): nicio linie dreapta nu trece de toate trei.
            InnerWall(gStructures, "SouthLane_Stub_West", -10f, -46f, -10f, -41.5f, wall);
            InnerWall(gStructures, "SouthLane_Stub_Mid", 2f, -42.5f, 2f, -38f, wall);
            InnerWall(gStructures, "SouthLane_Stub_East", 14f, -46f, 14f, -41.5f, wall);

            // ---------------- South Tunnel (subteran, -3,5 m) ----------------
            float depth = -TunnelFloorY;

            // Rampe la ambele capete: din spawn-ul T1 si din Loading Bay
            CreateRampAt(gUnderground, "Tunnel_Ramp_West", -50f, -58f, -40f, -52f, Vector3.left, TunnelFloorY, depth, rampMat);
            CreateRampAt(gUnderground, "Tunnel_Ramp_East", 10f, -58f, 20f, -52f, Vector3.right, TunnelFloorY, depth, rampMat);

            // Scara din South Lane, la mijlocul tunelului
            Stairs(gUnderground, "Tunnel_Stairs", -8f, -52f, -4f, -44f, Vector3.forward, TunnelFloorY, depth);

            // Balustrade la nivelul solului, pe marginea santurilor deschise
            Railing(gStructures, "TunnelMouth_West_Rail", -50f, -52f, -44f, -52f, 0f);
            Railing(gStructures, "TunnelStairs_Rail_W", -8f, -46f, -8f, -44f, 0f);
            Railing(gStructures, "TunnelStairs_Rail_E", -4f, -46f, -4f, -44f, 0f);

            // Pereti alternati (sicana): tunelul nu mai e un singur culoar drept
            // Lipiti alternativ de peretele de sud si de cel de nord, la ~7 m unul de altul.
            float[] baffleX = { -34f, -28f, -21f, -15f, -10f, -2f, 4f };

            for (int i = 0; i < baffleX.Length; i++)
            {
                bool south = i % 2 == 0;

                SolidBlock(gUnderground, $"Tunnel_Baffle_{i + 1}",
                    baffleX[i], south ? -58f : -55.5f, baffleX[i] + 1f, south ? -54.5f : -52f,
                    TunnelFloorY, depth, wallMat);
            }

            LowCover(-25f, -57f, -23f, -57f, TunnelFloorY);

            Barricade(gBarricades, "Barricade_TunnelStairs", -6f, -52f, TunnelFloorY, 4f, depth, true);

            for (float x = -36f; x <= 8f; x += 11f)
            {
                Lamp($"Light_Tunnel_{x:0}", x, -0.7f, -55f, WarmLight, 12f, 2.5f);
            }
        }
    }
}
