
using System.IO;
using ChokeHold.Gameplay;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace ChokeHold.Showcase
{
    /// <summary>
    /// Scena CharacterShowcase: personajele pe podium, armele pe stand si un rig FP separat. Meniul de debug
    /// (OnGUI) alege personajul si arma, ruleaza orice clip (sau Animator Controller-ul cu parametrii din slidere),
    /// forteaza LOD-ul, porneste ragdoll-ul si muta camera intre presetari. RenderSet() face capturile pentru Docs.
    /// </summary>
    public class CharacterShowcase : MonoBehaviour
    {
        [SerializeField] private GameObject[] characterPrefabs;
        [SerializeField] private WeaponData[] weapons;
        [SerializeField] private Transform podium;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform weaponStand;
        [SerializeField] private Camera firstPersonCamera;
        [SerializeField] private Transform firstPersonHolder;
        [SerializeField] private GameObject[] firstPersonArms;      // FPArms_CT, FPArms_T

        private GameObject character;
        private Animator animator;
        private int characterIndex;
        private int weaponIndex;
        private GameObject thirdPersonWeapon;
        private AnimationClip[] clips = new AnimationClip[0];
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private string playing = "Controller";
        private bool turntable;
        private Vector2 scroll;
        private bool menu = true;

        // parametrii pentru modul Controller
        private float moveX, moveZ, aimPitch, lean;
        private bool crouch, sprint;

        // camera orbitala
        private Vector3 focus = new Vector3(0f, 1.05f, 0f);
        private float yaw = 180f, pitch = 8f, distance = 3.4f;

        // FP
        private bool firstPerson;
        private bool aimDownSights;
        private GameObject fpWeapon;
        private FirstPersonArms fpArms;

        private void Start()
        {
            Spawn();
        }

        private void OnDestroy()
        {
            StopClip();
        }

        // ------------------------------------------------------------------
        // Personaj si arma
        // ------------------------------------------------------------------

        private void Spawn()
        {
            StopClip();
            if (character != null)
            {
                Destroy(character);
            }

            if (characterPrefabs == null || characterPrefabs.Length == 0)
            {
                return;
            }

            GameObject prefab = characterPrefabs[Mathf.Clamp(characterIndex, 0, characterPrefabs.Length - 1)];
            character = Instantiate(prefab, podium.position, podium.rotation);
            character.name = prefab.name;
            animator = character.GetComponent<Animator>();
            clips = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips
                : new AnimationClip[0];
            System.Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));

            // Driver-ul de joc citeste viteza / camera jucatorului; aici parametrii vin din meniu
            CharacterAnimatorDriver driver = character.GetComponent<CharacterAnimatorDriver>();
            if (driver != null)
            {
                driver.enabled = false;
            }

            AttachWeapon();
            BuildFirstPerson();
        }

        private WeaponData CurrentWeapon => weapons != null && weapons.Length > 0 ? weapons[Mathf.Clamp(weaponIndex, 0, weapons.Length - 1)] : null;

        private void AttachWeapon()
        {
            if (thirdPersonWeapon != null)
            {
                Destroy(thirdPersonWeapon);
            }

            WeaponData data = CurrentWeapon;
            Transform mount = Find(character.transform, "WeaponMount");
            if (data == null || mount == null || data.thirdPersonPrefab == null)
            {
                return;
            }

            thirdPersonWeapon = Instantiate(data.thirdPersonPrefab, mount);
            thirdPersonWeapon.transform.localPosition = Vector3.zero;
            thirdPersonWeapon.transform.localRotation = Quaternion.identity;

            if (animator != null)
            {
                animator.SetFloat("WeaponKind", (int)data.kind);
            }

            ThirdPersonWeaponIK ik = character.GetComponent<ThirdPersonWeaponIK>();
            if (ik != null)
            {
                ik.Bind(thirdPersonWeapon.GetComponent<WeaponSockets>(), data.kind);
            }
        }

        /// <summary>Mainile FP ale echipei personajului, cu arma FP curenta, sub camera FP.</summary>
        private void BuildFirstPerson()
        {
            if (fpWeapon != null)
            {
                Destroy(fpWeapon);
            }

            if (fpArms != null)
            {
                Destroy(fpArms.gameObject);
            }

            WeaponData data = CurrentWeapon;
            if (data == null || firstPersonCamera == null || firstPersonHolder == null || data.firstPersonPrefab == null)
            {
                return;
            }

            fpWeapon = Instantiate(data.firstPersonPrefab, firstPersonHolder);
            fpWeapon.transform.localPosition = Vector3.zero;
            fpWeapon.transform.localRotation = Quaternion.identity;
            WeaponSockets sockets = fpWeapon.GetComponent<WeaponSockets>();

            bool attacker = character != null && character.name.Contains("T_Insurgent");
            GameObject armsPrefab = firstPersonArms != null && firstPersonArms.Length > 0
                ? firstPersonArms[attacker && firstPersonArms.Length > 1 && firstPersonArms[1] != null ? 1 : 0]
                : null;
            if (armsPrefab != null)
            {
                GameObject arms = Instantiate(armsPrefab, firstPersonCamera.transform);
                fpArms = arms.GetComponent<FirstPersonArms>();
                if (fpArms != null)
                {
                    fpArms.Bind(sockets, data.kind);
                }
            }

            PlaceFirstPersonWeapon();
        }

        private void PlaceFirstPersonWeapon()
        {
            WeaponData data = CurrentWeapon;
            if (data == null || fpWeapon == null)
            {
                return;
            }

            firstPersonHolder.localPosition = data.firstPersonOffset;
            WeaponSockets sockets = fpWeapon.GetComponent<WeaponSockets>();
            if (aimDownSights && sockets != null && sockets.Sight != null)
            {
                Vector3 sight = firstPersonHolder.InverseTransformPoint(sockets.Sight.position);
                firstPersonHolder.localPosition = new Vector3(-sight.x, -sight.y, data.adsEyeRelief - sight.z);
            }
        }

        // ------------------------------------------------------------------
        // Animatie
        // ------------------------------------------------------------------

        public void PlayClip(string name, float normalizedTime = -1f)
        {
            AnimationClip clip = System.Array.Find(clips, c => c.name == name);
            if (clip == null || animator == null)
            {
                return;
            }

            StopClip();
            clipPlayable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
            playing = name;

            if (normalizedTime >= 0f)
            {
                clipPlayable.SetTime(clip.length * normalizedTime);
                clipPlayable.SetSpeed(0f);
                graph.Evaluate();
            }
        }

        public void UseController()
        {
            StopClip();
            playing = "Controller";
        }

        private void StopClip()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                menu = !menu;
            }

            if (animator != null && playing == "Controller")
            {
                animator.SetFloat("MoveX", moveX);
                animator.SetFloat("MoveZ", moveZ);
                animator.SetFloat("Speed", new Vector2(moveX, moveZ).magnitude);
                animator.SetFloat("AimPitch", aimPitch);
                animator.SetFloat("Lean", lean);
                animator.SetBool("Crouch", crouch);
                animator.SetBool("Sprint", sprint);
                animator.SetBool("Grounded", true);
                int leanLayer = animator.GetLayerIndex("Lean");
                if (leanLayer >= 0)
                {
                    animator.SetLayerWeight(leanLayer, Mathf.Abs(lean));
                }
            }

            if (turntable && character != null)
            {
                character.transform.Rotate(0f, 30f * Time.deltaTime, 0f);
            }

            UpdateOrbit();
        }

        private void UpdateOrbit()
        {
            if (viewCamera == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.25f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -20f, 70f);
            }

            if (mouse != null)
            {
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.002f, 0.6f, 9f);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            viewCamera.transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -distance), rotation);
            viewCamera.enabled = !firstPerson;
            if (firstPersonCamera != null)
            {
                firstPersonCamera.enabled = firstPerson;
            }
        }

        public void SetView(float viewYaw, float viewPitch, float viewDistance, Vector3 viewFocus)
        {
            yaw = viewYaw;
            pitch = viewPitch;
            distance = viewDistance;
            focus = viewFocus;
            UpdateOrbit();
        }

        // ------------------------------------------------------------------
        // Meniu de debug
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!menu)
            {
                GUI.Label(new Rect(10, 10, 300, 20), "Tab = meniu");
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 330, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("CHOKEHOLD - Character Showcase (Tab ascunde)");

            if (characterPrefabs != null && characterPrefabs.Length > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < characterPrefabs.Length; i++)
                {
                    if (characterPrefabs[i] != null && GUILayout.Toggle(characterIndex == i, characterPrefabs[i].name.Replace("Character_", ""), "Button") && characterIndex != i)
                    {
                        characterIndex = i;
                        Spawn();
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (weapons != null && weapons.Length > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < weapons.Length; i++)
                {
                    if (weapons[i] != null && GUILayout.Toggle(weaponIndex == i, weapons[i].displayName, "Button") && weaponIndex != i)
                    {
                        weaponIndex = i;
                        AttachWeapon();
                        BuildFirstPerson();
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fata")) SetView(180f, 8f, 3.4f, new Vector3(0f, 1.05f, 0f));
            if (GUILayout.Button("Spate")) SetView(0f, 8f, 3.4f, new Vector3(0f, 1.05f, 0f));
            if (GUILayout.Button("Profil")) SetView(270f, 5f, 3.4f, new Vector3(0f, 1.05f, 0f));
            if (GUILayout.Button("Cap")) SetView(160f, 4f, 0.9f, new Vector3(0f, 1.62f, 0f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            turntable = GUILayout.Toggle(turntable, "Rotire");
            bool fp = GUILayout.Toggle(firstPerson, "Vedere FP");
            if (fp != firstPerson)
            {
                firstPerson = fp;
            }
            bool ads = GUILayout.Toggle(aimDownSights, "ADS");
            if (ads != aimDownSights)
            {
                aimDownSights = ads;
                PlaceFirstPersonWeapon();
            }
            GUILayout.EndHorizontal();

            LODGroup lods = character != null ? character.GetComponent<LODGroup>() : null;
            if (lods != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("LOD auto")) lods.ForceLOD(-1);
                if (GUILayout.Button("LOD0")) lods.ForceLOD(0);
                if (GUILayout.Button("LOD1")) lods.ForceLOD(1);
                if (GUILayout.Button("LOD2")) lods.ForceLOD(2);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ragdoll"))
            {
                StopClip();
                Ragdoll ragdoll = character != null ? character.GetComponent<Ragdoll>() : null;
                if (ragdoll != null)
                {
                    ragdoll.SetAlive(false);
                }
            }
            if (GUILayout.Button("Reset"))
            {
                Spawn();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Ruleaza: {playing}");
            if (GUILayout.Button("Animator Controller (parametri)"))
            {
                UseController();
            }

            if (playing == "Controller")
            {
                moveX = Slider("MoveX", moveX, -1f, 1f);
                moveZ = Slider("MoveZ", moveZ, -1f, 1f);
                aimPitch = Slider("AimPitch", aimPitch, -60f, 60f);
                lean = Slider("Lean", lean, -1f, 1f);
                GUILayout.BeginHorizontal();
                crouch = GUILayout.Toggle(crouch, "Crouch");
                sprint = GUILayout.Toggle(sprint, "Sprint");
                foreach (string trigger in new[] { "Fire", "Reload", "Draw", "HitF" })
                {
                    if (GUILayout.Button(trigger) && animator != null)
                    {
                        animator.SetTrigger(trigger);
                    }
                }
                GUILayout.EndHorizontal();
            }

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (AnimationClip clip in clips)
            {
                if (GUILayout.Button(clip.name))
                {
                    PlayClip(clip.name);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:F2}", GUILayout.Width(110));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }

        // ------------------------------------------------------------------
        // Capturi pentru documentatie
        // ------------------------------------------------------------------

        public string CharacterName => character != null ? character.name.Replace("Character_", "") : "";
        public int CharacterCount => characterPrefabs != null ? characterPrefabs.Length : 0;

        public void SelectCharacter(int index, int weapon)
        {
            characterIndex = index;
            weaponIndex = weapon;
            Spawn();
        }

        public int WeaponIndexOf(string id)
        {
            for (int i = 0; weapons != null && i < weapons.Length; i++)
            {
                if (weapons[i] != null && weapons[i].name == id)
                {
                    return i;
                }
            }

            return 0;
        }

        public void SetFirstPerson(bool enabledView, bool ads)
        {
            firstPerson = enabledView;
            aimDownSights = ads;
            PlaceFirstPersonWeapon();
            UpdateOrbit();
            if (fpArms != null)
            {
                fpArms.Apply();
            }
        }

        public void ForceLod(int level)
        {
            LODGroup lods = character != null ? character.GetComponent<LODGroup>() : null;
            if (lods != null)
            {
                lods.ForceLOD(level);
            }
        }

        public void ShowWeaponStand(bool show)
        {
            if (weaponStand != null)
            {
                weaponStand.gameObject.SetActive(show);
            }
        }

        public void Capture(string path)
        {
            Camera camera = firstPerson ? firstPersonCamera : viewCamera;
            if (camera == null)
            {
                return;
            }

            const int width = 1600, height = 1000;
            RenderTexture rt = new RenderTexture(width, height, 24);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = camera.targetTexture;

            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = target;
                RenderTexture.active = previous;
                rt.Release();
                Destroy(rt);
                Destroy(image);
            }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
