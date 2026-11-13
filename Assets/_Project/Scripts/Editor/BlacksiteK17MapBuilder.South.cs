using UnityEngine;

namespace ChokeHold.EditorTools
{
    // South Courtyard, South Lane si South Tunnel (subteran).
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildSouth()
        {
            float wall = GroundCeiling;

            // ---------------- South Courtyard ----------------
            // Magazie in centru + ziduri care impart curtea in trei culoare.
            Block(gStructures, "Courtyard_Shed", -15f, -32f, -5f, -24f, 4f);
            InnerWall(gStructures, "Courtyard_Wall_East", 7f, -34f, 7f, -26f, wall);
            InnerWall(gStructures, "Courtyard_Wall_SouthWest", -20f, -40f, -20f, -34f, wall);
            InnerWall(gStructures, "Courtyard_Wall_North", -14f, -18f, -6f, -18f, wall);

            Container(gCover, -2f, -22f, 90f, containerRedMat);
            Crate(gCover, -22f, -20f, 2f, 1.2f, 2f);
            Crate(gCover, 4f, -34f, 1.5f, 1.5f, 1.5f);
            Crate(gCover, 10f, -24f, 2f, 2f, 2f);
            HalfWall(gCover, -8f, -36f, 4f, 0f);

            CreateDummy(gDummies, "Dummy_Defender_Courtyard", new Vector3(0f, 0f, -28f));

            // ---------------- South Lane ----------------
            InnerWall(gStructures, "SouthLane_Stub_West", -10f, -46f, -10f, -42f, wall);
            InnerWall(gStructures, "SouthLane_Stub_East", 14f, -42f, 14f, -38f, wall);
            Crate(gCover, 0f, -44f, 1.5f, 1.5f, 1.5f);

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
            SolidBlock(gUnderground, "Tunnel_Baffle_1", -29f, -58f, -28f, -54.5f, TunnelFloorY, depth, wallMat);
            SolidBlock(gUnderground, "Tunnel_Baffle_2", -19f, -55.5f, -18f, -52f, TunnelFloorY, depth, wallMat);
            SolidBlock(gUnderground, "Tunnel_Baffle_3", -1f, -58f, 0f, -54.5f, TunnelFloorY, depth, wallMat);
            SolidBlock(gUnderground, "Tunnel_Baffle_4", 5f, -55.5f, 6f, -52f, TunnelFloorY, depth, wallMat);

            Barricade(gBarricades, "Barricade_TunnelStairs", -6f, -52f, TunnelFloorY, 4f, depth, true);

            for (float x = -36f; x <= 8f; x += 11f)
            {
                Lamp($"Light_Tunnel_{x:0}", x, -0.7f, -55f, WarmLight, 12f, 2.5f);
            }
        }
    }
}
