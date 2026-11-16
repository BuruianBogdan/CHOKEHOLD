using System.Collections.Generic;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Kit-ul modular (Tools/Blender/k17_kit.py -> Assets/_Project/Art/Models/Kit).
    //
    // Fiecare piesa din kit inlocuieste doar partea vizuala a unei cutii din generator: colliderul,
    // numele si pozitia raman neschimbate, deci layout-ul, validatorul si gameplay-ul nu se schimba.
    // Cutia isi pierde MeshRenderer-ul si primeste un copil "Kit" (in metri, cu baza la podea) cu piesa.
    // Daca un FBX lipseste, cutia ramane cum era.
    public static partial class BlacksiteK17MapBuilder
    {
        private const string KitFolder = "Assets/_Project/Art/Models/Kit";
        private const string KitMaterialFolder = "Assets/_Project/Art/Materials/Kit";

        // Materialele kit-ului, dupa numele slotului din Blender. Se creeaza o singura data:
        // dupa aceea sunt ale tale (texturi, culori) si generatorul nu le mai suprascrie.
        private static readonly Dictionary<string, (Color color, float smoothness, float metallic)> KitSlots =
            new Dictionary<string, (Color, float, float)>
            {
                { "Wood", (new Color(0.55f, 0.42f, 0.28f), 0.2f, 0f) },
                { "WoodDark", (new Color(0.36f, 0.27f, 0.18f), 0.2f, 0f) },
                { "Concrete", (new Color(0.5f, 0.5f, 0.48f), 0.1f, 0f) },
                { "ConcreteDark", (new Color(0.38f, 0.38f, 0.37f), 0.1f, 0f) },
                { "Steel", (new Color(0.36f, 0.37f, 0.4f), 0.45f, 0.8f) },
                { "DoorPaint", (new Color(0.3f, 0.4f, 0.38f), 0.35f, 0.3f) },
                { "Glass", (new Color(0.08f, 0.1f, 0.12f), 0.9f, 0f) },
                { "BarrelPaint", (new Color(0.15f, 0.3f, 0.55f), 0.35f, 0.2f) },
                { "Hazard", (new Color(0.92f, 0.75f, 0.1f), 0.3f, 0f) },
                { "Black", (new Color(0.05f, 0.05f, 0.05f), 0.2f, 0f) },
                { "Rubber", (new Color(0.06f, 0.06f, 0.06f), 0.15f, 0f) },
                { "Paint", (new Color(0.5f, 0.52f, 0.5f), 0.3f, 0.1f) },
                { "ReactorPaint", (new Color(0.82f, 0.8f, 0.72f), 0.4f, 0.1f) },
                { "TankPaint", (new Color(0.45f, 0.55f, 0.5f), 0.35f, 0.2f) },
                { "MotorPaint", (new Color(0.2f, 0.35f, 0.55f), 0.4f, 0.2f) },
                { "TurbinePaint", (new Color(0.55f, 0.6f, 0.62f), 0.45f, 0.4f) },
                { "PipePaint", (new Color(0.55f, 0.2f, 0.12f), 0.3f, 0.2f) },
                // Sloturile *Glow lumineaza (emisie).
                { "ReactorGlow", (new Color(0.25f, 0.65f, 1f), 0.6f, 0f) },
                { "PoolGlow", (new Color(0.1f, 0.55f, 0.95f), 0.8f, 0f) },
                { "BeaconGlow", (new Color(1f, 0.5f, 0.05f), 0.6f, 0f) },
                { "EmergencyGlow", (new Color(1f, 0.1f, 0.05f), 0.6f, 0f) },
                { "ScreenGlow", (new Color(0.25f, 0.95f, 0.55f), 0.8f, 0f) },
                { "LampGlow", (new Color(1f, 0.95f, 0.85f), 0.6f, 0f) },
                { "SteelDark", (new Color(0.2f, 0.21f, 0.23f), 0.4f, 0.7f) },
                { "Hesco", (new Color(0.58f, 0.52f, 0.4f), 0.05f, 0f) },
                { "Soil", (new Color(0.33f, 0.27f, 0.2f), 0.05f, 0f) },
                { "Gravel", (new Color(0.4f, 0.39f, 0.37f), 0.05f, 0f) },
                { "Rust", (new Color(0.45f, 0.25f, 0.15f), 0.2f, 0.3f) },
                { "PipePaintGrey", (new Color(0.48f, 0.5f, 0.5f), 0.35f, 0.3f) },
                { "MilitaryPaint", (new Color(0.32f, 0.36f, 0.26f), 0.25f, 0.2f) },
                { "Canvas", (new Color(0.42f, 0.4f, 0.3f), 0.05f, 0f) },
                { "Ceramic", (new Color(0.55f, 0.38f, 0.28f), 0.6f, 0f) },
            };

        private static readonly Dictionary<string, Material> kitMaterials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, (Mesh mesh, string[] slots)> kitPieces =
            new Dictionary<string, (Mesh, string[])>();

        private static int kitPlaced;

        private static void ApplyKit(GameObject root)
        {
            kitMaterials.Clear();
            kitPieces.Clear();
            kitPlaced = 0;

            if (!AssetDatabase.IsValidFolder(KitFolder))
            {
                Debug.LogWarning("Kit-ul nu exista inca (ruleaza Tools/Blender/k17_kit.py). Raman cutiile gri.");
                return;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;

                if (n.StartsWith("BigCrate_"))
                {
                    KitFixed(t, "K17_Crate_Big", new Vector3(2f, 2f, 2f));
                }
                else if (n.StartsWith("CrateStack_"))
                {
                    KitFixed(t, "K17_Crate_Stack", new Vector3(2f, 3.5f, 2f));
                }
                else if (n.StartsWith("LowCover_"))
                {
                    KitTiled(t, "K17_Barrier_Low", 1.1f, 0.6f);
                }
                else if (n.StartsWith("HighCover_"))
                {
                    KitTiled(t, "K17_Barrier_High", 2f, 0.8f);
                }
                else if (n.StartsWith("Container40_"))
                {
                    KitFixed(t, "K17_Container_40", new Vector3(2.6f, 2.6f, 12f));
                }
                else if (n.StartsWith("Container_"))
                {
                    KitFixed(t, "K17_Container_20", new Vector3(2.6f, 2.6f, 6f));
                }
                else if (n.StartsWith("Bunker_Pillar_"))
                {
                    KitFixed(t, "K17_Pillar_Bunker", new Vector3(1.2f, 5.5f, 1.2f));
                }
                else if (n == "Panel" && t.parent != null && t.parent.GetComponent<SimpleDoor>() != null)
                {
                    KitDoor(t);
                }
                // Instalatia nucleara: utilajele existente isi schimba doar aspectul
                else if (n == "A_Tank_Main")
                {
                    KitFixed(t, "K17_Reactor_Vessel", new Vector3(4f, 4f, 4f));
                }
                else if (n == "A_Tank_Small")
                {
                    KitFixed(t, "K17_Pressurizer", new Vector3(3.5f, 3.4f, 3.5f));
                }
                else if (n.StartsWith("Pumps_Tank_"))
                {
                    KitFixed(t, "K17_Coolant_Tank", new Vector3(3f, 3.2f, 3f));
                }
                else if (n == "Yard_Generator")
                {
                    KitFixed(t, "K17_Transformer", new Vector3(10f, 4f, 8f));
                }
                else if (n == "Outpost_Shed")
                {
                    KitFixed(t, "K17_Guard_Hut", new Vector3(4f, 3f, 5f));
                }
                else if (n == "T1_Truck")
                {
                    KitFixed(t, "K17_Truck", new Vector3(2.5f, 3f, 6.5f));
                }
                else if (n == "A_Generator" || n == "Pumps_Machine")
                {
                    KitFixed(t, "K17_Coolant_Pump", new Vector3(4f, 2.5f, 4f));
                }
                else if (n == "Hangar_Machine")
                {
                    KitFixed(t, "K17_Turbine", new Vector3(6f, 3.6f, 8f));
                }
                else if (n == "Lab_ServerRack")
                {
                    KitFixed(t, "K17_Control_Panels", new Vector3(6f, 2.2f, 3f));
                }
                else if (n == "Security_Lockers")
                {
                    KitFacing(t, "K17_Hazmat_Lockers", new Vector3(1f, 2.2f, 4f));
                }
                else if (n.Contains("_Shelf"))
                {
                    KitTiled(t, "K17_Shelf", 2.2f, 1f, modelAlongX: false);
                }
                else if (n.StartsWith("Crate_"))
                {
                    // Mesele / pupitrele din laborator, birouri, camera de control
                    KitTiled(t, "K17_Console", 1.1f, 1.2f);
                }
                else if (n.StartsWith("Roof_HVAC_"))
                {
                    KitFixed(t, "K17_HVAC", new Vector3(3f, 1.6f, 2f));
                }
                else if (n.Contains("_Pipe_") && t.GetComponent<MeshFilter>() != null &&
                         t.GetComponent<MeshFilter>().sharedMesh != null && t.GetComponent<MeshFilter>().sharedMesh.name == "Cylinder")
                {
                    KitPipe(t);
                }
            }

            Debug.Log($"Kit: {kitPlaced} piese plasate.");
        }

        /// <summary>Piesa cu dimensiuni fixe, intinsa (putin) pe dimensiunea reala a cutiei.</summary>
        private static void KitFixed(Transform box, string piece, Vector3 nominal)
        {
            Transform holder = KitHolder(box, out Vector3 size);

            if (holder == null || !KitPiece(holder, piece, Vector3.zero, Quaternion.identity,
                    new Vector3(size.x / nominal.x, size.y / nominal.y, size.z / nominal.z), box))
            {
                KitRevert(box, holder);
            }
        }

        /// <summary>
        /// Piesa cu fata (usi de dulap) spre camera: se intoarce cu 180 de grade daca peretele e
        /// in partea spre care ar privi.
        /// </summary>
        private static void KitFacing(Transform box, string piece, Vector3 nominal)
        {
            Transform holder = KitHolder(box, out Vector3 size);

            if (holder == null)
            {
                return;
            }

            // Piesa priveste spre +X local; verificam ce e imediat in fata cutiei.
            Collider own = box.GetComponent<Collider>();
            Vector3 front = box.right;
            Vector3 origin = own != null ? own.bounds.center : box.position;
            bool wallInFront = Physics.Raycast(origin + front * (size.x * 0.5f + 0.02f), front, 0.4f, ~0, QueryTriggerInteraction.Ignore);

            if (!KitPiece(holder, piece, Vector3.zero, wallInFront ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity,
                    new Vector3(size.x / nominal.x, size.y / nominal.y, size.z / nominal.z), box))
            {
                KitRevert(box, holder);
            }
        }

        /// <summary>
        /// Piesa de 2 m pusa in sir pe latura lunga a cutiei (bariere, rafturi).
        /// modelAlongX: piesa e modelata cu lungimea pe X (altfel pe Z).
        /// </summary>
        private static void KitTiled(Transform box, string piece, float nominalHeight, float nominalThickness,
            bool modelAlongX = true)
        {
            Transform holder = KitHolder(box, out Vector3 size);

            if (holder == null)
            {
                return;
            }

            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            float segment = length / count;

            // Daca orientarea cutiei difera de a piesei, piesa se roteste cu 90 de grade.
            Quaternion rotation = alongX == modelAlongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            float thickness = alongX ? size.z : size.x;

            for (int k = 0; k < count; k++)
            {
                float offset = -length * 0.5f + segment * (k + 0.5f);
                Vector3 position = alongX ? new Vector3(offset, 0f, 0f) : new Vector3(0f, 0f, offset);

                Vector3 scale = modelAlongX
                    ? new Vector3(segment / 2f, size.y / nominalHeight, thickness / nominalThickness)
                    : new Vector3(thickness / nominalThickness, size.y / nominalHeight, segment / 2f);

                if (!KitPiece(holder, piece, position, rotation, scale, box))
                {
                    KitRevert(box, holder);
                    return;
                }
            }
        }

        /// <summary>
        /// Conducta mare (cilindrul Unity, axa pe Y local): tronsoane cu flanse de ~3 m; la capete intra
        /// in perete printr-o trecere, altfel se termina cu flansa oarba; sustineri pana la tavan sau podea.
        /// </summary>
        private static void KitPipe(Transform pipe)
        {
            Transform holder = KitHolder(pipe, out Vector3 size);

            if (holder == null)
            {
                return;
            }

            float length = size.y;
            float diameter = (size.x + size.z) * 0.5f;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 3f));
            float segment = length / count;

            for (int k = 0; k < count; k++)
            {
                if (!KitPiece(holder, "K17_Pipe_Big", new Vector3(0f, segment * (k + 0.5f), 0f), Quaternion.identity,
                        new Vector3(diameter, segment / 3f, diameter), pipe))
                {
                    KitRevert(pipe, holder);
                    return;
                }
            }

            Collider own = pipe.GetComponent<Collider>();
            Vector3 axis = pipe.up;
            Vector3 center = own.bounds.center;
            Transform group = holder;

            // Capete
            foreach (int s in new[] { 1, -1 })
            {
                Vector3 dir = axis * s;
                Vector3 end = center + dir * (length * 0.5f);
                RaycastHit wall = default;
                bool intoWall = false;

                foreach (RaycastHit hit in Physics.RaycastAll(end - dir * 0.3f, dir, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != own && hit.collider.transform.parent != pipe && Vector3.Dot(hit.normal, -dir) > 0.7f)
                    {
                        wall = hit;
                        intoWall = true;
                        break;
                    }
                }

                GameObject piece = intoWall
                    ? CreateKitObject(group, "K17_Pipe_Collar", Vector3.zero, Quaternion.identity, Vector3.one)
                    : CreateKitObject(group, "K17_Pipe_Cap", Vector3.zero, Quaternion.identity, Vector3.one);

                if (piece != null)
                {
                    Vector3 normal = intoWall ? wall.normal : dir;
                    piece.transform.SetPositionAndRotation(intoWall ? wall.point : end, Quaternion.FromToRotation(Vector3.up, normal));
                    piece.transform.localScale = Vector3.one;
                    SetWorldScale(piece.transform, new Vector3(diameter, 1f, diameter));
                }
            }

            // Sustineri pe conductele orizontale: tije spre tavan sau picioare spre podea, la ~4 m
            if (Mathf.Abs(axis.y) > 0.3f)
            {
                return;
            }

            // Grup aliniat cu lumea (holder-ul e rotit dupa teava).
            Transform supportsGroup = CreateGroup(pipe.parent, pipe.name + "_Supports");
            int supports = Mathf.Max(1, Mathf.FloorToInt(length / 4f));
            float radius = diameter * 0.5f;
            Quaternion along = Quaternion.LookRotation(new Vector3(axis.x, 0f, axis.z).normalized);

            for (int k = 0; k < supports; k++)
            {
                Vector3 p = center + axis * (-length * 0.5f + length * (k + 0.5f) / supports);

                // Conductele joase stau pe picioare; cele inalte atarna din tavan (daca e unul aproape).
                if (p.y > 2.5f && RayIgnoringPipes(p + Vector3.up * (radius + 0.05f), Vector3.up, 6f, out RaycastHit ceiling))
                {
                    // Tija pana in tavan si o bratara peste teava
                    float h = ceiling.point.y - (p.y + radius);
                    Decor(supportsGroup, $"Hanger_{k}", new Vector3(p.x, p.y + radius + h * 0.5f, p.z), new Vector3(0.05f, h, 0.05f), frameMat);
                    GameObject clamp = Decor(supportsGroup, $"Clamp_{k}", p + Vector3.up * (radius + 0.03f),
                        new Vector3(diameter + 0.08f, 0.06f, 0.1f), frameMat);
                    clamp.transform.rotation = along;
                }
                else if (Physics.Raycast(p - Vector3.up * (radius + 0.05f), Vector3.down, out RaycastHit floor, 8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Picior din profil pana la podea si sa sub teava
                    float h = (p.y - radius) - floor.point.y;
                    Decor(supportsGroup, $"Support_{k}", new Vector3(p.x, floor.point.y + h * 0.5f, p.z), new Vector3(0.18f, h, 0.18f), frameMat);
                    GameObject saddle = Decor(supportsGroup, $"Saddle_{k}", new Vector3(p.x, p.y - radius - 0.02f, p.z),
                        new Vector3(diameter + 0.1f, 0.08f, 0.22f), frameMat);
                    saddle.transform.rotation = along;
                }
            }
        }

        /// <summary>Prima suprafata pe directia data care nu e o alta conducta (tavan, podea, perete).</summary>
        private static bool RayIgnoringPipes(Vector3 from, Vector3 dir, float max, out RaycastHit result)
        {
            result = default;
            float best = float.MaxValue;

            foreach (RaycastHit hit in Physics.RaycastAll(from, dir, max, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < best && !hit.collider.name.Contains("_Pipe_"))
                {
                    best = hit.distance;
                    result = hit;
                }
            }

            return best < float.MaxValue;
        }

        /// <summary>Scara obiectului in lume, indiferent de parinte (fara rotatii neuniforme).</summary>
        private static void SetWorldScale(Transform t, Vector3 world)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
        }

        /// <summary>Canatul de usa: pivotul piesei e in balama, ca la pivotul usii din generator.</summary>
        private static void KitDoor(Transform panel)
        {
            Transform holder = KitHolder(panel, out Vector3 size);

            if (holder == null)
            {
                return;
            }

            // Panoul e centrat pe (0, 1.2, w/2) fata de balama: coboram la balama.
            holder.localPosition = new Vector3(0f, -0.5f, -0.5f);

            if (!KitPiece(holder, "K17_Door_Leaf", Vector3.zero, Quaternion.identity,
                    new Vector3(1f, size.y / 2.4f, size.z / 1.4f), panel, isStatic: false))
            {
                KitRevert(panel, holder);
            }
        }

        /// <summary>Copilul "Kit" al cutiei: spatiu in metri, fara rotatie fata de cutie, cu originea la baza.</summary>
        private static Transform KitHolder(Transform box, out Vector3 size)
        {
            MeshFilter filter = box.GetComponent<MeshFilter>();
            size = Vector3.zero;

            if (filter == null || filter.sharedMesh == null)
            {
                return null;
            }

            // Din limitele plasei: cubul are inaltimea 1, cilindrul Unity 2 (rezervoarele).
            Bounds bounds = filter.sharedMesh.bounds;
            size = Vector3.Scale(bounds.size, Abs(box.lossyScale));

            if (size.x < 0.01f || size.y < 0.01f || size.z < 0.01f)
            {
                return null;
            }

            GameObject holder = new GameObject("Kit");
            holder.transform.SetParent(box, false);
            holder.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            holder.transform.localRotation = Quaternion.identity;

            Vector3 local = box.localScale;
            holder.transform.localScale = new Vector3(1f / local.x, 1f / local.y, 1f / local.z);
            holder.layer = box.gameObject.layer;
            return holder.transform;
        }

        private static bool KitPiece(Transform holder, string piece, Vector3 position, Quaternion rotation,
            Vector3 scale, Transform box, bool isStatic = true)
        {
            MeshRenderer boxRenderer = box.GetComponent<MeshRenderer>();
            Material boxMaterial = boxRenderer != null ? boxRenderer.sharedMaterial : null;

            GameObject go = CreateKitObject(holder, piece, position, rotation, scale, boxMaterial, isStatic);

            if (go == null)
            {
                return false;
            }

            go.layer = box.gameObject.layer;

            // Cutia ramane doar collider.
            if (boxRenderer != null)
            {
                Object.DestroyImmediate(boxRenderer);
                Object.DestroyImmediate(box.GetComponent<MeshFilter>());
            }

            kitPlaced++;
            return true;
        }

        /// <summary>Obiect cu o piesa din kit (fara collider). containerMaterial inlocuieste slotul "Container".</summary>
        private static GameObject CreateKitObject(Transform parent, string piece, Vector3 localPosition,
            Quaternion localRotation, Vector3 localScale, Material containerMaterial = null, bool isStatic = true)
        {
            if (!LoadKitPiece(piece, out Mesh mesh, out string[] slots))
            {
                return null;
            }

            GameObject go = new GameObject(piece);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();

            Material[] materials = new Material[slots.Length];
            for (int k = 0; k < slots.Length; k++)
            {
                // Containerul isi pastreaza culoarea (rosu / albastru) data de generator.
                materials[k] = slots[k] == "Container" && containerMaterial != null ? containerMaterial : KitMaterial(slots[k]);
            }

            renderer.sharedMaterials = materials;

            if (isStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            }

            return go;
        }

        private static void KitRevert(Transform box, Transform holder)
        {
            if (holder != null)
            {
                Object.DestroyImmediate(holder.gameObject);
            }
        }

        private static bool LoadKitPiece(string piece, out Mesh mesh, out string[] slots)
        {
            if (kitPieces.TryGetValue(piece, out var cached))
            {
                mesh = cached.mesh;
                slots = cached.slots;
                return mesh != null;
            }

            mesh = null;
            slots = null;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/{piece}.fbx");
            MeshFilter filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
            MeshRenderer renderer = model != null ? model.GetComponentInChildren<MeshRenderer>() : null;

            if (filter != null && renderer != null)
            {
                mesh = filter.sharedMesh;
                slots = new string[renderer.sharedMaterials.Length];

                for (int k = 0; k < slots.Length; k++)
                {
                    Material material = renderer.sharedMaterials[k];
                    slots[k] = material != null ? material.name : "Concrete";
                }
            }
            else
            {
                Debug.LogWarning($"Kit: lipseste {KitFolder}/{piece}.fbx");
            }

            kitPieces[piece] = (mesh, slots);
            return mesh != null;
        }

        private static Material KitMaterial(string slot)
        {
            if (kitMaterials.TryGetValue(slot, out Material cached))
            {
                return cached;
            }

            EnsureFolder(KitMaterialFolder);
            string path = $"{KitMaterialFolder}/K17_{slot}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            bool created = material == null;

            if (created)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
            }

            // Culoarea de baza se aplica la fiecare generare pana cand materialul primeste o textura;
            // dupa aceea e al tau si generatorul nu il mai atinge.
            bool textured = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;

            if (!textured)
            {

                (Color color, float smoothness, float metallic) look =
                    KitSlots.TryGetValue(slot, out var known) ? known : (new Color(0.6f, 0.6f, 0.6f), 0.2f, 0f);

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", look.color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", look.color);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", look.smoothness);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", look.metallic);
                }

                if (slot.EndsWith("Glow") && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    material.SetColor("_EmissionColor", look.color * 3f);
                }

                EditorUtility.SetDirty(material);
            }

            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }

            kitMaterials[slot] = material;
            return material;
        }
    }

    /// <summary>Setari de import pentru piesele kit-ului (scara 1 m, UV2 pentru lightmap, fara collider).</summary>
    public class KitModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/Models/Kit/"))
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.generateSecondaryUV = true;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}
