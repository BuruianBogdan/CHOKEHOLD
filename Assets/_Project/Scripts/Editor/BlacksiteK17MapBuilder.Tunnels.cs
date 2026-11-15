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

            BuildBunker();
        }

        // ------------------------------------------------------------------
        // BUNCARUL (x 22..40, z -32..-16, sub partea de vest a hangarului)
        //
        //   - podeaua la -6 m, tavanul (placa de la sol) la -0,5 m: 5,5 m inaltime;
        //   - balcon in L pe vest si nord, la nivelul tunelurilor (-3,6 m); pe el ajung cele trei legaturi;
        //   - sub balcon raman nise de ~2 m inaltime (locuri de ascuns);
        //   - doua scari in groapa centrala, stalpi pana in tavan, cover;
        //   - fiecare legatura are o baricada pe care aparatorii o pot monta.
        // ------------------------------------------------------------------
        private static void BuildBunker()
        {
            float floor = BunkerFloorY;
            float balcony = TunnelFloorY;
            float toCeiling = -0.5f - floor;

            // Balcon in L (cu nise dedesubt)
            CreatePlatform(gUnderground, "Bunker_Balcony_West", 22f, -32f, 26f, -16f, balcony);
            CreatePlatform(gUnderground, "Bunker_Balcony_North", 26f, -20f, 40f, -16f, balcony);

            // Scari de pe balcon in groapa
            Stairs(gUnderground, "Bunker_Stairs_West", 26f, -26f, 32f, -22f, Vector3.left, floor, balcony - floor);
            Stairs(gUnderground, "Bunker_Stairs_North", 36f, -26f, 40f, -20f, Vector3.forward, floor, balcony - floor);

            // Stalpi de la podea pana in tavan (doi in groapa, doi sub muchia balconului de vest)
            SolidBlock(gUnderground, "Bunker_Pillar_1", 29.4f, -29.6f, 30.6f, -28.4f, floor, toCeiling, wallMat);
            SolidBlock(gUnderground, "Bunker_Pillar_2", 35.4f, -29.6f, 36.6f, -28.4f, floor, toCeiling, wallMat);
            SolidBlock(gUnderground, "Bunker_Pillar_3", 25.4f, -29.6f, 26.6f, -28.4f, floor, toCeiling, wallMat);
            SolidBlock(gUnderground, "Bunker_Pillar_4", 29.4f, -20.6f, 30.6f, -19.4f, floor, toCeiling, wallMat);

            // Locuri de ascuns si cover in groapa
            BigCrate(33.5f, -24.5f, floor);
            CrateStack(38.6f, -30.6f, floor);
            HighCover(27.2f, -31.2f, 29f, -31.2f, floor);
            LowCover(32.5f, -30.8f, 34.5f, -30.8f, floor);
            LowCover(23.2f, -28f, 23.2f, -25f, floor);   // in nisa de sub balconul de vest (cover de crouch)

            // Lumini calde, de buncar
            Lamp("Light_Bunker_Center", 32f, -1.1f, -27f, WarmLight, 14f, 3f);
            Lamp("Light_Bunker_East", 38f, -1.1f, -24f, WarmLight, 12f, 2.5f);

            // Pereti-sicana pe legaturile lungi: niciun culoar drept nu intra direct peste balcon.
            float linkWall = -0.5f - TunnelFloorY;
            SolidBlock(gUnderground, "BunkerWestLink_Baffle_1", 6f, -18f, 7f, -15.5f, TunnelFloorY, linkWall, wallMat);
            SolidBlock(gUnderground, "BunkerWestLink_Baffle_2", 14f, -16.5f, 15f, -14f, TunnelFloorY, linkWall, wallMat);
            SolidBlock(gUnderground, "BunkerSouthLink_Baffle_1", 13f, -32.5f, 14f, -30f, TunnelFloorY, linkWall, wallMat);
            SolidBlock(gUnderground, "BunkerSouthLink_Baffle_2", 6f, -42f, 8.5f, -41f, TunnelFloorY, linkWall, wallMat);
            SolidBlock(gUnderground, "BunkerWestLink_Baffle_3", 17f, -18f, 18f, -15.5f, TunnelFloorY, linkWall, wallMat);
            SolidBlock(gUnderground, "BunkerSouthLink_Baffle_3", 17f, -34f, 18f, -31.5f, TunnelFloorY, linkWall, wallMat);
            SolidBlock(gUnderground, "NorthTunnel_Baffle_South", -4.5f, -6f, -2f, -5f, TunnelFloorY, linkWall, wallMat);

            // Baricade pe cele trei legaturi (aparatorii pot inchide buncarul dinspre o parte)
            Barricade(gBarricades, "Barricade_Bunker_West", 20f, -16f, TunnelFloorY, 4f, 3f, false);
            Barricade(gBarricades, "Barricade_Bunker_North", 34f, -11f, TunnelFloorY, 4f, 3f, true);
            Barricade(gBarricades, "Barricade_Bunker_South", 20f, -32f, TunnelFloorY, 4f, 3f, false);
        }
    }
}
