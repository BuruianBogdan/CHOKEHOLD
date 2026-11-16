using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Pipeline-ul de constructie si helperele comune tuturor zonelor.
    //
    // Fisiere:
    //   BlacksiteK17MapBuilder.cs          - constante, materiale, primitive (cutii, usi, pereti moi, baricade...)
    //   BlacksiteK17MapBuilder.Build.cs    - ordinea de constructie, grupuri, scari, lumini
    //   BlacksiteK17MapBuilder.Layout.cs   - zonele walkable, podele, tunelul subteran, pereti, etichete
    //   BlacksiteK17MapBuilder.SiteA.cs    - Bombsite A, A Control, sala pompelor (A Pipes), depozit
    //   BlacksiteK17MapBuilder.Mid.cs      - Mid Lab, anexa, Mid Connector, Choke Point, coridoarele din mid
    //   BlacksiteK17MapBuilder.SiteB.cs    - Hangar / Bombsite B, Loading Bay, birou
    //   BlacksiteK17MapBuilder.Spawns.cs   - spawn-uri, West Outpost, East Yard, North Overlook, camera de securitate
    //   BlacksiteK17MapBuilder.South.cs    - South Courtyard, South Lane, South Tunnel
    public static partial class BlacksiteK17MapBuilder
    {
        // Grupurile din ierarhie. Zonele isi pun obiectele in grupul potrivit tipului lor,
        // ca sa poata fi ascunse pe categorii (ex. acoperisurile in vederea de sus).
        private static Transform gFloors;
        private static Transform gUnderground;
        private static Transform gWalls;
        private static Transform gStairs;
        private static Transform gUpper;
        private static Transform gRoofs;
        private static Transform gDoors;
        private static Transform gBarricades;
        private static Transform gStructures;
        private static Transform gCover;
        private static Transform gGameplay;
        private static Transform gSpawnsT1;
        private static Transform gSpawnsT2;
        private static Transform gDummies;
        private static Transform gZones;
        private static Transform gLights;

        private const string RoofGroupName = "Roofs";
        private const string WallGroupName = "Walls";
        private const string FloorGroupName = "Floors";
        private const string StairGroupName = "Stairs And Low Platforms";

        private static Material stairMat;

        [MenuItem("CHOKEHOLD/Map/Build Blacksite K-17")]
        public static void Build()
        {
            groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0)
            {
                groundLayer = 0;
            }

            coverIndex = 0;
            ResetDetailRecords();
            CreateMaterials();
            stairMat = GetOrCreateMaterial("GB_Stairs", new Color(0.46f, 0.46f, 0.45f));   // beton
            CreateDetailMaterials();
            AssetDatabase.SaveAssets();

            RemoveExistingRoot();

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Blacksite K-17");

            gFloors = CreateGroup(root.transform, FloorGroupName);
            gUnderground = CreateGroup(root.transform, "Underground (Tunnel)");
            gWalls = CreateGroup(root.transform, WallGroupName);
            gStairs = CreateGroup(root.transform, StairGroupName);
            gUpper = CreateGroup(root.transform, UpperGroupName);
            gRoofs = CreateGroup(root.transform, RoofGroupName);
            gDoors = CreateGroup(root.transform, "Doors");
            gBarricades = CreateGroup(gDoors, "Barricade Points");
            gStructures = CreateGroup(root.transform, "Structures");
            gCover = CreateGroup(root.transform, "Cover");
            gGameplay = CreateGroup(root.transform, "Gameplay");
            gSpawnsT1 = CreateGroup(gGameplay, "Spawns_T1_Attackers");
            gSpawnsT2 = CreateGroup(gGameplay, "Spawns_T2_Defenders");
            gDummies = CreateGroup(gGameplay, "TestDummies_Defenders");
            gZones = CreateGroup(root.transform, "Zones (editor labels)");
            gLights = CreateGroup(root.transform, "Lights");

            // Structura
            BuildFloors();
            BuildUnderground();
            BuildWalls();

            // Zone
            BuildSiteA();
            BuildMid();
            BuildSiteB();
            BuildSpawnsAndOuterZones();
            BuildSouth();
            BuildServiceTunnels();

            // Imbinari: recuzita lipita de pereti, fara fante
            SnapPropsToNeighbours();

            // Acoperisuri accesibile si camera de control (dupa lipire: citeste pozitiile finale)
            BuildRooftops();

            // Finisare (citeste ce au plasat zonele)
            BuildDetails(root.transform);

            // Piesele din kit-ul modular (Blender) in locul cutiilor de cover, containerelor, usilor
            ApplyKit(root);

            // Instalatia nucleara: sala reactorului, semne, girofare, lampi de urgenta, conducte, decontaminare
            BuildNuclearDressing(root);

            // UV la scara reala pe toate cutiile (pregatire pentru texturi)
            BakeBoxUVs(root);

            // Sisteme
            BuildGameMode();
            BuildZoneLabels();
            BuildAtmosphere();

            // Realism: texturi PBR pe materiale si post-procesare
            ApplySurfaces(root);
            BuildPostProcessing(root);

            MovePlayerToSpawn();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log(
                "Blacksite K-17 generat: 160 x 120 m, parter + etaj + tunel subteran. " +
                "Layout-ul se modifica in BlacksiteK17MapBuilder.Layout.cs."
            );
        }

        // ------------------------------------------------------------------
        // Bucla de runda + bomba + pregatire + HUD (HUD-ul isi construieste singur Canvas-ul)
        // ------------------------------------------------------------------
        private static void BuildGameMode()
        {
            GameObject gameMode = new GameObject("GameMode");
            gameMode.transform.SetParent(gGameplay, false);
            gameMode.AddComponent<RoundManager>();
            gameMode.AddComponent<BombController>();
            gameMode.AddComponent<DefenderPrepController>();
            gameMode.AddComponent<ChokeHold.UI.RoundHUD>();
        }

        // ------------------------------------------------------------------
        // Lumini pe zone (interioarele au acoperis, deci soarele nu ajunge in ele)
        // ------------------------------------------------------------------
        private static readonly Color WarmLight = new Color(1f, 0.8f, 0.55f);
        private static readonly Color ColdLight = new Color(0.75f, 0.9f, 1f);
        private static readonly Color WhiteLight = new Color(1f, 0.97f, 0.9f);

        private static void Lamp(string name, float x, float y, float z, Color color, float range = 16f, float intensity = 3f)
        {
            CreatePointLight(gLights, name, new Vector3(x, y, z), color, range, intensity);
        }

        // ------------------------------------------------------------------
        // Scari cu trepte reale
        // ------------------------------------------------------------------

        /// <summary>
        /// Scara plina (fara gol dedesubt) care ocupa dreptunghiul dat si urca pe directia riseDir
        /// de la baseY la baseY + height, cu trepte de ~0,2 m.
        /// Treptele sunt vizuale si blocheaza spatiul de sub ele; mersul se face pe o suprafata
        /// inclinata invizibila, ca urcatul sa fie lin.
        /// </summary>
        private static void Stairs(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax,
            Vector3 riseDir, float baseY, float height)
        {
            const float targetRise = 0.2f;

            int steps = Mathf.Max(1, Mathf.RoundToInt(height / targetRise));
            bool alongX = Mathf.Abs(riseDir.x) > 0.5f;
            bool positive = alongX ? riseDir.x > 0f : riseDir.z > 0f;

            float run = alongX ? xMax - xMin : zMax - zMin;
            float depth = run / steps;
            float stepRise = height / steps;

            Transform group = CreateGroup(parent, name);

            for (int i = 0; i < steps; i++)
            {
                float stepHeight = (i + 1) * stepRise;
                float near = i * depth;
                float far = (i + 1) * depth;

                float sx0 = xMin, sx1 = xMax, sz0 = zMin, sz1 = zMax;

                if (alongX)
                {
                    sx0 = positive ? xMin + near : xMax - far;
                    sx1 = positive ? xMin + far : xMax - near;
                }
                else
                {
                    sz0 = positive ? zMin + near : zMax - far;
                    sz1 = positive ? zMin + far : zMax - near;
                }

                GameObject step = CreateBox(
                    group,
                    $"Step_{i:00}",
                    new Vector3((sx0 + sx1) * 0.5f, baseY + stepHeight * 0.5f, (sz0 + sz1) * 0.5f),
                    new Vector3(sx1 - sx0, stepHeight, sz1 - sz0),
                    stairMat
                );

                // Colliderul treptei se opreste sub suprafata de mers (cu o treapta mai jos),
                // ca sa nu agate jucatorul, dar sa umple complet volumul de sub scara.
                BoxCollider box = step.GetComponent<BoxCollider>();

                if (i == 0)
                {
                    Object.DestroyImmediate(box);
                }
                else
                {
                    float fraction = (float)i / (i + 1);
                    box.size = new Vector3(1f, fraction, 1f);
                    box.center = new Vector3(0f, -0.5f + fraction * 0.5f, 0f);
                }
            }

            GameObject surface = CreateRampAt(group, "WalkSurface", xMin, zMin, xMax, zMax, riseDir, baseY, height, rampMat);
            Object.DestroyImmediate(surface.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(surface.GetComponent<MeshFilter>());
        }

        /// <summary>
        /// Rampa (placa inclinata) pe dreptunghiul dat, care urca pe riseDir de la baseY la baseY + height.
        /// </summary>
        private static GameObject CreateRampAt(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax,
            Vector3 riseDir, float baseY, float height, Material material)
        {
            const float thickness = 0.3f;

            bool alongX = Mathf.Abs(riseDir.x) > 0.5f;
            float run = alongX ? xMax - xMin : zMax - zMin;
            float width = alongX ? zMax - zMin : xMax - xMin;

            float angle = Mathf.Atan2(height, run);
            float slopeLength = Mathf.Sqrt(run * run + height * height);

            Vector3 surfaceMid = new Vector3((xMin + xMax) * 0.5f, baseY + height * 0.5f, (zMin + zMax) * 0.5f);

            // Coboram cutia pe normala suprafetei ca fata de sus sa fie exact pe panta.
            Vector3 center =
                surfaceMid +
                riseDir * (Mathf.Sin(angle) * thickness * 0.5f) -
                Vector3.up * (Mathf.Cos(angle) * thickness * 0.5f);

            GameObject ramp = CreateBox(parent, name, center, new Vector3(width, thickness, slopeLength), material);

            ramp.transform.rotation =
                Quaternion.LookRotation(riseDir) *
                Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);

            return ramp;
        }

        /// <summary>Bloc solid cu material si baza alese (platforme pline, utilaje, pereti in tunel).</summary>
        private static GameObject SolidBlock(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax,
            float baseY, float height, Material material)
        {
            return CreateBox(
                parent,
                name,
                new Vector3((xMin + xMax) * 0.5f, baseY + height * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, height, zMax - zMin),
                material
            );
        }

        /// <summary>Acoperis plin peste o camera (fata de jos la inaltimea data).</summary>
        private static void Roof(string name, float xMin, float zMin, float xMax, float zMax, float height)
        {
            CreateRoof(gRoofs, name, xMin, zMin, xMax, zMax, height);
        }
    }
}
