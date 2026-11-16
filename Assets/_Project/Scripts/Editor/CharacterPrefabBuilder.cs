using System.Collections.Generic;
using System.IO;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Construieste prefab-urile personajelor din FBX-urile generate de Tools/Blender/k17_characters.py:
    /// materiale K17 dupa numele slotului, LOD Group (din numele *_LOD0/1/2), hitbox-uri pe oase,
    /// TeamMember + Health. Prefab: Assets/_Project/Prefabs/Characters/Character_<Nume>.prefab.
    /// </summary>
    public static class CharacterPrefabBuilder
    {
        private const string CharactersFolder = "Assets/_Project/Art/Characters";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Characters";

        private static readonly Dictionary<string, Team> TeamOf = new Dictionary<string, Team>
        {
            { "CT_Operator", Team.Defenders },
            { "T_Insurgent_A", Team.Attackers },
            { "T_Insurgent_B", Team.Attackers },
        };

        [MenuItem("CHOKEHOLD/Characters/Build Prefabs")]
        public static void BuildAll()
        {
            BlacksiteK17MapBuilder.EnsureFolder(PrefabFolder);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);

                if (!TeamOf.ContainsKey(name))
                {
                    continue;
                }

                Build(path, name);
            }

            AssetDatabase.SaveAssets();
        }

        private static void Build(string modelPath, string name)
        {
            // Reimport: setarile din CharacterImporter se aplica si cand s-au schimbat dupa primul import.
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = $"Character_{name}";

            // Materiale K17 dupa numele slotului din Blender
            int materials = 0;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string slot = shared[i] != null ? shared[i].name.Replace(" (Instance)", "") : "Plastic";
                    shared[i] = BlacksiteK17MapBuilder.KitMaterial(slot);
                    materials++;
                }
                renderer.sharedMaterials = shared;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // LOD Group-ul din import se reface cu pragurile noastre (0,4 / 0,15 / 0,03 din inaltimea ecranului)
            LODGroup imported = instance.GetComponent<LODGroup>();
            if (imported != null)
            {
                Object.DestroyImmediate(imported);
            }

            LODGroup lodGroup = BuildLodGroup(instance);

            Animator animator = instance.GetComponent<Animator>();
            int hitboxes = animator != null ? BuildHitboxes(instance, animator) : 0;

            // (fara "??": obiectele Unity distruse sau lipsa nu sunt null in sensul C#)
            Health health = instance.GetComponent<Health>();
            if (health == null)
            {
                health = instance.AddComponent<Health>();
            }

            TeamMember member = instance.GetComponent<TeamMember>();
            if (member == null)
            {
                member = instance.AddComponent<TeamMember>();
            }
            SerializedObject serialized = new SerializedObject(member);
            serialized.FindProperty("team").enumValueIndex = (int)TeamOf[name];
            serialized.FindProperty("respawnAtSpawnPoint").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = $"{PrefabFolder}/Character_{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            Debug.Log($"Prefab: {prefabPath} ({materials} sloturi de material, LOD-uri: {(lodGroup != null ? lodGroup.lodCount : 0)}, " +
                      $"{hitboxes} hitbox-uri, health={health != null}, animator={animator != null && animator.isHuman})");
            Object.DestroyImmediate(instance);
        }

        private static LODGroup BuildLodGroup(GameObject instance)
        {
            List<LOD> lods = new List<LOD>();
            float[] heights = { 0.4f, 0.15f, 0.03f };

            for (int level = 0; level < 3; level++)
            {
                List<Renderer> renderers = new List<Renderer>();
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.EndsWith($"_LOD{level}"))
                    {
                        renderers.Add(renderer);
                    }
                }

                if (renderers.Count > 0)
                {
                    lods.Add(new LOD(heights[level], renderers.ToArray()));
                }
            }

            if (lods.Count == 0)
            {
                return null;
            }

            LODGroup group = instance.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();
            return group;
        }

        // ------------------------------------------------------------------
        // Hitbox-uri pe oase (collidere pline; Health e pe radacina, HitZone da multiplicatorul)
        // ------------------------------------------------------------------
        private static int BuildHitboxes(GameObject instance, Animator animator)
        {
            int count = 0;

            Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

            // (os, capat, raza, zona)
            (HumanBodyBones, HumanBodyBones, float, HitZoneKind)[] capsules =
            {
                (HumanBodyBones.Neck, HumanBodyBones.Head, 0.06f, HitZoneKind.Neck),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.065f, HitZoneKind.Arm),
                (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.05f, HitZoneKind.Arm),
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.065f, HitZoneKind.Arm),
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.05f, HitZoneKind.Arm),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.095f, HitZoneKind.Leg),
                (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.07f, HitZoneKind.Leg),
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.095f, HitZoneKind.Leg),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.07f, HitZoneKind.Leg),
            };

            foreach ((HumanBodyBones from, HumanBodyBones to, float radius, HitZoneKind kind) in capsules)
            {
                Transform a = Bone(from), b = Bone(to);
                if (a == null || b == null) continue;

                GameObject go = HitboxObject(a, kind, (a.position + b.position) * 0.5f, Quaternion.LookRotation(b.position - a.position, a.up));
                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = radius;
                capsule.height = Vector3.Distance(a.position, b.position) + radius * 2f;
                count++;
            }

            // Cap: sfera; trunchi: cutii pe UpperChest, Chest/Spine, Hips; maini, talpi: sfere mici
            Transform head = Bone(HumanBodyBones.Head);
            if (head != null)
            {
                GameObject go = HitboxObject(head, HitZoneKind.Head, head.position + Vector3.up * 0.11f, Quaternion.identity);
                go.AddComponent<SphereCollider>().radius = 0.125f;
                count++;
            }

            (HumanBodyBones, Vector3, Vector3, HitZoneKind)[] boxes =
            {
                (HumanBodyBones.UpperChest, new Vector3(0f, 0.08f, 0.01f), new Vector3(0.40f, 0.18f, 0.30f), HitZoneKind.Chest),
                (HumanBodyBones.Spine, new Vector3(0f, 0.07f, 0.01f), new Vector3(0.34f, 0.26f, 0.26f), HitZoneKind.Chest),
                (HumanBodyBones.Hips, new Vector3(0f, -0.03f, -0.01f), new Vector3(0.36f, 0.22f, 0.26f), HitZoneKind.Abdomen),
                (HumanBodyBones.RightHand, new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.05f, 0.18f), HitZoneKind.Arm),
                (HumanBodyBones.LeftHand, new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.05f, 0.18f), HitZoneKind.Arm),
                (HumanBodyBones.RightFoot, new Vector3(0f, -0.03f, 0.08f), new Vector3(0.11f, 0.09f, 0.28f), HitZoneKind.Leg),
                (HumanBodyBones.LeftFoot, new Vector3(0f, -0.03f, 0.08f), new Vector3(0.11f, 0.09f, 0.28f), HitZoneKind.Leg),
            };

            foreach ((HumanBodyBones bone, Vector3 offset, Vector3 size, HitZoneKind kind) in boxes)
            {
                Transform t = Bone(bone);
                if (t == null) continue;

                // Cutia e aliniata cu personajul (nu cu osul), ca dimensiunile sa fie previzibile.
                GameObject go = HitboxObject(t, kind, t.position + offset, Quaternion.identity);
                go.AddComponent<BoxCollider>().size = size;
                count++;
            }

            return count;
        }

        private static GameObject HitboxObject(Transform bone, HitZoneKind kind, Vector3 position, Quaternion rotation)
        {
            GameObject go = new GameObject($"Hitbox_{kind}_{bone.name}");
            go.transform.SetParent(bone, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<HitZone>().Configure(kind);
            return go;
        }
    }
}
