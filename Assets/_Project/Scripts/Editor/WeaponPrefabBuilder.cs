using System.IO;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Prefab-uri pentru arme din FBX-urile generate de Tools/Blender/k17_weapons.py:
    /// materiale K17 dupa numele slotului, WeaponSockets rezolvat, umbre (TP) / fara umbre (FP).
    /// Prefab: Assets/_Project/Prefabs/Weapons/<Nume>_FP.prefab si _TP.prefab.
    /// </summary>
    public static class WeaponPrefabBuilder
    {
        private const string WeaponsFolder = "Assets/_Project/Art/Weapons";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Weapons";

        [MenuItem("CHOKEHOLD/Characters/Build Weapon Prefabs")]
        public static void BuildAll()
        {
            BlacksiteK17MapBuilder.EnsureFolder(PrefabFolder);

            // Texturile din Tripo pot fi importate inainte de regulile din CharacterImporter (normal map ca
            // imagine color, harta metalica sRGB): reimportul le aplica din nou.
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { WeaponsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Textures/"))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { WeaponsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Build(path, Path.GetFileNameWithoutExtension(path));
            }

            AssetDatabase.SaveAssets();
        }

        private static void Build(string modelPath, string name)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;
            bool firstPerson = name.EndsWith("_FP");

            int slots = 0;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string slot = shared[i] != null ? shared[i].name.Replace(" (Instance)", "") : "GunMetal";
                    shared[i] = CharacterImporter.CharacterMaterial(slot);
                    slots++;
                }
                renderer.sharedMaterials = shared;
                // Arma de la persoana intai nu arunca umbre (ar aparea pe pereti langa camera)
                renderer.shadowCastingMode = firstPerson
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.On;
            }

            WeaponSockets sockets = instance.GetComponent<WeaponSockets>();
            if (sockets == null)
            {
                sockets = instance.AddComponent<WeaponSockets>();
            }
            sockets.Resolve();

            string prefabPath = $"{PrefabFolder}/{name}.prefab";
            // Texturile PBR (k17_textures.py) pe materialele K17 folosite de model; harta le aplica doar pe ce e in scena.
            BlacksiteK17MapBuilder.ApplySurfaces(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Debug.Log($"Weapon prefab: {prefabPath} ({slots} sloturi, muzzle={sockets.Muzzle != null}, sight={sockets.Sight != null}, " +
                      $"gripL={sockets.GripLeft != null}, magazine={sockets.Magazine != null}, bolt={sockets.Bolt != null})");
            Object.DestroyImmediate(instance);
        }
    }
}
