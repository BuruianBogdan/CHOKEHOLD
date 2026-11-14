using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Layout-ul hartii: zonele pe care se poate merge, podelele, tunelul subteran si peretii.
    public static partial class BlacksiteK17MapBuilder
    {
        // ------------------------------------------------------------------
        // ZONE WALKABLE (xMin, zMin, xMax, zMax), in metri, pe grila de 2 m.
        // Doua zone care se ating sau se suprapun sunt conectate (nu apare perete intre ele);
        // ca sa ramana un perete, lasa cel putin 2 m intre ele.
        // ------------------------------------------------------------------
        private static readonly Area[] WalkableAreas =
        {
            // Atacatori (Team 1) - sud-vest
            new Area("T1 Spawn",            -68f, -54f, -44f, -28f),
            new Area("T1 Ramp",             -60f, -30f, -34f, -16f),
            new Area("T1 Walkway South",    -36f, -22f, -26f,  -8f),
            new Area("T1 Walkway North",    -28f, -12f, -18f,   0f),

            // Camera de securitate: a treia iesire din spawn-ul T1, spre curte
            new Area("Security Room",       -42f, -46f, -30f, -32f),
            new Area("Security North Door", -38f, -32f, -36f, -30f),
            new Area("Security East Door",  -30f, -40f, -28f, -38f),
            new Area("Security West Door",  -44f, -42f, -42f, -40f),

            // Vest -> A
            new Area("West Outpost",        -78f, -22f, -54f,   6f),
            new Area("West Rocks",          -56f,  -4f, -42f,  14f),
            new Area("A Control",           -46f,  10f, -34f,  24f),

            // A Pipes: sala pompelor, ruta proprie din West Outpost in latura de vest a site-ului
            new Area("A Pipes",             -76f,  10f, -62f,  34f),
            new Area("A Pipes South Door",  -70f,   6f, -68f,  10f),
            new Area("A Pipes Site Door",   -62f,  28f, -58f,  30f),

            // Depozit intre A Control si Mid Connector, cu perete moale spre site
            new Area("Depot",               -32f,   4f, -24f,  20f),
            new Area("Depot East Door",     -24f,  10f, -22f,  12f),
            new Area("Depot West Door",     -34f,  14f, -32f,  16f),
            new Area("Depot Breach",        -30f,  20f, -28f,  22f),

            // Bombsite A - nord-vest (cladire inchisa, lupta de aproape)
            new Area("Bombsite A",          -58f,  22f, -24f,  56f),
            new Area("A Ramp",              -26f,  28f, -12f,  36f),
            new Area("A Heaven Door",       -26f,  48f, -22f,  56f),
            new Area("A Catwalks",          -22f,  40f, -10f,  56f),

            // Mid
            new Area("Mid Connector",       -22f,  -2f, -12f,  42f),
            new Area("Mid West Corridor",   -22f,  -6f,  -2f,   2f),
            new Area("Choke Point",          -4f, -16f,  10f,   4f),
            new Area("Lab South Door",        0f,   2f,   6f,  12f),
            new Area("Mid Lab",             -10f,  10f,  18f,  38f),
            new Area("Lab West Door",       -12f,  22f, -10f,  24f),
            new Area("Lab North Door",        0f,  36f,   6f,  44f),
            new Area("Lab Annex Door",       18f,  36f,  20f,  38f),
            new Area("Lab Annex",            20f,  22f,  42f,  38f),
            new Area("Annex South Passage",  26f,  16f,  30f,  22f),
            new Area("Annex East Door",      42f,  28f,  46f,  30f),
            new Area("Mid East Corridor",    20f,   6f,  48f,  16f),
            new Area("Mid East Bend",         8f,  -4f,  24f,   8f),

            // Nord - culoar lung, cu catwalk deasupra
            new Area("North Overlook",      -12f,  42f,  58f,  56f),

            // Aparatori (Team 2) - nord-est
            new Area("T2 Spawn",             56f,  36f,  78f,  58f),
            new Area("East Yard",            46f,   8f,  78f,  38f),
            new Area("Yard To Hangar",       58f,   0f,  66f,  10f),
            new Area("Hangar North Door",    38f,   0f,  44f,   8f),

            // Sud
            new Area("South Courtyard",     -28f, -40f,  16f, -14f),
            new Area("South Lane",          -22f, -46f,  24f, -38f),

            // Gurile tunelului (santuri deschise cu rampa) si scara din South Lane
            new Area("Tunnel West Mouth",   -54f, -58f, -40f, -52f),
            new Area("Tunnel East Mouth",    10f, -58f,  24f, -52f),
            new Area("Tunnel Stairs",        -8f, -52f,  -4f, -46f),

            // Bombsite B - sud-est (hangar)
            new Area("Hangar / Bombsite B",  22f, -40f,  78f,   2f),
            new Area("Loading Bay",          22f, -56f,  60f, -38f),

            // Birou in coltul sud-estic, intre Loading Bay si hangar
            new Area("Loading Office",       62f, -54f,  76f, -42f),
            new Area("Office West Door",     60f, -50f,  62f, -48f),
            new Area("Office North Door",    64f, -42f,  66f, -40f),
        };

        // Zonele de plantare (folosite si pentru podeaua galbena).
        private static readonly Area SiteA = new Area("A", -48f, 34f, -30f, 52f);
        private static readonly Area SiteB = new Area("B", 36f, -28f, 54f, -8f);

        // ------------------------------------------------------------------
        // SOUTH TUNNEL - subteran real, la -3,5 m.
        //   TunnelPit     : tot santul (rampe + partea acoperita)
        //   TunnelCovered : partea de sub pamant; deasupra ei harta e plina
        //   StairTrench   : scara care coboara din South Lane in mijlocul tunelului
        // ------------------------------------------------------------------
        private const float TunnelFloorY = -3.5f;

        private static readonly Area TunnelPit = new Area("Tunnel Pit", -50f, -58f, 20f, -52f);
        private static readonly Area TunnelCovered = new Area("South Tunnel", -40f, -58f, 10f, -52f);
        private static readonly Area StairTrench = new Area("Tunnel Stair Trench", -8f, -52f, -4f, -44f);

        private static bool IsPit(float x, float z)
        {
            return Inside(TunnelPit, x, z) || Inside(StairTrench, x, z);
        }

        private static bool Inside(Area area, float x, float z)
        {
            return x > area.XMin && x < area.XMax && z > area.ZMin && z < area.ZMax;
        }

        private static bool IsWalkable(float x, float z)
        {
            foreach (Area area in WalkableAreas)
            {
                if (Inside(area, x, z))
                {
                    return true;
                }
            }

            // Capatul de sus al scarii din tunel iese 2 m in South Lane.
            return Inside(StairTrench, x, z);
        }

        private static int GridX => Mathf.RoundToInt((MaxX - MinX) / Cell);
        private static int GridZ => Mathf.RoundToInt((MaxZ - MinZ) / Cell);

        private static float CellCenterX(int i) => MinX + (i + 0.5f) * Cell;
        private static float CellCenterZ(int j) => MinZ + (j + 0.5f) * Cell;

        // ------------------------------------------------------------------
        // Podea: placi pe toata harta, mai putin deasupra santului tunelului
        // ------------------------------------------------------------------
        private static void BuildFloors()
        {
            int nx = GridX;
            int nz = GridZ;

            bool[,] solid = new bool[nx, nz];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    solid[i, j] = !IsPit(CellCenterX(i), CellCenterZ(j));
                }
            }

            int index = 0;

            MergeCells(solid, (i, j, width, depth) =>
            {
                float sizeX = width * Cell;
                float sizeZ = depth * Cell;

                CreateBox(
                    gFloors,
                    $"Floor_{index++:00}",
                    new Vector3(MinX + i * Cell + sizeX * 0.5f, -0.25f, MinZ + j * Cell + sizeZ * 0.5f),
                    new Vector3(sizeX, 0.5f, sizeZ),
                    floorMat
                );
            });

            // Suprafete colorate ca sa recunosti zonele importante din prima.
            CreateFloorDecal(gFloors, "Floor_T1Spawn", -68f, -52f, -44f, -28f, t1FloorMat);
            CreateFloorDecal(gFloors, "Floor_T2Spawn", 56f, 36f, 78f, 58f, t2FloorMat);
            CreateFloorDecal(gFloors, "Floor_SiteA", SiteA.XMin, SiteA.ZMin, SiteA.XMax, SiteA.ZMax, siteFloorMat);
            CreateFloorDecal(gFloors, "Floor_SiteB", SiteB.XMin, SiteB.ZMin, SiteB.XMax, SiteB.ZMax, siteFloorMat);
        }

        /// <summary>
        /// Uneste celulele marcate in dreptunghiuri cat mai mari si apeleaza emit(i, j, latime, adancime)
        /// pentru fiecare. Modifica tabloul primit.
        /// </summary>
        private static void MergeCells(bool[,] cells, System.Action<int, int, int, int> emit)
        {
            int nx = cells.GetLength(0);
            int nz = cells.GetLength(1);

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    if (!cells[i, j])
                    {
                        continue;
                    }

                    int width = 1;
                    while (i + width < nx && cells[i + width, j])
                    {
                        width++;
                    }

                    int depth = 1;
                    bool canGrow = true;
                    while (j + depth < nz && canGrow)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (!cells[i + k, j + depth])
                            {
                                canGrow = false;
                                break;
                            }
                        }

                        if (canGrow)
                        {
                            depth++;
                        }
                    }

                    for (int a = 0; a < width; a++)
                    {
                        for (int b = 0; b < depth; b++)
                        {
                            cells[i + a, j + b] = false;
                        }
                    }

                    emit(i, j, width, depth);
                }
            }
        }

        // ------------------------------------------------------------------
        // Tunelul subteran: podeaua santului si peretii lui (de la -3,5 m pana la nivelul solului)
        // ------------------------------------------------------------------
        private static void BuildUnderground()
        {
            const float slab = 0.5f;
            const float wallThickness = 0.5f;

            foreach (Area pit in new[] { TunnelPit, StairTrench })
            {
                SolidBlock(gUnderground, $"PitFloor_{pit.Name}",
                    pit.XMin, pit.ZMin, pit.XMax, pit.ZMax,
                    TunnelFloorY - slab, slab, floorMat);
            }

            int nx = GridX;
            int nz = GridZ;

            float wallHeight = -TunnelFloorY + slab;
            float wallCenterY = (TunnelFloorY - slab) * 0.5f;
            int index = 0;

            int[] di = { 1, -1, 0, 0 };
            int[] dj = { 0, 0, 1, -1 };

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float cx = CellCenterX(i);
                    float cz = CellCenterZ(j);

                    if (!IsPit(cx, cz))
                    {
                        continue;
                    }

                    for (int d = 0; d < 4; d++)
                    {
                        float nxCenter = cx + di[d] * Cell;
                        float nzCenter = cz + dj[d] * Cell;

                        if (IsPit(nxCenter, nzCenter))
                        {
                            continue;
                        }

                        // Perete pe muchia comuna, impins in afara santului.
                        Vector3 center = new Vector3(
                            cx + di[d] * (Cell * 0.5f + wallThickness * 0.5f),
                            wallCenterY,
                            cz + dj[d] * (Cell * 0.5f + wallThickness * 0.5f));

                        Vector3 size = di[d] != 0
                            ? new Vector3(wallThickness, wallHeight, Cell)
                            : new Vector3(Cell, wallHeight, wallThickness);

                        CreateBox(gUnderground, $"PitWall_{index++:000}", center, size, outerWallMat);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Pereti: tot ce nu e walkable devine bloc solid (greedy merge pe grila)
        // ------------------------------------------------------------------
        private static void BuildWalls()
        {
            int nx = GridX;
            int nz = GridZ;

            bool[,] solid = new bool[nx, nz];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    solid[i, j] = !IsWalkable(CellCenterX(i), CellCenterZ(j));
                }
            }

            int wallIndex = 0;

            MergeCells(solid, (i, j, width, depth) =>
            {
                bool touchesBorder =
                    i == 0 || j == 0 ||
                    i + width == nx || j + depth == nz;

                float height = touchesBorder ? OuterWallHeight : InnerWallHeight;
                float sizeX = width * Cell;
                float sizeZ = depth * Cell;

                CreateBox(
                    gWalls,
                    $"Wall_{wallIndex++:000}",
                    new Vector3(MinX + i * Cell + sizeX * 0.5f, height * 0.5f, MinZ + j * Cell + sizeZ * 0.5f),
                    new Vector3(sizeX, height, sizeZ),
                    touchesBorder ? outerWallMat : wallMat
                );
            });

            // Colturi taiate la 45 de grade - dau forma octogonala a spawn-urilor si a hangarului.
            Transform chamfers = CreateGroup(gWalls, "Chamfers");

            Chamfer(chamfers, "T1Spawn_SW", -68f, -54f, 1, 1, 6f);
            Chamfer(chamfers, "T1Spawn_NW", -68f, -28f, 1, -1, 6f);

            Chamfer(chamfers, "T2Spawn_NE", 78f, 58f, -1, -1, 7f);

            Chamfer(chamfers, "Outpost_SW", -78f, -22f, 1, 1, 6f);
            Chamfer(chamfers, "Outpost_NW", -78f, 6f, 1, -1, 6f);

            Chamfer(chamfers, "Hangar_NE", 78f, 2f, -1, -1, 9f);
            Chamfer(chamfers, "Hangar_SE", 78f, -40f, -1, 1, 9f);
            Chamfer(chamfers, "LoadingBay_SE", 60f, -56f, -1, 1, 6f);

            Chamfer(chamfers, "EastYard_SE", 78f, 8f, -1, 1, 6f);
            Chamfer(chamfers, "SiteA_NW", -58f, 56f, 1, -1, 5f);
            Chamfer(chamfers, "SiteA_SW", -58f, 22f, 1, 1, 5f);
        }

        // ------------------------------------------------------------------
        // Etichete de zone pentru Scene view (si pentru validator)
        // ------------------------------------------------------------------
        private static void BuildZoneLabels()
        {
            foreach (Area area in WalkableAreas)
            {
                CreateZoneLabel(area, 0f);
            }

            CreateZoneLabel(TunnelCovered, TunnelFloorY);
        }

        private static void CreateZoneLabel(Area area, float y)
        {
            GameObject go = new GameObject($"Zone_{area.Name}");
            go.transform.SetParent(gZones, false);
            go.transform.position = area.Center + Vector3.up * y;

            MapZone zone = go.AddComponent<MapZone>();
            SerializedObject serialized = new SerializedObject(zone);
            serialized.FindProperty("displayName").stringValue = area.Name;
            serialized.FindProperty("size").vector3Value = new Vector3(area.Size2D.x, 3f, area.Size2D.z);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
