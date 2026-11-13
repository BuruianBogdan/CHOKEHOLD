using System.Collections.Generic;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Genereaza harta greybox "Blacksite K-17" in scena deschisa.
    ///
    /// Cum functioneaza:
    ///  - harta are 160 x 120 m (X = est, Z = nord, originea in centru);
    ///  - definesti doar ZONELE PE CARE SE POATE MERGE (WalkableAreas);
    ///  - tot restul suprafetei este umplut automat cu blocuri solide (cladiri/pereti),
    ///    pe o grila de 2 m, unite in cutii cat mai mari;
    ///  - peste ele se adauga platforme, rampe, catwalk-uri, acoperisuri, cover si containere.
    ///
    /// Ca sa modifici layout-ul, schimba dreptunghiurile de mai jos si apasa din nou
    /// CHOKEHOLD > Map > Build Blacksite K-17 (harta veche generata este inlocuita).
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

        private const float InnerWallHeight = 7f;
        private const float OuterWallHeight = 10f;

        // ------------------------------------------------------------------
        // LAYOUT - zone pe care se poate merge (xMin, zMin, xMax, zMax), in metri.
        // Valori pare, aliniate la grila de 2 m. Zonele care se conecteaza
        // trebuie sa se SUPRAPUNA (nu doar sa se atinga), altfel ramane un perete.
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
            new Area("T1 Spawn",            -78f, -58f, -54f, -32f),
            new Area("T1 North Exit",       -74f, -34f, -64f, -12f),
            new Area("T1 Ramp",             -56f, -46f, -38f, -38f),

            // Vest -> A
            new Area("West Outpost",        -78f, -14f, -50f,   8f),
            new Area("A Control",           -52f,   2f, -42f,  28f),
            new Area("A Pipes",             -76f,   6f, -68f,  48f),
            new Area("A Pipes Entry",       -76f,  40f, -54f,  48f),

            // Bombsite A - nord-vest
            new Area("Bombsite A",          -56f,  26f, -28f,  52f),
            new Area("A Heaven (under)",    -40f,  50f, -28f,  56f),
            new Area("A Ramp Link",         -30f,  30f, -16f,  38f),
            new Area("A Ramp",              -22f,  10f, -14f,  38f),

            // Mid
            new Area("Mid Connector",       -22f, -14f, -14f,  12f),
            new Area("Lab West Door",       -16f,  18f,  -4f,  24f),
            new Area("Mid Lab",              -6f,   6f,  26f,  30f),
            new Area("Lab North Door",       10f,  28f,  16f,  34f),
            new Area("Lab South Door",        4f,   0f,  10f,   8f),
            new Area("Choke Point",           0f, -14f,  12f,   2f),
            new Area("Choke To Hangar",      10f,  -8f,  26f,   0f),
            new Area("North Overlook",        4f,  32f,  24f,  44f),

            // Nord - culoar lung de sniper, cu catwalk deasupra
            new Area("North Corridor",      -30f,  42f,  46f,  56f),

            // Aparatori (Team 2) - nord-est
            new Area("T2 Spawn",             34f,  38f,  58f,  58f),
            new Area("East Yard",            52f,   4f,  78f,  40f),
            new Area("Mid East Corridor",    24f,  14f,  62f,  22f),

            // Sud
            new Area("South Courtyard",     -40f, -44f,  10f, -12f),
            new Area("Tunnel Stairs",       -28f, -52f, -24f, -42f),
            new Area("South Tunnel",        -56f, -56f,  22f, -50f),
            new Area("Loading Bay",          16f, -56f,  50f, -40f),

            // Bombsite B - sud-est
            new Area("Hangar / Bombsite B",  24f, -42f,  58f,   6f),
            new Area("Container Stack",      56f, -36f,  76f,   8f),
        };

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
        private static Material siteFloorMat;
        private static Material t1FloorMat;
        private static Material t2FloorMat;

        private static int groundLayer;

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
            Transform vertical = CreateGroup(root.transform, "Vertical (Platforms, Ramps, Catwalks)");
            Transform roofs = CreateGroup(root.transform, "Roofs");
            Transform cover = CreateGroup(root.transform, "Cover");
            Transform gameplay = CreateGroup(root.transform, "Gameplay");
            Transform zones = CreateGroup(root.transform, "Zones (editor labels)");
            Transform lights = CreateGroup(root.transform, "Lights");

            BuildFloors(floors);
            BuildWalls(walls);
            BuildVertical(vertical);
            BuildRoofs(roofs);
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
            CreateFloorDecal(parent, "Floor_T1Spawn", -78f, -58f, -54f, -32f, t1FloorMat);
            CreateFloorDecal(parent, "Floor_T2Spawn", 34f, 38f, 58f, 58f, t2FloorMat);
            CreateFloorDecal(parent, "Floor_SiteA", -50f, 30f, -34f, 46f, siteFloorMat);
            CreateFloorDecal(parent, "Floor_SiteB", 32f, -30f, 50f, -10f, siteFloorMat);
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
        }

        // ------------------------------------------------------------------
        // Verticalitate: catwalk-uri, A Heaven, platforma A, catwalk in hangar
        // ------------------------------------------------------------------
        private static void BuildVertical(Transform parent)
        {
            // North Catwalk - linie lunga de sniper deasupra North Corridor (h = 4 m)
            CreatePlatform(parent, "North_Catwalk", -28f, 52f, 40f, 56f, 4f);
            CreateRamp(parent, "North_Catwalk_Ramp_T2", 40f, 52f, 48f, 56f, Vector3.left, 4f);

            // A Heaven - balcon peste site-ul A, legat de North Catwalk
            CreatePlatform(parent, "A_Heaven", -40f, 44f, -28f, 56f, 4f);
            CreateRamp(parent, "A_Heaven_Stairs", -38f, 36f, -34f, 44f, Vector3.forward, 4f);

            // A Platform - zona de plantare ridicata
            CreatePlatform(parent, "A_Platform", -52f, 32f, -42f, 42f, 1.2f);
            CreateRamp(parent, "A_Platform_Ramp", -50f, 26f, -46f, 32f, Vector3.forward, 1.2f);

            // Hangar Catwalk - pe peretele vestic al hangarului, vede B site
            CreatePlatform(parent, "Hangar_Catwalk", 24f, -34f, 28f, 2f, 3.5f);
            CreateRamp(parent, "Hangar_Catwalk_Ramp", 24f, -42f, 28f, -34f, Vector3.forward, 3.5f);

            // North Overlook - mic balcon spre culoarul de nord
            CreatePlatform(parent, "North_Overlook_Balcony", 16f, 36f, 24f, 44f, 2f);
            CreateRamp(parent, "North_Overlook_Stairs", 16f, 32f, 24f, 36f, Vector3.forward, 2f);
        }

        // ------------------------------------------------------------------
        // Acoperisuri: tunel si laborator
        // ------------------------------------------------------------------
        private static void BuildRoofs(Transform parent)
        {
            CreateRoof(parent, "Roof_SouthTunnel", -52f, -56f, 16f, -50f, 4f);
            CreateRoof(parent, "Roof_MidLab", -6f, 6f, 26f, 30f, 4.5f);

            // Pereti interiori in laborator (cu goluri) - lupta close-quarters
            CreateBox(parent, "Lab_Partition_South", new Vector3(10f, 2.25f, 9f), new Vector3(0.4f, 4.5f, 6f), wallMat);
            CreateBox(parent, "Lab_Partition_North", new Vector3(10f, 2.25f, 27f), new Vector3(0.4f, 4.5f, 6f), wallMat);
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

        // ------------------------------------------------------------------
        // Cover, containere, tevi, rezervoare
        // ------------------------------------------------------------------
        private static void BuildCover(Transform parent)
        {
            // West Outpost
            Crate(parent, -66f, -4f, 2f, 1.2f, 2f);
            Crate(parent, -58f, 2f, 1.2f, 1.2f, 1.2f);
            Crate(parent, -72f, 4f, 3f, 1.5f, 1.5f);
            HalfWall(parent, -62f, -10f, 4f, 0f);

            // A Pipes - tevi de-a lungul peretelui vestic
            CreateBox(parent, "A_Pipes_Run", new Vector3(-75.5f, 0.75f, 27f), new Vector3(1f, 1.5f, 34f), pipeMat);
            CreateBox(parent, "A_Pipes_Overhead", new Vector3(-72f, 3.2f, 20f), new Vector3(8f, 0.6f, 0.6f), pipeMat);
            Crate(parent, -70f, 30f, 1.4f, 1.4f, 1.4f);

            // A Control
            Crate(parent, -45f, 14f, 1.5f, 1.5f, 1.5f);
            HalfWall(parent, -50f, 22f, 3f, 0f);

            // Bombsite A
            Crate(parent, -36f, 30f, 2f, 2f, 2f);
            Crate(parent, -44f, 48f, 1.5f, 1.5f, 1.5f);
            Crate(parent, -54f, 30f, 2f, 1.2f, 1.2f);
            Tank(parent, "A_Tank", -31f, 47f, 4f, 3.5f); // sub A Heaven
            Crate(parent, -47f, 37f, 1.2f, 1.2f, 1.2f, 1.2f); // pe platforma A

            // A Ramp / Mid Connector
            Crate(parent, -18f, 24f, 1.2f, 1.2f, 1.2f);
            Crate(parent, -19f, 0f, 1.4f, 1.4f, 1.4f);

            // South Courtyard
            CreateBox(parent, "Courtyard_Truck", new Vector3(-20f, 1.5f, -30f), new Vector3(6f, 3f, 3f), containerBlueMat);
            Container(parent, -4f, -22f, 90f, containerRedMat);
            Crate(parent, -32f, -20f, 2f, 1.2f, 2f);
            Crate(parent, 0f, -38f, 1.5f, 1.5f, 1.5f);
            HalfWall(parent, -12f, -40f, 4f, 0f);
            Crate(parent, 6f, -28f, 2f, 2f, 2f);

            // Choke
            Crate(parent, 10f, -12f, 1.2f, 1.2f, 1.2f);

            // Mid Lab - mese de laborator si consola centrala
            Crate(parent, 2f, 12f, 4f, 1f, 1.2f);
            Crate(parent, 2f, 24f, 4f, 1f, 1.2f);
            Crate(parent, 18f, 12f, 4f, 1f, 1.2f);
            Crate(parent, 18f, 24f, 4f, 1f, 1.2f);
            Crate(parent, 10f, 18f, 2f, 1.2f, 2f);

            // North Corridor (cover pe linia de sniper)
            Crate(parent, -10f, 46f, 2f, 1.2f, 2f);
            Crate(parent, 30f, 45f, 1.5f, 1.5f, 1.5f);
            HalfWall(parent, 8f, 49f, 4f, 0f);
            Crate(parent, 8f, 38f, 1.4f, 1.4f, 1.4f);

            // East Yard
            Crate(parent, 60f, 30f, 2f, 2f, 2f);
            Crate(parent, 70f, 22f, 2f, 2f, 2f);
            Container(parent, 66f, 12f, 0f, containerBlueMat);
            HalfWall(parent, 56f, 18f, 3f, 90f);

            // Mid East Corridor
            Crate(parent, 40f, 18f, 1.2f, 1.2f, 1.2f);

            // Hangar / Bombsite B
            Container(parent, 36f, -20f, 90f, containerRedMat);
            Crate(parent, 44f, -26f, 2f, 2f, 2f);
            Crate(parent, 46f, -14f, 2f, 2f, 2f);
            Crate(parent, 46f, -14f, 1.4f, 1.4f, 1.4f, 2f);
            Container(parent, 31f, -34f, 0f, containerBlueMat);
            Container(parent, 52f, -4f, 0f, containerRedMat);
            Crate(parent, 40f, -2f, 1.5f, 1.5f, 1.5f);

            // Loading Bay
            Container(parent, 24f, -48f, 90f, containerBlueMat);
            Container(parent, 40f, -52f, 90f, containerRedMat);
            Container(parent, 40f, -52f, 90f, containerBlueMat, 2.6f);
            Crate(parent, 33f, -46f, 2f, 1.2f, 2f);

            // Container Stack (B Long)
            Container(parent, 61f, -30f, 0f, containerRedMat);
            Container(parent, 61f, -30f, 0f, containerBlueMat, 2.6f);
            Container(parent, 71f, -22f, 0f, containerBlueMat);
            Container(parent, 61f, -12f, 0f, containerBlueMat);
            Container(parent, 71f, -2f, 0f, containerRedMat);
            Container(parent, 71f, -2f, 0f, containerRedMat, 2.6f);
        }

        private static int coverIndex;

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

        // ------------------------------------------------------------------
        // Gameplay: spawn-uri si bombsite-uri
        // ------------------------------------------------------------------
        private static void BuildGameplay(Transform parent)
        {
            Transform t1 = CreateGroup(parent, "Spawns_T1_Attackers");
            Vector3[] t1Points =
            {
                new Vector3(-70f, 0.1f, -50f),
                new Vector3(-66f, 0.1f, -50f),
                new Vector3(-62f, 0.1f, -50f),
                new Vector3(-68f, 0.1f, -44f),
                new Vector3(-64f, 0.1f, -44f),
            };

            for (int i = 0; i < t1Points.Length; i++)
            {
                CreateSpawnPoint(t1, $"Spawn_T1_{i + 1}", t1Points[i], 0f, Team.Attackers);
            }

            Transform t2 = CreateGroup(parent, "Spawns_T2_Defenders");
            Vector3[] t2Points =
            {
                new Vector3(40f, 0.1f, 50f),
                new Vector3(44f, 0.1f, 50f),
                new Vector3(48f, 0.1f, 50f),
                new Vector3(42f, 0.1f, 46f),
                new Vector3(46f, 0.1f, 46f),
            };

            for (int i = 0; i < t2Points.Length; i++)
            {
                CreateSpawnPoint(t2, $"Spawn_T2_{i + 1}", t2Points[i], 225f, Team.Defenders);
            }

            CreateBombSite(parent, "BombSite_A", "A", -50f, 30f, -34f, 46f);
            CreateBombSite(parent, "BombSite_B", "B", 32f, -30f, 50f, -10f);

            // Bucla de runda + bomba + HUD (HUD-ul isi construieste singur Canvas-ul).
            GameObject gameMode = new GameObject("GameMode");
            gameMode.transform.SetParent(parent, false);
            gameMode.AddComponent<RoundManager>();
            gameMode.AddComponent<BombController>();
            gameMode.AddComponent<ChokeHold.UI.RoundHUD>();

            // Manechine de test in echipa aparatorilor, pana exista boti:
            // daca le elimini pe toate, atacatorii castiga runda.
            Transform dummies = CreateGroup(parent, "TestDummies_Defenders");
            CreateDummy(dummies, "Dummy_Defender_SiteA", new Vector3(-40f, 0f, 40f));
            CreateDummy(dummies, "Dummy_Defender_SiteB", new Vector3(42f, 0f, -8f));
            CreateDummy(dummies, "Dummy_Defender_MidLab", new Vector3(16f, 0f, 18f));
            CreateDummy(dummies, "Dummy_Defender_Courtyard", new Vector3(-8f, 0f, -30f));
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

        private static void CreateBombSite(Transform parent, string name, string siteId,
            float xMin, float zMin, float xMax, float zMax)
        {
            const float height = 6f;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(xMax - xMin, height, zMax - zMin);

            BombSite site = go.AddComponent<BombSite>();
            SerializedObject serialized = new SerializedObject(site);
            serialized.FindProperty("siteId").stringValue = siteId;
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
            for (float x = -48f; x <= 12f; x += 12f)
            {
                CreatePointLight(parent, $"Light_Tunnel_{x:0}", new Vector3(x, 3.4f, -53f),
                    new Color(1f, 0.75f, 0.45f), 12f, 2.5f);
            }

            CreatePointLight(parent, "Light_Lab_West", new Vector3(2f, 4f, 18f), new Color(0.75f, 0.9f, 1f), 14f, 2.5f);
            CreatePointLight(parent, "Light_Lab_East", new Vector3(18f, 4f, 18f), new Color(0.75f, 0.9f, 1f), 14f, 2.5f);
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
        // Pune Player-ul existent pe primul spawn T1
        // ------------------------------------------------------------------
        private static void MovePlayerToSpawn()
        {
            FpsController player = Object.FindAnyObjectByType<FpsController>();

            if (player == null)
            {
                return;
            }

            Undo.RecordObject(player.transform, "Move Player To Spawn");
            player.transform.position = new Vector3(-66f, 0.1f, -50f);
            player.transform.rotation = Quaternion.identity;

            // Fara TeamMember, RoundManager nu stie de jucator (respawn, echipa, bomba).
            if (player.GetComponent<TeamMember>() == null)
            {
                Undo.AddComponent<TeamMember>(player.gameObject);
            }
        }
    }
}
