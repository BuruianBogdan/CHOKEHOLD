using System.IO;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Mainile de la persoana intai: prefab-urile FPArms_CT / FPArms_T (din Art/Characters/FPArms, exportate de
    /// k17_characters.py), layer-ul FirstPerson pentru camera overlay si legarea lor de jucatorul din scena
    /// (FirstPersonAnimator pe WeaponHolder, langa HitscanWeapon).
    /// </summary>
    public static class FirstPersonBuilder
    {
        private const string ArmsFolder = "Assets/_Project/Art/Characters/FPArms";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Characters";

        [MenuItem("CHOKEHOLD/Characters/Build FP Arms")]
        public static void BuildAll()
        {
            EnsureLayer(FirstPersonAnimator.LayerName);
            BlacksiteK17MapBuilder.EnsureFolder(PrefabFolder);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ArmsFolder }))
            {
                Build(AssetDatabase.GUIDToAssetPath(guid));
            }

            AssetDatabase.SaveAssets();

            FpsController player = Object.FindFirstObjectByType<FpsController>();
            if (player != null)
            {
                WirePlayer(player);
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
                EditorSceneManager.SaveScene(player.gameObject.scene);
            }
        }

        private static void Build(string modelPath)
        {
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            string name = Path.GetFileNameWithoutExtension(modelPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;

            int triangles = 0;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string slot = shared[i] != null ? shared[i].name.Replace(" (Instance)", "") : "Plastic";
                    shared[i] = BlacksiteK17MapBuilder.KitMaterial(slot);
                }
                renderer.sharedMaterials = shared;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    triangles += skinned.sharedMesh.triangles.Length / 3;
                }
            }

            // Animator-ul din import nu e folosit: mainile se misca prin IK (FirstPersonArms)
            Animator animator = instance.GetComponent<Animator>();
            if (animator != null)
            {
                Object.DestroyImmediate(animator);
            }

            if (instance.GetComponent<FirstPersonArms>() == null)
            {
                instance.AddComponent<FirstPersonArms>();
            }

            BlacksiteK17MapBuilder.ApplySurfaces(instance);
            string prefabPath = $"{PrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"FP arms: {prefabPath} ({triangles} triunghiuri)");
        }

        /// <summary>FirstPersonAnimator pe WeaponHolder-ul jucatorului, cu mainile ambelor echipe.</summary>
        internal static void WirePlayer(FpsController player)
        {
            HitscanWeapon weapon = player.GetComponentInChildren<HitscanWeapon>(true);
            if (weapon == null)
            {
                return;
            }

            FirstPersonAnimator animator = weapon.GetComponent<FirstPersonAnimator>();
            if (animator == null)
            {
                animator = weapon.gameObject.AddComponent<FirstPersonAnimator>();
            }

            SerializedObject serialized = new SerializedObject(animator);
            serialized.FindProperty("defenderArms").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FPArms_CT.prefab");
            serialized.FindProperty("attackerArms").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FPArms_T.prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Adauga layer-ul in primul slot liber (8 - 31) daca nu exista deja.</summary>
        internal static void EnsureLayer(string layer)
        {
            if (LayerMask.NameToLayer(layer) >= 0)
            {
                return;
            }

            SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layer;
                    tags.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"Layer {layer} = {i}");
                    return;
                }
            }

            Debug.LogWarning($"Nu exista slot liber pentru layer-ul {layer}.");
        }
    }
}
