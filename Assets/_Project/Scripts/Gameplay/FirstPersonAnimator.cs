using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Animatiile de la persoana intai, procedurale, peste miscarea pe care HitscanWeapon o da holder-ului
    /// (sway, bob, recul, ADS): scoaterea armei, tragerea (piedica / inchizatorul), reincarcarea (mana stanga ia
    /// incarcatorul, il scoate si pune altul), inspectarea (Y), sprintul si arma coborata la plantare / dezamorsare.
    /// Pune si mainile FP dupa echipa si le desenaza, impreuna cu arma, cu o camera overlay (layer FirstPerson):
    /// nu intra in pereti, nu fac umbre (umbra jucatorului vine de la modelul complet).
    /// </summary>
    [RequireComponent(typeof(HitscanWeapon))]
    public class FirstPersonAnimator : MonoBehaviour
    {
        public const string LayerName = "FirstPerson";

        [SerializeField] private GameObject defenderArms;
        [SerializeField] private GameObject attackerArms;
        [SerializeField] private float drawTime = 0.5f;
        [SerializeField] private float inspectTime = 2.6f;
        [SerializeField] private float viewModelFieldOfView = 58f;

        private HitscanWeapon weapon;
        private FpsController fps;
        private TeamMember member;
        private BombController bomb;
        private FirstPersonArms arms;
        private WeaponSockets sockets;
        private Transform model;
        private Renderer magazineRenderer;
        private WeaponKind kind;

        private Vector3 magazineRest;
        private Vector3 boltRest;

        private float drawT = 1f;
        private float reloadT = -1f;
        private float reloadDuration = 2f;
        private bool reloadEmpty;
        private float inspectT = -1f;
        private float fireKick;
        private float boltKick;
        private float sprintWeight;
        private float loweredWeight;

        public FirstPersonArms Arms => arms;

        private void Start()
        {
            weapon = GetComponent<HitscanWeapon>();
            fps = GetComponentInParent<FpsController>();
            member = GetComponentInParent<TeamMember>();
            bomb = FindAnyObjectByType<BombController>();
            sockets = weapon.Sockets;

            if (sockets == null)
            {
                enabled = false;
                return;
            }

            model = sockets.transform;
            kind = weapon.Data != null ? weapon.Data.kind : WeaponKind.Rifle;

            if (sockets.Magazine != null)
            {
                magazineRest = sockets.Magazine.localPosition;
                magazineRenderer = sockets.Magazine.GetComponentInChildren<Renderer>();
            }

            if (sockets.Bolt != null)
            {
                boltRest = sockets.Bolt.localPosition;
            }

            GameObject prefab = member != null && member.Team == Team.Attackers && attackerArms != null ? attackerArms : defenderArms;
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, transform.parent);
                instance.name = prefab.name;
                arms = instance.GetComponent<FirstPersonArms>();
                if (arms != null)
                {
                    arms.Bind(sockets, kind);
                }
            }

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0)
            {
                PrepareViewModel(model.gameObject, layer);
                if (arms != null)
                {
                    PrepareViewModel(arms.gameObject, layer);
                }
                SetupOverlayCamera(layer);
            }

            weapon.Fired += OnFired;
            weapon.ReloadStarted += OnReloadStarted;
            drawT = 0f;
        }

        private void OnDestroy()
        {
            if (weapon != null)
            {
                weapon.Fired -= OnFired;
                weapon.ReloadStarted -= OnReloadStarted;
            }
        }

        /// <summary>Repornește animatia de scoatere (la spawn sau la schimbarea armei).</summary>
        public void PlayDraw()
        {
            drawT = 0f;
            inspectT = -1f;
        }

        private void OnFired()
        {
            fireKick = 1f;
            boltKick = 1f;
            inspectT = -1f;
        }

        private void OnReloadStarted()
        {
            reloadT = 0f;
            reloadEmpty = weapon.CurrentAmmo == 0;
            // HitscanWeapon termina reincarcarea dupa reloadTime; animatia se incadreaza in acelasi timp
            reloadDuration = weapon.Data != null ? weapon.Data.reloadTime : 2f;
            inspectT = -1f;
        }

        private void Update()
        {
            Animate(Time.deltaTime);
        }

        /// <summary>
        /// Forteaza o stare pentru capturi / meniul de debug: draw, fire, reload, sprint, inspect, idle (t = 0..1).
        /// </summary>
        public void PreviewState(string state, float t)
        {
            drawT = 1f;
            reloadT = -1f;
            inspectT = -1f;
            sprintWeight = 0f;
            fireKick = 0f;
            boltKick = 0f;
            switch (state)
            {
                case "draw": drawT = t; break;
                case "fire": fireKick = 1f; boltKick = 1f; break;
                case "reload": reloadT = t; reloadDuration = 1e6f; break;
                case "sprint": sprintWeight = 1f; break;
                case "inspect": inspectT = t; break;
            }
            Animate(0f);
        }

        private void Animate(float dt)
        {

            Vector3 position = Vector3.zero;
            Vector3 rotation = Vector3.zero;

            // respiratie
            float time = Time.time;
            position.y += Mathf.Sin(time * 1.7f) * 0.0018f;
            rotation.x += Mathf.Sin(time * 1.1f) * 0.25f;

            // scoaterea armei: vine de jos, rotita
            if (drawT < 1f)
            {
                drawT = Mathf.Min(1f, drawT + dt / drawTime);
                float away = 1f - Ease(drawT);
                position += new Vector3(0.03f, -0.24f, -0.06f) * away;
                rotation += new Vector3(40f, -12f, -25f) * away;
            }

            // sprint: arma de-a curmezisul, coborata
            bool aiming = Mouse.current != null && Mouse.current.rightButton.isPressed;
            bool sprinting = fps != null && fps.IsSprinting && reloadT < 0f && !aiming;
            sprintWeight = Mathf.MoveTowards(sprintWeight, sprinting ? 1f : 0f, dt * 5f);
            position += new Vector3(-0.05f, -0.05f, -0.03f) * Ease(sprintWeight);
            rotation += new Vector3(14f, -38f, 24f) * Ease(sprintWeight);

            // plantare / dezamorsare: arma coborata din cadru
            bool lowered = fps != null && fps.InputLocked && bomb != null && (bomb.IsPlanting || bomb.IsDefusing);
            loweredWeight = Mathf.MoveTowards(loweredWeight, lowered ? 1f : 0f, dt * 4f);
            position += new Vector3(0f, -0.32f, -0.05f) * Ease(loweredWeight);
            rotation += new Vector3(45f, 0f, 0f) * Ease(loweredWeight);

            // tragere: teava urca putin, inchizatorul / piedica merge inapoi
            fireKick = Mathf.MoveTowards(fireKick, 0f, dt * 12f);
            rotation.x -= fireKick * (kind == WeaponKind.Pistol ? 6f : 2.5f);
            boltKick = Mathf.MoveTowards(boltKick, 0f, dt * 22f);

            Vector3 magazineOffset = Vector3.zero;
            float leftHand = 0f;
            Vector3 leftHandPosition = Vector3.zero;

            if (reloadT >= 0f)
            {
                reloadT += dt / Mathf.Max(0.3f, reloadDuration);
                float t = reloadT;

                float tilt = Window(t, 0.04f, 0.18f, 0.80f, 0.96f);
                position += new Vector3(-0.05f, 0.06f, 0.02f) * tilt;     // ridicata: puțul se vede in cadru
                rotation += new Vector3(-8f, -10f, -26f) * tilt;    // rotit spre dreapta: puțul incarcatorului spre mana stanga

                // incarcatorul iese in jos (0.28 - 0.42), lipseste, cel nou urca (0.50 - 0.66)
                float drop = t < 0.5f ? Ramp(t, 0.28f, 0.42f) : 1f - Ramp(t, 0.50f, 0.66f);
                magazineOffset = new Vector3(0f, -1f, 0.15f) * (kind == WeaponKind.Pistol ? 0.22f : 0.30f) * drop;

                // mana stanga: la incarcator, coboara cu el, revine pe garda
                leftHand = Window(t, 0.12f, 0.27f, 0.70f, 0.84f);

                if (reloadEmpty && t > 0.70f && t < 0.88f)
                {
                    boltKick = Mathf.Max(boltKick, Window(t, 0.70f, 0.74f, 0.80f, 0.88f));
                }

                if (t >= 1f)
                {
                    reloadT = -1f;
                }
            }

            // inspectare: Y, cand nu trage / nu ocheste / nu reincarca
            if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame && reloadT < 0f && !aiming)
            {
                inspectT = 0f;
            }

            if (inspectT >= 0f)
            {
                if (aiming || reloadT >= 0f)
                {
                    inspectT = -1f;
                }
                else
                {
                    inspectT += dt / inspectTime;
                    float t = inspectT;
                    // flancul stang (0 - 0.5), apoi partea de sus (0.5 - 0.85), apoi inapoi
                    Vector3 side = new Vector3(-6f, -42f, 55f);
                    Vector3 top = new Vector3(-28f, 20f, -12f);
                    Vector3 pose = Vector3.Lerp(Vector3.zero, side, Ease(Ramp(t, 0f, 0.25f)));
                    pose = Vector3.Lerp(pose, top, Ease(Ramp(t, 0.5f, 0.65f)));
                    pose = Vector3.Lerp(pose, Vector3.zero, Ease(Ramp(t, 0.85f, 1f)));
                    rotation += pose;
                    position += new Vector3(-0.06f, 0.05f, 0.03f) * Window(t, 0f, 0.2f, 0.85f, 1f);
                    if (t >= 1f)
                    {
                        inspectT = -1f;
                    }
                }
            }

            model.localPosition = position;
            model.localRotation = Quaternion.Euler(rotation);

            // Deplasarile sunt in spatiul armei (x dreapta, y sus, z teava); piesele pot sta sub un nod cu
            // rotatia din import, deci se trec in spatiul parintelui lor.
            if (sockets.Magazine != null)
            {
                sockets.Magazine.localPosition = magazineRest + WeaponToParent(sockets.Magazine, magazineOffset);
            }

            if (sockets.Bolt != null)
            {
                float travel = kind == WeaponKind.Pistol ? 0.026f : 0.05f;
                sockets.Bolt.localPosition = boltRest + WeaponToParent(sockets.Bolt, Vector3.back * travel * boltKick);
            }

            if (arms != null)
            {
                if (leftHand > 0f && magazineRenderer != null)
                {
                    Bounds bounds = magazineRenderer.bounds;
                    leftHandPosition = bounds.center - model.up * bounds.extents.y * 0.1f;
                    arms.SetLeftOverride(leftHandPosition, model.rotation, leftHand);
                }
                else
                {
                    arms.SetLeftOverride(Vector3.zero, Quaternion.identity, 0f);
                }
            }
        }

        // ------------------------------------------------------------------

        private void SetupOverlayCamera(int layer)
        {
            Camera main = transform.parent != null ? transform.parent.GetComponent<Camera>() : null;
            if (main == null)
            {
                return;
            }

            main.cullingMask &= ~(1 << layer);

            GameObject go = new GameObject("FirstPersonCamera");
            go.transform.SetParent(main.transform, false);
            Camera overlay = go.AddComponent<Camera>();
            overlay.cullingMask = 1 << layer;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 4f;
            overlay.fieldOfView = viewModelFieldOfView;

            UniversalAdditionalCameraData overlayData = overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);
        }

        private static void PrepareViewModel(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.updateWhenOffscreen = true;     // bounds-urile nu urmaresc IK-ul
                }
            }
        }

        private Vector3 WeaponToParent(Transform part, Vector3 offset)
        {
            return part.parent != null ? part.parent.InverseTransformVector(model.TransformVector(offset)) : offset;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Ramp(float t, float from, float to)
        {
            return Mathf.Clamp01((t - from) / (to - from));
        }

        /// <summary>0 pana la a, urca pana la b, 1 pana la c, coboara pana la d.</summary>
        private static float Window(float t, float a, float b, float c, float d)
        {
            return Ease(Ramp(t, a, b)) * (1f - Ease(Ramp(t, c, d)));
        }
    }
}
