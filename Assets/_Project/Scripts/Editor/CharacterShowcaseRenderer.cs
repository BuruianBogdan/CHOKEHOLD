using System;
using System.Collections.Generic;
using System.IO;
using ChokeHold.Showcase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Capturile din Docs/Characters facute in scena CharacterShowcase, in Play mode (Animator, rig IK si
    /// post-procesare reale): fata / spate / profil / cap, poze din clipuri, LOD-uri, mainile FP (hip si ADS)
    /// pentru fiecare arma a echipei si standul de arme. La final iese din Play mode si redeschide scena de dinainte.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterShowcaseRenderer
    {
        private const string StepKey = "K17.ShowcaseRenderStep";
        private const string WaitKey = "K17.ShowcaseRenderWait";
        private const string SceneKey = "K17.ShowcaseRenderPrevious";
        private const string ReopenKey = "K17.ShowcaseRenderReopen";

        static CharacterShowcaseRenderer()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Scena de dinainte se redeschide abia dupa iesirea din Play mode (inainte, OpenScene ar fi refuzat)
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            string previous = SessionState.GetString(ReopenKey, "");
            if (state != PlayModeStateChange.EnteredEditMode || string.IsNullOrEmpty(previous))
            {
                return;
            }

            SessionState.EraseString(ReopenKey);
            if (File.Exists(previous))
            {
                EditorSceneManager.OpenScene(previous);
            }
        }

        [MenuItem("CHOKEHOLD/Characters/Render Showcase")]
        public static void Run()
        {
            if (!File.Exists(CharacterShowcaseBuilder.ScenePath))
            {
                CharacterShowcaseBuilder.Build();
            }

            SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(CharacterShowcaseBuilder.ScenePath);
            SessionState.SetInt(StepKey, 0);
            SessionState.SetInt(WaitKey, 60);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            int step = SessionState.GetInt(StepKey, -1);
            if (step < 0 || !EditorApplication.isPlaying)
            {
                return;
            }

            // Editorul poate fi in fundal: bucla jocului trebuie sa ruleze (doar pe durata capturilor)
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();

            int wait = SessionState.GetInt(WaitKey, 0);
            if (wait > 0)
            {
                SessionState.SetInt(WaitKey, wait - 1);
                return;
            }

            CharacterShowcase showcase = UnityEngine.Object.FindAnyObjectByType<CharacterShowcase>();
            if (showcase == null)
            {
                Finish("scena nu are CharacterShowcase");
                return;
            }

            List<(Action<CharacterShowcase> setup, string capture)> steps = Steps(showcase);
            if (step >= steps.Count)
            {
                Finish($"{steps.Count} pasi");
                return;
            }

            // Fiecare pas: intai capturile pasului anterior (dupa ce Animator-ul si rig-ul au rulat), apoi setarea noua
            try
            {
                if (step > 0 && steps[step - 1].capture != null)
                {
                    showcase.Capture(steps[step - 1].capture);
                }

                steps[step].setup(showcase);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            SessionState.SetInt(StepKey, step + 1);
            SessionState.SetInt(WaitKey, 6);
        }

        private static void Finish(string message)
        {
            SessionState.SetInt(StepKey, -1);
            Debug.Log("Showcase renders: " + message);
            SessionState.SetString(ReopenKey, SessionState.GetString(SceneKey, ""));
            EditorApplication.isPlaying = false;
        }

        /// <summary>Lista de pasi (setare, captura). Ultimul pas are captura nula: doar declanseaza captura penultimului.</summary>
        private static List<(Action<CharacterShowcase>, string)> Steps(CharacterShowcase showcase)
        {
            string docs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "Characters");
            Vector3 body = new Vector3(0f, 1.0f, 0f);
            Vector3 head = new Vector3(0f, 1.62f, 0f);
            var steps = new List<(Action<CharacterShowcase>, string)>();

            string[] names = { "CT_Operator", "T_Insurgent_A", "T_Insurgent_B" };
            for (int c = 0; c < showcase.CharacterCount && c < names.Length; c++)
            {
                int index = c;
                string name = names[c];
                bool attacker = name.StartsWith("T_");
                string rifle = attacker ? "AK_K17" : "M4A1_S";
                string folder = Path.Combine(docs, name);

                steps.Add((s => { s.SelectCharacter(index, s.WeaponIndexOf(rifle)); s.SetFirstPerson(false, false); s.ShowWeaponStand(false); s.PlayClip("Rifle_Loco_Idle", 0.2f); s.SetView(180f, 6f, 3.6f, body); }, Path.Combine(folder, $"{name}_Unity_Front.png")));
                steps.Add((s => s.SetView(0f, 6f, 3.6f, body), Path.Combine(folder, $"{name}_Unity_Back.png")));
                steps.Add((s => s.SetView(270f, 4f, 3.6f, body), Path.Combine(folder, $"{name}_Unity_Side.png")));
                steps.Add((s => s.SetView(155f, 4f, 0.95f, head), Path.Combine(folder, $"{name}_Unity_Head.png")));

                (string clip, float t, float yaw)[] poses =
                {
                    ("Rifle_Aim_C_C", 0f, 215f), ("Rifle_Loco_Run_F", 0.3f, 250f), ("Rifle_Loco_CrouchIdle", 0.2f, 215f),
                    ("Rifle_Weapon_Reload", 0.45f, 215f), ("Rifle_Aim_LeanL", 0f, 180f), ("Rifle_Action_Plant", 0.5f, 230f),
                    ("Rifle_React_HitF", 0.25f, 215f),
                };
                foreach (var pose in poses)
                {
                    var p = pose;
                    steps.Add((s => { s.PlayClip(p.clip, p.t); s.SetView(p.yaw, 8f, 3.4f, body); }, Path.Combine(folder, $"{name}_Pose_{p.clip.Replace("Rifle_", "")}.png")));
                }

                string pistol = attacker ? "P17_K" : "P_SD";
                steps.Add((s => { s.SelectCharacter(index, s.WeaponIndexOf(pistol)); s.PlayClip("Pistol_Aim_C_C", 0f); s.SetView(215f, 8f, 3.4f, body); }, Path.Combine(folder, $"{name}_Pose_Pistol_Aim.png")));

                for (int lod = 0; lod < 3; lod++)
                {
                    int level = lod;
                    steps.Add((s => { s.SelectCharacter(index, s.WeaponIndexOf(rifle)); s.PlayClip("Rifle_Loco_Idle", 0.2f); s.ForceLod(level); s.SetView(200f, 6f, 3.6f, body); }, Path.Combine(folder, $"{name}_LOD{level}.png")));
                }

                // Mainile FP cu armele echipei
                string[] fpWeapons = attacker ? new[] { "AK_K17", "Galil_K", "P17_K", "Knife_Karambit" } : new[] { "M4A1_S", "M4A4", "P_SD", "Knife_Combat" };
                foreach (string weapon in fpWeapons)
                {
                    string w = weapon;
                    steps.Add((s => { s.ForceLod(-1); s.SelectCharacter(index, s.WeaponIndexOf(w)); s.SetFirstPerson(true, false); }, Path.Combine(folder, $"FP_{w}_Hip.png")));
                    if (!w.StartsWith("Knife_") && w != "M4A4")     // cutitele nu ochesc; ACOG-ul foloseste ecranul de luneta
                    {
                        steps.Add((s => s.SetFirstPerson(true, true), Path.Combine(folder, $"FP_{w}_ADS.png")));
                    }
                }

                steps.Add((s => s.SetFirstPerson(false, false), null));
            }

            // Standul de arme
            steps.Add((s => { s.ShowWeaponStand(true); s.SetView(180f, 3f, 3.6f, new Vector3(2.7f, 1.1f, 0f)); }, Path.Combine(docs, "Weapons_Unity.png")));
            steps.Add((s => { }, null));
            return steps;
        }
    }
}
