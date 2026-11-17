using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Construieste Animator Controller-ul personajelor (persoana a treia) din clipurile generate de
    /// Tools/Blender/k17_anims.py:
    ///   Base      locomotie (blend tree 2D MoveX/MoveZ, stand/crouch), sprint, aer, scara; pusca sau pistol dupa WeaponKind
    ///   UpperBody aim offset 2D (AimYaw, AimPitch) pe masca de trunchi + brate
    ///   Lean      inclinare stanga/dreapta (Lean -1..1), pe aceeasi masca
    ///   Actions   fire / reload / draw / holster / plant / defuse / door / barricade / hit react, prin triggere
    /// Controller: Assets/_Project/Animation/Character_TP.controller
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        private const string AnimationsFolder = "Assets/_Project/Art/Animations";
        private const string OutputFolder = "Assets/_Project/Animation";
        private const string ControllerPath = OutputFolder + "/Character_TP.controller";
        private const string MaskPath = OutputFolder + "/UpperBody.mask";

        private static readonly string[] Kinds = { "Rifle", "Pistol" };

        private static readonly (string dir, float x, float z)[] Directions =
        {
            ("F", 0f, 1f), ("B", 0f, -1f), ("L", -1f, 0f), ("R", 1f, 0f),
            ("FL", -0.7071f, 0.7071f), ("FR", 0.7071f, 0.7071f), ("BL", -0.7071f, -0.7071f), ("BR", 0.7071f, -0.7071f),
        };

        private static Dictionary<string, AnimationClip> clips;
        private static int missing;

        [MenuItem("CHOKEHOLD/Characters/Build Animator")]
        public static void Build()
        {
            BlacksiteK17MapBuilder.EnsureFolder(OutputFolder);
            LoadClips();

            AvatarMask mask = BuildUpperBodyMask();

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            else
            {
                // Se goleste pe loc (nu se sterge): prefab-urile raman legate de acelasi obiect.
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                {
                    if (sub != null && sub != controller)
                    {
                        AssetDatabase.RemoveObjectFromAsset(sub);
                        Object.DestroyImmediate(sub, true);
                    }
                }

                controller.parameters = new AnimatorControllerParameter[0];
                AnimatorStateMachine baseMachine = new AnimatorStateMachine { name = "Base Layer", hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(baseMachine, controller);
                controller.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = baseMachine } };
            }

            foreach (string p in new[] { "MoveX", "MoveZ", "Speed", "AimPitch", "AimYaw", "Lean", "WeaponKind" })
            {
                controller.AddParameter(p, AnimatorControllerParameterType.Float);
            }

            foreach (string p in new[] { "Crouch", "Sprint", "Grounded", "Climb", "Plant", "Defuse" })
            {
                controller.AddParameter(p, AnimatorControllerParameterType.Bool);
            }

            foreach (string p in new[] { "Fire", "Reload", "ReloadEmpty", "Draw", "Holster", "Door", "Barricade", "Jump",
                                         "HitF", "HitB", "HitL", "HitR" })
            {
                controller.AddParameter(p, AnimatorControllerParameterType.Trigger);
            }

            BuildBaseLayer(controller);
            BuildAimLayer(controller, mask);
            BuildLeanLayer(controller, mask);
            BuildActionLayer(controller, mask);

            AssetDatabase.SaveAssets();
            int prefabs = AssignToCharacterPrefabs(controller);
            Debug.Log($"Animator: {ControllerPath} ({clips.Count} clipuri, {missing} lipsa, {controller.layers.Length} straturi, {prefabs} prefab-uri legate)");
        }

        /// <summary>
        /// Controller-ul se recreeaza (ID nou), deci prefab-urile de personaj se leaga din nou la el,
        /// indiferent de ordinea in care se ruleaza Build Prefabs / Build Animator.
        /// </summary>
        private static int AssignToCharacterPrefabs(AnimatorController controller)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Characters" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                    {
                        animator.runtimeAnimatorController = controller;
                        changed = true;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        count++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return count;
        }

        // ------------------------------------------------------------------

        private static void LoadClips()
        {
            clips = new Dictionary<string, AnimationClip>();
            missing = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Reimport: setarile de clip (nume, bucla, fara root motion) din CharacterImporter se aplica sigur.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    {
                        clips[Path.GetFileNameWithoutExtension(path)] = clip;
                    }
                }
            }
        }

        private static AnimationClip Clip(string name)
        {
            if (clips.TryGetValue(name, out AnimationClip clip))
            {
                return clip;
            }

            missing++;
            Debug.LogWarning($"Animator: lipseste clipul {name}");
            return null;
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                AvatarMaskBodyPart part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>Blend tree 1D pe WeaponKind (0 = pusca, 1 = pistol) cu cate un copil pe fel de arma.</summary>
        private static BlendTree ByWeapon(AnimatorController controller, string name, System.Func<string, Motion> child)
        {
            BlendTree tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = "WeaponKind", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (int k = 0; k < Kinds.Length; k++)
            {
                Motion motion = child(Kinds[k]);
                if (motion != null)
                {
                    tree.AddChild(motion, k);
                }
            }
            return tree;
        }

        private static BlendTree Locomotion(AnimatorController controller, string kind, bool crouch)
        {
            BlendTree tree = new BlendTree
            {
                name = $"{kind}_{(crouch ? "Crouch" : "Stand")}",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            AnimationClip idle = Clip(crouch ? $"{kind}_Loco_CrouchIdle" : $"{kind}_Loco_Idle");
            if (idle != null)
            {
                tree.AddChild(idle, Vector2.zero);
            }

            foreach ((string dir, float x, float z) in Directions)
            {
                AnimationClip walk = Clip(crouch ? $"{kind}_Loco_CrouchWalk_{dir}" : $"{kind}_Loco_Walk_{dir}");
                if (walk != null)
                {
                    tree.AddChild(walk, new Vector2(x, z));
                }

                if (!crouch)
                {
                    AnimationClip run = Clip($"{kind}_Loco_Run_{dir}");
                    if (run != null)
                    {
                        tree.AddChild(run, new Vector2(x, z) * 1.5f);
                    }
                }
            }

            return tree;
        }

        private static void BuildBaseLayer(AnimatorController controller)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState stand = sm.AddState("Stand", new Vector3(300, 0));
            stand.motion = ByWeapon(controller, "Stand", k => Locomotion(controller, k, false));
            AnimatorState crouch = sm.AddState("Crouch", new Vector3(300, 100));
            crouch.motion = ByWeapon(controller, "Crouch", k => Locomotion(controller, k, true));
            AnimatorState sprint = sm.AddState("Sprint", new Vector3(300, -100));
            sprint.motion = ByWeapon(controller, "Sprint", k => Clip($"{k}_Loco_Sprint"));
            AnimatorState jump = sm.AddState("JumpStart", new Vector3(600, -100));
            jump.motion = ByWeapon(controller, "JumpStart", k => Clip($"{k}_Air_JumpStart"));
            AnimatorState fall = sm.AddState("Fall", new Vector3(600, 0));
            fall.motion = ByWeapon(controller, "Fall", k => Clip($"{k}_Air_Fall"));
            AnimatorState land = sm.AddState("Land", new Vector3(600, 100));
            land.motion = ByWeapon(controller, "Land", k => Clip($"{k}_Air_Land"));
            AnimatorState ladder = sm.AddState("Ladder", new Vector3(300, 200));
            ladder.motion = Clip("Rifle_Action_Ladder");
            ladder.speedParameterActive = true;
            ladder.speedParameter = "Speed";
            sm.defaultState = stand;

            Transition(stand, crouch, 0.15f, ("Crouch", true));
            Transition(crouch, stand, 0.15f, ("Crouch", false));
            Transition(stand, sprint, 0.15f, ("Sprint", true));
            Transition(sprint, stand, 0.2f, ("Sprint", false));
            foreach (AnimatorState s in new[] { stand, crouch, sprint })
            {
                Transition(s, fall, 0.1f, ("Grounded", false));
                Transition(s, ladder, 0.15f, ("Climb", true));
            }
            AnimatorStateTransition toJump = sm.AddAnyStateTransition(jump);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toJump.duration = 0.05f;
            toJump.canTransitionToSelf = false;
            Transition(jump, fall, 0.05f).hasExitTime = true;
            Transition(fall, land, 0.05f, ("Grounded", true));
            Transition(land, stand, 0.1f).hasExitTime = true;
            Transition(ladder, stand, 0.15f, ("Climb", false));
        }

        private static void BuildAimLayer(AnimatorController controller, AvatarMask mask)
        {
            AnimatorControllerLayer layer = NewLayer(controller, "UpperBody", mask, 1f);
            AnimatorState aim = layer.stateMachine.AddState("Aim", new Vector3(300, 0));
            aim.motion = ByWeapon(controller, "AimOffset", kind =>
            {
                BlendTree tree = new BlendTree
                {
                    name = $"{kind}_Aim", blendType = BlendTreeType.FreeformCartesian2D,
                    blendParameter = "AimYaw", blendParameterY = "AimPitch",
                };
                AssetDatabase.AddObjectToAsset(tree, controller);
                foreach ((string pn, float pitch) in new[] { ("U", 60f), ("C", 0f), ("D", -60f) })
                {
                    foreach ((string yn, float yaw) in new[] { ("L", -45f), ("C", 0f), ("R", 45f) })
                    {
                        AnimationClip clip = Clip($"{kind}_Aim_{pn}_{yn}");
                        if (clip != null)
                        {
                            tree.AddChild(clip, new Vector2(yaw, pitch));
                        }
                    }
                }
                return tree;
            });
            layer.stateMachine.defaultState = aim;
        }

        private static void BuildLeanLayer(AnimatorController controller, AvatarMask mask)
        {
            // Greutatea stratului o da codul (|Lean|); arborele alege stanga / dreapta.
            AnimatorControllerLayer layer = NewLayer(controller, "Lean", mask, 0f);
            AnimatorState lean = layer.stateMachine.AddState("Lean", new Vector3(300, 0));
            lean.motion = ByWeapon(controller, "Lean", kind =>
            {
                BlendTree tree = new BlendTree { name = $"{kind}_Lean", blendType = BlendTreeType.Simple1D, blendParameter = "Lean", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, controller);
                AnimationClip left = Clip($"{kind}_Aim_LeanL"), center = Clip($"{kind}_Aim_C_C"), right = Clip($"{kind}_Aim_LeanR");
                if (left != null) tree.AddChild(left, -1f);
                if (center != null) tree.AddChild(center, 0f);
                if (right != null) tree.AddChild(right, 1f);
                return tree;
            });
            layer.stateMachine.defaultState = lean;
        }

        private static void BuildActionLayer(AnimatorController controller, AvatarMask mask)
        {
            AnimatorControllerLayer layer = NewLayer(controller, "Actions", mask, 1f);
            AnimatorStateMachine sm = layer.stateMachine;
            AnimatorState empty = sm.AddState("Empty", new Vector3(300, 0));
            sm.defaultState = empty;

            int row = 0;
            void Action(string stateName, string trigger, System.Func<string, Motion> motion, bool perWeapon = true, bool loopUntilFalse = false)
            {
                AnimatorState state = sm.AddState(stateName, new Vector3(600, 80 * row++));
                state.motion = perWeapon ? ByWeapon(controller, stateName, motion) : motion("Rifle");
                AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
                enter.duration = 0.08f;
                enter.canTransitionToSelf = stateName == "Fire";
                if (loopUntilFalse)
                {
                    enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    AnimatorStateTransition exit = state.AddTransition(empty);
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0, trigger);
                    exit.duration = 0.15f;
                }
                else
                {
                    enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    AnimatorStateTransition exit = state.AddTransition(empty);
                    exit.hasExitTime = true;
                    exit.exitTime = 0.95f;
                    exit.duration = 0.15f;
                }
            }

            Action("Fire", "Fire", k => Clip($"{k}_Weapon_Fire"));
            Action("Reload", "Reload", k => Clip($"{k}_Weapon_Reload"));
            Action("ReloadEmpty", "ReloadEmpty", k => Clip($"{k}_Weapon_ReloadEmpty"));
            Action("Draw", "Draw", k => Clip($"{k}_Weapon_Draw"));
            Action("Holster", "Holster", k => Clip($"{k}_Weapon_Holster"));
            Action("Door", "Door", k => Clip("Rifle_Action_Door"), perWeapon: false);
            Action("Barricade", "Barricade", k => Clip("Rifle_Action_Barricade"), perWeapon: false);
            Action("Plant", "Plant", k => Clip("Rifle_Action_Plant"), perWeapon: false, loopUntilFalse: true);
            Action("Defuse", "Defuse", k => Clip("Rifle_Action_Defuse"), perWeapon: false, loopUntilFalse: true);
            foreach (string d in new[] { "F", "B", "L", "R" })
            {
                Action($"Hit{d}", $"Hit{d}", k => Clip($"{k}_React_Hit{d}"));
            }
        }

        private static AnimatorControllerLayer NewLayer(AnimatorController controller, string name, AvatarMask mask, float weight)
        {
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = name,
                stateMachine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy },
                avatarMask = mask,
                defaultWeight = weight,
                blendingMode = AnimatorLayerBlendingMode.Override,
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);
            return layer;
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration, params (string, bool)[] bools)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach ((string name, bool value) in bools)
            {
                t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, name);
            }
            return t;
        }
    }
}
