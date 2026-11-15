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
    public static partial class BlacksiteK17MapBuilder
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

        /// <summary>
        /// Planseu la inaltimea top: acopera dreptunghiurile "solids", mai putin golurile "holes"
        /// (scari, trape). Toate coordonatele trebuie aliniate la grila de 2 m.
        /// </summary>
        private static void Slab(Transform parent, string name, float top, Area[] solids, Area[] holes,
            Material material = null)
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
                        material != null ? material : upperFloorMat
                    );

                    // Pentru finisare: planseele de etaj primesc balustrade, acoperisurile grinzi.
                    PlacedRect placed = new PlacedRect(
                        xMin + i * Cell, zMin + j * Cell,
                        xMin + i * Cell + sizeX, zMin + j * Cell + sizeZ,
                        top);

                    if (parent == gRoofs)
                    {
                        placed.Y = top - SlabThickness;
                        placedRoofs.Add(placed);
                    }
                    else
                    {
                        placedPlatforms.Add(placed);
                    }
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

            // Colliderul ramane un panou plin (nu te poti strecura printre bare);
            // ce se vede sunt stalpii si barele din RailingVisual.
            GameObject collider = CreateBox(
                parent,
                name,
                new Vector3((x0 + x1) * 0.5f, platformHeight + height * 0.5f, (z0 + z1) * 0.5f),
                size,
                pipeMat
            );

            Object.DestroyImmediate(collider.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(collider.GetComponent<MeshFilter>());

            RailingVisual(parent, name, x0, z0, x1, z1, platformHeight);
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

            placedRoofs.Add(new PlacedRect(xMin, zMin, xMax, zMax, height));
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

        /// <summary>Bloc solid (cladire mica, utilaj, stalp) pe dreptunghiul dat.</summary>
        private static void Block(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax, float height)
        {
            CreateBox(
                parent,
                name,
                new Vector3((xMin + xMax) * 0.5f, height * 0.5f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, height, zMax - zMin),
                wallMat
            );
        }

        /// <summary>Stanca: cutie rotita, asezata pe sol in basePosition.</summary>
        private static void Rock(Transform parent, string name,
            Vector3 basePosition, Vector3 size, float rotationY)
        {
            GameObject rock = CreateBox(
                parent,
                name,
                basePosition + Vector3.up * size.y * 0.5f,
                size,
                pipeMat
            );

            rock.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        /// <summary>
        /// Mobilier / cover cu dimensiuni libere (mese de laborator, birouri). Inaltimea este adusa
        /// la standard: sub 1,5 m devine cover de crouch (1,1 m), intre 1,5 si 1,9 m devine cover
        /// in picioare (2 m). Latura mare are minimum 2 m, ca obiectul sa ascunda un jucator.
        /// </summary>
        private static void Crate(Transform parent, float x, float z,
            float sizeX, float sizeY, float sizeZ, float baseY = 0f)
        {
            if (sizeY < 1.5f)
            {
                sizeY = CrouchCoverHeight;
            }
            else if (sizeY < 1.9f)
            {
                sizeY = StandingCoverHeight;
            }

            if (Mathf.Max(sizeX, sizeZ) < MinCoverWidth)
            {
                sizeX = MinCoverWidth;
                sizeZ = MinCoverWidth;
            }

            CreateBox(
                parent,
                $"Crate_{coverIndex++:000}",
                new Vector3(x, baseY + sizeY * 0.5f, z),
                new Vector3(sizeX, sizeY, sizeZ),
                coverMat
            );
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
            tank.AddComponent<MeshCollider>().convex = true;

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
            pipe.AddComponent<MeshCollider>().convex = true;

            FinishStaticObject(pipe, pipeMat);
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

            placedLights.Add(go.transform);
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

            placedPlatforms.Add(new PlacedRect(xMin, zMin, xMax, zMax, topHeight));
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

            // Complexul e acoperit si fara soare: pentru imagini punem temporar un soare si scoatem ceata.
            GameObject previewSun = new GameObject("PreviewSun (temp)") { hideFlags = HideFlags.HideAndDontSave };
            previewSun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            Light sunLight = previewSun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.1f;
            sunLight.shadows = LightShadows.Soft;

            bool fog = RenderSettings.fog;
            UnityEngine.Rendering.AmbientMode ambientMode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);

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

                // Subteranul: ascundem si peretii si podeaua, ca sa se vada santul tunelului.
                // Subteranul: ascundem tot ce e deasupra solului, ramane doar nivelul de -3,5 m.
                List<GameObject> hiddenGroups = new List<GameObject>();

                foreach (Transform group in root.transform)
                {
                    if (group.name == "Underground (Tunnel)" || group.name == "Lights")
                    {
                        continue;
                    }

                    if (group.gameObject.activeSelf)
                    {
                        group.gameObject.SetActive(false);
                        hiddenGroups.Add(group.gameObject);
                    }
                }

                Capture(
                    Path.Combine(docs, "BlacksiteK17_TopDown_Underground.png"),
                    new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f),
                    true, 61f, 1600, 1200
                );

                foreach (GameObject group in hiddenGroups)
                {
                    group.SetActive(true);
                }

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

                Object.DestroyImmediate(previewSun);
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
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
