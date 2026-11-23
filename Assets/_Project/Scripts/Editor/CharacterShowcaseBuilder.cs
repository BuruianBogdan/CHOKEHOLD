using System.Linq;
using ChokeHold.Gameplay;
using ChokeHold.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Genereaza scena Assets/_Project/Scenes/CharacterShowcase.unity: podium cu personajele, stand cu armele,
    /// rig FP separat (camera + mainile + arma) si meniul de debug (CharacterShowcase). Scena se reface din cod.
    /// </summary>
    public static class CharacterShowcaseBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CharacterShowcase.unity";
        private const string PostProfilePath = "Assets/_Project/Settings/K17_PostProcess.asset";

        [MenuItem("CHOKEHOLD/Characters/Build Showcase Scene")]
        public static void Build()
        {
            string previous = SceneManager.GetActiveScene().path;
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lumini: cheie (umbre), umplere rece, contur din spate; ambient in trei tonuri
            Light(new Vector3(38f, 148f, 0f), 2.4f, new Color(1f, 0.96f, 0.9f), LightShadows.Soft, "Key Light");
            Light(new Vector3(18f, -62f, 0f), 0.8f, new Color(0.75f, 0.85f, 1f), LightShadows.None, "Fill Light");
            Light(new Vector3(22f, 10f, 0f), 1.3f, new Color(0.9f, 0.95f, 1f), LightShadows.None, "Rim Light");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.73f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.16f);
            RenderSettings.skybox = null;

            // Podea si podium (blatul podiumului la y = 0, acolo stau personajele)
            GameObject floor = Primitive(PrimitiveType.Plane, "Floor", new Vector3(4f, -0.15f, 0f), Vector3.one * 4f, "ConcreteDark", true);
            floor.GetComponent<Renderer>().sharedMaterial = StudioMaterial("Showcase_Floor", new Color(0.24f, 0.25f, 0.26f), 0.25f);
            floor.isStatic = true;
            Primitive(PrimitiveType.Cylinder, "Podium", new Vector3(0f, -0.075f, 0f), new Vector3(2.2f, 0.075f, 2.2f), "Steel", true);
            Primitive(PrimitiveType.Cylinder, "PodiumTop", new Vector3(0f, -0.004f, 0f), new Vector3(2.0f, 0.004f, 2.0f), "Rubber");
            GameObject spot = new GameObject("PodiumSpot");

            // Standul de arme: panou si armele FP (mai detaliate) una sub alta, cu teava spre dreapta
            GameObject stand = new GameObject("WeaponStand");
            stand.transform.position = new Vector3(2.9f, 0f, 0f);
            GameObject board = Primitive(PrimitiveType.Cube, "Board", new Vector3(2.7f, 1.05f, -0.14f), new Vector3(2.7f, 2.1f, 0.04f), "WoodDark");
            board.transform.SetParent(stand.transform, true);
            WeaponData[] weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/_Project/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w != null)
                .OrderBy(w => w.kind).ThenBy(w => w.team).ThenBy(w => w.name)
                .ToArray();

            // Pe stand: armele din joc (de cumparat + cutitele), pustile intr-o coloana, pistoalele si cutitele in alta
            WeaponData[] displayed = weapons.Where(w => w.firstPersonPrefab != null && (w.buyable || w.IsKnife))
                .OrderBy(w => w.category).ThenBy(w => w.price).ThenBy(w => w.name).ToArray();
            int rifleRow = 0;
            int sideRow = 0;
            foreach (WeaponData data in displayed)
            {
                bool longGun = data.kind == WeaponKind.Rifle;
                int row = longGun ? rifleRow++ : sideRow++;
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(data.firstPersonPrefab);
                model.name = data.name;
                model.transform.SetParent(stand.transform, false);
                // teava spre -X (dreapta in vederea din fata): pustile cu manerul la 2,6 m, restul la 3,6 m
                model.transform.position = new Vector3(longGun ? 2.6f : 3.6f, 1.88f - row * 0.32f, 0f);
                model.transform.rotation = Quaternion.Euler(0f, -90f, 0f);    // teava spre dreapta in vederea din fata
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }

            // Camera principala (orbitala)
            Camera camera = new GameObject("ShowcaseCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.14f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // Rig FP: departe de podium, cu un perete in fata ca fundal
            GameObject fpRoot = new GameObject("FirstPersonRig");
            fpRoot.transform.position = new Vector3(14f, 1.65f, 0f);
            Camera fpCamera = new GameObject("FirstPersonCamera").AddComponent<Camera>();
            fpCamera.transform.SetParent(fpRoot.transform, false);
            fpCamera.fieldOfView = 58f;
            fpCamera.nearClipPlane = 0.01f;
            fpCamera.clearFlags = CameraClearFlags.SolidColor;
            fpCamera.backgroundColor = new Color(0.12f, 0.13f, 0.14f);
            fpCamera.enabled = false;
            fpCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            GameObject holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(fpCamera.transform, false);
            Primitive(PrimitiveType.Cube, "FP Backdrop", new Vector3(14f, 1.6f, 7f), new Vector3(10f, 4f, 0.2f), "Concrete");

            // Post-procesarea hartii (tonemapping, expunere)
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile != null)
            {
                Volume volume = new GameObject("Post Process").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            CharacterShowcase showcase = new GameObject("Showcase").AddComponent<CharacterShowcase>();
            GameObject[] characters = AssetDatabase.FindAssets("Character_ t:Prefab", new[] { "Assets/_Project/Prefabs/Characters" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .OrderBy(p => p.name.StartsWith("Character_CT") ? 0 : 1).ThenBy(p => p.name)
                .ToArray();
            GameObject[] arms =
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FPArms_CT.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FPArms_T.prefab"),
            };

            SerializedObject serialized = new SerializedObject(showcase);
            SetArray(serialized.FindProperty("characterPrefabs"), characters);
            SetArray(serialized.FindProperty("weapons"), weapons);
            SetArray(serialized.FindProperty("firstPersonArms"), arms);
            serialized.FindProperty("podium").objectReferenceValue = spot.transform;
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("weaponStand").objectReferenceValue = stand.transform;
            serialized.FindProperty("firstPersonCamera").objectReferenceValue = fpCamera;
            serialized.FindProperty("firstPersonHolder").objectReferenceValue = holder.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BlacksiteK17MapBuilder.EnsureFolder("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Showcase: {ScenePath} ({characters.Length} personaje, {weapons.Length} arme)");

            if (!string.IsNullOrEmpty(previous) && previous != ScenePath)
            {
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
        }

        /// <summary>Material simplu (fara textura) pentru studio: podeaua nu trebuie sa concureze cu personajul.</summary>
        private static Material StudioMaterial(string name, Color color, float smoothness)
        {
            const string folder = "Assets/_Project/Art/Materials/Showcase";
            BlacksiteK17MapBuilder.EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Light(Vector3 euler, float intensity, Color color, LightShadows shadows, string name)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(euler);
            light.intensity = intensity;
            light.color = color;
            light.shadows = shadows;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, string slot, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            if (!collider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            else if (type == PrimitiveType.Cylinder)
            {
                // capsula implicita a cilindrului ar face o cocoasa; podiumul are coliziune pe forma lui
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().convex = true;
            }
            go.GetComponent<Renderer>().sharedMaterial = BlacksiteK17MapBuilder.KitMaterial(slot);
            return go;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.ClearArray();
            for (int i = 0; i < values.Length; i++)
            {
                property.InsertArrayElementAtIndex(i);
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
