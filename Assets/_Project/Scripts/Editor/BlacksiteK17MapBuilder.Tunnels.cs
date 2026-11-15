using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Tunelurile de serviciu: doua treceri subterane noi care leaga partea atacatorilor de spatele
    // aparatorilor (si invers), pe langa South Tunnel. Geometria subterana (podea, pereti) vine din
    // UndergroundAreas / OpenPitAreas din Layout.cs; aici sunt scarile, camerele, usile si detaliile.
    //
    //   North Service Tunnel  camera de scara langa South Courtyard / T1 Walkway North
    //                         -> pe sub Choke Point si Mid Lab -> put de scara in North Overlook
    //   East Service Tunnel   camera de scara langa South Courtyard
    //                         -> pe sub hangar -> put de scara in East Yard
    //
    // Aparatorii pot baricada iesirea de la capatul lor (baricada se sparge cu gloante).
    public static partial class BlacksiteK17MapBuilder
    {
        private static void BuildServiceTunnels()
        {
            float depth = -TunnelFloorY;
            float full = InnerWallHeight;
            float roof = UpperFloorHeight;

            // ---------------- North Service Tunnel ----------------

            // Camera de scara (atacatori): palier pe vest, scara coboara spre est.
            Roof("Roof_NorthStairRoom", -16f, -12f, -6f, -8f, roof);
            DoorCell("Door_NorthStair_S", -16f, -14f, -14f, -12f, false, full);
            DoorCell("Door_NorthStair_W", -18f, -12f, -16f, -10f, true, full);
            Stairs(gUnderground, "NorthTunnel_Stairs_South", -14f, -12f, -6f, -8f, Vector3.left, TunnelFloorY, depth);

            // Put de scara in North Overlook (aparatori), cu balustrade pe laturi.
            Stairs(gUnderground, "NorthTunnel_Stairs_North", -6f, 42f, -2f, 50f, Vector3.forward, TunnelFloorY, depth);
            Railing(gStructures, "NorthTunnel_Rail_W", -6f, 42f, -6f, 49.5f, 0f);
            Railing(gStructures, "NorthTunnel_Rail_E", -2f, 42f, -2f, 49.5f, 0f);
            Barricade(gBarricades, "Barricade_NorthTunnel_Exit", -4f, 42.3f, TunnelFloorY, 4f, 3f, true);

            // Pereti alternati: culoarul de 54 m nu e o singura linie de tragere.
            for (int k = 0; k < 4; k++)
            {
                float z = 2f + k * 10f;
                bool west = k % 2 == 0;

                SolidBlock(gUnderground, $"NorthTunnel_Baffle_{k + 1}",
                    west ? -6f : -4.5f, z, west ? -3.5f : -2f, z + 1f,
                    TunnelFloorY, depth - 0.5f, wallMat);
            }

            // ---------------- East Service Tunnel ----------------

            // Camera de scara (atacatori): palier pe sud, scara coboara spre est.
            Roof("Roof_EastStairRoom", 12f, -12f, 20f, -6f, roof);
            DoorCell("Door_EastStair", 14f, -14f, 16f, -12f, false, full);
            Stairs(gUnderground, "EastTunnel_Stairs_West", 12f, -10f, 20f, -6f, Vector3.left, TunnelFloorY, depth);
            Railing(gStructures, "EastTunnel_Rail_Landing", 14f, -10f, 20f, -10f, 0f);

            // Put de scara in East Yard (aparatori).
            Stairs(gUnderground, "EastTunnel_Stairs_East", 66f, 10f, 70f, 18f, Vector3.forward, TunnelFloorY, depth);
            Railing(gStructures, "EastTunnel_Rail_W", 66f, 10f, 66f, 17.5f, 0f);
            Railing(gStructures, "EastTunnel_Rail_E", 70f, 10f, 70f, 17.5f, 0f);
            Barricade(gBarricades, "Barricade_EastTunnel_Exit", 68f, 10.3f, TunnelFloorY, 4f, 3f, true);

            for (int k = 0; k < 4; k++)
            {
                float x = 29f + k * 10f;
                bool south = k % 2 == 0;

                SolidBlock(gUnderground, $"EastTunnel_Baffle_{k + 1}",
                    x, south ? -10f : -8.5f, x + 1f, south ? -7.5f : -6f,
                    TunnelFloorY, depth - 0.5f, wallMat);
            }

            // Cotul spre nord: un perete care opreste linia de pe culoarul lung in puțul de scara.
            SolidBlock(gUnderground, "EastTunnel_Baffle_Corner", 66f, -1f, 68.5f, 0f, TunnelFloorY, depth - 0.5f, wallMat);
        }
    }
}
