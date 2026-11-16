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
                    shared[i] = BlacksiteK17MapBuilder.KitMaterial(slot);
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
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Debug.Log($"Weapon prefab: {prefabPath} ({slots} sloturi, muzzle={sockets.Muzzle != null}, sight={sockets.Sight != null}, " +
                      $"gripL={sockets.GripLeft != null}, magazine={sockets.Magazine != null}, bolt={sockets.Bolt != null})");
            Object.DestroyImmediate(instance);
        }
    }
}
