using System.Collections.Generic;
using System.IO;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Genereaza harta greybox "Blacksite K-17" in scena deschisa, dupa schita tactica a hartii.
    ///
    /// Cum functioneaza:
    ///  - harta are 160 x 120 m (X = est, Z = nord, originea in centru);
    ///  - definesti doar ZONELE PE CARE SE POATE MERGE (WalkableAreas);
    ///  - tot restul suprafetei este umplut automat cu blocuri solide (cladiri/pereti),
    ///    pe o grila de 2 m, unite in cutii cat mai mari;
    ///  - peste ele se adauga platforme, rampe, catwalk-uri, acoperisuri, usi, cover si containere.
    ///
    /// Meniu:
    ///  - CHOKEHOLD > Map > Rebuild Scene From Scratch : sterge harta veche din scena si o construieste pe cea noua;
    ///  - CHOKEHOLD > Map > Build Blacksite K-17        : doar (re)genereaza harta;
    ///  - CHOKEHOLD > Map > Capture Preview Images      : salveaza imagini cu harta in Docs/.
    /// </summary>
    public static class BlacksiteK17MapBuilder
    {
        private const string RootName = "MAP_BlacksiteK17_Generated";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Greybox";

        private const float MinX = -80f;
        private const float MaxX = 80f;
        private const float MinZ = -60f;
        private const float MaxZ = 60f;
        private const float Cell = 2f;

        // Doua niveluri: parterul la 0 m, etajul la 4 m. Peretii trec de capul unui jucator aflat la etaj.
        private const float UpperFloorHeight = 4f;
        private const float SlabThickness = 0.4f;
        private const float GroundCeiling = UpperFloorHeight - SlabThickness;
        private const float InnerWallHeight = 8.5f;
        private const float OuterWallHeight = 11f;

        private const string UpperGroupName = "Level 2 (Upper Floor)";

        // ------------------------------------------------------------------
        // LAYOUT - zone pe care se poate merge (xMin, zMin, xMax, zMax), in metri.
        // Valori pare, aliniate la grila de 2 m.
        // Doua zone care se ating sau se suprapun sunt conectate (nu apare perete intre ele);
        // ca sa ramana un perete, lasa cel putin 2 m intre ele.
        // ------------------------------------------------------------------
        private struct Area
        {
            public string Name;
            public float XMin, ZMin, XMax, ZMax;

            public Area(string name, float xMin, float zMin, float xMax, float zMax)
            {
                Name = name;
                XMin = xMin;
                ZMin = zMin;
                XMax = xMax;
                ZMax = zMax;
            }

            public Vector3 Center => new Vector3((XMin + XMax) * 0.5f, 0f, (ZMin + ZMax) * 0.5f);
            public Vector3 Size2D => new Vector3(XMax - XMin, 0f, ZMax - ZMin);
        }

        private static readonly Area[] WalkableAreas =
        {
            // Atacatori (Team 1) - sud-vest
            new Area("T1 Spawn",            -68f, -54f, -44f, -28f),
            new Area("T1 Ramp",             -60f, -30f, -34f, -16f),
            new Area("T1 Walkway South",    -36f, -22f, -26f,  -8f),
            new Area("T1 Walkway North",    -28f, -12f, -18f,   0f),

            // Vest -> A
            new Area("West Outpost",        -78f, -22f, -54f,   6f),
            new Area("West Rocks",          -56f,  -4f, -42f,  14f),
            new Area("A Control",           -46f,  10f, -34f,  24f),

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
            new Area("Lab Annex Door",       18f,  32f,  20f,  34f),
            new Area("Lab Annex",            20f,  22f,  42f,  38f),
            new Area("Annex South Passage",  30f,  16f,  32f,  22f),
            new Area("Annex East Door",      42f,  28f,  46f,  30f),
            new Area("Mid East Corridor",    20f,   6f,  48f,  16f),
            new Area("Mid East Bend",         8f,  -4f,  24f,   8f),

            // Nord - culoar lung de sniper, cu catwalk deasupra
            new Area("North Overlook",      -12f,  42f,  58f,  56f),

            // Aparatori (Team 2) - nord-est
            new Area("T2 Spawn",             56f,  36f,  78f,  58f),
            new Area("East Yard",            46f,   8f,  78f,  38f),
            new Area("Yard To Hangar",       58f,   0f,  66f,  10f),
            new Area("Hangar North Door",    38f,   0f,  44f,   8f),

            // Sud
            new Area("South Courtyard",     -28f, -40f,  16f, -14f),
            new Area("South Lane",          -22f, -46f,  24f, -38f),
            new Area("Tunnel Hatch",          8f, -52f,  12f, -46f),
            new Area("South Tunnel",        -54f, -58f,  24f, -52f),

            // Bombsite B - sud-est (hangar deschis)
            new Area("Hangar / Bombsite B",  22f, -40f,  78f,   2f),
            new Area("Loading Bay",          22f, -56f,  60f, -38f),
        };

        // Zonele de plantare (folosite si pentru podeaua galbena).
        private static readonly Area SiteA = new Area("A", -48f, 34f, -30f, 52f);
        private static readonly Area SiteB = new Area("B", 36f, -28f, 54f, -8f);

        private static readonly Vector3 PlayerStart = new Vector3(-56f, 0.1f, -44f);

        // ------------------------------------------------------------------
        // Materiale
        // ------------------------------------------------------------------
        private static Material floorMat;
        private static Material wallMat;
        private static Material outerWallMat;
        private static Material platformMat;
        private static Material rampMat;
        private static Material coverMat;
        private static Material containerRedMat;
        private static Material containerBlueMat;
        private static Material pipeMat;
        private static Material roofMat;
        private static Material doorMat;
        private static Material upperFloorMat;
        private static Material softWallMat;
        private static Material hatchMat;
        private static Material reinforcedMat;
        private static Material barricadeMat;
        private static Material siteFloorMat;
        private static Material t1FloorMat;
        private static Material t2FloorMat;

        private static int groundLayer;
        private static int coverIndex;

        // ------------------------------------------------------------------
        // Meniu
        // ------------------------------------------------------------------
        [MenuItem("CHOKEHOLD/Map/Rebuild Scene From Scratch")]
        public static void RebuildFromScratch()
        {
            WipeOldMapObjects();
            Build();
        }

        [MenuItem("CHOKEHOLD/Map/Build Blacksite K-17")]
        public static void Build()
        {
            groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0)
            {
                groundLayer = 0;
            }

            coverIndex = 0;
            CreateMaterials();
            RemoveExistingRoot();

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Blacksite K-17");

            Transform floors = CreateGroup(root.transform, "Floors");
            Transform walls = CreateGroup(root.transform, "Walls");
            Transform vertical = CreateGroup(root.transform, "Stairs And Low Platforms");
            Transform upper = CreateGroup(root.transform, UpperGroupName);
            Transform roofs = CreateGroup(root.transform, "Roofs");
            Transform doors = CreateGroup(root.transform, "Doors");
            Transform cover = CreateGroup(root.transform, "Cover");
            Transform gameplay = CreateGroup(root.transform, "Gameplay");
            Transform zones = CreateGroup(root.transform, "Zones (editor labels)");
            Transform lights = CreateGroup(root.transform, "Lights");

            BuildFloors(floors);
            BuildWalls(walls);
            BuildStairs(vertical);
            BuildUpperLevel(upper);
            BuildRoofs(roofs);
            BuildDoors(doors);
            BuildCover(cover);
            BuildGameplay(gameplay);
            BuildZoneLabels(zones);
            BuildLights(lights);

            MovePlayerToSpawn();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log(
                "Blacksite K-17 generat: 160 x 120 m. " +
                "Modifica layout-ul in BlacksiteK17MapBuilder.WalkableAreas si regenereaza."
            );
        }

        [MenuItem("CHOKEHOLD/Map/Clear Generated Map")]
        public static void Clear()
        {
            RemoveExistingRoot();
        }

        private static void RemoveExistingRoot()
        {
            GameObject existing = GameObject.Find(RootName);

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }
        }

        /// <summary>
        /// Sterge din scena tot ce tine de harta veche (podele, pereti, tinte de test etc.).
        /// Pastreaza jucatorul, camera, UI-ul, EventSystem-ul si lumina directionala.
        /// </summary>
        private static void WipeOldMapObjects()
        {
            Scene scene = SceneManager.GetActiveScene();
            int removed = 0;

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (IsProtected(rootObject))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(rootObject);
                removed++;
            }

            Debug.Log($"Harta veche stearsa: {removed} obiecte radacina eliminate din scena '{scene.name}'.");
        }

        private static bool IsProtected(GameObject rootObject)
        {
            if (rootObject.GetComponentInChildren<FpsController>(true) != null ||
                rootObject.GetComponentInChildren<Camera>(true) != null ||
                rootObject.GetComponentInChildren<Canvas>(true) != null ||
                rootObject.GetComponentInChildren<EventSystem>(true) != null)
            {
                return true;
            }

            Light light = rootObject.GetComponent<Light>();
            return light != null && light.type == LightType.Directional;
        }

        // ------------------------------------------------------------------
        // Podea
        // ------------------------------------------------------------------
        private static void BuildFloors(Transform parent)
        {
            CreateBox(
                parent,
                "Floor_Base",
                new Vector3((MinX + MaxX) * 0.5f, -0.25f, (MinZ + MaxZ) * 0.5f),
                new Vector3(MaxX - MinX, 0.5f, MaxZ - MinZ),
                floorMat
            );

            // Suprafete colorate ca sa recunosti zonele importante din prima.
            CreateFloorDecal(parent, "Floor_T1Spawn", -68f, -54f, -44f, -28f, t1FloorMat);
            CreateFloorDecal(parent, "Floor_T2Spawn", 56f, 36f, 78f, 58f, t2FloorMat);
            CreateFloorDecal(parent, "Floor_SiteA", SiteA.XMin, SiteA.ZMin, SiteA.XMax, SiteA.ZMax, siteFloorMat);
            CreateFloorDecal(parent, "Floor_SiteB", SiteB.XMin, SiteB.ZMin, SiteB.XMax, SiteB.ZMax, siteFloorMat);
        }

        private static void CreateFloorDecal(
            Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax,
            Material material)
        {
            const float thickness = 0.04f;

            CreateBox(
                parent,
                name,
                new Vector3((xMin + xMax) * 0.5f, thickness * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, thickness, zMax - zMin),
                material
            );
        }

        // ------------------------------------------------------------------
        // Pereti: tot ce nu e walkable devine bloc solid (greedy merge pe grila)
        // ------------------------------------------------------------------
        private static void BuildWalls(Transform parent)
        {
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell);
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell);

            bool[,] walkable = new bool[nx, nz];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float cx = MinX + (i + 0.5f) * Cell;
                    float cz = MinZ + (j + 0.5f) * Cell;

                    foreach (Area area in WalkableAreas)
                    {
                        if (cx > area.XMin && cx < area.XMax &&
                            cz > area.ZMin && cz < area.ZMax)
                        {
                            walkable[i, j] = true;
                            break;
                        }
                    }
                }
            }

            bool[,] used = new bool[nx, nz];
            int wallIndex = 0;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    if (walkable[i, j] || used[i, j])
                    {
                        continue;
                    }

                    int width = 1;
                    while (i + width < nx &&
                           !walkable[i + width, j] &&
                           !used[i + width, j])
                    {
                        width++;
                    }

                    int depth = 1;
                    bool canGrow = true;
                    while (j + depth < nz && canGrow)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (walkable[i + k, j + depth] || used[i + k, j + depth])
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
                            used[i + a, j + b] = true;
                        }
                    }

                    bool touchesBorder =
                        i == 0 || j == 0 ||
                        i + width == nx || j + depth == nz;

                    float height = touchesBorder ? OuterWallHeight : InnerWallHeight;

                    float x0 = MinX + i * Cell;
                    float z0 = MinZ + j * Cell;
                    float sizeX = width * Cell;
                    float sizeZ = depth * Cell;

                    CreateBox(
                        parent,
                        $"Wall_{wallIndex:000}",
                        new Vector3(x0 + sizeX * 0.5f, height * 0.5f, z0 + sizeZ * 0.5f),
                        new Vector3(sizeX, height, sizeZ),
                        touchesBorder ? outerWallMat : wallMat
                    );

                    wallIndex++;
                }
            }

            // Colturi taiate la 45 de grade - dau forma octogonala a spawn-urilor si a hangarului.
            Transform chamfers = CreateGroup(parent, "Chamfers");

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

        /// <summary>
        /// Perete diagonal care taie un colt interior. (cornerX, cornerZ) este coltul,
        /// (dirX, dirZ) arata spre interiorul zonei, size este lungimea catetei.
        /// </summary>
        private static void Chamfer(Transform parent, string name,
            float cornerX, float cornerZ, int dirX, int dirZ, float size)
        {
            const float thickness = 1f;

            Vector3 a = new Vector3(cornerX + dirX * size, 0f, cornerZ);
            Vector3 b = new Vector3(cornerX, 0f, cornerZ + dirZ * size);
            Vector3 mid = (a + b) * 0.5f;

            GameObject wall = CreateBox(
                parent,
                $"Chamfer_{name}",
                new Vector3(mid.x, InnerWallHeight * 0.5f, mid.z),
                new Vector3(thickness, InnerWallHeight, Vector3.Distance(a, b) + thickness),
                wallMat
            );

            wall.transform.rotation = Quaternion.LookRotation(b - a);
        }

        // ------------------------------------------------------------------
        // Scari (rampe) intre parter si etaj + platforme joase
        // ------------------------------------------------------------------
        private static void BuildStairs(Transform parent)
        {
            float up = UpperFloorHeight;

            // Nord / A
            CreateRamp(parent, "Stairs_T2_To_Catwalk", 46f, 52f, 54f, 56f, Vector3.left, up);
            CreateRamp(parent, "Stairs_A_Heaven", -34f, 38f, -30f, 46f, Vector3.forward, up);
            CreateRamp(parent, "Stairs_A_SouthGallery", -52f, 22f, -44f, 26f, Vector3.right, up);

            // Mid Lab + anexa
            CreateRamp(parent, "Stairs_Lab_SouthWest", -10f, 12f, -6f, 20f, Vector3.forward, up);
            CreateRamp(parent, "Stairs_Lab_NorthEast", 14f, 28f, 18f, 36f, Vector3.forward, up);
            CreateRamp(parent, "Stairs_Annex", 32f, 22f, 40f, 26f, Vector3.right, up);
            CreateRamp(parent, "Stairs_EastYard", 46f, 18f, 50f, 26f, Vector3.forward, up);

            // Hangar
            CreateRamp(parent, "Stairs_Hangar_West", 22f, -42f, 26f, -34f, Vector3.forward, up);
            CreateRamp(parent, "Stairs_Hangar_North", 50f, -2f, 58f, 2f, Vector3.left, up);

            // A Platform - coltul nord-vestic al site-ului, usor ridicat
            CreatePlatform(parent, "A_Platform", -56f, 44f, -44f, 54f, 1.2f);
            CreateRamp(parent, "A_Platform_Ramp", -50f, 38f, -46f, 44f, Vector3.forward, 1.2f);

            // North Overlook - balcon mic (2 m) pe latura sudica a culoarului de nord
            CreatePlatform(parent, "North_Overlook_Balcony", 14f, 42f, 24f, 46f, 2f);
            CreateRamp(parent, "North_Overlook_Stairs", 24f, 42f, 30f, 46f, Vector3.left, 2f);
        }

        // ------------------------------------------------------------------
        // ETAJUL (4 m): planseele cladirilor, pasarelele dintre ele, balcoane, trape
        //
        // Reteaua de sus:  rampa T2 -> North Catwalk -> A Heaven -> galeriile A
        //                  North Catwalk -> pod -> Mid Lab etaj -> anexa etaj -> balcon East Yard
        //                  galeria A -> pod peste A Ramp / Mid Connector -> Mid Lab etaj
        //                  Hangar: pasarela vest + mezanin nord + balcon spre Mid East
        // ------------------------------------------------------------------
        private static void BuildUpperLevel(Transform parent)
        {
            float up = UpperFloorHeight;

            // --- North Catwalk: linie lunga de sniper deasupra North Overlook
            CreatePlatform(parent, "North_Catwalk", -24f, 52f, 46f, 56f, up);
            Railing(parent, "North_Catwalk_Rail_W", -22f, 52f, 1f, 52f, up);
            Railing(parent, "North_Catwalk_Rail_E", 5f, 52f, 46f, 52f, up);

            for (float x = -16f; x <= 40f; x += 14f)
            {
                CreateBox(parent, $"North_Catwalk_Pillar_{x:0}", new Vector3(x, GroundCeiling * 0.5f, 52.3f),
                    new Vector3(0.5f, GroundCeiling, 0.5f), pipeMat);
            }

            CreatePlatform(parent, "Bridge_Catwalk_To_Lab", 1f, 44f, 5f, 52f, up);
            Railing(parent, "Bridge_Catwalk_To_Lab_Rail_W", 1f, 44f, 1f, 52f, up);
            Railing(parent, "Bridge_Catwalk_To_Lab_Rail_E", 5f, 44f, 5f, 52f, up);

            // --- Bombsite A: galerie in forma de L deasupra site-ului
            CreatePlatform(parent, "A_Heaven", -36f, 46f, -24f, 56f, up);
            CreatePlatform(parent, "A_Gallery_East", -30f, 26f, -24f, 46f, up);
            CreatePlatform(parent, "A_Gallery_South", -44f, 22f, -30f, 26f, up);
            Railing(parent, "A_Heaven_Rail_W", -36f, 46f, -36f, 56f, up);
            Railing(parent, "A_Heaven_Rail_S", -36f, 46f, -34f, 46f, up);
            Railing(parent, "A_Gallery_East_Rail", -30f, 26f, -30f, 46f, up);
            Railing(parent, "A_Gallery_South_Rail", -44f, 26f, -30f, 26f, up);

            // Pod de la galeria A, peste A Ramp si Mid Connector, pana la usa de vest a laboratorului
            CreatePlatform(parent, "Bridge_A_To_Lab_1", -24f, 30f, -12f, 34f, up);
            CreatePlatform(parent, "Bridge_A_To_Lab_2", -16f, 22f, -12f, 30f, up);
            Railing(parent, "Bridge_A_To_Lab_Rail_N", -24f, 34f, -12f, 34f, up);
            Railing(parent, "Bridge_A_To_Lab_Rail_S", -24f, 30f, -16f, 30f, up);
            Railing(parent, "Bridge_A_To_Lab_Rail_W", -16f, 22f, -16f, 30f, up);
            Railing(parent, "Bridge_A_To_Lab_Rail_End", -16f, 22f, -12f, 22f, up);

            // --- Mid Lab: planseu cu goluri pentru scari si trape, balcoane peste usi
            Slab(parent, "Lab_Floor2", up,
                new[]
                {
                    new Area("Lab", -10f, 10f, 18f, 38f),
                    new Area("South Balcony", 0f, 4f, 6f, 10f),      // deasupra Choke Point
                    new Area("North Balcony", 0f, 38f, 6f, 44f),     // spre North Catwalk
                    new Area("West Door", -12f, 22f, -10f, 24f),     // spre podul din A
                    new Area("Annex Door", 18f, 32f, 20f, 34f),
                },
                new[]
                {
                    new Area("Stairs SW", -10f, 12f, -6f, 20f),
                    new Area("Stairs NE", 14f, 28f, 18f, 36f),
                    new Area("Hatch SE", 10f, 14f, 12f, 16f),
                    new Area("Hatch NW", -4f, 30f, -2f, 32f),
                });

            Hatch(parent, "Hatch_Lab_SE", 10f, 14f, 12f, 16f, up);
            Hatch(parent, "Hatch_Lab_NW", -4f, 30f, -2f, 32f, up);
            Parapet(parent, "Lab_SouthBalcony_Parapet", 0f, 4f, 6f, 4f, up);
            Railing(parent, "Lab_Stairs_SW_Rail", -6f, 12f, -6f, 20f, up);
            Railing(parent, "Lab_Stairs_NE_Rail", 14f, 28f, 14f, 36f, up);

            SoftWall(parent, "Lab2_SoftWall", 4f, 12f, 4f, 26f, up, 3f);
            Crate(parent, 12f, 24f, 2f, 1.2f, 2f, up);
            Crate(parent, -6f, 26f, 1.4f, 1.4f, 1.4f, up);
            Crate(parent, 8f, 34f, 3f, 1f, 1.2f, up);

            // --- Lab Annex: etaj, balcon cu scari in East Yard
            Slab(parent, "Annex_Floor2", up,
                new[]
                {
                    new Area("Annex", 20f, 22f, 42f, 38f),
                    new Area("East Door", 42f, 28f, 46f, 30f),
                },
                new[]
                {
                    new Area("Stairs", 32f, 22f, 40f, 26f),
                    new Area("Hatch", 24f, 34f, 26f, 36f),
                });

            Hatch(parent, "Hatch_Annex", 24f, 34f, 26f, 36f, up);
            Railing(parent, "Annex_Stairs_Rail", 32f, 26f, 40f, 26f, up);
            Crate(parent, 30f, 30f, 2f, 1.2f, 2f, up);

            CreatePlatform(parent, "EastYard_Balcony", 46f, 26f, 50f, 32f, up);
            Railing(parent, "EastYard_Balcony_Rail_E", 50f, 26f, 50f, 32f, up);
            Railing(parent, "EastYard_Balcony_Rail_N", 46f, 32f, 50f, 32f, up);

            // --- Hangar: pasarela pe vest, mezanin pe nord, balcon spre Mid East Corridor
            CreatePlatform(parent, "Hangar_Catwalk_West", 22f, -34f, 26f, 2f, up);
            CreatePlatform(parent, "Hangar_Mezzanine_North", 26f, -2f, 50f, 2f, up);
            CreatePlatform(parent, "Hangar_North_Balcony", 38f, 2f, 44f, 8f, up);
            Railing(parent, "Hangar_Catwalk_West_Rail", 26f, -34f, 26f, -2f, up);
            Railing(parent, "Hangar_Mezzanine_Rail", 26f, -2f, 50f, -2f, up);
            Parapet(parent, "Hangar_North_Balcony_Parapet", 38f, 8f, 44f, 8f, up);
        }

        /// <summary>
        /// Planseu la inaltimea top: acopera dreptunghiurile "solids", mai putin golurile "holes"
        /// (scari, trape). Toate coordonatele trebuie aliniate la grila de 2 m.
        /// </summary>
        private static void Slab(Transform parent, string name, float top, Area[] solids, Area[] holes)
        {
            float xMin = float.MaxValue, zMin = float.MaxValue;
            float xMax = float.MinValue, zMax = float.MinValue;

            foreach (Area solid in solids)
            {
                xMin = Mathf.Min(xMin, solid.XMin);
                zMin = Mathf.Min(zMin, solid.ZMin);
                xMax = Mathf.Max(xMax, solid.XMax);
                zMax = Mathf.Max(zMax, solid.ZMax);
            }

            int nx = Mathf.RoundToInt((xMax - xMin) / Cell);
            int nz = Mathf.RoundToInt((zMax - zMin) / Cell);

            bool[,] filled = new bool[nx, nz];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float cx = xMin + (i + 0.5f) * Cell;
                    float cz = zMin + (j + 0.5f) * Cell;

                    filled[i, j] = Contains(solids, cx, cz) && !Contains(holes, cx, cz);
                }
            }

            int index = 0;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    if (!filled[i, j])
                    {
                        continue;
                    }

                    int width = 1;
                    while (i + width < nx && filled[i + width, j])
                    {
                        width++;
                    }

                    int depth = 1;
                    bool canGrow = true;
                    while (j + depth < nz && canGrow)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (!filled[i + k, j + depth])
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
                            filled[i + a, j + b] = false;
                        }
                    }

                    float sizeX = width * Cell;
                    float sizeZ = depth * Cell;

                    CreateBox(
                        parent,
                        $"{name}_{index++:00}",
                        new Vector3(
                            xMin + i * Cell + sizeX * 0.5f,
                            top - SlabThickness * 0.5f,
                            zMin + j * Cell + sizeZ * 0.5f),
                        new Vector3(sizeX, SlabThickness, sizeZ),
                        upperFloorMat
                    );
                }
            }
        }

        private static bool Contains(Area[] areas, float x, float z)
        {
            foreach (Area area in areas)
            {
                if (x > area.XMin && x < area.XMax && z > area.ZMin && z < area.ZMax)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Trapa in planseu: un panou care se sparge cu gloante (Health). Dupa ce e spart,
        /// poti cobori prin gol sau trage prin el de sus / de jos.
        /// </summary>
        private static void Hatch(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax, float top)
        {
            GameObject hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hatch.name = name;
            hatch.transform.SetParent(parent, false);
            hatch.transform.position = new Vector3((xMin + xMax) * 0.5f, top - 0.15f, (zMin + zMax) * 0.5f);
            hatch.transform.localScale = new Vector3(xMax - xMin, 0.3f, zMax - zMin);
            hatch.layer = groundLayer;
            hatch.GetComponent<MeshRenderer>().sharedMaterial = hatchMat;

            AddDestructible(hatch, 75f, true);
        }

        /// <summary>
        /// Perete "moale": segmente de 2 m care se sparg separat cu gloante, ca sa poti
        /// deschide o linie de tragere sau o trecere noua. De la (x0,z0) la (x1,z1), paralel cu o axa.
        /// </summary>
        private static void SoftWall(Transform parent, string name,
            float x0, float z0, float x1, float z1, float baseY, float height)
        {
            const float thickness = 0.25f;
            const float segment = 2f;

            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            float length = alongX ? Mathf.Abs(x1 - x0) : Mathf.Abs(z1 - z0);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / segment));
            float step = length / count;

            float startX = Mathf.Min(x0, x1);
            float startZ = Mathf.Min(z0, z1);

            for (int i = 0; i < count; i++)
            {
                float offset = (i + 0.5f) * step;

                Vector3 center = alongX
                    ? new Vector3(startX + offset, baseY + height * 0.5f, z0)
                    : new Vector3(x0, baseY + height * 0.5f, startZ + offset);

                Vector3 size = alongX
                    ? new Vector3(step - 0.04f, height, thickness)
                    : new Vector3(thickness, height, step - 0.04f);

                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = $"{name}_{i:00}";
                panel.transform.SetParent(parent, false);
                panel.transform.position = center;
                panel.transform.localScale = size;
                panel.layer = groundLayer;
                panel.GetComponent<MeshRenderer>().sharedMaterial = softWallMat;

                AddDestructible(panel, 100f, true);
            }
        }

        /// <summary>
        /// Face obiectul destructibil: se sparge cand ramane fara viata si se reface la runda urmatoare.
        /// Daca poate fi intarit, aparatorii il pot face indestructibil pentru runda curenta.
        /// </summary>
        private static void AddDestructible(GameObject go, float maximumHealth, bool canBeReinforced)
        {
            Health health = go.AddComponent<Health>();
            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maximumHealth").floatValue = maximumHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();

            Destructible destructible = go.AddComponent<Destructible>();
            SerializedObject serialized = new SerializedObject(destructible);
            serialized.FindProperty("canBeReinforced").boolValue = canBeReinforced;
            serialized.FindProperty("reinforcedMaterial").objectReferenceValue = reinforcedMat;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Punct de baricada pe o deschidere: (x, z) este mijlocul deschiderii la sol,
        /// width latimea ei. alongX = true daca panoul se intinde pe axa X (blocheaza mersul pe Z).
        /// </summary>
        private static void Barricade(Transform parent, string name,
            float x, float z, float baseY, float width, float height, bool alongX)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.position = new Vector3(x, baseY, z);
            point.transform.rotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            // Trigger-ul acopera deschiderea, ca sa poata fi tintita si cand baricada nu e montata.
            BoxCollider trigger = point.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, height * 0.5f, 0f);
            trigger.size = new Vector3(width, height, 0.6f);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(point.transform, false);
            panel.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            panel.transform.localScale = new Vector3(width, height, 0.15f);
            panel.layer = groundLayer;
            panel.GetComponent<MeshRenderer>().sharedMaterial = barricadeMat;

            AddDestructible(panel, 60f, false);

            BarricadePoint barricade = point.AddComponent<BarricadePoint>();
            SerializedObject serialized = new SerializedObject(barricade);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Parapet plin (1.1 m) pe o muchie de balcon - cover la etaj.</summary>
        private static void Parapet(Transform parent, string name,
            float x0, float z0, float x1, float z1, float floorHeight)
        {
            const float height = 1.1f;
            const float thickness = 0.3f;

            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);

            Vector3 size = alongX
                ? new Vector3(Mathf.Abs(x1 - x0), height, thickness)
                : new Vector3(thickness, height, Mathf.Abs(z1 - z0));

            CreateBox(
                parent,
                name,
                new Vector3((x0 + x1) * 0.5f, floorHeight + height * 0.5f, (z0 + z1) * 0.5f),
                size,
                wallMat
            );
        }

        /// <summary>Balustrada joasa (1 m) pe o muchie de platforma, de la (x0,z0) la (x1,z1).</summary>
        private static void Railing(Transform parent, string name,
            float x0, float z0, float x1, float z1, float platformHeight)
        {
            const float height = 1f;
            const float thickness = 0.12f;

            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);

            Vector3 size = alongX
                ? new Vector3(Mathf.Abs(x1 - x0), height, thickness)
                : new Vector3(thickness, height, Mathf.Abs(z1 - z0));

            CreateBox(
                parent,
                name,
                new Vector3((x0 + x1) * 0.5f, platformHeight + height * 0.5f, (z0 + z1) * 0.5f),
                size,
                pipeMat
            );
        }

        // ------------------------------------------------------------------
        // Acoperisuri si pereti interiori
        // ------------------------------------------------------------------
        private static void BuildRoofs(Transform parent)
        {
            CreateRoof(parent, "Roof_SouthTunnel", -42f, -58f, 20f, -52f, 4f);
            CreateRoof(parent, "Roof_MidLab", -10f, 10f, 18f, 38f, 2f * UpperFloorHeight);
            CreateRoof(parent, "Roof_LabAnnex", 20f, 22f, 42f, 38f, 2f * UpperFloorHeight);
        }

        private static void CreateRoof(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax, float height)
        {
            const float thickness = 0.4f;

            CreateBox(
                parent,
                name,
                new Vector3((xMin + xMax) * 0.5f, height + thickness * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, thickness, zMax - zMin),
                roofMat
            );
        }

        /// <summary>Perete interior subtire, de la (x0,z0) la (x1,z1), paralel cu o axa.</summary>
        private static void InnerWall(Transform parent, string name,
            float x0, float z0, float x1, float z1, float height)
        {
            const float thickness = 0.4f;

            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);

            Vector3 size = alongX
                ? new Vector3(Mathf.Abs(x1 - x0), height, thickness)
                : new Vector3(thickness, height, Mathf.Abs(z1 - z0));

            CreateBox(
                parent,
                name,
                new Vector3((x0 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f),
                size,
                wallMat
            );
        }

        // ------------------------------------------------------------------
        // Usi (SimpleDoor - se deschid cu F)
        // ------------------------------------------------------------------
        private static void BuildDoors(Transform parent)
        {
            // Golurile de usa au 2 m latime si trec printr-un perete de 2 m grosime.
            Door(parent, "Door_LabWest", new Vector3(-11f, 0f, 22f), Vector3.forward, 2f);
            Door(parent, "Door_LabAnnex", new Vector3(19f, 0f, 32f), Vector3.forward, 2f);
            Door(parent, "Door_AnnexEast", new Vector3(44f, 0f, 28f), Vector3.forward, 2f);

            // Buiandrugi: inchid golul deasupra usilor.
            // Unde exista etaj deasupra, buiandrugul se opreste sub planseu (golul de la etaj ramane liber).
            Lintel(parent, "Lintel_LabWest", -12f, 22f, -10f, 24f, GroundCeiling);
            Lintel(parent, "Lintel_LabAnnex", 18f, 32f, 20f, 34f, GroundCeiling);
            Lintel(parent, "Lintel_AnnexEast", 42f, 28f, 46f, 30f, GroundCeiling);
            Lintel(parent, "Lintel_AnnexSouth", 30f, 16f, 32f, 22f, InnerWallHeight);

            // Puncte de baricada: deschideri pe care aparatorii le pot inchide in faza de pregatire.
            Transform barricades = CreateGroup(parent, "Barricade Points");
            float up = UpperFloorHeight;

            // Parter
            Barricade(barricades, "Barricade_LabSouthDoor", 3f, 7f, 0f, 6f, GroundCeiling, true);
            Barricade(barricades, "Barricade_LabNorthDoor", 3f, 40f, 0f, 6f, GroundCeiling, true);
            Barricade(barricades, "Barricade_AnnexSouth", 31f, 19f, 0f, 2f, 2.5f, true);
            Barricade(barricades, "Barricade_HangarNorthDoor", 41f, 4f, 0f, 6f, GroundCeiling, true);
            Barricade(barricades, "Barricade_HangarWestDoor", 23f, -1f, 0f, 6f, GroundCeiling, false);
            Barricade(barricades, "Barricade_TunnelHatch", 10f, -49f, 0f, 4f, 3f, true);

            // Etaj
            Barricade(barricades, "Barricade_Lab2_WestDoor", -11f, 23f, up, 2f, 3f, false);
            Barricade(barricades, "Barricade_Lab2_AnnexDoor", 19f, 33f, up, 2f, 3f, false);
            Barricade(barricades, "Barricade_Lab2_NorthBalcony", 3f, 40f, up, 6f, 3f, true);
        }

        /// <summary>
        /// Usa cu balama in hingePosition; panoul se intinde pe directia "along" pe lungimea width.
        /// </summary>
        private static void Door(Transform parent, string name,
            Vector3 hingePosition, Vector3 along, float width)
        {
            const float height = 2.4f;
            const float thickness = 0.15f;

            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = hingePosition;
            pivot.transform.rotation = Quaternion.LookRotation(along);
            pivot.AddComponent<SimpleDoor>();

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(pivot.transform, false);
            panel.transform.localPosition = new Vector3(0f, height * 0.5f, width * 0.5f);
            panel.transform.localScale = new Vector3(thickness, height, width);
            panel.layer = groundLayer;
            panel.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
        }

        private static void Lintel(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax, float topY)
        {
            const float doorHeight = 2.5f;
            float height = topY - doorHeight;

            CreateBox(
                parent,
                name,
                new Vector3((xMin + xMax) * 0.5f, doorHeight + height * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, height, zMax - zMin),
                wallMat
            );
        }

        // ------------------------------------------------------------------
        // Cover, containere, tevi, rezervoare, pereti interiori
        // ------------------------------------------------------------------
        private static void BuildCover(Transform parent)
        {
            // T1 Ramp (iesirea din spawn)
            Crate(parent, -48f, -20f, 2f, 1.2f, 2f);
            HalfWall(parent, -40f, -26f, 3f, 90f);
            Crate(parent, -31f, -16f, 1.4f, 1.4f, 1.4f);

            // West Outpost
            Crate(parent, -70f, -10f, 2f, 1.2f, 2f);
            Crate(parent, -62f, 0f, 1.2f, 1.2f, 1.2f);
            Crate(parent, -72f, 1f, 3f, 1.5f, 1.5f);
            HalfWall(parent, -64f, -16f, 4f, 0f);
            CreateBox(parent, "Outpost_Shed", new Vector3(-74f, 1.5f, -4f), new Vector3(4f, 3f, 5f), containerBlueMat);

            // West Rocks / A Control
            Crate(parent, -50f, 6f, 2f, 1.5f, 2f);
            Crate(parent, -38f, 16f, 1.5f, 1.5f, 1.5f);
            HalfWall(parent, -43f, 20f, 3f, 0f);

            // Bombsite A - tevi pe latura vestica, rezervoare in centru
            InnerWall(parent, "A_Pipes_Wall_South", -50f, 26f, -50f, 34f, 3.5f);
            SoftWall(parent, "A_Pipes_SoftWall", -50f, 38f, -50f, 42f, 0f, 3.5f);
            Pipe(parent, "A_Pipe_West_Low", new Vector3(-57.3f, 0.6f, 24f), new Vector3(-57.3f, 0.6f, 44f), 0.9f);
            Pipe(parent, "A_Pipe_West_High", new Vector3(-57.3f, 1.8f, 24f), new Vector3(-57.3f, 1.8f, 44f), 0.7f);
            Pipe(parent, "A_Pipe_Overhead_1", new Vector3(-58f, 4.2f, 30f), new Vector3(-36f, 4.2f, 30f), 0.6f);
            Pipe(parent, "A_Pipe_Overhead_2", new Vector3(-58f, 4.6f, 40f), new Vector3(-44f, 4.6f, 40f), 0.6f);
            Tank(parent, "A_Tank_Main", -36f, 31f, 4f, 4f);
            Tank(parent, "A_Tank_Small", -27f, 42f, 3.5f, 3.4f);
            Crate(parent, -42f, 30f, 2f, 2f, 2f);
            Crate(parent, -54f, 30f, 1.4f, 1.4f, 1.4f);
            Crate(parent, -40f, 52f, 1.5f, 1.5f, 1.5f);
            Crate(parent, -50f, 49f, 1.2f, 1.2f, 1.2f, 1.2f); // pe platforma A
            HalfWall(parent, -40f, 42f, 3f, 0f);

            // A Catwalks / Mid Connector
            Crate(parent, -16f, 46f, 2f, 1.2f, 2f);
            Crate(parent, -17f, 16f, 1.2f, 1.2f, 1.2f);
            Crate(parent, -15f, 33f, 1.4f, 1.4f, 1.4f);

            // South Courtyard
            CreateBox(parent, "Courtyard_Truck", new Vector3(-16f, 1.5f, -30f), new Vector3(6f, 3f, 3f), containerBlueMat);
            Container(parent, -2f, -22f, 90f, containerRedMat);
            Crate(parent, -22f, -20f, 2f, 1.2f, 2f);
            Crate(parent, 4f, -34f, 1.5f, 1.5f, 1.5f);
            Crate(parent, 10f, -24f, 2f, 2f, 2f);
            HalfWall(parent, -8f, -36f, 4f, 0f);

            // South Lane / Tunnel
            Crate(parent, 0f, -44f, 1.5f, 1.5f, 1.5f);
            Crate(parent, -14f, -57f, 1.2f, 1.2f, 1.2f);
            Crate(parent, 2f, -53f, 1.2f, 1.2f, 1.2f);

            // Choke / Mid East
            Crate(parent, 8f, -10f, 1.2f, 1.2f, 1.2f);
            Crate(parent, 16f, 4f, 1.4f, 1.4f, 1.4f);
            Crate(parent, 36f, 8f, 2f, 1.2f, 2f);
            HalfWall(parent, 42f, 12f, 3f, 90f);

            // Mid Lab - camere despartite de pereti interiori, mese de laborator
            // Peretii "moi" (culoare de lemn) se sparg cu gloante.
            InnerWall(parent, "Lab_Wall_Z28_West", -10f, 28f, -2f, 28f, GroundCeiling);
            InnerWall(parent, "Lab_Wall_Z28_East", 4f, 28f, 10f, 28f, GroundCeiling);
            SoftWall(parent, "Lab_SoftWall_Z28", 10f, 28f, 14f, 28f, 0f, GroundCeiling);
            InnerWall(parent, "Lab_Wall_X8_South", 8f, 10f, 8f, 18f, GroundCeiling);
            SoftWall(parent, "Lab_SoftWall_X8", 8f, 22f, 8f, 28f, 0f, GroundCeiling);
            Crate(parent, -3f, 15f, 4f, 1f, 1.2f);
            Crate(parent, 13f, 14f, 4f, 1f, 1.2f);
            Crate(parent, 9f, 34f, 4f, 1f, 1.2f);
            Crate(parent, -6f, 33f, 1.2f, 1f, 4f);
            Crate(parent, 3f, 20f, 2f, 1.2f, 2f);

            // Lab Annex
            Crate(parent, 32f, 35f, 6f, 1f, 1.2f);
            Crate(parent, 24f, 26f, 1.2f, 1f, 3f);
            SoftWall(parent, "Annex_SoftWall", 30f, 28f, 30f, 34f, 0f, GroundCeiling);

            // North Overlook (cover pe linia de sniper)
            Crate(parent, -4f, 48f, 2f, 1.2f, 2f);
            HalfWall(parent, 36f, 48f, 4f, 0f);
            Crate(parent, 44f, 45f, 1.5f, 1.5f, 1.5f);
            Crate(parent, 8f, 50f, 1.4f, 1.4f, 1.4f);

            // East Yard
            Crate(parent, 54f, 30f, 2f, 2f, 2f);
            Crate(parent, 70f, 24f, 2f, 2f, 2f);
            Container(parent, 64f, 17f, 0f, containerBlueMat);
            HalfWall(parent, 52f, 14f, 3f, 90f);
            Crate(parent, 72f, 12f, 1.5f, 1.5f, 1.5f);

            // Hangar / Bombsite B
            Container(parent, 40f, -18f, 90f, containerRedMat);
            Crate(parent, 50f, -24f, 2f, 2f, 2f);
            Crate(parent, 48f, -12f, 2f, 2f, 2f);
            Crate(parent, 48f, -12f, 1.4f, 1.4f, 1.4f, 2f);
            Container(parent, 31f, -30f, 0f, containerBlueMat);
            Container(parent, 52f, -4f, 90f, containerRedMat);
            Crate(parent, 34f, -4f, 1.5f, 1.5f, 1.5f);

            // Container Stack - latura estica a hangarului
            Container(parent, 67f, -10f, 0f, containerRedMat);
            Container(parent, 67f, -10f, 0f, containerBlueMat, 2.6f);
            Container(parent, 73f, -18f, 0f, containerBlueMat);
            Container(parent, 67f, -26f, 0f, containerBlueMat);
            Container(parent, 73f, -30f, 0f, containerRedMat);
            Container(parent, 73f, -30f, 0f, containerRedMat, 2.6f);

            // Loading Bay
            Container(parent, 32f, -48f, 90f, containerBlueMat);
            Container(parent, 46f, -52f, 90f, containerRedMat);
            Container(parent, 46f, -52f, 90f, containerBlueMat, 2.6f);
            Crate(parent, 40f, -44f, 2f, 1.2f, 2f);
            Crate(parent, 54f, -46f, 1.5f, 1.5f, 1.5f);
        }

        private static void Crate(Transform parent, float x, float z,
            float sizeX, float sizeY, float sizeZ, float baseY = 0f)
        {
            CreateBox(
                parent,
                $"Crate_{coverIndex++:000}",
                new Vector3(x, baseY + sizeY * 0.5f, z),
                new Vector3(sizeX, sizeY, sizeZ),
                coverMat
            );
        }

        private static void HalfWall(Transform parent, float x, float z, float length, float rotationY)
        {
            GameObject wall = CreateBox(
                parent,
                $"HalfWall_{coverIndex++:000}",
                new Vector3(x, 0.6f, z),
                new Vector3(length, 1.2f, 0.5f),
                coverMat
            );

            wall.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        private static void Container(Transform parent, float x, float z, float rotationY,
            Material material, float baseY = 0f)
        {
            // Container standard de 20 ft: ~2.6 x 2.6 x 6 m
            GameObject container = CreateBox(
                parent,
                $"Container_{coverIndex++:000}",
                new Vector3(x, baseY + 1.3f, z),
                new Vector3(2.6f, 2.6f, 6f),
                material
            );

            container.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        private static void Tank(Transform parent, string name, float x, float z, float diameter, float height)
        {
            GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = name;
            tank.transform.SetParent(parent, false);
            tank.transform.position = new Vector3(x, height * 0.5f, z);
            tank.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);

            // CapsuleCollider are capete rotunjite - pentru cover folosim colliderul exact al mesh-ului.
            Object.DestroyImmediate(tank.GetComponent<Collider>());
            tank.AddComponent<MeshCollider>();

            FinishStaticObject(tank, pipeMat);
        }

        /// <summary>Teava (cilindru) intre doua puncte.</summary>
        private static void Pipe(Transform parent, string name, Vector3 from, Vector3 to, float diameter)
        {
            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = name;
            pipe.transform.SetParent(parent, false);
            pipe.transform.position = (from + to) * 0.5f;

            // Cilindrul Unity are axa pe Y si inaltimea 2 la scara 1.
            pipe.transform.rotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
            pipe.transform.localScale = new Vector3(diameter, Vector3.Distance(from, to) * 0.5f, diameter);

            Object.DestroyImmediate(pipe.GetComponent<Collider>());
            pipe.AddComponent<MeshCollider>();

            FinishStaticObject(pipe, pipeMat);
        }

        // ------------------------------------------------------------------
        // Gameplay: spawn-uri, bombsite-uri, bucla de runda, manechine
        // ------------------------------------------------------------------
        private static void BuildGameplay(Transform parent)
        {
            Transform t1 = CreateGroup(parent, "Spawns_T1_Attackers");
            Vector3[] t1Points =
            {
                new Vector3(-60f, 0.1f, -44f),
                PlayerStart,
                new Vector3(-52f, 0.1f, -44f),
                new Vector3(-58f, 0.1f, -38f),
                new Vector3(-54f, 0.1f, -38f),
            };

            for (int i = 0; i < t1Points.Length; i++)
            {
                CreateSpawnPoint(t1, $"Spawn_T1_{i + 1}", t1Points[i], 0f, Team.Attackers);
            }

            Transform t2 = CreateGroup(parent, "Spawns_T2_Defenders");
            Vector3[] t2Points =
            {
                new Vector3(64f, 0.1f, 50f),
                new Vector3(68f, 0.1f, 50f),
                new Vector3(72f, 0.1f, 50f),
                new Vector3(66f, 0.1f, 44f),
                new Vector3(70f, 0.1f, 44f),
            };

            for (int i = 0; i < t2Points.Length; i++)
            {
                CreateSpawnPoint(t2, $"Spawn_T2_{i + 1}", t2Points[i], 225f, Team.Defenders);
            }

            CreateBombSite(parent, "BombSite_A", SiteA);
            CreateBombSite(parent, "BombSite_B", SiteB);

            // Bucla de runda + bomba + HUD (HUD-ul isi construieste singur Canvas-ul).
            GameObject gameMode = new GameObject("GameMode");
            gameMode.transform.SetParent(parent, false);
            gameMode.AddComponent<RoundManager>();
            gameMode.AddComponent<BombController>();
            gameMode.AddComponent<DefenderPrepController>();
            gameMode.AddComponent<ChokeHold.UI.RoundHUD>();

            // Manechine de test in echipa aparatorilor, pana exista boti:
            // daca le elimini pe toate, atacatorii castiga runda.
            Transform dummies = CreateGroup(parent, "TestDummies_Defenders");
            CreateDummy(dummies, "Dummy_Defender_SiteA", new Vector3(-40f, 0f, 37f));
            CreateDummy(dummies, "Dummy_Defender_SiteB", new Vector3(44f, 0f, -8f));
            CreateDummy(dummies, "Dummy_Defender_MidLab", new Vector3(12f, 0f, 20f));
            CreateDummy(dummies, "Dummy_Defender_Courtyard", new Vector3(-8f, 0f, -26f));
            CreateDummy(dummies, "Dummy_Defender_LabUpper", new Vector3(-2f, UpperFloorHeight, 20f));
            CreateDummy(dummies, "Dummy_Defender_AHeaven", new Vector3(-28f, UpperFloorHeight, 52f));
        }

        private static void CreateDummy(Transform parent, string name, Vector3 feetPosition)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = name;
            dummy.transform.SetParent(parent, false);
            dummy.transform.position = feetPosition + Vector3.up; // capsula are 2 m, pivot la mijloc
            dummy.GetComponent<MeshRenderer>().sharedMaterial = t2FloorMat;

            // RequireComponent adauga automat si Health.
            TeamMember member = dummy.AddComponent<TeamMember>();
            SerializedObject serialized = new SerializedObject(member);
            serialized.FindProperty("team").enumValueIndex = (int)Team.Defenders;
            serialized.FindProperty("respawnAtSpawnPoint").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateSpawnPoint(Transform parent, string name, Vector3 position,
            float rotationY, Team team)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

            SpawnPoint spawnPoint = go.AddComponent<SpawnPoint>();
            SerializedObject serialized = new SerializedObject(spawnPoint);
            serialized.FindProperty("team").enumValueIndex = (int)team;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateBombSite(Transform parent, string name, Area area)
        {
            const float height = 6f;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = area.Center;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(area.Size2D.x, height, area.Size2D.z);

            BombSite site = go.AddComponent<BombSite>();
            SerializedObject serialized = new SerializedObject(site);
            serialized.FindProperty("siteId").stringValue = area.Name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Etichete de zone pentru Scene view
        // ------------------------------------------------------------------
        private static void BuildZoneLabels(Transform parent)
        {
            foreach (Area area in WalkableAreas)
            {
                GameObject go = new GameObject($"Zone_{area.Name}");
                go.transform.SetParent(parent, false);
                go.transform.position = area.Center;

                MapZone zone = go.AddComponent<MapZone>();
                SerializedObject serialized = new SerializedObject(zone);
                serialized.FindProperty("displayName").stringValue = area.Name;
                serialized.FindProperty("size").vector3Value = new Vector3(area.Size2D.x, 3f, area.Size2D.z);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------
        // Lumini
        // ------------------------------------------------------------------
        private static void BuildLights(Transform parent)
        {
            bool hasDirectional = false;
            foreach (Light light in Object.FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    hasDirectional = true;
                    break;
                }
            }

            if (!hasDirectional)
            {
                GameObject sun = new GameObject("Map_Sun");
                sun.transform.SetParent(parent, false);
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Light sunLight = sun.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                sunLight.intensity = 1.2f;
                sunLight.shadows = LightShadows.Soft;
            }

            // Tunelul si laboratorul sunt acoperite - au nevoie de lumina proprie.
            for (float x = -40f; x <= 16f; x += 14f)
            {
                CreatePointLight(parent, $"Light_Tunnel_{x:0}", new Vector3(x, 3.4f, -55f),
                    new Color(1f, 0.75f, 0.45f), 12f, 2.5f);
            }

            Color labColor = new Color(0.75f, 0.9f, 1f);
            float low = GroundCeiling - 0.4f;
            float high = 2f * UpperFloorHeight - 0.6f;

            CreatePointLight(parent, "Light_Lab_SW", new Vector3(-2f, low, 18f), labColor, 14f, 2.5f);
            CreatePointLight(parent, "Light_Lab_SE", new Vector3(13f, low, 18f), labColor, 12f, 2.5f);
            CreatePointLight(parent, "Light_Lab_North", new Vector3(4f, low, 33f), labColor, 14f, 2.5f);
            CreatePointLight(parent, "Light_Annex", new Vector3(31f, low, 30f), labColor, 14f, 2.5f);

            // Etaj
            CreatePointLight(parent, "Light_Lab2_South", new Vector3(4f, high, 17f), labColor, 16f, 2.5f);
            CreatePointLight(parent, "Light_Lab2_North", new Vector3(4f, high, 31f), labColor, 16f, 2.5f);
            CreatePointLight(parent, "Light_Annex2", new Vector3(31f, high, 30f), labColor, 16f, 2.5f);
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 position,
            Color color, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
        }

        // ------------------------------------------------------------------
        // Primitive de constructie
        // ------------------------------------------------------------------
        private static void CreatePlatform(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax, float topHeight)
        {
            const float thickness = 0.4f;

            CreateBox(
                parent,
                name,
                new Vector3((xMin + xMax) * 0.5f, topHeight - thickness * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, thickness, zMax - zMin),
                platformMat
            );
        }

        /// <summary>
        /// Rampa care ocupa dreptunghiul dat si urca pe directia riseDir
        /// (Vector3.forward/back/left/right) de la 0 la height.
        /// </summary>
        private static void CreateRamp(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax,
            Vector3 riseDir, float height)
        {
            const float thickness = 0.3f;

            bool alongX = Mathf.Abs(riseDir.x) > 0.5f;
            float run = alongX ? xMax - xMin : zMax - zMin;
            float width = alongX ? zMax - zMin : xMax - xMin;

            float angle = Mathf.Atan2(height, run);
            float slopeLength = Mathf.Sqrt(run * run + height * height);

            Vector3 surfaceMid = new Vector3((xMin + xMax) * 0.5f, height * 0.5f, (zMin + zMax) * 0.5f);

            // Coboram cutia pe normala suprafetei ca fata de sus sa fie exact pe panta.
            Vector3 center =
                surfaceMid +
                riseDir * (Mathf.Sin(angle) * thickness * 0.5f) -
                Vector3.up * (Mathf.Cos(angle) * thickness * 0.5f);

            GameObject ramp = CreateBox(parent, name, center, new Vector3(width, thickness, slopeLength), rampMat);

            ramp.transform.rotation =
                Quaternion.LookRotation(riseDir) *
                Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
        }

        private static GameObject CreateBox(Transform parent, string name,
            Vector3 center, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;

            FinishStaticObject(box, material);
            return box;
        }

        private static void FinishStaticObject(GameObject go, Material material)
        {
            go.layer = groundLayer;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic
            );
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        // ------------------------------------------------------------------
        // Materiale greybox (salvate in Assets/_Project/Art/Materials/Greybox)
        // ------------------------------------------------------------------
        private static void CreateMaterials()
        {
            EnsureFolder(MaterialFolder);

            floorMat = GetOrCreateMaterial("GB_Floor", new Color(0.34f, 0.35f, 0.37f));
            wallMat = GetOrCreateMaterial("GB_Wall", new Color(0.55f, 0.56f, 0.58f));
            outerWallMat = GetOrCreateMaterial("GB_WallOuter", new Color(0.22f, 0.23f, 0.26f));
            platformMat = GetOrCreateMaterial("GB_Platform", new Color(0.2f, 0.45f, 0.85f));
            rampMat = GetOrCreateMaterial("GB_Ramp", new Color(0.3f, 0.65f, 0.95f));
            coverMat = GetOrCreateMaterial("GB_Cover", new Color(0.9f, 0.55f, 0.15f));
            containerRedMat = GetOrCreateMaterial("GB_ContainerRed", new Color(0.65f, 0.22f, 0.16f));
            containerBlueMat = GetOrCreateMaterial("GB_ContainerBlue", new Color(0.16f, 0.32f, 0.58f));
            pipeMat = GetOrCreateMaterial("GB_Pipe", new Color(0.45f, 0.47f, 0.5f));
            roofMat = GetOrCreateMaterial("GB_Roof", new Color(0.3f, 0.28f, 0.34f));
            doorMat = GetOrCreateMaterial("GB_Door", new Color(0.25f, 0.6f, 0.45f));
            upperFloorMat = GetOrCreateMaterial("GB_UpperFloor", new Color(0.3f, 0.4f, 0.56f));
            softWallMat = GetOrCreateMaterial("GB_SoftWall", new Color(0.72f, 0.56f, 0.36f));
            hatchMat = GetOrCreateMaterial("GB_Hatch", new Color(0.95f, 0.8f, 0.25f));
            reinforcedMat = GetOrCreateMaterial("GB_Reinforced", new Color(0.12f, 0.13f, 0.16f));
            barricadeMat = GetOrCreateMaterial("GB_Barricade", new Color(0.55f, 0.38f, 0.2f));
            siteFloorMat = GetOrCreateMaterial("GB_SiteFloor", new Color(0.9f, 0.75f, 0.1f));
            t1FloorMat = GetOrCreateMaterial("GB_T1Floor", new Color(0.15f, 0.3f, 0.8f));
            t2FloorMat = GetOrCreateMaterial("GB_T2Floor", new Color(0.8f, 0.18f, 0.15f));

            AssetDatabase.SaveAssets();
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        // ------------------------------------------------------------------
        // Pune Player-ul existent pe spawn-ul T1
        // ------------------------------------------------------------------
        private static void MovePlayerToSpawn()
        {
            FpsController player = Object.FindAnyObjectByType<FpsController>();

            if (player == null)
            {
                Debug.LogWarning(
                    "Nu exista niciun Player (FpsController) in scena. " +
                    "Harta e generata, dar nu ai cu cine sa o joci."
                );
                return;
            }

            Undo.RecordObject(player.transform, "Move Player To Spawn");
            player.transform.position = PlayerStart;
            player.transform.rotation = Quaternion.identity;

            // Fara TeamMember, RoundManager nu stie de jucator (respawn, echipa, bomba).
            if (player.GetComponent<TeamMember>() == null)
            {
                Undo.AddComponent<TeamMember>(player.gameObject);
            }

            RepairPlayerReferences(player);
        }

        /// <summary>
        /// FpsController se dezactiveaza daca Ground Check lipseste. Daca referinta e goala sau
        /// arata spre un obiect din afara jucatorului (ex. o podea veche, stearsa intre timp),
        /// o legam de copilul "GroundCheck" al jucatorului.
        /// </summary>
        private static void RepairPlayerReferences(FpsController player)
        {
            SerializedObject serialized = new SerializedObject(player);

            SerializedProperty groundCheck = serialized.FindProperty("groundCheck");
            Transform current = groundCheck.objectReferenceValue as Transform;

            bool isValid =
                current != null &&
                current != player.transform &&
                current.IsChildOf(player.transform);

            if (!isValid)
            {
                Transform child = player.transform.Find("GroundCheck");

                if (child == null)
                {
                    GameObject created = new GameObject("GroundCheck");
                    Undo.RegisterCreatedObjectUndo(created, "Create GroundCheck");
                    created.transform.SetParent(player.transform, false);
                    created.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    child = created.transform;
                }

                groundCheck.objectReferenceValue = child;
                Debug.Log("Player: Ground Check a fost legat de copilul 'GroundCheck' al jucatorului.");
            }

            // Fara masca, verificarea de sol nu vede nimic; harta generata e pe layer-ul Ground.
            SerializedProperty groundMask = serialized.FindProperty("groundMask");

            if (groundMask.intValue == 0 && groundLayer > 0)
            {
                groundMask.intValue = 1 << groundLayer;
            }

            serialized.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------
        // Imagini de previzualizare (Docs/)
        // ------------------------------------------------------------------
        [MenuItem("CHOKEHOLD/Map/Capture Preview Images")]
        public static void CapturePreviews()
        {
            GameObject root = GameObject.Find(RootName);

            if (root == null)
            {
                Debug.LogWarning("Genereaza intai harta (CHOKEHOLD > Map > Build Blacksite K-17).");
                return;
            }

            string docs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs");
            Directory.CreateDirectory(docs);

            // Acoperisurile ar ascunde laboratorul si tunelul in vederea de sus.
            Transform roofs = root.transform.Find("Roofs");
            bool roofsWereActive = roofs != null && roofs.gameObject.activeSelf;

            if (roofs != null)
            {
                roofs.gameObject.SetActive(false);
            }

            Transform upper = root.transform.Find(UpperGroupName);

            try
            {
                // Vedere de sus cu etajul: nordul in sus, estul in dreapta.
                Capture(
                    Path.Combine(docs, "BlacksiteK17_TopDown_Level2.png"),
                    new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f),
                    true, 61f, 1600, 1200
                );

                // Doar parterul (etajul ascuns).
                if (upper != null)
                {
                    upper.gameObject.SetActive(false);
                }

                Capture(
                    Path.Combine(docs, "BlacksiteK17_TopDown_Level1.png"),
                    new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f),
                    true, 61f, 1600, 1200
                );

                if (upper != null)
                {
                    upper.gameObject.SetActive(true);
                }

                // Vedere in perspectiva, din sud-vest.
                Vector3 eye = new Vector3(-70f, 110f, -135f);
                Capture(
                    Path.Combine(docs, "BlacksiteK17_Perspective.png"),
                    eye, Quaternion.LookRotation(new Vector3(0f, 0f, -8f) - eye),
                    false, 40f, 1600, 900
                );
            }
            finally
            {
                if (roofs != null)
                {
                    roofs.gameObject.SetActive(roofsWereActive);
                }
            }

            Debug.Log($"Imagini salvate in {docs}");
        }

        private static void Capture(string path, Vector3 position, Quaternion rotation,
            bool orthographic, float sizeOrFov, int width, int height)
        {
            GameObject cameraObject = new GameObject("PreviewCamera (temp)")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
                camera.nearClipPlane = 1f;
                camera.farClipPlane = 600f;
                camera.orthographic = orthographic;

                if (orthographic)
                {
                    camera.orthographicSize = sizeOrFov;
                }
                else
                {
                    camera.fieldOfView = sizeOrFov;
                }

                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
