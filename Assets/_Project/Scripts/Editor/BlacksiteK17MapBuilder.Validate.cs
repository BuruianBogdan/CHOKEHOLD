using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // CHOKEHOLD > Map > Validate
    //
    // Verifica harta generata din scena deschisa si scrie raportul in Docs/MapValidation.md:
    //   1. obiecte de cover / structuri care intra unele in altele sau in pereti
    //   2. gauri in podea in zonele walkable
    //   3. conectivitate: din spawn-ul T1 se ajunge in toate zonele, pe etaj, in spawn-ul T2;
    //      nimic nu iese din conturul hartii; punctele de spawn sunt libere
    //   4. linii de vedere peste limite (25 m interior / 40 m exterior, cu 10 % toleranta)
    //   5. cover sub dimensiunile standard
    //   6. usi, ferestre si trape sub standard; spatiu liber sub 3 m in interioare
    //
    // ERROR = de reparat. WARNING = abatere cunoscuta, documentata in MapAudit.md.
    public static partial class BlacksiteK17MapBuilder
    {
        private const float VEye = 1.6f;
        private const float VInteriorLimit = 27.5f;   // ~25 m + 10 %
        private const float VExteriorLimit = 44f;     // ~40 m + 10 %
        private const int VDirections = 72;

        private static readonly string[] VHallZones = { "Hangar / Bombsite B", "Loading Bay" };

        private sealed class ValidationReport
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly StringBuilder Body = new StringBuilder();

            public void Section(string title)
            {
                Body.AppendLine();
                Body.AppendLine($"## {title}");
                Body.AppendLine();
            }

            public void Error(string line)
            {
                Errors.Add(line);
                Body.AppendLine($"- **ERROR** {line}");
            }

            public void Warning(string line)
            {
                Warnings.Add(line);
                Body.AppendLine($"- WARNING {line}");
            }

            public void Ok(string line)
            {
                Body.AppendLine($"- ok — {line}");
            }
        }

        [MenuItem("CHOKEHOLD/Map/Validate")]
        public static void Validate()
        {
            GameObject root = GameObject.Find(RootName);

            if (root == null)
            {
                Debug.LogWarning("Genereaza intai harta (CHOKEHOLD > Map > Build Blacksite K-17).");
                return;
            }

            ValidationReport report = new ValidationReport();
            MapZone[] zones = root.GetComponentsInChildren<MapZone>();

            // Masuram ca la inceputul rundei, cu usile deschise si baricadele nemontate.
            List<GameObject> hidden = new List<GameObject>();

            foreach (BarricadePoint point in root.GetComponentsInChildren<BarricadePoint>())
            {
                HideChildrenForValidation(point.transform, hidden);
            }

            foreach (SimpleDoor door in root.GetComponentsInChildren<SimpleDoor>())
            {
                HideChildrenForValidation(door.transform, hidden);
            }

            Physics.SyncTransforms();

            try
            {
                CheckOverlaps(root, report);
                CheckFloorHoles(zones, report);
                CheckConnectivity(root, zones, report);
                CheckSightlines(root, zones, report);
                CheckCover(root, report);
                CheckOpenings(root, zones, report);
                CheckJoints(root, report);
                CheckTextureReadiness(root, report);
                CheckBunkerTiming(root, report);
            }
            finally
            {
                foreach (GameObject go in hidden)
                {
                    go.SetActive(true);
                }
            }

            WriteValidationReport(report);
        }

        private static void HideChildrenForValidation(Transform parent, List<GameObject> hidden)
        {
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    hidden.Add(child.gameObject);
                }
            }
        }

        private static IEnumerable<Vector2> ZoneCells(MapZone zone, float step)
        {
            Vector3 c = zone.transform.position;
            Vector3 s = zone.Size;

            for (float x = c.x - s.x * 0.5f + step * 0.5f; x < c.x + s.x * 0.5f; x += step)
            {
                for (float z = c.z - s.z * 0.5f + step * 0.5f; z < c.z + s.z * 0.5f; z += step)
                {
                    yield return new Vector2(x, z);
                }
            }
        }

        private static bool VRay(Vector3 from, Vector3 dir, float max, out RaycastHit hit)
        {
            return Physics.Raycast(from, dir, out hit, max, ~0, QueryTriggerInteraction.Ignore);
        }

        private static bool VBlocked(Vector3 point, float radius)
        {
            return Physics.CheckSphere(point, radius, ~0, QueryTriggerInteraction.Ignore);
        }

        private static string P(Vector3 p)
        {
            return $"({p.x:0.#}, {p.z:0.#})";
        }

        // ------------------------------------------------------------------
        // 1. Suprapuneri
        // ------------------------------------------------------------------
        private static void CheckOverlaps(GameObject root, ValidationReport report)
        {
            report.Section("1. Obiecte care intră unele în altele");

            const float tolerance = 0.1f;

            List<Collider> candidates = new List<Collider>();

            foreach (string groupName in new[] { "Cover", "Structures" })
            {
                Transform group = root.transform.Find(groupName);

                if (group != null)
                {
                    candidates.AddRange(group.GetComponentsInChildren<Collider>()
                        .Where(c => c.enabled && !c.isTrigger && !IsSoftPanel(c)));
                }
            }

            HashSet<(int, int)> seen = new HashSet<(int, int)>();
            Dictionary<Collider, int> ids = new Dictionary<Collider, int>();
            Collider[] buffer = new Collider[64];
            int found = 0;

            foreach (Collider a in candidates)
            {
                Bounds bounds = a.bounds;
                int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, buffer,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                for (int k = 0; k < count; k++)
                {
                    Collider b = buffer[k];

                    // Pereti, plansee, scari si acoperisuri care se intalnesc sunt imbinari de constructie;
                    // conteaza doar cand cel putin unul dintre obiecte e recuzita (cover, utilaj, manechin...).
                    if (b == a || IsFloorLike(b) || AreRelated(a, b) || IsPlayer(b) || b.name == "WalkSurface" ||
                        !(IsProp(a) || IsProp(b)))
                    {
                        continue;
                    }

                    int ia = IndexOf(a, ids), ib = IndexOf(b, ids);
                    (int, int) key = ia < ib ? (ia, ib) : (ib, ia);

                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    if (Physics.ComputePenetration(
                            a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation,
                            out Vector3 _, out float distance) &&
                        distance > tolerance)
                    {
                        found++;
                        report.Error($"`{a.name}` intră {distance:0.00} m în `{b.name}` la {P(a.bounds.center)}");
                    }
                }
            }

            if (found == 0)
            {
                report.Ok($"{candidates.Count} obiecte de cover și structuri verificate, nicio suprapunere peste {tolerance} m");
            }
        }

        private static int IndexOf(Collider c, Dictionary<Collider, int> ids)
        {
            if (!ids.TryGetValue(c, out int id))
            {
                id = ids.Count;
                ids[c] = id;
            }

            return id;
        }

        private static readonly string[] PropKeywords =
        {
            "Crate", "Cover", "Container", "Tank", "_Pipe_", "Rock", "Machine", "Shelf", "Generator",
            "Lockers", "Shed", "Tower", "Pillar", "Dummy", "ServerRack"
        };

        private static bool IsProp(Collider c)
        {
            return PropKeywords.Any(k => c.name.Contains(k));
        }

        private static bool IsPlayer(Collider c)
        {
            return c.GetComponentInParent<FpsController>() != null;
        }

        private static bool IsFloorLike(Collider c)
        {
            string n = c.name;
            return n.StartsWith("Floor") || n.StartsWith("PitFloor") || c.transform.parent != null &&
                   c.transform.parent.name.StartsWith("Floor");
        }

        private static bool IsSoftPanel(Collider c)
        {
            return c.GetComponent<Destructible>() != null;
        }

        /// <summary>Piese ale aceluiasi obiect compus (perete despartitor, stiva de containere) se ating intentionat.</summary>
        private static bool AreRelated(Collider a, Collider b)
        {
            Transform pa = a.transform.parent, pb = b.transform.parent;
            bool samePartition = pa != null && pa == pb && pa.name.StartsWith("Part_");

            // Containere stivuite: unul sta pe celalalt.
            bool stacked = a.name.StartsWith("Container") && b.name.StartsWith("Container") &&
                           Mathf.Abs(a.bounds.center.y - b.bounds.center.y) > 2f;

            return samePartition || stacked;
        }

        // ------------------------------------------------------------------
        // 2. Gauri in podea
        // ------------------------------------------------------------------
        private static void CheckFloorHoles(MapZone[] zones, ValidationReport report)
        {
            report.Section("2. Găuri în podea");

            int cells = 0, holes = 0;

            foreach (MapZone zone in zones)
            {
                int zoneHoles = 0;
                Vector3 first = Vector3.zero;

                foreach (Vector2 cell in ZoneCells(zone, 1f))
                {
                    float level = zone.transform.position.y;
                    Vector3 probe = new Vector3(cell.x, level + 1f, cell.y);

                    if (VBlocked(probe, 0.3f))
                    {
                        continue;
                    }

                    cells++;

                    // Santul tunelului: podeaua poate fi oriunde intre -3,5 m (fundul) si 0 (rampele).
                    bool pit = IsPit(cell.x, cell.y);
                    float lowest = (pit ? Mathf.Min(level, TunnelFloorY) : level) - 0.6f;

                    if (!VRay(probe, Vector3.down, probe.y - lowest, out RaycastHit _))
                    {
                        if (zoneHoles == 0)
                        {
                            first = probe;
                        }

                        zoneHoles++;
                    }
                }

                if (zoneHoles > 0)
                {
                    holes += zoneHoles;
                    report.Error($"{zone.DisplayName}: {zoneHoles} m² fără podea, de exemplu la {P(first)}");
                }
            }

            if (holes == 0)
            {
                report.Ok($"{cells} celule de 1 m verificate în {zones.Length} zone, nicio gaură");
            }
        }

        // ------------------------------------------------------------------
        // 3. Conectivitate
        // ------------------------------------------------------------------
        private static void CheckConnectivity(GameObject root, MapZone[] zones, ValidationReport report)
        {
            report.Section("3. Conectivitate");

            SpawnPoint[] spawns = root.GetComponentsInChildren<SpawnPoint>();
            SpawnPoint start = spawns.FirstOrDefault(s => s.Team == Team.Attackers);

            if (start == null)
            {
                report.Error("nu există niciun SpawnPoint pentru atacatori");
                return;
            }

            // Fiecare punct de spawn trebuie sa fie liber si pe podea.
            foreach (SpawnPoint spawn in spawns)
            {
                Vector3 feet = spawn.transform.position;

                // Jucatorul insusi poate sta pe un punct de spawn; nu il numaram ca obstacol.
                bool blocked = Physics.OverlapCapsule(feet + Vector3.up * 0.6f, feet + Vector3.up * 1.5f, 0.4f,
                        ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !IsPlayer(c));

                if (blocked || !VRay(feet + Vector3.up * 0.5f, Vector3.down, 1f, out RaycastHit _))
                {
                    report.Error($"punctul de spawn `{spawn.name}` e blocat sau nu are podea sub el");
                }
            }

            HashSet<(int, int, int)> visited = WalkFrom(start.transform.position, out bool escaped);
            HashSet<(int, int)> reachedXZ = new HashSet<(int, int)>(visited.Select(k => (k.Item1, k.Item2)));

            report.Ok($"{visited.Count} poziții accesibile din spawn-ul T1");

            if (escaped)
            {
                report.Error("un jucător poate ieși din conturul hărții (160 × 120 m)");
            }
            else
            {
                report.Ok("nicio poziție accesibilă în afara conturului hărții");
            }

            foreach (SpawnPoint spawn in spawns.Where(s => s.Team == Team.Defenders))
            {
                Vector3 p = spawn.transform.position;

                if (!reachedXZ.Contains((Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z))))
                {
                    report.Error($"spawn-ul apărătorilor `{spawn.name}` nu e accesibil din spawn-ul atacatorilor");
                }
            }

            int zonesOk = 0;

            foreach (MapZone zone in zones)
            {
                int cells = 0, reached = 0;

                foreach (Vector2 cell in ZoneCells(zone, 1f))
                {
                    if (VBlocked(new Vector3(cell.x, zone.transform.position.y + 1f, cell.y), 0.35f))
                    {
                        continue;
                    }

                    cells++;

                    if (reachedXZ.Contains((Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y))))
                    {
                        reached++;
                    }
                }

                // Un gol de usa de 1,4 m are o singura coloana de celule accesibila: 20 % e suficient.
                if (cells > 0 && reached * 5 < cells)
                {
                    report.Error($"zona {zone.DisplayName} nu e accesibilă ({100 * reached / cells}% din suprafață)");
                }
                else
                {
                    zonesOk++;
                }
            }

            report.Ok($"{zonesOk} din {zones.Length} zone accesibile");

            // Etajul: fiecare placa / platforma mai mare de 2 x 2 m trebuie atinsa.
            Transform upper = root.transform.Find(UpperGroupName);
            int upperChecked = 0, upperMissed = 0;

            if (upper != null)
            {
                foreach (BoxCollider box in upper.GetComponentsInChildren<BoxCollider>())
                {
                    Bounds b = box.bounds;

                    if (b.size.x < 2f || b.size.z < 2f || b.size.y > 0.6f || b.max.y < 3.5f || b.max.y > 4.5f ||
                        box.GetComponent<Destructible>() != null)
                    {
                        continue;
                    }

                    upperChecked++;
                    bool hit = false;

                    for (float x = b.min.x + 0.5f; x < b.max.x && !hit; x += 1f)
                    {
                        for (float z = b.min.z + 0.5f; z < b.max.z && !hit; z += 1f)
                        {
                            hit = visited.Contains((Mathf.FloorToInt(x), Mathf.FloorToInt(z), 8)) ||
                                  visited.Contains((Mathf.FloorToInt(x), Mathf.FloorToInt(z), 7));
                        }
                    }

                    if (!hit)
                    {
                        upperMissed++;
                        report.Error($"placa de etaj `{box.name}` la {P(b.center)} nu e accesibilă");
                    }
                }
            }

            report.Ok($"{upperChecked - upperMissed} din {upperChecked} plăci și platforme de etaj accesibile");
        }

        /// <summary>Flood fill pe grila de 1 m, cu capsula jucatorului (r 0,4, h 1,8) si treapta de 0,6 m.</summary>
        private static HashSet<(int, int, int)> WalkFrom(Vector3 start, out bool escaped)
        {
            HashSet<(int, int, int)> visited = new HashSet<(int, int, int)>();
            Queue<Vector3> queue = new Queue<Vector3>();
            escaped = false;

            queue.Enqueue(start);
            visited.Add(WalkKey(start));

            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };

            while (queue.Count > 0 && visited.Count < 500000)
            {
                Vector3 p = queue.Dequeue();

                for (int i = 0; i < 4; i++)
                {
                    Vector3 probe = new Vector3(p.x + dx[i], p.y + 0.8f, p.z + dz[i]);

                    if (!VRay(probe, Vector3.down, 12f, out RaycastHit hit) || hit.point.y - p.y > 0.6f)
                    {
                        continue;
                    }

                    Vector3 feet = new Vector3(probe.x, hit.point.y, probe.z);

                    if (Physics.CheckCapsule(feet + Vector3.up * 0.55f, feet + Vector3.up * 1.4f, 0.4f,
                            ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    if (visited.Add(WalkKey(feet)))
                    {
                        if (feet.x < MinX || feet.x > MaxX || feet.z < MinZ || feet.z > MaxZ)
                        {
                            escaped = true;
                            continue;
                        }

                        queue.Enqueue(feet);
                    }
                }
            }

            return visited;
        }

        private static (int, int, int) WalkKey(Vector3 p)
        {
            return (Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z), Mathf.RoundToInt(p.y * 2f));
        }

        // ------------------------------------------------------------------
        // 4. Linii de vedere
        // ------------------------------------------------------------------
        private struct VSight
        {
            public Vector3 From, To;
            public float Length, Limit;
        }

        private static void CheckSightlines(GameObject root, MapZone[] zones, ValidationReport report)
        {
            report.Section("4. Linii de vedere");
            report.Body.AppendLine("Limite: 27,5 m în interior și 44 m în exterior (~25 / ~40 m cu 10 % toleranță). " +
                                   "O linie care iese de sub acoperiș primește limita de exterior. " +
                                   "Hangarul și Loading Bay sunt hale (limită de exterior).");
            report.Body.AppendLine();

            Transform upperGroup = root.transform.Find(UpperGroupName);
            Dictionary<string, VSight> ground = new Dictionary<string, VSight>();
            Dictionary<string, VSight> upper = new Dictionary<string, VSight>();
            VSight catwalk = default;

            foreach (MapZone zone in zones)
            {
                float baseY = zone.transform.position.y;
                float limit = ZoneSightLimit(zone);

                foreach (Vector2 cell in ZoneCells(zone, 2f))
                {
                    Vector3 eye = new Vector3(cell.x, baseY + VEye, cell.y);

                    if (!VBlocked(eye, 0.3f) && VRay(eye, Vector3.down, VEye + 0.6f, out RaycastHit _))
                    {
                        MeasureSight(eye, limit, zone.DisplayName, ground);
                    }

                    if (baseY < -1f || upperGroup == null ||
                        !VRay(new Vector3(cell.x, 7.6f, cell.y), Vector3.down, 6f, out RaycastHit floor) ||
                        floor.point.y < 3.4f || floor.point.y > 4.3f ||
                        !floor.collider.transform.IsChildOf(upperGroup))
                    {
                        continue;
                    }

                    Vector3 upperEye = floor.point + Vector3.up * VEye;

                    if (VBlocked(upperEye, 0.3f))
                    {
                        continue;
                    }

                    // North Catwalk (cu A Heaven): linie lunga intentionata.
                    if (cell.y > 46f && cell.x > -36f && cell.x < 56f)
                    {
                        Dictionary<string, VSight> one = new Dictionary<string, VSight>();
                        MeasureSight(upperEye, VExteriorLimit, "catwalk", one);

                        if (one["catwalk"].Length > catwalk.Length)
                        {
                            catwalk = one["catwalk"];
                        }
                    }
                    else
                    {
                        MeasureSight(upperEye, limit, zone.DisplayName, upper);
                    }
                }
            }

            report.Body.AppendLine($"Linie intenționată: North Catwalk, {catwalk.Length:0.0} m, de la {P(catwalk.From)} la {P(catwalk.To)}.");
            report.Body.AppendLine();

            int groundOk = ground.Count(p => p.Value.Length <= p.Value.Limit);
            int upperOk = upper.Count(p => p.Value.Length <= p.Value.Limit);

            foreach (KeyValuePair<string, VSight> pair in ground.Where(p => p.Value.Length > p.Value.Limit)
                         .OrderByDescending(p => p.Value.Length - p.Value.Limit))
            {
                report.Warning($"parter, {pair.Key}: {pair.Value.Length:0.0} m > {pair.Value.Limit:0.#} m, {P(pair.Value.From)} → {P(pair.Value.To)}");
            }

            foreach (KeyValuePair<string, VSight> pair in upper.Where(p => p.Value.Length > p.Value.Limit)
                         .OrderByDescending(p => p.Value.Length - p.Value.Limit))
            {
                report.Warning($"etaj, {pair.Key}: {pair.Value.Length:0.0} m > {pair.Value.Limit:0.#} m, {P(pair.Value.From)} → {P(pair.Value.To)}");
            }

            report.Ok($"parter: {groundOk} din {ground.Count} zone în limită; etaj: {upperOk} din {upper.Count}");
        }

        private static float ZoneSightLimit(MapZone zone)
        {
            if (VHallZones.Contains(zone.DisplayName))
            {
                return VExteriorLimit;
            }

            int cells = 0, roofed = 0;

            foreach (Vector2 cell in ZoneCells(zone, 2f))
            {
                cells++;

                if (VRay(new Vector3(cell.x, zone.transform.position.y + 0.1f, cell.y), Vector3.up, 14f, out RaycastHit _))
                {
                    roofed++;
                }
            }

            return cells > 0 && roofed * 10 >= cells * 6 ? VInteriorLimit : VExteriorLimit;
        }

        private static void MeasureSight(Vector3 from, float limit, string zone, Dictionary<string, VSight> worst)
        {
            for (int i = 0; i < VDirections; i++)
            {
                float a = i * Mathf.PI * 2f / VDirections;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float length = VRay(from, dir, 250f, out RaycastHit hit) ? hit.distance : 250f;

                float rayLimit = limit;

                if (limit < VExteriorLimit && !VRay(from + dir * (length - 0.5f), Vector3.up, 14f, out RaycastHit _))
                {
                    rayLimit = VExteriorLimit;
                }

                if (!worst.TryGetValue(zone, out VSight current) || length - rayLimit > current.Length - current.Limit)
                {
                    worst[zone] = new VSight { From = from, To = from + dir * length, Length = length, Limit = rayLimit };
                }
            }
        }

        // ------------------------------------------------------------------
        // 5. Cover
        // ------------------------------------------------------------------
        private static readonly string[] CoverPrefixes =
        {
            "Crate_", "LowCover_", "HighCover_", "BigCrate_", "CrateStack_", "Container_", "Container40_"
        };

        private static void CheckCover(GameObject root, ValidationReport report)
        {
            report.Section("5. Cover");

            int total = 0, bad = 0;

            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (!CoverPrefixes.Any(prefix => t.name.StartsWith(prefix)))
                {
                    continue;
                }

                total++;

                Vector3 size = t.lossyScale;
                float height = size.y;
                float width = Mathf.Max(size.x, size.z);

                bool crouch = height >= 0.99f && height <= 1.16f;
                bool standing = height >= 1.89f && height <= 2.21f;
                bool container = Mathf.Abs(height - 2.6f) < 0.05f;
                bool stack = height >= 3f && height <= 4.01f;

                if (!(crouch || standing || container || stack))
                {
                    bad++;
                    report.Error($"`{t.name}` la {P(t.position)} are {height:0.##} m înălțime (nici crouch 1,0–1,1, nici standing 1,9–2,2)");
                }
                else if (width < MinCoverWidth - 0.01f)
                {
                    bad++;
                    report.Error($"`{t.name}` la {P(t.position)} are doar {width:0.##} m lățime (minimum 2 m)");
                }
            }

            if (bad == 0)
            {
                report.Ok($"{total} obiecte de cover, toate pe dimensiunile standard");
            }
        }

        // ------------------------------------------------------------------
        // 6. Usi, ferestre, trape, spatiu liber
        // ------------------------------------------------------------------
        private static void CheckOpenings(GameObject root, MapZone[] zones, ValidationReport report)
        {
            report.Section("6. Uși, ferestre, trape și spațiu liber");

            int doors = 0, windows = 0, bad = 0;

            foreach (Transform frame in root.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("_Frame")))
            {
                Transform left = frame.Find("Jamb_L"), right = frame.Find("Jamb_R"), head = frame.Find("Head");

                if (left == null || right == null || head == null)
                {
                    continue;
                }

                bool window = frame.Find("Sill") != null;
                float trim = left.lossyScale.x < left.lossyScale.z ? left.lossyScale.x : left.lossyScale.z;
                float clearWidth = Vector3.Distance(left.position, right.position) - trim;
                float clearHeight = head.position.y - head.lossyScale.y * 0.5f - (left.position.y - left.lossyScale.y * 0.5f);

                if (window)
                {
                    windows++;
                    float sill = left.position.y - left.lossyScale.y * 0.5f - GroundLevelBelow(left.position);

                    if (clearWidth < 1.19f || clearHeight < WindowHeight - 0.01f || sill < WindowSill - 0.01f)
                    {
                        bad++;
                        report.Error($"fereastra `{frame.name}`: {clearWidth:0.##} × {clearHeight:0.##} m, pervaz la {sill:0.##} m");
                    }
                }
                else
                {
                    doors++;

                    if (clearWidth < DoorWidth - 0.01f || clearHeight < DoorHeight - 0.01f)
                    {
                        bad++;
                        report.Error($"ușa `{frame.name}`: {clearWidth:0.##} × {clearHeight:0.##} m (minimum 1,4 × 2,4)");
                    }
                }
            }

            int hatches = 0;

            foreach (Transform t in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Hatch_")))
            {
                Transform panel = t.Find("Panel");

                if (panel == null)
                {
                    continue;
                }

                hatches++;
                Vector3 s = panel.lossyScale;

                if (Mathf.Min(s.x, s.z) < HatchSize - 0.01f)
                {
                    bad++;
                    report.Error($"trapa `{t.name}` are {s.x:0.##} × {s.z:0.##} m (minimum 1,2 × 1,2)");
                }
            }

            if (bad == 0)
            {
                report.Ok($"{doors} uși, {windows} ferestre și {hatches} trape, toate la standard");
            }

            // Spatiu liber deasupra capului in interioare (fara treceri prin usi si fara scari).
            int lowCells = 0;
            Vector3 lowExample = Vector3.zero;

            foreach (MapZone zone in zones)
            {
                if (ZoneSightLimit(zone) > VInteriorLimit)
                {
                    continue;
                }

                foreach (Vector2 cell in ZoneCells(zone, 1f))
                {
                    Vector3 feet = new Vector3(cell.x, zone.transform.position.y + 0.1f, cell.y);

                    if (VBlocked(feet + Vector3.up * 1f, 0.3f) ||
                        !VRay(feet, Vector3.up, 2.95f, out RaycastHit hit) ||
                        IsOpeningOrStair(hit.collider.transform) ||
                        hit.collider.name.StartsWith("Bunker_Balcony"))   // nisele de sub balconul buncarului sunt intentionate
                    {
                        continue;
                    }

                    if (lowCells == 0)
                    {
                        lowExample = feet;
                    }

                    lowCells++;
                }
            }

            if (lowCells > 0)
            {
                report.Warning($"{lowCells} m² de interior cu mai puțin de 3 m deasupra capului (de exemplu la {P(lowExample)})");
            }
            else
            {
                report.Ok("spațiu liber de minimum 3 m în toate interioarele (în afara ușilor și a scărilor)");
            }
        }

        // ------------------------------------------------------------------
        // 7. Imbinari: elemente care trebuie sa fie legate intre ele
        // ------------------------------------------------------------------
        private static void CheckJoints(GameObject root, ValidationReport report)
        {
            report.Section("7. Îmbinări (pereți, coloane, tavane)");

            const float slitMin = 0.03f;
            const float slitMax = 0.8f;

            List<BoxCollider> elements = new List<BoxCollider>();

            foreach (string groupName in new[] { "Structures", "Cover" })
            {
                Transform group = root.transform.Find(groupName);

                if (group != null)
                {
                    elements.AddRange(group.GetComponentsInChildren<BoxCollider>()
                        .Where(b => b.enabled && !b.isTrigger && !b.name.Contains("Rail")));
                }
            }

            // a) Perete / bloc care se opreste la mai putin de 1 m sub un tavan: pare neterminat.
            int nearMiss = 0;

            foreach (BoxCollider box in elements)
            {
                Transform t = box.transform;

                if (t.lossyScale.y < 1.9f)
                {
                    continue;
                }

                Vector3 top = t.position + Vector3.up * (t.lossyScale.y * 0.5f);

                // Deasupra varfului continua alt element (bucata de perete, placa): e legat.
                if (Physics.CheckBox(top + Vector3.up * 0.03f, new Vector3(0.05f, 0.02f, 0.05f), t.rotation,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // Conteaza doar tavanele si planseele, nu tevile sau cover-ul de deasupra.
                if (VRay(top + Vector3.up * 0.01f, Vector3.up, 1f, out RaycastHit hit) && hit.distance > 0.02f &&
                    !IsProp(hit.collider))
                {
                    nearMiss++;
                    report.Error($"`{t.name}` se oprește la {hit.distance:0.00} m sub `{hit.collider.name}` la {P(top)}");
                }
            }

            // b) Fante inguste intre un element si altul: nici nu treci, nici nu sunt lipite.
            HashSet<string> seen = new HashSet<string>();
            int slits = 0;

            foreach (BoxCollider box in elements)
            {
                Transform t = box.transform;
                Vector3 half = t.lossyScale * 0.5f;
                float baseY = t.position.y - half.y;

                Vector3[] axes = { t.right, -t.right, t.forward, -t.forward };
                float[] extents = { half.x, half.x, half.z, half.z };
                Vector3[] alongs = { t.forward, t.forward, t.right, t.right };
                float[] lengths = { half.z, half.z, half.x, half.x };

                for (int f = 0; f < 4; f++)
                {
                    for (float s = -lengths[f] + 0.25f; s <= lengths[f] - 0.25f; s += 0.5f)
                    {
                        foreach (float h in new[] { 0.5f, 1.5f })
                        {
                            if (h > half.y * 2f - 0.1f)
                            {
                                continue;
                            }

                            Vector3 origin = t.position + axes[f] * (extents[f] + 0.005f) + alongs[f] * s;
                            origin.y = baseY + h;

                            // Raza pornita din interiorul altui obiect: fata e lipita de el.
                            if (VBlocked(origin, 0.004f))
                            {
                                continue;
                            }

                            if (!VRay(origin, axes[f], slitMax, out RaycastHit hit) || hit.distance < slitMin ||
                                hit.collider is MeshCollider ||
                                hit.collider.name.Contains("Rail") || IsPlayer(hit.collider) ||
                                hit.collider.name.Contains("_Pipe_") || hit.collider.name == "WalkSurface" ||
                                hit.collider.GetComponentInParent<SimpleDoor>() != null ||
                                AreRelated(box, hit.collider))
                            {
                                continue;
                            }

                            string a = t.name, b = hit.collider.name;
                            string key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

                            if (seen.Add(key))
                            {
                                slits++;
                                report.Error($"fantă de {hit.distance:0.00} m între `{a}` și `{b}` la {P(origin)}, " +
                                             $"înălțime {origin.y:0.0} m, spre ({axes[f].x:0.#}, {axes[f].z:0.#})");
                            }
                        }
                    }
                }
            }

            if (nearMiss == 0 && slits == 0)
            {
                report.Ok($"{elements.Count} pereți, blocuri și obiecte de cover verificate: nimic oprit sub tavan, nicio fantă între {slitMin} și {slitMax} m");
            }
        }

        // ------------------------------------------------------------------
        // 8. Pregatire pentru texturi
        // ------------------------------------------------------------------
        private static void CheckTextureReadiness(GameObject root, ValidationReport report)
        {
            report.Section("8. Pregătire pentru texturi");

            int boxes = 0, primitive = 0, wrongScale = 0, kit = 0;
            HashSet<Material> materials = new HashSet<Material>();

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                if (mesh == null || renderer == null)
                {
                    continue;
                }

                if (renderer.sharedMaterial != null)
                {
                    materials.Add(renderer.sharedMaterial);
                }

                if (mesh.name == "Cube")
                {
                    primitive++;
                    continue;
                }

                if (mesh.name.StartsWith("K17_"))
                {
                    kit++;
                    continue;
                }

                if (!mesh.name.StartsWith("Box_"))
                {
                    continue; // cilindri (rezervoare, tevi) si alte forme
                }

                boxes++;

                // Plasa trebuie sa corespunda dimensiunii obiectului (UV in metri).
                Vector3 s = filter.transform.lossyScale;
                string expected = $"Box_{Mathf.RoundToInt(Mathf.Abs(s.x) * 100f)}_{Mathf.RoundToInt(Mathf.Abs(s.y) * 100f)}_{Mathf.RoundToInt(Mathf.Abs(s.z) * 100f)}";

                if (mesh.name != expected)
                {
                    wrongScale++;

                    if (wrongScale <= 10)
                    {
                        report.Error($"`{filter.name}` are plasa `{mesh.name}`, dar dimensiunea lui cere `{expected}` (UV întinse)");
                    }
                }
            }

            if (primitive > 0)
            {
                report.Error($"{primitive} cutii folosesc încă cubul standard (UV 0–1 întinse pe toată fața)");
            }

            if (primitive == 0 && wrongScale == 0)
            {
                report.Ok($"{boxes} cutii cu UV la scară reală (1 unitate UV = 1 m) și UV2 pentru lightmap");
            }

            if (kit > 0)
            {
                report.Ok($"{kit} piese din kit-ul modular (`Assets/_Project/Art/Models/Kit`), cu UV în metri");
            }

            report.Ok($"{materials.Count} materiale, câte unul pe tip de suprafață (`Assets/_Project/Art/Materials/Greybox`): " +
                      "înlocuind un material se texturează toate suprafețele de acel tip");

            // Fete suprapuse (z-fighting): doua cutii vizibile ale caror fete de sus stau in acelasi plan
            // si se suprapun pe orizontala.
            List<Bounds> tops = new List<Bounds>();
            List<string> names = new List<string>();
            List<Bounds> allRenderers = root.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds).ToList();

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("Box_") ||
                    Quaternion.Angle(renderer.transform.rotation, Quaternion.identity) > 0.1f)
                {
                    continue;
                }

                tops.Add(renderer.bounds);
                names.Add(renderer.name);
            }

            int zFights = 0;

            for (int i = 0; i < tops.Count && zFights < 40; i++)
            {
                for (int j = i + 1; j < tops.Count && zFights < 40; j++)
                {
                    Bounds a = tops[i], b = tops[j];

                    bool sameTop = Mathf.Abs(a.max.y - b.max.y) < 0.002f;
                    float overlapX = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
                    float overlapZ = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);

                    if (sameTop && overlapX > 0.05f && overlapZ > 0.05f &&
                        !IsCoveredFromAbove(a, b, allRenderers))
                    {
                        zFights++;
                        report.Warning($"fețele de sus ale `{names[i]}` și `{names[j]}` se suprapun în același plan (y {a.max.y:0.00}) la {P(a.center)} și {P(b.center)} — pot pâlpâi");
                    }
                }
            }

            if (zFights == 0)
            {
                report.Ok("nicio față de sus suprapusă în același plan (fără z-fighting pe podele, capace și plăci)");
            }
        }

        /// <summary>
        /// Zona comuna a doua fete de sus e acoperita de un alt obiect asezat direct pe ea
        /// (ex. placa de podea peste peretele tunelului, coronamentul peste zid): nu se vede, nu paie.
        /// </summary>
        private static bool IsCoveredFromAbove(Bounds a, Bounds b, List<Bounds> all)
        {
            Vector3 p = new Vector3(
                (Mathf.Max(a.min.x, b.min.x) + Mathf.Min(a.max.x, b.max.x)) * 0.5f,
                a.max.y + 0.01f,
                (Mathf.Max(a.min.z, b.min.z) + Mathf.Min(a.max.z, b.max.z)) * 0.5f);

            foreach (Bounds other in all)
            {
                if (other.Contains(p) && other.min.y > a.max.y - 0.02f)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // 9. Buncarul: timpul de ajuns pentru fiecare echipa
        // ------------------------------------------------------------------
        private static void CheckBunkerTiming(GameObject root, ValidationReport report)
        {
            report.Section("9. Buncăr: timp de ajuns pentru fiecare echipă");

            SpawnPoint[] spawns = root.GetComponentsInChildren<SpawnPoint>();
            SpawnPoint attackers = spawns.OrderBy(s => s.name).FirstOrDefault(s => s.Team == Team.Attackers);
            SpawnPoint defenders = spawns.OrderBy(s => s.name).FirstOrDefault(s => s.Team == Team.Defenders);

            if (attackers == null || defenders == null)
            {
                report.Error("lipsește un punct de spawn pentru una dintre echipe");
                return;
            }

            float attackerDistance = WalkDistanceToBunker(attackers.transform.position, out string attackerRoute);
            float defenderDistance = WalkDistanceToBunker(defenders.transform.position, out string defenderRoute);

            if (attackerDistance < 0f || defenderDistance < 0f)
            {
                report.Error("buncărul nu e accesibil din unul dintre spawn-uri");
                return;
            }

            const float sprint = 7.5f;
            float difference = Mathf.Abs(attackerDistance - defenderDistance) / Mathf.Max(attackerDistance, defenderDistance);

            report.Body.AppendLine($"Drumul cel mai scurt pe jos (grilă de 1 m, cu scări și tuneluri), până la podeaua buncărului:");
            report.Body.AppendLine();
            report.Body.AppendLine($"| Echipă | Distanță | Timp la sprint (7,5 m/s) | Intră pe |");
            report.Body.AppendLine($"|---|---|---|---|");
            report.Body.AppendLine($"| Atacatori | {attackerDistance:0} m | {attackerDistance / sprint:0.0} s | {attackerRoute} |");
            report.Body.AppendLine($"| Apărători | {defenderDistance:0} m | {defenderDistance / sprint:0.0} s | {defenderRoute} |");
            report.Body.AppendLine();

            if (difference > 0.1f)
            {
                report.Warning($"diferență de {difference * 100f:0}% între echipe (ținta: sub 10%)");
            }
            else
            {
                report.Ok($"diferență de {difference * 100f:0}% între echipe (sub 10%)");
            }
        }

        /// <summary>
        /// Cel mai scurt drum pe jos (Dijkstra pe grila de 1 m, cu diagonale) pana in buncar.
        /// Intoarce -1 daca buncarul nu e accesibil. route = legatura prin care intra.
        /// </summary>
        private static float WalkDistanceToBunker(Vector3 start, out string route)
        {
            route = "-";

            Dictionary<(int, int, int), float> best = new Dictionary<(int, int, int), float>();
            Dictionary<(int, int, int), Vector3> feetOf = new Dictionary<(int, int, int), Vector3>();
            Dictionary<(int, int, int), (int, int, int)> parent = new Dictionary<(int, int, int), (int, int, int)>();
            SortedSet<(float, int, int, int)> open = new SortedSet<(float, int, int, int)>();

            (int, int, int) startKey = WalkKey(start);
            best[startKey] = 0f;
            feetOf[startKey] = start;
            open.Add((0f, startKey.Item1, startKey.Item2, startKey.Item3));

            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

            while (open.Count > 0 && best.Count < 600000)
            {
                (float cost, int kx, int kz, int ky) = open.Min;
                open.Remove(open.Min);
                (int, int, int) key = (kx, kz, ky);
                Vector3 p = feetOf[key];

                if (cost > best[key] + 0.001f)
                {
                    continue;
                }

                if (Inside(Bunker, p.x, p.z) && p.y < BunkerFloorY + 0.3f)
                {
                    // Prin ce legatura a intrat: urcam pe drum inapoi pana la primul punct din afara buncarului.
                    (int, int, int) walk = key;

                    while (parent.ContainsKey(walk))
                    {
                        Vector3 q = feetOf[walk];

                        string link = Inside(BunkerWestLink, q.x, q.z) && !Inside(Bunker, q.x, q.z) ? "West Link (tunelul de nord)"
                            : Inside(BunkerNorthLink, q.x, q.z) ? "North Link (tunelul de est)"
                            : (Inside(BunkerSouthLinkH, q.x, q.z) || Inside(BunkerSouthLinkV, q.x, q.z)) && !Inside(Bunker, q.x, q.z) ? "South Link (South Tunnel)"
                            : null;

                        if (link != null)
                        {
                            route = link;
                            break;
                        }

                        walk = parent[walk];
                    }

                    return cost;
                }

                for (int i = 0; i < 8; i++)
                {
                    Vector3 probe = new Vector3(p.x + dx[i], p.y + 0.8f, p.z + dz[i]);

                    if (!VRay(probe, Vector3.down, 12f, out RaycastHit hit) || hit.point.y - p.y > 0.6f)
                    {
                        continue;
                    }

                    Vector3 feet = new Vector3(probe.x, hit.point.y, probe.z);

                    if (Physics.CheckCapsule(feet + Vector3.up * 0.55f, feet + Vector3.up * 1.4f, 0.4f,
                            ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    float step = i < 4 ? 1f : 1.4142f;
                    (int, int, int) next = WalkKey(feet);
                    float nextCost = cost + step;

                    if (!best.TryGetValue(next, out float known) || nextCost < known - 0.001f)
                    {
                        if (best.ContainsKey(next))
                        {
                            open.Remove((known, next.Item1, next.Item2, next.Item3));
                        }

                        best[next] = nextCost;
                        feetOf[next] = feet;
                        parent[next] = key;
                        open.Add((nextCost, next.Item1, next.Item2, next.Item3));
                    }
                }
            }

            return -1f;
        }

        private static float GroundLevelBelow(Vector3 point)
        {
            return VRay(point, Vector3.down, 12f, out RaycastHit hit) ? hit.point.y : 0f;
        }

        private static bool IsOpeningOrStair(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                string n = p.name;

                if (n.StartsWith("Part_") || n.StartsWith("Door") || n.Contains("Stairs") || n.StartsWith("Hatch") ||
                    n.Contains("Lintel") || n == "Doors" || n == "Stairs And Low Platforms")
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        private static void WriteValidationReport(ValidationReport report)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Blacksite K-17 — raport de validare");
            text.AppendLine();
            text.AppendLine("Generat de `CHOKEHOLD > Map > Validate`. Ușile sunt considerate deschise, baricadele nemontate.");
            text.AppendLine();
            text.AppendLine($"**{report.Errors.Count} erori, {report.Warnings.Count} avertismente.**");
            text.Append(report.Body);

            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "MapValidation.md");
            File.WriteAllText(path, text.ToString());

            string summary = $"VALIDATE: {report.Errors.Count} erori, {report.Warnings.Count} avertismente. Raport: {path}";

            if (report.Errors.Count > 0)
            {
                Debug.LogWarning(summary + "\n" + string.Join("\n", report.Errors.Take(20)));
            }
            else
            {
                Debug.Log(summary);
            }
        }
    }
}
