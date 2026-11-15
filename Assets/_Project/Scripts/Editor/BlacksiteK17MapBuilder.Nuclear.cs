using System.Collections.Generic;
using System.Linq;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChokeHold.EditorTools
{
    // Blacksite K-17 ca instalatie nucleara: cladiri industriale (reactor, turbina, laborator de
    // control) legate prin curti exterioare. Interioarele au lampi; afara e lumina zilei, cu cer acoperit.
    public static partial class BlacksiteK17MapBuilder
    {
        private static readonly Color HallLight = new Color(0.82f, 0.95f, 0.92f);

        // ------------------------------------------------------------------
        // Decorul instalatiei nucleare (fara collider: nu schimba jocul)
        // ------------------------------------------------------------------
        private static readonly Color ReactorBlue = new Color(0.3f, 0.65f, 1f);
        private static readonly Color BeaconOrange = new Color(1f, 0.55f, 0.1f);
        private static readonly Color EmergencyRed = new Color(1f, 0.12f, 0.08f);

        private static Transform gNuclear;
        private static int nuclearPlaced;

        private static void BuildNuclearDressing(GameObject root)
        {
            if (!AssetDatabase.IsValidFolder(KitFolder))
            {
                return;
            }

            gNuclear = CreateGroup(gDetail, "Nuclear Dressing");
            nuclearPlaced = 0;
            Physics.SyncTransforms();

            List<Doorway> doorways = CollectDoorways(root);

            BuildReactorHall();
            BuildDoorSigns(doorways);
            BuildSiteBeacons(doorways);
            BuildDeconFrames(doorways);
            BuildEmergencyLights();
            BuildPipeRuns();
            BuildFacades();
            DressReactorDeck();
            BuildExteriorDressing();

            Debug.Log($"Instalatie nucleara: {nuclearPlaced} piese de decor, {doorways.Count} usi gasite.");
        }

        private static GameObject NuclearPiece(string piece, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent = null)
        {
            GameObject go = CreateKitObject(parent != null ? parent : gNuclear, piece, Vector3.zero, Quaternion.identity, scale);

            if (go != null)
            {
                go.transform.SetPositionAndRotation(position, rotation);
                nuclearPlaced++;
            }

            return go;
        }

        private static void NuclearLight(string name, Vector3 position, Color color, float range, float intensity, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : gLights, false);
            go.transform.position = position;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
        }

        // ---------------- Sala reactorului (site A) ----------------

        private static void BuildReactorHall()
        {
            GameObject vessel = GameObject.Find("A_Tank_Main");

            if (vessel == null)
            {
                return;
            }

            Collider body = vessel.GetComponent<Collider>();
            Vector3 c = body.bounds.center;
            float top = body.bounds.max.y;

            // Mecanismul barelor de control atarna din tavan, deasupra capului jucatorilor de la etaj.
            if (Physics.Raycast(new Vector3(c.x, top + 0.2f, c.z), Vector3.up, out RaycastHit ceiling, 10f, ~0, QueryTriggerInteraction.Ignore) &&
                ceiling.point.y - 2.1f > UpperFloorHeight + 2.2f)
            {
                NuclearPiece("K17_Control_Rod_Drive", new Vector3(c.x, ceiling.point.y - 2.1f, c.z), Quaternion.identity, Vector3.one);
            }

            // Lumina albastra a reactorului (Cerenkov)
            NuclearLight("Light_Reactor_Glow", new Vector3(c.x, 1.6f, c.z), ReactorBlue, 6f, 2.2f);

            // Bazinul de combustibil uzat sub gratar: cel mai apropiat loc liber de 6 x 4 m din sala A
            Area hall = WalkableAreas.First(a => a.Name == "Bombsite A");
            Vector3 best = Vector3.zero;
            float bestDistance = float.MaxValue;
            bool alongX = true;

            for (float x = hall.XMin + 3f; x <= hall.XMax - 3f; x += 1f)
            {
                for (float z = hall.ZMin + 3f; z <= hall.ZMax - 3f; z += 1f)
                {
                    foreach (bool ax in new[] { true, false })
                    {
                        Vector3 size = ax ? new Vector3(6f, 0f, 4f) : new Vector3(4f, 0f, 6f);
                        Vector3 p = new Vector3(x, 0f, z);

                        if (OverlapsArea(SiteA, p, size, 1f) || !FloorFree(p, size, 0f))
                        {
                            continue;
                        }

                        float distance = Vector3.Distance(p, new Vector3(c.x, 0f, c.z));

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = p;
                            alongX = ax;
                        }
                    }
                }
            }

            if (bestDistance < float.MaxValue)
            {
                NuclearPiece("K17_Fuel_Pool", best + Vector3.up * 0.022f, alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                NuclearLight("Light_Fuel_Pool", best + Vector3.up * 0.6f, ReactorBlue, 7f, 2f);
            }

            Vector3 poolSize = alongX ? new Vector3(6f, 0f, 4f) : new Vector3(4f, 0f, 6f);

            // Marcaj de radiatii pe podea langa vas, unde e liber (nu peste bazin, nu pe site)
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(c.x + Mathf.Sin(a) * 4.2f, 0f, c.z + Mathf.Cos(a) * 4.2f);
                Vector3 size = new Vector3(3f, 0f, 3f);

                if (FloorFree(p, size, 0f) && !OverlapsArea(SiteA, p, size, 0.5f) &&
                    (bestDistance == float.MaxValue || !OverlapsArea(new Area("Pool", best.x - poolSize.x * 0.5f, best.z - poolSize.z * 0.5f,
                        best.x + poolSize.x * 0.5f, best.z + poolSize.z * 0.5f), p, size, 0.3f)))
                {
                    NuclearPiece("K17_Floor_Trefoil", p + Vector3.up * 0.025f, Quaternion.identity, Vector3.one);
                    break;
                }
            }
        }

        /// <summary>Podea la nivelul baseY si nimic deasupra pe dreptunghiul dat (pana la 2,2 m).</summary>
        private static bool FloorFree(Vector3 center, Vector3 size, float baseY)
        {
            Vector3 half = new Vector3(size.x * 0.5f, 1.1f, size.z * 0.5f);

            if (Physics.CheckBox(new Vector3(center.x, baseY + 1.2f, center.z), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Podea sub toate cele patru colturi si in centru
            foreach (Vector2 o in new[] { Vector2.zero, new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                Vector3 p = new Vector3(center.x + o.x * (half.x - 0.1f), baseY + 0.5f, center.z + o.y * (half.z - 0.1f));

                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore) ||
                    Mathf.Abs(hit.point.y - baseY) > 0.05f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool OverlapsArea(Area area, Vector3 center, Vector3 size, float margin)
        {
            return center.x + size.x * 0.5f > area.XMin - margin && center.x - size.x * 0.5f < area.XMax + margin &&
                   center.z + size.z * 0.5f > area.ZMin - margin && center.z - size.z * 0.5f < area.ZMax + margin;
        }

        // ---------------- Usi ----------------

        private struct Doorway
        {
            public Vector3 Center;   // mijlocul golului, la podea
            public Vector3 Along;    // directia peretelui
            public Vector3 Normal;   // perpendicular pe perete
            public float Width;
            public float Depth;      // grosimea peretelui / adancimea pasajului
            public bool Cell;        // DoorCell (pasaj cu jambaje), altfel usa intr-un perete despartitor
        }

        private static List<Doorway> CollectDoorways(GameObject root)
        {
            List<Doorway> result = new List<Doorway>();

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Transform a = t.Find("Jamb_A");
                Transform b = t.Find("Jamb_B");

                if (a != null && b != null)
                {
                    Vector3 pa = a.position, pb = b.position;
                    Vector3 along = new Vector3(pb.x - pa.x, 0f, pb.z - pa.z);
                    float gap = along.magnitude;
                    along /= Mathf.Max(gap, 0.001f);

                    Vector3 scaleA = Abs(a.lossyScale);
                    float jamb = Mathf.Abs(along.x) > 0.5f ? scaleA.x : scaleA.z;
                    float depth = Mathf.Abs(along.x) > 0.5f ? scaleA.z : scaleA.x;

                    result.Add(new Doorway
                    {
                        Center = new Vector3((pa.x + pb.x) * 0.5f, 0f, (pa.z + pb.z) * 0.5f),
                        Along = along,
                        Normal = new Vector3(-along.z, 0f, along.x),
                        Width = gap - jamb,
                        Depth = depth,
                        Cell = true,
                    });
                    continue;
                }

                SimpleDoor door = t.GetComponent<SimpleDoor>();

                if (door != null && t.parent != null && t.parent.Find("Jamb_A") == null)
                {
                    Transform panel = t.Find("Panel");
                    float width = panel != null ? Abs(panel.lossyScale).z : DoorWidth;
                    Vector3 along = t.forward;

                    result.Add(new Doorway
                    {
                        Center = new Vector3(t.position.x, t.position.y, t.position.z) + along * (width * 0.5f),
                        Along = along,
                        Normal = t.right,
                        Width = width,
                        Depth = PartitionThickness,
                        Cell = false,
                    });
                }
            }

            return result;
        }

        /// <summary>Punct pe fata peretelui de langa usa (raza spre perete), cu normala fetei.</summary>
        private static bool WallSpot(Doorway door, float alongOffset, float height, int side, out RaycastHit hit)
        {
            Vector3 n = door.Normal * side;
            Vector3 start = door.Center + door.Along * alongOffset + Vector3.up * height + n * (door.Depth * 0.5f + 1.2f);

            return Physics.Raycast(start, -n, out hit, 1.4f, ~0, QueryTriggerInteraction.Ignore) &&
                   Vector3.Dot(hit.normal, n) > 0.9f;
        }

        private static void BuildDoorSigns(List<Doorway> doorways)
        {
            List<Vector3> placed = new List<Vector3>();

            foreach (Doorway door in doorways)
            {
                if (door.Center.y < -1f)
                {
                    continue; // tuneluri: acolo sunt lampile de urgenta
                }

                foreach (int side in new[] { 1, -1 })
                {
                    if (!WallSpot(door, door.Width * 0.5f + 0.75f, door.Center.y + 1.65f, side, out RaycastHit hit) ||
                        placed.Any(p => (p - hit.point).sqrMagnitude < 1f))
                    {
                        continue;
                    }

                    NuclearPiece("K17_Sign_Radiation", hit.point + hit.normal * 0.005f, Quaternion.LookRotation(hit.normal), Vector3.one);
                    placed.Add(hit.point);
                }
            }
        }

        private static void BuildSiteBeacons(List<Doorway> doorways)
        {
            Area siteHallA = WalkableAreas.First(a => a.Name == "Bombsite A");
            Area hangar = WalkableAreas.First(a => a.Name == "Hangar / Bombsite B");
            int index = 0;

            foreach (Doorway door in doorways)
            {
                bool nearSite = Mathf.Abs(door.Center.y) < 0.5f &&
                                (OverlapsArea(siteHallA, door.Center, Vector3.zero, 1.5f) || OverlapsArea(hangar, door.Center, Vector3.zero, 1.5f));

                if (!nearSite)
                {
                    continue;
                }

                foreach (int side in new[] { 1, -1 })
                {
                    if (!WallSpot(door, 0f, DoorHeight + 0.55f, side, out RaycastHit hit))
                    {
                        continue;
                    }

                    GameObject beacon = NuclearPiece("K17_Beacon", hit.point + hit.normal * 0.005f, Quaternion.LookRotation(hit.normal), Vector3.one);

                    if (beacon == null)
                    {
                        continue;
                    }

                    // Lumina rotitoare (spot) in cupola girofarului
                    GameObject spin = new GameObject("Spin");
                    spin.transform.SetParent(beacon.transform, false);
                    spin.transform.localPosition = new Vector3(0f, 0.15f, 0.2f);
                    spin.AddComponent<BeaconSpin>();

                    GameObject lamp = new GameObject($"Light_Beacon_{index++:00}");
                    lamp.transform.SetParent(spin.transform, false);
                    lamp.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

                    Light light = lamp.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = BeaconOrange;
                    light.range = 10f;
                    light.spotAngle = 45f;
                    light.intensity = 6f;
                }
            }
        }

        private static void BuildDeconFrames(List<Doorway> doorways)
        {
            Vector3[] spawns =
            {
                AreaCenter(WalkableAreas.First(a => a.Name == "T1 Spawn")),
                AreaCenter(WalkableAreas.First(a => a.Name == "T2 Spawn")),
            };

            foreach (Doorway door in doorways)
            {
                if (!door.Cell || door.Depth < 1.5f || Mathf.Abs(door.Center.y) > 0.5f ||
                    !spawns.Any(s => Vector3.Distance(s, door.Center) < 32f))
                {
                    continue;
                }

                // Cadrul e modelat pe X pentru o usa de 1,4 m; il rotim pe directia peretelui.
                Quaternion rotation = Quaternion.LookRotation(door.Normal);
                // Stalpii (la +-0,76 m in model) stau in interiorul golului, bara de sus sub buiandrug.
                float widthScale = Mathf.Max(0.3f, door.Width * 0.5f - 0.08f) / 0.76f;
                NuclearPiece("K17_Decon_Arch", door.Center + Vector3.up * 0.01f, rotation, new Vector3(widthScale, 0.9f, 1f));
                NuclearLight("Light_Decon", door.Center + Vector3.up * 2.2f, new Color(0.6f, 1f, 0.75f), 4f, 1.5f);
            }
        }

        private static Vector3 AreaCenter(Area area)
        {
            return new Vector3((area.XMin + area.XMax) * 0.5f, 0f, (area.ZMin + area.ZMax) * 0.5f);
        }

        // ---------------- Tuneluri si buncar: lampi de urgenta rosii ----------------

        private static void BuildEmergencyLights()
        {
            List<Vector3> placed = new List<Vector3>();
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

            for (int i = 0; i < GridX; i++)
            {
                for (int j = 0; j < GridZ; j++)
                {
                    float x = CellCenterX(i), z = CellCenterZ(j);

                    if (!IsPit(x, z))
                    {
                        continue;
                    }

                    float floor = PitFloorAt(x, z);
                    Vector3 p = new Vector3(x, floor + 2.3f, z);

                    if (placed.Any(q => (q - p).sqrMagnitude < 81f) ||
                        Physics.CheckSphere(p, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    foreach (Vector3 dir in directions)
                    {
                        if (!Physics.Raycast(p, dir, out RaycastHit hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) ||
                            Vector3.Dot(hit.normal, -dir) < 0.9f)
                        {
                            continue;
                        }

                        NuclearPiece("K17_Emergency_Light", hit.point + hit.normal * 0.005f, Quaternion.LookRotation(hit.normal), Vector3.one);
                        NuclearLight("Light_Emergency", hit.point + hit.normal * 0.35f, EmergencyRed, 6f, 1.6f);
                        placed.Add(p);
                        break;
                    }
                }
            }
        }

        // ---------------- Platforma de pe site-ul A: punte de intretinere a reactorului ----------------

        /// <summary>
        /// Platforma joasa (1,2 m) de langa reactor devine puntea de intretinere: tabla striata, chenar galben
        /// pe muchii, pupitru local si conducta de racire care coboara din ea in podea.
        /// </summary>
        private static void DressReactorDeck()
        {
            GameObject deck = GameObject.Find("A_Platform");

            if (deck == null)
            {
                return;
            }

            Bounds b = deck.GetComponent<Collider>().bounds;
            float top = b.max.y + 0.012f;
            Transform group = CreateGroup(gNuclear, "Reactor Deck");

            HazardStrip(group, "Edge_S", new Vector3(b.min.x, top, b.min.z + 0.1f), new Vector3(b.max.x, top, b.min.z + 0.1f), 0.2f);
            HazardStrip(group, "Edge_E", new Vector3(b.max.x - 0.1f, top, b.min.z + 0.2f), new Vector3(b.max.x - 0.1f, top, b.max.z), 0.2f);

            // Tabla striata: benzi subtiri pe toata puntea
            for (float x = b.min.x + 0.6f; x < b.max.x - 0.3f; x += 0.6f)
            {
                Decor(group, "Tread", new Vector3(x, top - 0.006f, b.center.z), new Vector3(0.04f, 0.008f, b.size.z - 0.6f), frameMat);
            }

            // Fete laterale: placi de otel cu nituri (benzi verticale)
            for (float x = b.min.x + 1f; x < b.max.x; x += 2f)
            {
                Decor(group, "Rib_S", new Vector3(x, b.center.y, b.min.z - 0.02f), new Vector3(0.08f, b.size.y - 0.1f, 0.04f), frameMat);
            }

            for (float z = b.min.z + 1f; z < b.max.z; z += 2f)
            {
                Decor(group, "Rib_E", new Vector3(b.max.x + 0.02f, b.center.y, z), new Vector3(0.04f, b.size.y - 0.1f, 0.08f), frameMat);
            }
        }

        // ---------------- Exterior: cladiri pline, stalpi de iluminat, sarma ghimpata ----------------

        private static void BuildExteriorDressing()
        {
            Transform group = CreateGroup(gNuclear, "Exterior");

            // Turnul de paza si magazia din curte sunt blocuri pline: le dam usi, ferestre, usa rulanta.
            // (latura, pozitie pe latura fata de mijloc, piesa, inaltime)
            DressBlockBuilding(group, "Outpost_Tower",
                (Vector3.back, 0f, "K17_Door_Panel", 0f),
                (Vector3.left, 0f, "K17_Window_Panel", 1.4f),
                (Vector3.forward, 0f, "K17_Window_Panel", 1.4f),
                (Vector3.right, 1.9f, "K17_Window_Panel", 1.4f),
                (Vector3.right, -1.9f, "K17_Window_Panel", 1.4f));

            DressBlockBuilding(group, "Courtyard_Shed",
                (Vector3.left, 0f, "K17_Roll_Door", 0f),
                (Vector3.back, -2.2f, "K17_Door_Panel", 0f),
                (Vector3.back, 2.2f, "K17_Window_Panel", 1.4f),
                (Vector3.forward, 3f, "K17_Window_Panel", 1.4f),
                (Vector3.forward, -3f, "K17_Window_Panel", 1.4f),
                (Vector3.right, 0f, "K17_Window_Panel", 1.4f));

            // Stalpi cu proiectoare in curtile de spawn si in curtile mari
            foreach (Vector2 p in new[]
                     {
                         new Vector2(-63f, -32f), new Vector2(-47f, -51f),     // T1 Spawn
                         new Vector2(58f, 39f), new Vector2(76f, 56f),         // T2 Spawn
                         new Vector2(-25f, -16f), new Vector2(13f, -37f),      // South Courtyard
                         new Vector2(48f, 36f), new Vector2(76f, 10f),         // East Yard
                     })
            {
                Vector3 at = new Vector3(p.x, 0f, p.y);

                if (FloorFree(at, new Vector3(1f, 0f, 1f), 0f))
                {
                    NuclearPiece("K17_Floodlight", at, Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.y).normalized), Vector3.one, group);
                }
            }

            // Sarma ghimpata pe coronamentul zidului de incinta (inelul exterior al hartii, 11 m)
            Transform wire = CreateGroup(group, "Razor Wire");

            void WireRun(Vector3 from, Vector3 to)
            {
                Vector3 dir = (to - from).normalized;
                float length = Vector3.Distance(from, to);
                int count = Mathf.RoundToInt(length / 2f);
                Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));

                for (int k = 0; k < count; k++)
                {
                    Vector3 p = from + dir * (length * (k + 0.5f) / count);

                    if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit top, 25f, ~0, QueryTriggerInteraction.Ignore) &&
                        top.point.y > OuterWallHeight - 0.3f)
                    {
                        NuclearPiece("K17_Razor_Wire", top.point, rotation, new Vector3(length / count / 2f, 1f, 1f), wire);
                    }
                }
            }

            float e = Cell * 0.5f;
            WireRun(new Vector3(MinX + e, 0f, MinZ + e), new Vector3(MaxX - e, 0f, MinZ + e));
            WireRun(new Vector3(MinX + e, 0f, MaxZ - e), new Vector3(MaxX - e, 0f, MaxZ - e));
            WireRun(new Vector3(MinX + e, 0f, MinZ + e), new Vector3(MinX + e, 0f, MaxZ - e));
            WireRun(new Vector3(MaxX - e, 0f, MinZ + e), new Vector3(MaxX - e, 0f, MaxZ - e));
        }

        /// <summary>Panouri (usa, ferestre) pe fetele libere ale unui bloc plin, ca sa arate a cladire.</summary>
        private static void DressBlockBuilding(Transform group, string blockName,
            params (Vector3 side, float offset, string piece, float y)[] panels)
        {
            GameObject block = GameObject.Find(blockName);

            if (block == null)
            {
                return;
            }

            Bounds b = block.GetComponent<Collider>().bounds;

            foreach ((Vector3 side, float offset, string piece, float y) in panels)
            {
                Vector3 along = Vector3.Cross(Vector3.up, side);
                Vector3 faceCenter = new Vector3(b.center.x + side.x * b.extents.x, b.min.y, b.center.z + side.z * b.extents.z);
                Vector3 at = faceCenter + along * offset + Vector3.up * y;

                // Doar daca in fata panoului e loc liber (nu e lipit de alt perete)
                if (Physics.CheckBox(at + side * 0.4f + Vector3.up * 1f, new Vector3(0.6f, 0.9f, 0.3f), Quaternion.LookRotation(side), ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                NuclearPiece(piece, at + side * 0.005f, Quaternion.LookRotation(side), Vector3.one, group);
            }

            // Banda de beton la baza si sub acoperis
            float top = b.max.y;
            Decor(group, blockName + "_Plinth", new Vector3(b.center.x, 0.2f, b.center.z), new Vector3(b.size.x + 0.08f, 0.4f, b.size.z + 0.08f), plinthMat);
            Decor(group, blockName + "_Band", new Vector3(b.center.x, top - 0.25f, b.center.z), new Vector3(b.size.x + 0.06f, 0.2f, b.size.z + 0.06f), frameMat);
        }

        // ---------------- Fatadele exterioare ----------------

        /// <summary>
        /// Peretii exteriori primesc ce are o cladire industriala reala: burlane de la acoperis la rigola,
        /// tablouri electrice cu tub de cablu spre interior, grile de ventilatie si lampi de exterior.
        /// Totul fara collider, pe portiuni de perete plin (fara usi sau ferestre).
        /// </summary>
        private static void BuildFacades()
        {
            Transform group = CreateGroup(gNuclear, "Facades");
            int faceIndex = 0;

            foreach (Face face in CollectWallFaces())
            {
                if (face.Theme != Theme.Exterior || face.BaseY != 0f || face.Length < 6f)
                {
                    continue;
                }

                Quaternion rotation = Quaternion.LookRotation(face.Normal);
                faceIndex++;
                int k = 0;

                for (float o = 3f; o < face.Length - 2f; o += 6.5f, k++)
                {
                    Vector3 p = face.Start + face.Along * o;

                    if (!SolidWallAt(p, face.Normal, face.Along, 2.6f) ||
                        Physics.CheckBox(p + face.Normal * 0.35f + Vector3.up * 1.4f, new Vector3(0.6f, 1.2f, 0.25f), rotation, ~0,
                            QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    switch ((faceIndex + k) % 3)
                    {
                        case 0:
                            Downpipe(group, p, face.Normal, rotation);
                            break;
                        case 1:
                            NuclearPiece("K17_Electrical_Box", p + Vector3.up * 1.0f + face.Normal * 0.005f, rotation, Vector3.one, group);
                            WallLamp(group, p + face.Along * 1.2f, face.Normal, rotation);
                            break;
                        default:
                            NuclearPiece("K17_Wall_Vent", p + Vector3.up * 2.4f + face.Normal * 0.005f, rotation, Vector3.one, group);
                            WallLamp(group, p - face.Along * 1.4f, face.Normal, rotation);
                            break;
                    }
                }
            }
        }

        private static bool SolidWallAt(Vector3 p, Vector3 normal, Vector3 along, float height)
        {
            foreach (float h in new[] { 0.3f, height * 0.5f, height, height + 1.0f })
            {
                foreach (float s in new[] { -0.7f, 0.7f })
                {
                    if (!Physics.Raycast(p + along * s + Vector3.up * h + normal * 0.6f, -normal, out RaycastHit hit, 0.7f, ~0,
                            QueryTriggerInteraction.Ignore) || hit.distance < 0.5f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void Downpipe(Transform group, Vector3 p, Vector3 normal, Quaternion rotation)
        {
            // Inaltimea peretelui (acolo e marginea acoperisului)
            if (!Physics.Raycast(p - normal * 0.2f + Vector3.up * 30f, Vector3.down, out RaycastHit top, 40f, ~0,
                    QueryTriggerInteraction.Ignore) || top.point.y < 3f)
            {
                return;
            }

            float roof = top.point.y - 0.05f;
            float bottom = 0.45f;
            float run = roof - 0.36f - bottom;
            int sections = Mathf.Max(1, Mathf.RoundToInt(run));

            NuclearPiece("K17_Downpipe_Shoe", p + normal * 0.005f, rotation, Vector3.one, group);
            for (int s = 0; s < sections; s++)
            {
                NuclearPiece("K17_Downpipe_Section", p + Vector3.up * (bottom + s * run / sections) + normal * 0.005f, rotation,
                    new Vector3(1f, run / sections, 1f), group);
            }

            NuclearPiece("K17_Downpipe_Head", p + Vector3.up * roof + normal * 0.005f, rotation, Vector3.one, group);
        }

        private static void WallLamp(Transform group, Vector3 p, Vector3 normal, Quaternion rotation)
        {
            if (!SolidWallAt(p, normal, Vector3.Cross(Vector3.up, normal), 2.6f))
            {
                return;
            }

            Vector3 at = p + Vector3.up * 3.3f + normal * 0.005f;
            NuclearPiece("K17_Wall_Lamp", at, rotation, Vector3.one, group);
            NuclearLight("Light_Facade", at + normal * 0.5f - Vector3.up * 0.2f, WarmLight, 7f, 1.2f, group);
        }

        // ---------------- Conducte pe peretii halelor si ai salilor industriale ----------------

        private static void BuildPipeRuns()
        {
            Transform group = CreateGroup(gNuclear, "Pipe Runs");

            foreach (Face face in CollectWallFaces())
            {
                float height;

                switch (face.Theme)
                {
                    case Theme.Hangar:
                        height = 6.0f;
                        break;
                    case Theme.Industrial:
                        height = 2.75f;
                        break;
                    default:
                        continue;
                }

                if (face.Length < 6f)
                {
                    continue;
                }

                // Tronsoanele umplu toata fata (cap la cap), ca sirul sa ajunga pana in colturi.
                int segments = Mathf.Max(1, Mathf.RoundToInt(face.Length / 2f));
                float segment = face.Length / segments;
                Quaternion rotation = Quaternion.LookRotation(face.Normal);
                Vector3 baseLine = face.Start + Vector3.up * (face.BaseY + height - face.Start.y);

                // X-ul local al piesei (LookRotation(normala)) poate fi +Along sau -Along.
                bool localXAlong = Vector3.Dot(rotation * Vector3.right, face.Along) > 0f;

                bool[] fits = new bool[segments];
                for (int k = 0; k < segments; k++)
                {
                    fits[k] = PipeSegmentFits(baseLine + face.Along * (segment * (k + 0.5f)), face.Normal, face.Along, segment);
                }

                // Siruri continue de tronsoane; fiecare sir are capete: intra in peretele de colt
                // (daca sirul ajunge in colt) sau face cot in peretele din spate.
                int k0 = 0;
                while (k0 < segments)
                {
                    if (!fits[k0])
                    {
                        k0++;
                        continue;
                    }

                    int k1 = k0;
                    while (k1 + 1 < segments && fits[k1 + 1])
                    {
                        k1++;
                    }

                    if (k1 - k0 + 1 >= 2)
                    {
                        for (int k = k0; k <= k1; k++)
                        {
                            Vector3 p = baseLine + face.Along * (segment * (k + 0.5f));
                            NuclearPiece("K17_Pipe_Run", p + face.Normal * 0.01f, rotation,
                                new Vector3(segment / 2f, 1f, 1f), group);
                        }

                        PipeRunEnd(group, baseLine + face.Along * (segment * k0), -face.Along, face.Normal, rotation, localXAlong, k0 == 0);
                        PipeRunEnd(group, baseLine + face.Along * (segment * (k1 + 1)), face.Along, face.Normal, rotation, localXAlong,
                            k1 == segments - 1);
                    }

                    k0 = k1 + 1;
                }
            }
        }

        /// <summary>
        /// Capatul unui sir de conducte: daca sirul ajunge intr-un colt interior, tevile intra drept in
        /// peretele perpendicular (manson); altfel fac cot de 90 de grade in peretele pe care stau.
        /// </summary>
        private static void PipeRunEnd(Transform group, Vector3 end, Vector3 travel, Vector3 normal, Quaternion rotation,
            bool localXAlong, bool atFaceEnd)
        {
            // Piesele de capat sunt modelate pentru un sir care merge spre +X local; oglindim pe X la nevoie.
            bool travelIsLocalX = Vector3.Dot(travel, rotation * Vector3.right) > 0f;
            Vector3 scale = travelIsLocalX ? Vector3.one : new Vector3(-1f, 1f, 1f);

            bool cornerWall = atFaceEnd && Physics.Raycast(end + normal * 0.25f + Vector3.up * 0.45f - travel * 0.1f, travel,
                0.4f, ~0, QueryTriggerInteraction.Ignore);

            NuclearPiece(cornerWall ? "K17_Pipe_Run_Collar" : "K17_Pipe_Run_End", end + normal * 0.01f, rotation, scale, group);
        }

        /// <summary>Peretele e plin in spatele segmentului si nimic nu sta in calea conductelor.</summary>
        private static bool PipeSegmentFits(Vector3 p, Vector3 normal, Vector3 along, float length)
        {
            float edge = length * 0.5f - 0.05f;

            foreach (float s in new[] { -edge, 0f, edge })
            {
                foreach (float h in new[] { 0.1f, 0.8f })
                {
                    Vector3 probe = p + along * s + Vector3.up * h + normal * 0.6f;

                    if (!Physics.Raycast(probe, -normal, out RaycastHit hit, 0.7f, ~0, QueryTriggerInteraction.Ignore) ||
                        hit.distance < 0.5f)
                    {
                        return false;
                    }
                }
            }

            // Spatiul ocupat de conducte (0,9 x 0,45) liber
            Vector3 center = p + normal * 0.25f + Vector3.up * 0.45f;
            Vector3 half = new Vector3(edge, 0.45f, 0.2f);

            return !Physics.CheckBox(center, half, Quaternion.LookRotation(normal), ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Lumina: cer acoperit de dupa-amiaza (soare bland, cu umbre), ambient din cer si o ceata
        /// usoara care da adancime curtilor. Interioarele au lampile lor.
        /// </summary>
        private static void BuildAtmosphere()
        {
            Light sun = null;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                GameObject go = new GameObject("Map_Sun");
                go.transform.SetParent(gLights, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.enabled = true;
            sun.gameObject.SetActive(true);
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            sun.color = new Color(1f, 0.93f, 0.84f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.64f);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
        }
    }
}
