using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Acoperisuri pe care se poate juca (ca in Siege: urci si tii un unghi de sus) si camera de control:
    //
    //   Outpost Tower       turn de paza in West Outpost (4 m), scara de mana, parapet
    //   Courtyard Shed      magazia din South Courtyard (4 m), scara de mana, parapet, unitate de ventilatie
    //   Lab Roof            acoperisul laboratorului (8,4 m): parapet de jur imprejur, scara de mana din Mid Connector
    //   Control Room        etajul 3 al laboratorului (punctul de control al centralei), pe acoperis;
    //                       scara interioara de la etajul 2, doua usi spre acoperis, ferestre pe toate laturile
    //
    // Marginile acoperisurilor au deasupra parapetului o bariera invizibila (strat "Ignore Raycast"):
    // opreste jucatorii (nu poti sari pe crestele zidurilor), dar nu si gloantele sau privirea.
    public static partial class BlacksiteK17MapBuilder
    {
        private const float LabRoofY = 2f * UpperFloorHeight + SlabThickness;   // 8,4 m
        private const float ControlRoomHeight = 3.6f;
        private const int ClipLayer = 2;                                        // "Ignore Raycast"

        private static readonly Area LabRoof = new Area("Lab Roof", -12f, 4f, 20f, 42f);
        private static readonly Area ControlRoom = new Area("Control Room", 6f, 14f, 18f, 30f);
        private static Area outpostTowerRoof = new Area("Outpost Tower Roof", -70f, -14f, -64f, -8f);
        private static Area shedRoof = new Area("Courtyard Shed Roof", -15f, -32f, -5f, -24f);

        private static Transform gRooftops;

        private static void BuildRooftops()
        {
            gRooftops = CreateGroup(gStructures, "Rooftops");
            Physics.SyncTransforms();

            BuildBlockRooftop("Outpost_Tower", "OutpostTower", ref outpostTowerRoof, Vector3.right);
            BuildBlockRooftop("Courtyard_Shed", "CourtyardShed", ref shedRoof, Vector3.forward);
            BuildLabRoof();
            BuildControlRoom();
        }

        // ------------------------------------------------------------------
        // Turn / magazie: acoperis plat la 4 m, parapet cu gol pentru scara
        // ------------------------------------------------------------------
        private static void BuildBlockRooftop(string blockName, string prefix, ref Area area, Vector3 ladderSide)
        {
            GameObject block = GameObject.Find(blockName);

            if (block == null)
            {
                return;
            }

            Bounds b = block.GetComponent<Collider>().bounds;
            float top = b.max.y;
            area = new Area(area.Name, b.min.x, b.min.z, b.max.x, b.max.z);

            const float inset = 0.15f;
            float x0 = b.min.x + inset, x1 = b.max.x - inset;
            float z0 = b.min.z + inset, z1 = b.max.z - inset;

            // Scara pe mijlocul laturii alese; parapetul are gol in dreptul ei.
            Vector3 c = b.center;
            Vector3 wallPoint = new Vector3(
                ladderSide.x > 0 ? b.max.x : ladderSide.x < 0 ? b.min.x : c.x,
                b.min.y,
                ladderSide.z > 0 ? b.max.z : ladderSide.z < 0 ? b.min.z : c.z);

            const float gap = 0.8f;

            void Edge(string name, float ax, float az, float bx, float bz, bool ladderEdge)
            {
                if (!ladderEdge)
                {
                    RoofParapet($"{prefix}_{name}", ax, az, bx, bz, top);
                    return;
                }

                bool alongX = Mathf.Abs(bx - ax) > Mathf.Abs(bz - az);
                float mid = alongX ? wallPoint.x : wallPoint.z;

                if (alongX)
                {
                    RoofParapet($"{prefix}_{name}_A", ax, az, mid - gap, bz, top);
                    RoofParapet($"{prefix}_{name}_B", mid + gap, az, bx, bz, top);
                }
                else
                {
                    RoofParapet($"{prefix}_{name}_A", ax, az, bx, mid - gap, top);
                    RoofParapet($"{prefix}_{name}_B", ax, mid + gap, bx, bz, top);
                }
            }

            Edge("Parapet_S", x0, z0, x1, z0, ladderSide.z < 0);
            Edge("Parapet_N", x0, z1, x1, z1, ladderSide.z > 0);
            Edge("Parapet_W", x0, z0 + 0.15f, x0, z1 - 0.15f, ladderSide.x < 0);
            Edge("Parapet_E", x1, z0 + 0.15f, x1, z1 - 0.15f, ladderSide.x > 0);

            LadderAt($"Ladder_{prefix}", wallPoint, ladderSide, top - b.min.y);
        }

        /// <summary>Parapet de 1,1 m (cover) pe muchia acoperisului, cu bariera invizibila deasupra.</summary>
        private static void RoofParapet(string name, float x0, float z0, float x1, float z1, float floorY)
        {
            if (Vector2.Distance(new Vector2(x0, z0), new Vector2(x1, z1)) < 0.3f)
            {
                return;
            }

            Parapet(gRooftops, name, x0, z0, x1, z1, floorY);
            PlayerClip(name + "_Clip", x0, z0, x1, z1, floorY + 1.1f, 1.6f, 0.3f);
        }

        /// <summary>Perete invizibil care opreste doar jucatorii (gloantele si razele de vedere trec).</summary>
        private static void PlayerClip(string name, float x0, float z0, float x1, float z1, float baseY, float height, float thickness)
        {
            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            Vector3 size = alongX
                ? new Vector3(Mathf.Abs(x1 - x0), height, thickness)
                : new Vector3(thickness, height, Mathf.Abs(z1 - z0));

            GameObject clip = new GameObject(name);
            clip.transform.SetParent(gRooftops, false);
            clip.transform.position = new Vector3((x0 + x1) * 0.5f, baseY + height * 0.5f, (z0 + z1) * 0.5f);
            clip.layer = ClipLayer;
            clip.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------
        // Scara de mana
        // ------------------------------------------------------------------

        /// <summary>wallPoint: pe fata peretelui, la podea; outward: din perete spre jucator.</summary>
        private static void LadderAt(string name, Vector3 wallPoint, Vector3 outward, float height)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(gRooftops, false);
            go.transform.SetPositionAndRotation(wallPoint + outward * 0.35f, Quaternion.LookRotation(outward));

            BoxCollider volume = go.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.center = new Vector3(0f, (height + 1f) * 0.5f, 0f);
            volume.size = new Vector3(1.0f, height + 1f, 0.7f);
            go.AddComponent<Ladder>();

            // Vizual: tronsoane de 1 m (kit) pe perete, plus manerele de sus
            int sections = Mathf.Max(1, Mathf.RoundToInt(height));
            float sectionHeight = height / sections;
            Quaternion facing = Quaternion.LookRotation(outward);

            for (int k = 0; k < sections; k++)
            {
                GameObject section = CreateKitObject(go.transform, "K17_Ladder_Section", Vector3.zero, Quaternion.identity,
                    new Vector3(1f, sectionHeight, 1f));

                if (section == null)
                {
                    break;
                }

                section.transform.SetPositionAndRotation(wallPoint + Vector3.up * (k * sectionHeight) + outward * 0.005f, facing);
            }

            GameObject topRails = CreateKitObject(go.transform, "K17_Ladder_Top", Vector3.zero, Quaternion.identity, Vector3.one);

            if (topRails != null)
            {
                topRails.transform.SetPositionAndRotation(wallPoint + Vector3.up * height + outward * 0.005f, facing);
            }
        }

        // ------------------------------------------------------------------
        // Acoperisul laboratorului
        // ------------------------------------------------------------------
        /// <summary>
        /// Acoperisul jucabil al cladirii din mijloc: laboratorul plus crestele zidurilor groase din jur,
        /// pana la marginea curtilor (de acolo se vede in Choke Point, Mid Connector, North Overlook, Mid East).
        /// Celulele de 2 m cu varful la 8,4-8,5 m din dreptunghiul de mai jos formeaza acoperisul;
        /// pe fiecare muchie exterioara: parapet de 1,1 m si bariera invizibila deasupra.
        /// </summary>
        private static readonly Area LabRoofExtent = new Area("Lab Roof Extent", -12f, 4f, 20f, 42f);

        private static void BuildLabRoof()
        {
            float y = LabRoofY;
            Area e = LabRoofExtent;
            int nx = Mathf.RoundToInt((e.XMax - e.XMin) / Cell);
            int nz = Mathf.RoundToInt((e.ZMax - e.ZMin) / Cell);
            bool[,] roof = new bool[nx, nz];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float cx = e.XMin + (i + 0.5f) * Cell, cz = e.ZMin + (j + 0.5f) * Cell;
                    // Golul scarii spre camera de control face parte din acoperis (balustrada lui e separata).
                    bool stairHole = cx > 14f && cx < 18f && cz > 14f && cz < 24f;

                    roof[i, j] = stairHole ||
                                 (Physics.Raycast(new Vector3(cx, 30f, cz), Vector3.down, out RaycastHit top, 40f, ~0,
                                      QueryTriggerInteraction.Ignore) &&
                                  top.point.y > y - 0.15f && top.point.y < InnerWallHeight + 0.15f);
                }
            }

            // Scara de mana din Mid Connector, pe fata de vest a zidului dintre alee si laborator (celula z 12..14).
            const float ladderZ = 13f;
            int ladderJ = Mathf.FloorToInt((ladderZ - e.ZMin) / Cell);
            bool ladderOk = roof[0, ladderJ];

            bool IsRoof(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && roof[i, j];

            // Muchiile exterioare, unite in segmente drepte. Segmentele pe X au lungimea intreaga;
            // cele pe Z se scurteaza cu 0,3 m la capete, ca la colturi sa nu intre unele in altele.
            const float inset = 0.15f;
            int index = 0;

            for (int j = 0; j <= nz; j++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int start = -1;
                    for (int i = 0; i <= nx; i++)
                    {
                        // muchia de jos (side -1) a celulei (i, j) sau muchia de sus (side +1) a celulei (i, j-1)
                        bool edge = side < 0
                            ? IsRoof(i, j) && !IsRoof(i, j - 1)
                            : IsRoof(i, j - 1) && !IsRoof(i, j);

                        if (edge && start < 0)
                        {
                            start = i;
                        }
                        else if (!edge && start >= 0)
                        {
                            float z = e.ZMin + j * Cell + (side < 0 ? inset : -inset);
                            RoofParapet($"LabRoof_Parapet_{index++:00}", e.XMin + start * Cell, z, e.XMin + i * Cell, z, y);
                            start = -1;
                        }
                    }
                }
            }

            for (int i = 0; i <= nx; i++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int start = -1;
                    for (int j = 0; j <= nz; j++)
                    {
                        bool edge = side < 0
                            ? IsRoof(i, j) && !IsRoof(i - 1, j)
                            : IsRoof(i - 1, j) && !IsRoof(i, j);

                        // gol in parapet in dreptul scarii de mana
                        if (ladderOk && i == 0 && side < 0 && j == ladderJ)
                        {
                            edge = false;
                        }

                        if (edge && start < 0)
                        {
                            start = j;
                        }
                        else if (!edge && start >= 0)
                        {
                            float x = e.XMin + i * Cell + (side < 0 ? inset : -inset);
                            RoofParapet($"LabRoof_Parapet_{index++:00}", x, e.ZMin + start * Cell + 0.3f, x, e.ZMin + j * Cell - 0.3f, y);
                            start = -1;
                        }
                    }
                }
            }

            if (ladderOk)
            {
                LadderAt("Ladder_LabRoof", new Vector3(e.XMin, 0f, ladderZ), Vector3.left, InnerWallHeight);
            }
            else
            {
                Debug.LogWarning("Acoperisul laboratorului: zidul din Mid Connector nu e la 8,5 m; scara de mana lipseste.");
            }

            // Unitati de ventilatie: cover pe acoperis (si rup liniile lungi peste el).
            SolidBlock(gRooftops, "Roof_HVAC_1", -6f, 29f, -3f, 31f, y, 1.6f, coverMat);
            SolidBlock(gRooftops, "Roof_HVAC_2", -1f, 15f, 2f, 17f, y, 1.6f, coverMat);
            SolidBlock(gRooftops, "Roof_HVAC_3", 9f, 32.5f, 12f, 34.5f, y, 1.6f, coverMat);

            // Iesirea scarii din camera de control nu cade pe acoperis: balustrada pe latura libera a golului.
            Railing(gRooftops, "ControlStair_Rail_Roof", 14f, 14.4f, 14f, 24f, y);
        }

        // ------------------------------------------------------------------
        // Camera de control (etajul 3)
        // ------------------------------------------------------------------
        private static void BuildControlRoom()
        {
            float floor = LabRoofY;
            float top = floor + ControlRoomHeight;
            const float t = 0.4f;
            Area r = ControlRoom;

            // Scara de la etajul 2 (4 m) la camera de control (8,4 m), in coltul de sud-est al laboratorului.
            Stairs(gStairs, "Stairs_ControlRoom", 14f, 14f, 17.6f, 24f, Vector3.forward, UpperFloorHeight, floor - UpperFloorHeight);

            // Pereti cu ferestre largi pe toate laturile si doua usi spre acoperis.
            // (pozitie pe latura, latime, pervaz, sus) - pervaz 0 = usa
            RoomWall("Control_Wall_W", r.XMin, r.ZMin, r.XMin, r.ZMax, floor, top, t,
                (19f, 5f, 1.0f, 2.3f), (26f, DoorWidth, 0f, DoorHeight));
            RoomWall("Control_Wall_S", r.XMin, r.ZMin, r.XMax, r.ZMin, floor, top, t,
                (9f, DoorWidth, 0f, DoorHeight), (15.8f, 3f, 1.0f, 2.3f));
            RoomWall("Control_Wall_N", r.XMin, r.ZMax, r.XMax, r.ZMax, floor, top, t,
                (9.5f, 4f, 1.0f, 2.3f), (14.5f, 4f, 1.0f, 2.3f));
            RoomWall("Control_Wall_E", r.XMax - 0.2f, r.ZMin, r.XMax - 0.2f, r.ZMax, floor, top, t,
                (26.5f, 4f, 1.0f, 2.3f));

            Roof("Roof_ControlRoom", r.XMin, r.ZMin, r.XMax, r.ZMax, top);

            // Pupitre de comanda (cover de crouch) cu fata spre ferestrele de nord si de vest
            Crate(gCover, 10f, 27f, 5f, 1.1f, 1.2f, floor);
            Crate(gCover, 9.5f, 20.6f, 4f, 1.1f, 1.2f, floor);

            Lamp("Light_ControlRoom_N", 11f, top - 0.6f, 26f, ColdLight, 10f, 3f);
            Lamp("Light_ControlRoom_S", 10f, top - 0.6f, 18f, ColdLight, 10f, 3f);
        }

        /// <summary>
        /// Perete drept intre (x0,z0) si (x1,z1), centrat pe linie cu grosimea t, de la baseY la topY,
        /// cu goluri (pozitie pe latura, latime, pervaz, inaltimea de sus a golului fata de podea).
        /// </summary>
        private static void RoomWall(string name, float x0, float z0, float x1, float z1, float baseY, float topY, float t,
            params (float at, float width, float sill, float head)[] openings)
        {
            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            float start = alongX ? Mathf.Min(x0, x1) : Mathf.Min(z0, z1);
            float end = alongX ? Mathf.Max(x0, x1) : Mathf.Max(z0, z1);
            float line = alongX ? z0 : x0;

            void Piece(string piece, float a, float b, float y0, float y1)
            {
                if (b - a < 0.05f || y1 - y0 < 0.05f)
                {
                    return;
                }

                if (alongX)
                {
                    SolidBlock(gRooftops, $"{name}_{piece}", a, line - t * 0.5f, b, line + t * 0.5f, y0, y1 - y0, wallMat);
                }
                else
                {
                    SolidBlock(gRooftops, $"{name}_{piece}", line - t * 0.5f, a, line + t * 0.5f, b, y0, y1 - y0, wallMat);
                }
            }

            System.Array.Sort(openings, (p, q) => p.at.CompareTo(q.at));
            float cursor = start;
            int index = 0;

            foreach ((float at, float width, float sill, float head) in openings)
            {
                float a = at - width * 0.5f, b = at + width * 0.5f;
                Piece($"{index}_Solid", cursor, a, baseY, topY);
                Piece($"{index}_Below", a, b, baseY, baseY + sill);
                Piece($"{index}_Above", a, b, baseY + head, topY);

                Vector3 mid = alongX ? new Vector3(at, 0f, line) : new Vector3(line, 0f, at);

                if (sill <= 0.01f)
                {
                    DoorFrame(gRooftops, $"{name}_{index}", mid, alongX, width, head, t, baseY);
                }
                else
                {
                    WindowFrame(gRooftops, $"{name}_{index}", mid, alongX, width, head - sill, t, baseY + sill);
                }

                cursor = b;
                index++;
            }

            Piece($"{index}_Solid", cursor, end, baseY, topY);
        }
    }
}
