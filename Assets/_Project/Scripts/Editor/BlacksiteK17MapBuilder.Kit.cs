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
                { "Concrete", (new Color(0.62f, 0.62f, 0.6f), 0.1f, 0f) },
                { "ConcreteDark", (new Color(0.45f, 0.45f, 0.44f), 0.1f, 0f) },
                { "Steel", (new Color(0.36f, 0.37f, 0.4f), 0.45f, 0.8f) },
                { "DoorPaint", (new Color(0.3f, 0.4f, 0.38f), 0.35f, 0.3f) },
                { "Glass", (new Color(0.08f, 0.1f, 0.12f), 0.9f, 0f) },
                { "BarrelPaint", (new Color(0.15f, 0.3f, 0.55f), 0.35f, 0.2f) },
                { "Hazard", (new Color(0.92f, 0.75f, 0.1f), 0.3f, 0f) },
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
                    KitTiled(t, "K17_Barrier_Low");
                }
                else if (n.StartsWith("HighCover_"))
                {
                    KitTiled(t, "K17_Barrier_High");
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

        /// <summary>Piesa de 2 m pusa in sir pe latura lunga a cutiei (bariere).</summary>
        private static void KitTiled(Transform box, string piece)
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

            // Piesa e modelata pe X; pe o cutie lunga pe Z se roteste cu 90 de grade.
            Quaternion rotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            float thickness = alongX ? size.z : size.x;

            for (int k = 0; k < count; k++)
            {
                float offset = -length * 0.5f + segment * (k + 0.5f);
                Vector3 position = alongX ? new Vector3(offset, 0f, 0f) : new Vector3(0f, 0f, offset);

                // Bariera inalta are 0,8 m grosime nominala, cea joasa 0,6.
                float nominalThickness = piece == "K17_Barrier_High" ? 0.8f : 0.6f;
                float nominalHeight = piece == "K17_Barrier_High" ? 2f : 1.1f;
                Vector3 scale = new Vector3(segment / 2f, size.y / nominalHeight, thickness / nominalThickness);

                if (!KitPiece(holder, piece, position, rotation, scale, box))
                {
                    KitRevert(box, holder);
                    return;
                }
            }
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
            size = Abs(box.lossyScale);

            if (box.GetComponent<MeshFilter>() == null || size.x < 0.01f || size.y < 0.01f || size.z < 0.01f)
            {
                return null;
            }

            GameObject holder = new GameObject("Kit");
            holder.transform.SetParent(box, false);
            holder.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            holder.transform.localRotation = Quaternion.identity;

            Vector3 local = box.localScale;
            holder.transform.localScale = new Vector3(1f / local.x, 1f / local.y, 1f / local.z);
            holder.layer = box.gameObject.layer;
            return holder.transform;
        }

        private static bool KitPiece(Transform holder, string piece, Vector3 position, Quaternion rotation,
            Vector3 scale, Transform box, bool isStatic = true)
        {
            if (!LoadKitPiece(piece, out Mesh mesh, out string[] slots))
            {
                return false;
            }

            MeshRenderer boxRenderer = box.GetComponent<MeshRenderer>();
            Material boxMaterial = boxRenderer != null ? boxRenderer.sharedMaterial : null;

            GameObject go = new GameObject(piece);
            go.transform.SetParent(holder, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.layer = box.gameObject.layer;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();

            Material[] materials = new Material[slots.Length];
            for (int k = 0; k < slots.Length; k++)
            {
                // Containerul isi pastreaza culoarea (rosu / albastru) data de generator.
                materials[k] = slots[k] == "Container" && boxMaterial != null ? boxMaterial : KitMaterial(slots[k]);
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

            // Cutia ramane doar collider.
            if (boxRenderer != null)
            {
                Object.DestroyImmediate(boxRenderer);
                Object.DestroyImmediate(box.GetComponent<MeshFilter>());
            }

            kitPlaced++;
            return true;
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

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);

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
