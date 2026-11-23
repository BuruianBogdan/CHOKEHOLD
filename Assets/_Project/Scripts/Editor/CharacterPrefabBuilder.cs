using System.Collections.Generic;
using System.IO;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

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

            // Hitbox-urile au layer-ul lor: primesc gloantele, nu se ciocnesc cu nimic (setarea se salveaza in proiect)
            FirstPersonBuilder.EnsureLayer(HitZone.LayerName);
            int hitboxLayer = LayerMask.NameToLayer(HitZone.LayerName);
            for (int other = 0; hitboxLayer >= 0 && other < 32; other++)
            {
                Physics.IgnoreLayerCollision(hitboxLayer, other, true);
            }

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
            RefreshSceneCharacters();
        }

        /// <summary>
        /// Personajele din scena deschisa (jucatorul, tintele) primesc prefab-urile curente: CT pentru aparatori,
        /// teroristii care exista pentru atacatori. Doar referintele se schimba; scena se salveaza daca s-a schimbat ceva.
        /// </summary>
        private static void RefreshSceneCharacters()
        {
            GameObject defender = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Character_CT_Operator.prefab");
            List<GameObject> attackers = new List<GameObject>();
            foreach (string name in new[] { "Character_T_Insurgent_A", "Character_T_Insurgent_B" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
                if (prefab != null)
                {
                    attackers.Add(prefab);
                }
            }

            int changed = 0;
            foreach (CharacterVisuals visuals in Object.FindObjectsByType<CharacterVisuals>(FindObjectsInactive.Include))
            {
                SerializedObject serialized = new SerializedObject(visuals);
                SerializedProperty defenderProperty = serialized.FindProperty("defenderPrefab");
                SerializedProperty attackersProperty = serialized.FindProperty("attackerPrefabs");

                bool same = defenderProperty.objectReferenceValue == defender && attackersProperty.arraySize == attackers.Count;
                for (int i = 0; same && i < attackers.Count; i++)
                {
                    same = attackersProperty.GetArrayElementAtIndex(i).objectReferenceValue == attackers[i];
                }

                if (same)
                {
                    continue;
                }

                defenderProperty.objectReferenceValue = defender;
                attackersProperty.ClearArray();
                for (int i = 0; i < attackers.Count; i++)
                {
                    attackersProperty.InsertArrayElementAtIndex(i);
                    attackersProperty.GetArrayElementAtIndex(i).objectReferenceValue = attackers[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }

            if (changed > 0)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"Personaje in scena: {changed} actualizate (atacatori: {attackers.Count}).");
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
                    shared[i] = CharacterImporter.CharacterMaterial(slot);
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

            if (animator != null)
            {
                BuildWeaponMount(instance, animator);
                BuildRagdoll(instance, animator);
                BuildRig(instance, animator);
                if (instance.GetComponent<Ragdoll>() == null)
                {
                    instance.AddComponent<Ragdoll>();
                }
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animation/Character_TP.controller");
                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                }
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            if (instance.GetComponent<CharacterAnimatorDriver>() == null)
            {
                instance.AddComponent<CharacterAnimatorDriver>();
            }

            // Prefab-ul e doar partea vizuala: TeamMember si Health stau pe obiectul care il poarta
            // (jucatorul sau tinta, prin CharacterVisuals), ca loviturile sa ajunga la un singur Health.
            foreach (Component extra in new Component[] { instance.GetComponent<TeamMember>(), instance.GetComponent<Health>() })
            {
                if (extra != null)
                {
                    Object.DestroyImmediate(extra);
                }
            }

            string prefabPath = $"{PrefabFolder}/Character_{name}.prefab";
            // Texturile PBR (k17_textures.py) pe materialele K17 folosite de model; harta le aplica doar pe ce e in scena.
            BlacksiteK17MapBuilder.ApplySurfaces(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            Debug.Log($"Prefab: {prefabPath} ({materials} sloturi de material, LOD-uri: {(lodGroup != null ? lodGroup.lodCount : 0)}, " +
                      $"{hitboxes} hitbox-uri, animator={animator != null && animator.isHuman})");
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
                (HumanBodyBones.Chest, new Vector3(0f, 0.065f, 0.01f), new Vector3(0.37f, 0.17f, 0.28f), HitZoneKind.Chest),
                (HumanBodyBones.Spine, new Vector3(0f, 0.07f, 0.01f), new Vector3(0.34f, 0.26f, 0.26f), HitZoneKind.Chest),
                (HumanBodyBones.Hips, new Vector3(0f, -0.03f, -0.01f), new Vector3(0.36f, 0.22f, 0.26f), HitZoneKind.Abdomen),
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

            // Mainile: cutii pe directia palmei (spre MiddleProximal), nu pe axele personajului
            foreach ((HumanBodyBones hand, HumanBodyBones middle) in new[] { (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal), (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal) })
            {
                Transform h = Bone(hand), m = Bone(middle);
                if (h == null || m == null) continue;

                Vector3 along = m.position - h.position;
                GameObject go = HitboxObject(h, HitZoneKind.Arm, h.position + along * 0.8f, Quaternion.LookRotation(along, instance.transform.forward));
                go.AddComponent<BoxCollider>().size = new Vector3(0.10f, 0.05f, along.magnitude * 1.9f);
                count++;
            }

            return count;
        }

        // ------------------------------------------------------------------
        // Ragdoll: corpuri rigide + collidere pe oase (cinematice / dezactivate cat timp traieste)
        // ------------------------------------------------------------------
        private static void BuildRagdoll(GameObject instance, Animator animator)
        {
            Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

            // (os, capatul segmentului, parinte in ragdoll, masa, raza)
            (HumanBodyBones bone, HumanBodyBones end, HumanBodyBones parent, float mass, float radius)[] parts =
            {
                (HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.LastBone, 12f, 0.14f),
                (HumanBodyBones.Spine, HumanBodyBones.UpperChest, HumanBodyBones.Hips, 10f, 0.13f),
                (HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Spine, 10f, 0.15f),
                (HumanBodyBones.Head, HumanBodyBones.LastBone, HumanBodyBones.UpperChest, 5f, 0.12f),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.UpperChest, 2.5f, 0.06f),
                (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightUpperArm, 1.8f, 0.05f),
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.UpperChest, 2.5f, 0.06f),
                (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftUpperArm, 1.8f, 0.05f),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.Hips, 8f, 0.09f),
                (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightUpperLeg, 4f, 0.07f),
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.Hips, 8f, 0.09f),
                (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftUpperLeg, 4f, 0.07f),
            };

            foreach ((HumanBodyBones bone, HumanBodyBones end, HumanBodyBones _, float mass, float radius) in parts)
            {
                Transform t = Bone(bone);
                if (t == null) continue;

                Rigidbody body = t.gameObject.AddComponent<Rigidbody>();
                body.mass = mass;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;

                if (bone == HumanBodyBones.Head)
                {
                    SphereCollider sphere = t.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = radius;
                    sphere.center = t.InverseTransformPoint(t.position + Vector3.up * 0.11f);
                    sphere.enabled = false;
                    continue;
                }

                Transform endBone = Bone(end);
                Vector3 endPosition = endBone != null ? endBone.position : t.position + Vector3.up * 0.2f;
                Vector3 local = t.InverseTransformDirection(endPosition - t.position);
                int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) && Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0
                    : Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2;

                CapsuleCollider capsule = t.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = axis;
                capsule.radius = radius;
                capsule.height = Vector3.Distance(t.position, endPosition) + radius * 2f;
                capsule.center = t.InverseTransformPoint((t.position + endPosition) * 0.5f);
                capsule.enabled = false;
            }

            foreach ((HumanBodyBones bone, HumanBodyBones _, HumanBodyBones parent, float _, float _) in parts)
            {
                if (parent == HumanBodyBones.LastBone) continue;
                Transform t = Bone(bone), p = Bone(parent);
                if (t == null || p == null) continue;

                CharacterJoint joint = t.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = p.GetComponent<Rigidbody>();
                joint.enableProjection = true;
                joint.swing1Limit = new SoftJointLimit { limit = 35f };
                joint.swing2Limit = new SoftJointLimit { limit = 35f };
                joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
                joint.highTwistLimit = new SoftJointLimit { limit = 25f };
            }
        }

        /// <summary>
        /// "WeaponMount" sub osul weapon_R: +Z de-a lungul mainii (incheietura -> baza degetului mijlociu),
        /// +Y in sus in poza de repaus. Arma de la persoana a treia se pune aici cu transformarea identitate.
        /// </summary>
        private static void BuildWeaponMount(GameObject instance, Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform socket = null;

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "weapon_R")
                {
                    socket = t;
                }
            }

            if (hand == null || socket == null)
            {
                return;
            }

            Vector3 forward = middle != null ? (middle.position - hand.position).normalized : instance.transform.forward;
            GameObject mount = new GameObject("WeaponMount");
            mount.transform.SetParent(socket, false);
            mount.transform.SetPositionAndRotation(socket.position, Quaternion.LookRotation(forward, instance.transform.up));
        }

        /// <summary>
        /// Rig de IK (com.unity.animation.rigging): mana stanga pe arma (TwoBoneIK, tinta sub WeaponMount) si capul
        /// spre AimTarget (MultiAim, limitat). ThirdPersonWeaponIK pune tintele la runtime.
        /// </summary>
        private static void BuildRig(GameObject instance, Animator animator)
        {
            Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            Transform mount = null;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "WeaponMount")
                {
                    mount = t;
                }
            }

            if (upper == null || lower == null || hand == null || head == null || mount == null)
            {
                Debug.LogWarning($"{instance.name}: rig-ul IK nu s-a putut face (lipsesc oase sau WeaponMount).");
                return;
            }

            GameObject rigObject = new GameObject("Rig_IK");
            rigObject.transform.SetParent(instance.transform, false);
            Rig rig = rigObject.AddComponent<Rig>();
            RigBuilder builder = instance.AddComponent<RigBuilder>();
            builder.layers.Add(new RigLayer(rig, true));

            // Mana stanga: tinta sub WeaponMount (urmeaza mana dreapta animata), cotul in jos-afara
            GameObject target = new GameObject("LeftHandTarget");
            target.transform.SetParent(mount, false);
            GameObject hint = new GameObject("LeftElbowHint");
            hint.transform.SetParent(chest != null ? chest : instance.transform, false);
            hint.transform.position = lower.position + instance.transform.TransformVector(new Vector3(-0.25f, -0.35f, 0.05f));

            GameObject leftObject = new GameObject("LeftHandIK");
            leftObject.transform.SetParent(rigObject.transform, false);
            TwoBoneIKConstraint ik = leftObject.AddComponent<TwoBoneIKConstraint>();
            ik.data.root = upper;
            ik.data.mid = lower;
            ik.data.tip = hand;
            ik.data.target = target.transform;
            ik.data.hint = hint.transform;
            ik.data.targetPositionWeight = 1f;
            ik.data.targetRotationWeight = 1f;
            ik.data.hintWeight = 1f;
            ik.weight = 0f;     // pornit de ThirdPersonWeaponIK.Bind cand arma are Grip_L

            // Capul: axa locala a osului care priveste inainte / in sus (depinde de importul scheletului)
            GameObject aimTarget = new GameObject("AimTarget");
            aimTarget.transform.SetParent(instance.transform, false);
            aimTarget.transform.position = head.position + instance.transform.forward * 10f;

            GameObject headObject = new GameObject("HeadAim");
            headObject.transform.SetParent(rigObject.transform, false);
            MultiAimConstraint aim = headObject.AddComponent<MultiAimConstraint>();
            aim.data.constrainedObject = head;
            aim.data.aimAxis = ClosestAxis(head, instance.transform.forward);
            aim.data.upAxis = ClosestAxis(head, instance.transform.up);
            WeightedTransformArray sources = new WeightedTransformArray();
            sources.Add(new WeightedTransform(aimTarget.transform, 1f));
            aim.data.sourceObjects = sources;
            aim.data.limits = new Vector2(-50f, 50f);
            aim.data.constrainedXAxis = true;
            aim.data.constrainedYAxis = true;
            aim.data.constrainedZAxis = true;
            aim.weight = 0.55f;

            ThirdPersonWeaponIK component = instance.AddComponent<ThirdPersonWeaponIK>();
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty("leftHand").objectReferenceValue = ik;
            serialized.FindProperty("leftHandTarget").objectReferenceValue = target.transform;
            serialized.FindProperty("headAim").objectReferenceValue = aim;
            serialized.FindProperty("aimTarget").objectReferenceValue = aimTarget.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static MultiAimConstraintData.Axis ClosestAxis(Transform bone, Vector3 world)
        {
            (Vector3 axis, MultiAimConstraintData.Axis value)[] axes =
            {
                (bone.right, MultiAimConstraintData.Axis.X), (-bone.right, MultiAimConstraintData.Axis.X_NEG),
                (bone.up, MultiAimConstraintData.Axis.Y), (-bone.up, MultiAimConstraintData.Axis.Y_NEG),
                (bone.forward, MultiAimConstraintData.Axis.Z), (-bone.forward, MultiAimConstraintData.Axis.Z_NEG),
            };

            MultiAimConstraintData.Axis best = MultiAimConstraintData.Axis.Z;
            float bestDot = -2f;
            foreach (var (axis, value) in axes)
            {
                float dot = Vector3.Dot(axis, world);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = value;
                }
            }

            return best;
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
