using System.Collections.Generic;
using System.Linq;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChokeHold.EditorTools
{
    // Blacksite K-17 ca instalatie nucleara inchisa:
    //   - tot complexul e sub acoperis (curtile devin hale cu tavan la 8,5 m);
    //   - fara soare: lumina vine doar din lampi, ambient rece si intunecat, ceata usoara.
    public static partial class BlacksiteK17MapBuilder
    {
        private static readonly Color HallLight = new Color(0.82f, 0.95f, 0.92f);

        /// <summary>
        /// Pune tavan peste fiecare celula walkable care vede cerul. Tavanul sta pe varful peretilor
        /// interiori (8,5 m), deci se leaga de toti peretii; ce e deja acoperit ramane cum e.
        /// </summary>
        private static void EncloseMap()
        {
            Physics.SyncTransforms();

            bool[,] open = new bool[GridX, GridZ];
            int count = 0;

            for (int i = 0; i < GridX; i++)
            {
                for (int j = 0; j < GridZ; j++)
                {
                    float x = CellCenterX(i);
                    float z = CellCenterZ(j);

                    if (!IsWalkable(x, z) && !IsOpenPit(x, z))
                    {
                        continue;
                    }

                    // Primul obstacol de sus in jos: daca e sub varful peretilor, celula vede cerul.
                    bool sky = !Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 60f,
                                   ~0, QueryTriggerInteraction.Ignore) ||
                               hit.point.y < InnerWallHeight - 0.1f;

                    open[i, j] = sky;
                    count += sky ? 1 : 0;
                }
            }

            int index = 0;

            MergeCells(open, (i, j, width, depth) =>
            {
                float xMin = MinX + i * Cell;
                float zMin = MinZ + j * Cell;
                Roof($"Roof_Hall_{index++:00}", xMin, zMin, xMin + width * Cell, zMin + depth * Cell, InnerWallHeight);
            });

            Debug.Log($"Inchidere: {count} celule acoperite cu {index} placi de tavan.");
        }

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
            NuclearLight("Light_Reactor_Glow", new Vector3(c.x, 1.6f, c.z), ReactorBlue, 9f, 3f);

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

        // ---------------- Conducte pe peretii halelor si ai salilor industriale ----------------

        private static void BuildPipeRuns()
        {
            Transform group = CreateGroup(gNuclear, "Pipe Runs");

            foreach (Face face in CollectWallFaces())
            {
                float height;

                switch (face.Theme)
                {
                    case Theme.Exterior:
                    case Theme.Hangar:
                        height = 6.0f;
                        break;
                    case Theme.Industrial:
                        height = 2.75f;
                        break;
                    default:
                        continue;
                }

                int segments = Mathf.FloorToInt(face.Length / 2f);
                Quaternion rotation = Quaternion.LookRotation(face.Normal);

                for (int k = 0; k < segments; k++)
                {
                    // Pozitie pe fata peretelui (pivotul piesei e pe perete, jos), centrat pe segment.
                    Vector3 p = face.Start + face.Along * (k * 2f + 1f + (face.Length - segments * 2f) * 0.5f) +
                                Vector3.up * (face.BaseY + height - face.Start.y);

                    if (!PipeSegmentFits(p, face.Normal, face.Along))
                    {
                        continue;
                    }

                    // Piesa e modelata pe X; LookRotation(normala) pune X-ul local de-a lungul peretelui.
                    NuclearPiece("K17_Pipe_Run", p + face.Normal * 0.01f, rotation, Vector3.one, group);
                }
            }
        }

        /// <summary>Peretele e plin in spatele segmentului si nimic nu sta in calea conductelor.</summary>
        private static bool PipeSegmentFits(Vector3 p, Vector3 normal, Vector3 along)
        {
            foreach (float s in new[] { -0.95f, 0f, 0.95f })
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

            // Spatiul ocupat de conducte (0,9 x 0,45) liber, si tavan deasupra
            Vector3 center = p + normal * 0.25f + Vector3.up * 0.45f;
            Vector3 half = new Vector3(0.95f, 0.45f, 0.2f);

            return !Physics.CheckBox(center, half, Quaternion.LookRotation(normal), ~0, QueryTriggerInteraction.Ignore) &&
                   Physics.Raycast(p + normal * 0.4f, Vector3.up, 3f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Atmosfera de complex subteran: fara soare, ambient intunecat si rece, ceata.</summary>
        private static void BuildAtmosphere()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional && light.enabled)
                {
                    light.enabled = false;
                    Debug.Log($"Atmosfera: lumina directionala '{light.name}' oprita (harta e acoperita).");
                }
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Destul de luminos ca un jucator sa se vada pe orice fundal (joc competitiv),
            // destul de intunecat ca lampile sa faca atmosfera.
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.34f, 0.35f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.25f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.13f, 0.135f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.005f;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.065f);

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.35f;
        }
    }
}
