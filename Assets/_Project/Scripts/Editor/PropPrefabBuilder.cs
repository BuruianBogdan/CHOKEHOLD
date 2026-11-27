using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Prefab-uri pentru recuzita din Tripo (FBX-urile din Tools/Blender/k17_tripo_props.py):
    /// LODGroup din LOD0/1/2, materialul "&lt;Nume&gt;_Body" facut din texturi, BoxCollider pe forma piesei.
    /// Prefab: Assets/_Project/Prefabs/Props/&lt;Nume&gt;.prefab.
    /// </summary>
    public static class PropPrefabBuilder
    {
        private const string PropsFolder = "Assets/_Project/Art/Props";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Props";

        [MenuItem("CHOKEHOLD/Map/Build Prop Prefabs")]
        public static void BuildAll()
        {
            BlacksiteK17MapBuilder.EnsureFolder(PrefabFolder);

            // texturile se reimporta cu regulile din CharacterImporter (normal map, harta metalica liniara)
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PropsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Textures/"))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            int built = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PropsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Build(path, Path.GetFileNameWithoutExtension(path));
                built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Prop prefabs: {built}");
        }

        private static void Build(string modelPath, string name)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            Material material = CharacterImporter.CharacterMaterial($"{name}_Body");
            Renderer[][] levels = new Renderer[3][];
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                for (int level = 0; level < 3; level++)
                {
                    if (renderer.name.EndsWith($"LOD{level}"))
                    {
                        levels[level] = new[] { renderer };
                        // umbrele doar de la LOD0 si LOD1 (LOD2 e departe)
                        if (level == 2) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }

            LODGroup group = instance.GetComponent<LODGroup>();
            if (group == null) group = instance.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.30f, levels[0] ?? new Renderer[0]),
                new LOD(0.10f, levels[1] ?? new Renderer[0]),
                new LOD(0.012f, levels[2] ?? new Renderer[0]),
            });
            group.RecalculateBounds();

            // Collider: cutia LOD0 (simplu si previzibil pentru tragere si miscare)
            Bounds bounds = levels[0] != null ? levels[0][0].bounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
            BoxCollider box = instance.GetComponent<BoxCollider>();
            if (box == null) box = instance.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            string prefabPath = $"{PrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Debug.Log($"Prop prefab: {prefabPath} size={bounds.size}");
            Object.DestroyImmediate(instance);
        }
    }
}
