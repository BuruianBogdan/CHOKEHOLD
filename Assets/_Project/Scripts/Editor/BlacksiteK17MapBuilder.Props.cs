using System.Collections.Generic;
using System.Linq;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Recuzita din Tripo (Prefabs/Props, din Tools/Blender/k17_tripo_props.py) pe harta.
    //
    // 1) Inlocuieste aspectul unor cutii de gameplay (coliziunea ramane exact aceeasi): rafturile, ventilatiile de pe
    //    acoperis, pupitrele si cover-ul jos din exterior (saci de nisip).
    // 2) Decor lipit de pereti, pe tema fiecarei zone. Fiecare piesa se pune doar daca: sta pe podea plata, are perete
    //    continuu in spate (nu in fata unei usi / ferestre), are cel putin 2,2 m liberi in fata, nu atinge alte obiecte
    //    si e departe de usi, scari, spawn-uri, pereti care se sparg si baricade.
    //
    // Merge si pe scena existenta (CHOKEHOLD > Map > Dress Tripo Props): sterge ce a pus inainte si reface totul,
    // deterministic, fara sa atinga restul hartii.
    public static partial class BlacksiteK17MapBuilder
    {
        private const string TripoPropsGroup = "Tripo Props";
        private const string TripoKitName = "TripoKit";

        private struct PropOrder
        {
            public string Prefab;
            public int Count;
            public bool Cluster;     // piesele se aduna langa prima (butoaie, lazi)

            public PropOrder(string prefab, int count, bool cluster = false)
            {
                Prefab = prefab;
                Count = count;
                Cluster = cluster;
            }
        }

        private static readonly Dictionary<string, PropOrder[]> ZoneProps = new Dictionary<string, PropOrder[]>
        {
            // Laborator si camere tehnice
            { "Mid Lab", new[] { new PropOrder("Workbench_Lab", 2), new PropOrder("Cryo_Capsule", 2, true), new PropOrder("Rack_Server", 2, true), new PropOrder("Cabinet_Electrical", 1) } },
            { "Lab Annex", new[] { new PropOrder("Workbench_Lab", 1), new PropOrder("Cryo_Capsule", 1), new PropOrder("Desk_Office", 1), new PropOrder("Cabinet_Electrical", 1) } },
            { "Control Room", new[] { new PropOrder("Rack_Server", 2, true), new PropOrder("Desk_Office", 1), new PropOrder("Cabinet_Electrical", 1) } },
            { "A Control", new[] { new PropOrder("Desk_Office", 1), new PropOrder("Cabinet_Electrical", 2, true), new PropOrder("Valve_Manifold_B", 1) } },
            { "Security Room", new[] { new PropOrder("Desk_Office", 1), new PropOrder("Locker_Row", 1), new PropOrder("Rack_Server", 1) } },
            { "Loading Office", new[] { new PropOrder("Desk_Office", 2), new PropOrder("Locker_Row", 1), new PropOrder("Cabinet_Electrical", 1) } },
            { "Bunker", new[] { new PropOrder("Rack_Server", 2, true), new PropOrder("Cabinet_Electrical", 2), new PropOrder("Locker_Row", 1), new PropOrder("Crate_Military", 2, true) } },

            // Instalatia de racire (A Pipes) si site-ul A
            { "A Pipes", new[] { new PropOrder("Valve_Manifold_B", 2), new PropOrder("Pump_Coolant", 1), new PropOrder("Tank_Pressure", 1), new PropOrder("Drum_Hazard", 3, true) } },
            { "Bombsite A", new[] { new PropOrder("Drum_Hazard", 3, true), new PropOrder("Tank_Pressure", 1), new PropOrder("Cable_Drum", 1) } },

            // Depozit, hangar, incarcare
            { "Depot", new[] { new PropOrder("Crate_Military", 3, true), new PropOrder("Cart_Tools", 1), new PropOrder("Drum_Hazard", 2, true) } },
            { "Hangar / Bombsite B", new[] { new PropOrder("Forklift", 1), new PropOrder("Cart_Tools", 2), new PropOrder("Generator_Diesel", 1), new PropOrder("Crate_Military", 3, true), new PropOrder("Drum_Hazard", 3, true), new PropOrder("Cable_Drum", 1) } },
            { "Loading Bay", new[] { new PropOrder("Forklift", 1), new PropOrder("Crate_Military", 4, true), new PropOrder("Shelf_Storage", 2, true), new PropOrder("Drum_Hazard", 2, true) } },

            // Exterior
            { "East Yard", new[] { new PropOrder("Light_Floodlight", 2), new PropOrder("Cable_Drum", 2, true), new PropOrder("Drum_Hazard", 3, true), new PropOrder("Barrier_Concrete", 2) } },
            { "T1 Spawn", new[] { new PropOrder("Crate_Military", 3, true), new PropOrder("Light_Floodlight", 1), new PropOrder("Generator_Diesel", 1), new PropOrder("Drum_Hazard", 2, true), new PropOrder("Barrier_Concrete", 2) } },
            { "T2 Spawn", new[] { new PropOrder("Crate_Military", 3, true), new PropOrder("Light_Floodlight", 1), new PropOrder("Drum_Hazard", 2, true), new PropOrder("Cable_Drum", 1) } },
            { "West Outpost", new[] { new PropOrder("Crate_Military", 2, true), new PropOrder("Light_Floodlight", 1), new PropOrder("Generator_Diesel", 1) } },
            { "South Courtyard", new[] { new PropOrder("Cable_Drum", 2, true), new PropOrder("Drum_Hazard", 3, true), new PropOrder("Barrier_Concrete", 2), new PropOrder("Light_Floodlight", 1) } },
            { "North Overlook", new[] { new PropOrder("Barrier_Concrete", 2), new PropOrder("Drum_Hazard", 2, true), new PropOrder("Cable_Drum", 1) } },
            { "T1 Ramp", new[] { new PropOrder("Drum_Hazard", 2, true), new PropOrder("Crate_Military", 1) } },
            { "Mid East Corridor", new[] { new PropOrder("Drum_Hazard", 2, true) } },

            // Tuneluri (doar piese subtiri pe pereti)
            { "North Service Tunnel", new[] { new PropOrder("Cabinet_Electrical", 2) } },
            { "East Service Tunnel", new[] { new PropOrder("Cabinet_Electrical", 2) } },
            { "South Tunnel", new[] { new PropOrder("Cabinet_Electrical", 2) } },
            { "Bunker West Link", new[] { new PropOrder("Cabinet_Electrical", 1) } },
        };

        private const float MinAisle = 2.2f;
        private const float KeepAway = 2.5f;

        [MenuItem("CHOKEHOLD/Map/Dress Tripo Props")]
        public static void DressTripoPropsMenu()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogWarning("Genereaza intai harta.");
                return;
            }

            groundLayer = LayerMask.NameToLayer("Ground");
            DressTripoProps(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        private static void DressTripoProps(GameObject root)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabBuilder.PrefabFolder}/Drum_Hazard.prefab") == null)
            {
                Debug.LogWarning("Recuzita Tripo lipseste: CHOKEHOLD > Map > Build Prop Prefabs.");
                return;
            }

            keepOutCache = null;
            ClearTripoProps(root);
            Physics.SyncTransforms();

            int swapped = SwapTripoVisuals(root);
            Physics.SyncTransforms();

            Transform group = new GameObject(TripoPropsGroup).transform;
            group.SetParent(root.transform, false);
            int placed = 0, missed = 0;
            List<string> report = new List<string>();

            foreach (MapZone zone in Object.FindObjectsByType<MapZone>(FindObjectsSortMode.None).OrderBy(z => z.DisplayName))
            {
                if (!ZoneProps.TryGetValue(zone.DisplayName, out PropOrder[] orders))
                {
                    continue;
                }

                List<PropWallSpot> spots = FindWallSpots(zone);
                Transform zoneGroup = new GameObject(zone.DisplayName.Replace("/", "-")).transform;
                zoneGroup.SetParent(group, false);

                foreach (PropOrder order in orders)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabBuilder.PrefabFolder}/{order.Prefab}.prefab");
                    if (prefab == null)
                    {
                        continue;
                    }

                    Vector3? anchor = null;
                    GameObject previous = null;
                    for (int i = 0; i < order.Count; i++)
                    {
                        IEnumerable<PropWallSpot> candidates = spots;
                        if (order.Cluster && anchor.HasValue)
                        {
                            // grupurile stau lipite una de alta (fara goluri de 20-60 cm in care te blochezi)
                            Vector3 a = anchor.Value;
                            candidates = FlushNeighbours(previous, prefab)
                                .Concat(spots.Where(s => (s.Point - a).sqrMagnitude < 4.5f * 4.5f).OrderBy(s => (s.Point - a).sqrMagnitude));
                        }

                        GameObject instance = TryPlace(prefab, candidates, zone, zoneGroup);
                        if (instance == null)
                        {
                            missed++;
                            report.Add($"{zone.DisplayName}: {order.Prefab} #{i + 1}");
                            continue;
                        }

                        anchor ??= instance.transform.position;
                        previous = instance;
                        placed++;
                    }
                }
            }

            // verificarea finala, pe toate partile fiecarei piese (si fata de ce s-a pus dupa ea): o piesa care tot
            // lasa un gol in care nu incape un om e scoasa
            Physics.SyncTransforms();
            foreach (BoxCollider prop in group.GetComponentsInChildren<BoxCollider>().ToList())
            {
                if (LeavesTrapGap(prop))
                {
                    report.Add($"scos (gol ingust): {prop.transform.parent.name}/{prop.name}");
                    Object.DestroyImmediate(prop.gameObject);
                    Physics.SyncTransforms();
                    placed--;
                    missed++;
                }
            }

            Debug.Log($"Tripo props: {placed} piese puse, {swapped} cutii cu aspect nou, {missed} fara loc" +
                      (report.Count > 0 ? " (" + string.Join("; ", report) + ")" : ""));
        }

        /// <summary>Locurile lipite de piesa anterioara din grup, la stanga si la dreapta ei, pe acelasi perete.</summary>
        private static IEnumerable<PropWallSpot> FlushNeighbours(GameObject previous, GameObject prefab)
        {
            if (previous == null)
            {
                yield break;
            }

            Vector3 n = previous.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, n);
            Vector3 prevSize = previous.GetComponent<BoxCollider>().size;
            Vector3 size = prefab.GetComponent<BoxCollider>().size;
            Vector3 wall = previous.transform.position - n * (prevSize.z / 2f + 0.04f);
            float step = prevSize.x / 2f + size.x / 2f + 0.03f;
            yield return new PropWallSpot { Point = wall + right * step, Normal = n };
            yield return new PropWallSpot { Point = wall - right * step, Normal = n };
        }

        private static void ClearTripoProps(GameObject root)
        {
            Transform old = root.transform.Find(TripoPropsGroup);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == TripoKitName).ToList())
            {
                Transform kit = t.parent.Find("Kit");
                if (kit != null)
                {
                    kit.gameObject.SetActive(true);
                }
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // ------------------------------------------------------------------
        // Aspect nou pentru cutiile de gameplay (coliziunea ramane a cutiei)
        // ------------------------------------------------------------------

        private static int SwapTripoVisuals(GameObject root)
        {
            int count = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).ToList())
            {
                BoxCollider box = t.GetComponent<BoxCollider>();
                Transform kit = t.Find("Kit");
                if (box == null || kit == null)
                {
                    continue;
                }

                Bounds b = box.bounds;
                string n = t.name;
                bool done = false;

                if (n.StartsWith("Depot_Shelf_") || n == "Office_Shelf")
                {
                    done = SwapShelves(t, kit, b);
                }
                else if (n.StartsWith("Roof_HVAC_"))
                {
                    done = SwapSingle(t, kit, b, "Ventilation_Unit");
                }
                else if (n.StartsWith("LowCover_") && IsOutdoors(b))
                {
                    done = SwapTiled(t, kit, b, "Sandbag_Wall", 2f);
                }
                else if (n.StartsWith("Crate_") && kit.GetComponentsInChildren<MeshFilter>().Any(f => f.sharedMesh != null && f.sharedMesh.name == "K17_Console"))
                {
                    done = SwapTiled(t, kit, b, "Console_Control", 2f, faceOpenSide: true);
                }

                if (done)
                {
                    kit.gameObject.SetActive(false);
                    count++;
                }
            }
            return count;
        }

        private static bool IsOutdoors(Bounds b)
        {
            return !Physics.Raycast(b.center + Vector3.up * (b.extents.y + 0.1f), Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>O piesa intinsa exact pe cutie (latimea pe latura lunga).</summary>
        private static bool SwapSingle(Transform box, Transform kit, Bounds b, string prefabName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabBuilder.PrefabFolder}/{prefabName}.prefab");
            if (prefab == null)
            {
                return false;
            }

            Transform holder = NewTripoKit(box);
            bool alongX = b.size.x >= b.size.z;
            Vector3 native = prefab.GetComponent<BoxCollider>().size;
            GameObject piece = PlaceVisual(prefab, holder, new Vector3(b.center.x, b.min.y, b.center.z), alongX ? 0f : 90f);
            float length = alongX ? b.size.x : b.size.z;
            float depth = alongX ? b.size.z : b.size.x;
            piece.transform.localScale = new Vector3(length / native.x, b.size.y / native.y, depth / native.z);
            return true;
        }

        /// <summary>Piese in sir pe latura lunga a cutiei (saci de nisip, pupitre).</summary>
        private static bool SwapTiled(Transform box, Transform kit, Bounds b, string prefabName, float nominal, bool faceOpenSide = false)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabBuilder.PrefabFolder}/{prefabName}.prefab");
            if (prefab == null)
            {
                return false;
            }

            Transform holder = NewTripoKit(box);
            bool alongX = b.size.x >= b.size.z;
            float length = alongX ? b.size.x : b.size.z;
            float depth = alongX ? b.size.z : b.size.x;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(length / nominal));
            float tile = length / tiles;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Vector3 side = alongX ? Vector3.forward : Vector3.right;

            // fata spre partea cu mai mult loc (pupitrele: operatorul sta acolo)
            float yaw = alongX ? 0f : 90f;
            if (faceOpenSide)
            {
                float plus = OpenDistance(b.center, side, depth);
                float minus = OpenDistance(b.center, -side, depth);
                Vector3 front = plus >= minus ? side : -side;
                yaw = Quaternion.LookRotation(front).eulerAngles.y;
            }

            Vector3 native = prefab.GetComponent<BoxCollider>().size;
            for (int i = 0; i < tiles; i++)
            {
                Vector3 center = b.center + axis * (-length / 2f + tile * (i + 0.5f));
                GameObject piece = PlaceVisual(prefab, holder, new Vector3(center.x, b.min.y, center.z), yaw);
                piece.transform.localScale = new Vector3(tile / native.x, b.size.y / native.y, depth / native.z);
            }
            return true;
        }

        /// <summary>Rafturi: doua randuri spate in spate pe toata lungimea cutiei, cu fata spre ambele culoare.</summary>
        private static bool SwapShelves(Transform box, Transform kit, Bounds b)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabBuilder.PrefabFolder}/Shelf_Storage.prefab");
            if (prefab == null)
            {
                return false;
            }

            Transform holder = NewTripoKit(box);
            bool alongX = b.size.x >= b.size.z;
            float length = alongX ? b.size.x : b.size.z;
            float depth = (alongX ? b.size.z : b.size.x) / 2f;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            float tile = length / tiles;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Vector3 side = alongX ? Vector3.forward : Vector3.right;
            Vector3 native = prefab.GetComponent<BoxCollider>().size;

            foreach (float s in new[] { 1f, -1f })
            {
                for (int i = 0; i < tiles; i++)
                {
                    Vector3 center = b.center + axis * (-length / 2f + tile * (i + 0.5f)) + side * (s * depth / 2f);
                    float yaw = Quaternion.LookRotation(side * s).eulerAngles.y;
                    GameObject piece = PlaceVisual(prefab, holder, new Vector3(center.x, b.min.y, center.z), yaw);
                    piece.transform.localScale = new Vector3(tile / native.x, b.size.y / native.y, depth / native.z);
                }
            }
            return true;
        }

        private static float OpenDistance(Vector3 center, Vector3 direction, float depth)
        {
            Vector3 origin = center + direction * (depth / 2f + 0.05f);
            return Physics.Raycast(origin, direction, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 20f;
        }

        private static Transform NewTripoKit(Transform box)
        {
            Transform holder = new GameObject(TripoKitName).transform;
            holder.SetParent(box, false);
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            // cutiile au scara (marimea lor): suportul o anuleaza, ca piesele sa aiba scara in lume
            Vector3 s = box.lossyScale;
            holder.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            return holder;
        }

        /// <summary>Piesa doar vizuala (fara collider), cu scara in lume independenta de cutie.</summary>
        private static GameObject PlaceVisual(GameObject prefab, Transform holder, Vector3 position, float yaw)
        {
            GameObject neutral = new GameObject(prefab.name);
            neutral.transform.SetParent(holder, false);
            neutral.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, neutral.transform);
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localRotation = Quaternion.identity;
            Object.DestroyImmediate(piece.GetComponent<BoxCollider>());
            FinishProp(neutral);
            return neutral;
        }

        // ------------------------------------------------------------------
        // Decor lipit de pereti
        // ------------------------------------------------------------------

        private struct PropWallSpot
        {
            public Vector3 Point;     // pe perete, la nivelul podelei
            public Vector3 Normal;    // spre camera
        }

        private static List<PropWallSpot> FindWallSpots(MapZone zone)
        {
            Vector3 c = zone.transform.position;
            Vector3 half = new Vector3(zone.Size.x / 2f, 0f, zone.Size.z / 2f);
            float floorY = c.y;
            Dictionary<Vector3Int, PropWallSpot> spots = new Dictionary<Vector3Int, PropWallSpot>();

            for (float x = -half.x + 1f; x <= half.x - 1f + 0.01f; x += 3f)
            {
                for (float z = -half.z + 1f; z <= half.z - 1f + 0.01f; z += 3f)
                {
                    Vector3 origin = new Vector3(c.x + x, floorY + 1f, c.z + z);
                    if (Physics.CheckSphere(origin, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    for (int a = 0; a < 24; a++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward;
                        if (!Physics.Raycast(origin, dir, out RaycastHit hit, 16f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z);
                        if (Mathf.Abs(hit.normal.y) > 0.2f || n.sqrMagnitude < 0.5f || !IsStructuralWall(hit.collider))
                        {
                            continue;
                        }

                        // normalele aliniate la axe (peretii hartii sunt pe axe), ca piesele sa stea drept
                        n = Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector3(Mathf.Sign(n.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(n.z));
                        Vector3 p = new Vector3(hit.point.x, floorY, hit.point.z);
                        if (Mathf.Abs(p.x - c.x) > half.x + 0.5f || Mathf.Abs(p.z - c.z) > half.z + 0.5f)
                        {
                            continue;
                        }

                        Vector3Int key = new Vector3Int(Mathf.RoundToInt(p.x * 2f), Mathf.RoundToInt(p.z * 2f), Mathf.RoundToInt(n.x * 2f + n.z * 4f));
                        spots[key] = new PropWallSpot { Point = p, Normal = n };
                    }
                }
            }

            // ordine stabila, amestecata dupa pozitie (nu toate piesele in acelasi colt)
            return spots.Values.OrderBy(s => Hash(s.Point)).ToList();
        }

        private static int Hash(Vector3 p)
        {
            unchecked
            {
                int h = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.z * 10f) * 19349663;
                return h * 83492791;
            }
        }

        private static bool IsStructuralWall(Collider collider)
        {
            if (collider == null || collider.isTrigger || !collider.gameObject.isStatic)
            {
                return false;
            }

            Transform t = collider.transform;
            if (t.GetComponentInParent<Destructible>() != null || t.GetComponentInParent<BarricadePoint>() != null ||
                t.GetComponentInParent<SimpleDoor>() != null || PathContains(t, TripoPropsGroup) || PathContains(t, "Cover"))
            {
                return false;
            }

            return collider.bounds.size.y >= 2.4f;
        }

        private static bool PathContains(Transform t, string name)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static GameObject TryPlace(GameObject prefab, IEnumerable<PropWallSpot> candidates, MapZone zone, Transform parent)
        {
            BoxCollider box = prefab.GetComponent<BoxCollider>();
            Vector3 size = box.size;
            float floorY = zone.transform.position.y;
            bool narrow = Mathf.Min(zone.Size.x, zone.Size.z) <= 6f;
            if (narrow && size.z > 0.65f)
            {
                return null;    // in tuneluri si culoare doar piese subtiri
            }

            foreach (PropWallSpot spot in candidates)
            {
                Vector3 n = spot.Normal;
                Vector3 right = Vector3.Cross(Vector3.up, n);
                Vector3 pos = spot.Point + n * (size.z / 2f + 0.04f);
                Quaternion rotation = Quaternion.LookRotation(n);

                if (!SpotIsValid(pos, rotation, n, right, size, floorY))
                {
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(new Vector3(pos.x, floorY, pos.z), rotation);
                FinishProp(instance);
                Physics.SyncTransforms();
                return instance;
            }
            return null;
        }

        private static bool SpotIsValid(Vector3 pos, Quaternion rotation, Vector3 n, Vector3 right, Vector3 size, float floorY)
        {
            float hw = size.x / 2f, hd = size.z / 2f, h = size.y;

            // podea plata sub toata piesa
            foreach (Vector3 offset in new[] { Vector3.zero, right * hw + n * hd, -right * hw + n * hd, right * hw - n * hd, -right * hw - n * hd })
            {
                Vector3 p = new Vector3(pos.x, floorY + 0.5f, pos.z) + offset * 0.9f;
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit floor, 1f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(floor.point.y - floorY) > 0.12f)
                {
                    return false;
                }
            }

            // locul e liber
            Vector3 center = new Vector3(pos.x, floorY + h / 2f + 0.06f, pos.z);
            if (Physics.CheckBox(center, new Vector3(hw - 0.02f, h / 2f - 0.05f, hd - 0.02f), rotation, ~0, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            // perete continuu in spate, pe toata latimea si inaltimea, si inca 1,5 m in fiecare parte (nu langa usi)
            float top = Mathf.Min(h, 2.2f) - 0.1f;
            foreach (float x in new[] { -hw - 1.5f, -hw - 0.75f, -hw + 0.1f, 0f, hw - 0.1f, hw + 0.75f, hw + 1.5f })
            {
                foreach (float y in new[] { 0.6f, top })
                {
                    Vector3 origin = new Vector3(pos.x, floorY + y, pos.z) + right * x;
                    if (!Physics.Raycast(origin, -n, out RaycastHit wall, hd + 0.3f, ~0, QueryTriggerInteraction.Ignore) || !IsStructuralWall(wall.collider))
                    {
                        return false;
                    }
                }
            }

            // culoar liber in fata, pe toata latimea piesei
            foreach (float x in new[] { -hw + 0.05f, 0f, hw - 0.05f })
            {
                foreach (float y in new[] { 0.3f, 1f, 1.7f })
                {
                    Vector3 origin = new Vector3(pos.x, floorY + y, pos.z) + n * (hd + 0.02f) + right * x;
                    if (Physics.Raycast(origin, n, MinAisle, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return false;
                    }
                }
            }

            // pe laterale: ori lipita de ceva, ori loc de trecut pentru un om. Un gol mai mic e o capcana in care
            // jucatorul intra si se blocheaza (sau pare ca poate trece si nu poate).
            if (!SidesPassable(new Vector3(pos.x, floorY, pos.z), n, right, hw, hd))
            {
                return false;
            }

            // departe de usi, scari, spawn-uri, pereti care se sparg, baricade si zonele de plantat
            Vector3 flat = new Vector3(pos.x, floorY, pos.z);
            foreach (Bounds keep in KeepOutBounds())
            {
                Bounds grown = keep;
                grown.Expand(new Vector3(KeepAway * 2f + size.x, 2f, KeepAway * 2f + size.x));
                if (grown.Contains(flat + Vector3.up * 1f))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Latimea minima a unui gol prin care trece jucatorul (hull 0,8 m + marja).</summary>
        public const float PassWidth = 1.0f;
        /// <summary>Sub atat, doua obiecte sunt considerate lipite (nu ramane gol).</summary>
        public const float FlushGap = 0.06f;

        /// <summary>
        /// Gol intre FlushGap si PassWidth pe oricare parte a piesei (raze din fetele ei, la 3 inaltimi, cu podea in gol).
        /// </summary>
        public static bool LeavesTrapGap(BoxCollider box)
        {
            Transform t = box.transform;
            Vector3 size = Vector3.Scale(box.size, t.lossyScale);
            Vector3 floor = t.TransformPoint(new Vector3(box.center.x, box.center.y - box.size.y / 2f, box.center.z));
            Vector3[] axes = { t.right, -t.right, t.forward, -t.forward };
            float[] reach = { size.x / 2f, size.x / 2f, size.z / 2f, size.z / 2f };
            float[] across = { size.z / 2f, size.z / 2f, size.x / 2f, size.x / 2f };

            for (int a = 0; a < 4; a++)
            {
                Vector3 dir = axes[a];
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                foreach (float u in new[] { -0.97f, -0.5f, 0f, 0.5f, 0.97f })
                {
                    foreach (float y in new[] { 0.3f, 1f, 1.7f })
                    {
                        if (y > size.y - 0.05f)
                        {
                            continue;
                        }

                        Vector3 origin = floor + Vector3.up * y + side * (across[a] * u) + dir * (reach[a] + 0.01f);
                        if (!Physics.Raycast(origin, dir, out RaycastHit hit, PassWidth, ~0, QueryTriggerInteraction.Ignore) ||
                            hit.collider == box || hit.distance <= FlushGap)
                        {
                            continue;
                        }

                        Vector3 mid = origin + dir * (hit.distance / 2f);
                        if (Physics.Raycast(mid + Vector3.up * 0.2f, Vector3.down, y + 0.6f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool SidesPassable(Vector3 floor, Vector3 n, Vector3 right, float hw, float hd)
        {
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 dir = right * s;
                foreach (float z in new[] { -hd + 0.08f, 0f, hd - 0.08f })
                {
                    foreach (float y in new[] { 0.25f, 1f, 1.7f })
                    {
                        Vector3 origin = floor + Vector3.up * y + n * z + dir * (hw + 0.01f);
                        if (Physics.Raycast(origin, dir, out RaycastHit hit, PassWidth, ~0, QueryTriggerInteraction.Ignore) && hit.distance > FlushGap)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static List<Bounds> keepOutCache;

        private static IEnumerable<Bounds> KeepOutBounds()
        {
            if (keepOutCache != null)
            {
                return keepOutCache;
            }

            keepOutCache = new List<Bounds>();
            void Add(Component c)
            {
                // conturul pieselor vizibile (colliderele usilor se pot opri cand usa e deschisa: atunci n-au contur)
                Renderer[] rs = c.GetComponentsInChildren<Renderer>(true);
                Collider col = c.GetComponentInChildren<Collider>(true);
                Bounds b = new Bounds(c.transform.position + Vector3.up, Vector3.one);
                if (rs.Length > 0)
                {
                    b = rs[0].bounds;
                    foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                }
                else if (col != null && col.enabled)
                {
                    b = col.bounds;
                }
                keepOutCache.Add(b);
            }

            foreach (SimpleDoor c in Object.FindObjectsByType<SimpleDoor>(FindObjectsSortMode.None)) Add(c);
            foreach (Ladder c in Object.FindObjectsByType<Ladder>(FindObjectsSortMode.None)) Add(c);
            foreach (SpawnPoint c in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None)) Add(c);
            foreach (Destructible c in Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None)) Add(c);
            foreach (BarricadePoint c in Object.FindObjectsByType<BarricadePoint>(FindObjectsSortMode.None)) Add(c);

            // ramele golurilor de usa, usile rulante si trapele sunt doar decor (fara componente), dar tot usi sunt
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = r.name;
                if ((n.StartsWith("Jamb") || n.Contains("Roll_Door") || n.Contains("Hatch") || n.Contains("Door")) && !PathContains(r.transform, TripoPropsGroup))
                {
                    keepOutCache.Add(r.bounds);
                }
            }
            return keepOutCache;
        }

        private static void FinishProp(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = groundLayer;
                // recuzita nu ascunde nimic in occlusion culling (piese mici, multe goluri)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }
    }
}
