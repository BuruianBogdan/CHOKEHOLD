using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Animatiile de la persoana intai, procedurale, peste miscarea pe care HitscanWeapon o da holder-ului
    /// (sway, bob, recul, ADS): scoaterea armei, tragerea (piedica / inchizatorul), armarea lunetistelor,
    /// reincarcarea (mana stanga ia incarcatorul, il scoate si pune altul), cutitul (taietura / injunghiere),
    /// inspectarea (Y), sprintul si arma coborata la plantare / dezamorsare.
    /// Pune mainile FP ale echipei jucatorului si le desenaza, impreuna cu arma, cu o camera overlay (layer
    /// FirstPerson): nu intra in pereti, nu fac umbre (umbra jucatorului vine de la modelul complet).
    /// Se reface la fiecare arma noua (HitscanWeapon.Equipped).
    /// </summary>
    [RequireComponent(typeof(HitscanWeapon))]
    public class FirstPersonAnimator : MonoBehaviour
    {
        public const string LayerName = "FirstPerson";

        [SerializeField] private GameObject defenderArms;
        [SerializeField] private GameObject attackerArms;
        [SerializeField] private float drawTime = 0.5f;
        [SerializeField] private float inspectTime = 2.6f;
        [SerializeField] private float viewModelFieldOfView = 66f;

        private HitscanWeapon weapon;
        private FpsController fps;
        private TeamMember member;
        private BombController bomb;
        private FirstPersonArms arms;
        private GameObject armsPrefab;
        private WeaponSockets sockets;
        private Transform model;
        private Renderer magazineRenderer;
        private Renderer[] modelRenderers = new Renderer[0];
        private WeaponKind kind;
        private WeaponData data;
        private Vector3 baseRotation;
        private int layer = -1;
        private bool hidden;

        private Vector3 magazineRest;
        private Vector3 boltRest;

        private float drawT = 1f;
        private float reloadT = -1f;
        private float reloadDuration = 2f;
        private bool reloadEmpty;
        private float inspectT = -1f;
        private float fireKick;
        private float boltKick;
        private float cycleT = -1f;
        private float meleeT = -1f;
        private bool meleeHeavy;
        private float sprintWeight;
        private float loweredWeight;

        public FirstPersonArms Arms => arms;

        /// <summary>Arma coborata din cadru (grenada in mana).</summary>
        public bool Lowered { get; set; }

        /// <summary>Ascunde doar arma (mainile raman: grenada).</summary>
        public void SetWeaponHidden(bool value)
        {
            foreach (Renderer r in modelRenderers)
            {
                if (r != null) r.enabled = !value && !hidden;
            }
        }

        private void Start()
        {
            weapon = GetComponent<HitscanWeapon>();
            fps = GetComponentInParent<FpsController>();
            member = GetComponentInParent<TeamMember>();
            bomb = FindAnyObjectByType<BombController>();

            layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0)
            {
                SetupOverlayCamera(layer);
            }

            weapon.Fired += OnFired;
            weapon.ReloadStarted += OnReloadStarted;
            weapon.Equipped += Rebind;
            weapon.MeleeSwing += OnMeleeSwing;

            Rebind(weapon.Data);
        }

        private void OnDestroy()
        {
            if (weapon != null)
            {
                weapon.Fired -= OnFired;
                weapon.ReloadStarted -= OnReloadStarted;
                weapon.Equipped -= Rebind;
                weapon.MeleeSwing -= OnMeleeSwing;
            }
        }

        /// <summary>Arma noua in mana: piesele animabile, mainile pe noile socket-uri, animatia de scoatere.</summary>
        public void Rebind(WeaponData newData)
        {
            if (weapon == null)
            {
                return;
            }

            data = newData;
            sockets = weapon.Sockets;
            model = sockets != null ? sockets.transform : null;
            kind = data != null ? data.kind : WeaponKind.Rifle;
            baseRotation = data != null ? data.firstPersonRotation : Vector3.zero;
            magazineRenderer = null;
            reloadT = -1f;
            inspectT = -1f;
            meleeT = -1f;
            cycleT = -1f;

            if (sockets != null && sockets.Magazine != null)
            {
                magazineRest = sockets.Magazine.localPosition;
                magazineRenderer = sockets.Magazine.GetComponentInChildren<Renderer>();
            }

            if (sockets != null && sockets.Bolt != null)
            {
                boltRest = sockets.Bolt.localPosition;
            }

            EnsureArms();
            if (arms != null)
            {
                arms.Bind(sockets, kind, data != null ? data.leftGrip : LeftGrip.Handguard, data != null && data.reverseGrip);
                arms.SetLeftOverride(Vector3.zero, Quaternion.identity, 0f);
            }

            modelRenderers = model != null ? model.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (layer >= 0 && model != null)
            {
                PrepareViewModel(model.gameObject, layer);
            }

            SetHidden(hidden);
            PlayDraw();
        }

        /// <summary>Mainile echipei curente (echipa jucatorului se poate schimba intre meciuri).</summary>
        private void EnsureArms()
        {
            GameObject prefab = member != null && member.Team == Team.Attackers && attackerArms != null ? attackerArms : defenderArms;
            if (prefab == null || (arms != null && prefab == armsPrefab))
            {
                return;
            }

            if (arms != null)
            {
                Destroy(arms.gameObject);
            }

            GameObject instance = Instantiate(prefab, transform.parent);
            instance.name = prefab.name;
            armsPrefab = prefab;
            arms = instance.GetComponent<FirstPersonArms>();
            if (layer >= 0)
            {
                PrepareViewModel(instance, layer);
            }
        }

        /// <summary>Luneta: arma si mainile dispar (se vede doar ecranul lunetei).</summary>
        public void SetHidden(bool value)
        {
            hidden = value;
            foreach (Renderer r in modelRenderers)
            {
                if (r != null) r.enabled = !value;
            }
            if (arms != null)
            {
                arms.Hidden = value;
            }
        }

        /// <summary>Reporneste animatia de scoatere (la spawn sau la schimbarea armei).</summary>
        public void PlayDraw()
        {
            drawT = 0f;
            inspectT = -1f;
            if (data != null)
            {
                drawTime = Mathf.Max(0.2f, data.drawTime * 0.85f);
            }
        }

        private void OnFired()
        {
            fireKick = 1f;
            boltKick = 1f;
            inspectT = -1f;
            if (data != null && data.sight == SightType.SniperScope)
            {
                cycleT = 0f;
            }
        }

        private void OnMeleeSwing(bool heavy)
        {
            meleeT = 0f;
            meleeHeavy = heavy;
            inspectT = -1f;
        }

        private void OnReloadStarted()
        {
            reloadT = 0f;
            reloadEmpty = weapon.CurrentAmmo == 0;
            // HitscanWeapon termina reincarcarea dupa aceeasi durata; animatia se incadreaza in ea
            reloadDuration = weapon.ReloadDuration;
            inspectT = -1f;
        }

        private void Update()
        {
            Animate(Time.deltaTime);
        }

        /// <summary>
        /// Forteaza o stare pentru capturi / meniul de debug: draw, fire, reload, sprint, inspect, slash, stab, idle (t = 0..1).
        /// </summary>
        public void PreviewState(string state, float t)
        {
            drawT = 1f;
            reloadT = -1f;
            inspectT = -1f;
            meleeT = -1f;
            cycleT = -1f;
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
                case "slash": meleeT = t; meleeHeavy = false; break;
                case "stab": meleeT = t; meleeHeavy = true; break;
            }
            Animate(0f);
        }

        private void Animate(float dt)
        {
            if (model == null)
            {
                return;
            }

            Vector3 position = Vector3.zero;
            Vector3 rotation = Vector3.zero;
            bool knife = kind == WeaponKind.Knife;

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

            // sprint: arma de-a curmezisul, coborata (cutitul doar coborat)
            bool aiming = weapon != null && weapon.IsAiming;
            bool sprinting = fps != null && fps.IsSprinting && reloadT < 0f && !aiming;
            sprintWeight = Mathf.MoveTowards(sprintWeight, sprinting ? 1f : 0f, dt * 5f);
            position += (knife ? new Vector3(0f, -0.04f, -0.02f) : new Vector3(-0.05f, -0.05f, -0.03f)) * Ease(sprintWeight);
            rotation += (knife ? new Vector3(20f, 0f, 10f) : new Vector3(14f, -38f, 24f)) * Ease(sprintWeight);

            // plantare / dezamorsare: arma coborata din cadru
            bool lowered = Lowered || (fps != null && fps.InputLocked && bomb != null && (bomb.IsPlanting || bomb.IsDefusing));
            loweredWeight = Mathf.MoveTowards(loweredWeight, lowered ? 1f : 0f, dt * 4f);
            position += new Vector3(0f, -0.32f, -0.05f) * Ease(loweredWeight);
            rotation += new Vector3(45f, 0f, 0f) * Ease(loweredWeight);

            // tragere: teava urca putin, inchizatorul / piedica merge inapoi
            fireKick = Mathf.MoveTowards(fireKick, 0f, dt * 12f);
            if (!knife)
            {
                rotation.x -= fireKick * (kind == WeaponKind.Pistol ? 3.5f : (data != null && data.sight == SightType.SniperScope ? 4f : 1.2f));
            }
            boltKick = Mathf.MoveTowards(boltKick, 0f, dt * 22f);

            // lunetiste: dupa foc arma se roteste putin si se ridica (armarea inchizatorului)
            if (cycleT >= 0f)
            {
                cycleT += dt / 0.75f;
                float c = Window(cycleT, 0.15f, 0.4f, 0.65f, 1f);
                rotation += new Vector3(-6f, 8f, 18f) * c;
                position += new Vector3(0.02f, 0.03f, -0.03f) * c;
                if (cycleT >= 1f)
                {
                    cycleT = -1f;
                }
            }

            // cutit: taietura de la dreapta la stanga sau injunghiere inainte
            if (meleeT >= 0f)
            {
                meleeT += dt / (meleeHeavy ? 0.55f : 0.36f);
                float m = meleeT;
                // cutitul drept sta cu lama in sus: varful se culca inainte (+x); karambitul tinut invers are lama
                // in jos, asa ca il aducem inainte cu -x (carligul taie spre tinta)
                bool reverse = data != null && data.reverseGrip;
                if (meleeHeavy)
                {
                    // injunghiere: mana se trage putin inapoi, apoi impinge drept spre cruce, cu varful inainte
                    if (reverse)
                    {
                        // karambit: lovitura de sus in jos (ciocan), carligul trece prin fata inainte-jos
                        Keyframes(m, StabTimes, HookPositions, HookRotations, 40f, ref position, ref rotation);
                    }
                    else
                    {
                        Keyframes(m, StabTimes, StabPositions, StabRotations, 60f, ref position, ref rotation);
                    }
                }
                else
                {
                    // taietura: armat in dreapta-sus, apoi o trecere pe diagonala spre stanga-jos, lama inainte
                    float p = reverse ? -35f : 45f;
                    Keyframes(m, SlashTimes, SlashPositions, SlashRotations, p, ref position, ref rotation);
                }
                if (meleeT >= 1f)
                {
                    meleeT = -1f;
                }
            }

            Vector3 magazineOffset = Vector3.zero;
            float leftHand = 0f;

            if (reloadT >= 0f && !knife)
            {
                reloadT += dt / Mathf.Max(0.3f, reloadDuration);
                float t = reloadT;

                float tilt = Window(t, 0.04f, 0.18f, 0.80f, 0.96f);
                position += new Vector3(-0.05f, 0.06f, 0.02f) * tilt;     // ridicata: putul se vede in cadru
                rotation += new Vector3(-8f, -10f, -26f) * tilt;           // rotit spre dreapta: putul spre mana stanga

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
            if (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame && reloadT < 0f && !aiming && !PlayerInputGate.Blocked)
            {
                inspectT = 0f;
            }

            if (inspectT >= 0f)
            {
                if (aiming || reloadT >= 0f || meleeT >= 0f)
                {
                    inspectT = -1f;
                }
                else
                {
                    inspectT += dt / inspectTime;
                    float t = inspectT;
                    // flancul stang (0 - 0.5), apoi partea de sus (0.5 - 0.85), apoi inapoi
                    Vector3 side = knife ? new Vector3(0f, -60f, 70f) : new Vector3(-6f, -42f, 55f);
                    Vector3 top = knife ? new Vector3(-30f, 40f, -80f) : new Vector3(-28f, 20f, -12f);
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
            model.localRotation = Quaternion.Euler(baseRotation + rotation);

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
                    arms.SetLeftOverride(bounds.center - model.up * bounds.extents.y * 0.1f, model.rotation, leftHand);
                }
                else
                {
                    arms.SetLeftOverride(Vector3.zero, Quaternion.identity, 0f);
                }
            }
        }

        // ------------------------------------------------------------------

        private void SetupOverlayCamera(int viewLayer)
        {
            Camera main = transform.parent != null ? transform.parent.GetComponent<Camera>() : null;
            if (main == null)
            {
                return;
            }

            main.cullingMask &= ~(1 << viewLayer);

            GameObject go = new GameObject("FirstPersonCamera");
            go.transform.SetParent(main.transform, false);
            Camera overlay = go.AddComponent<Camera>();
            overlay.cullingMask = 1 << viewLayer;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 4f;
            overlay.fieldOfView = viewModelFieldOfView;

            UniversalAdditionalCameraData overlayData = overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);
        }

        private static void PrepareViewModel(GameObject root, int viewLayer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = viewLayer;
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

        // chei pentru cutit: x-ul rotatiei e o fractiune din inclinarea lamei (p), y si z sunt in grade
        private static readonly float[] SlashTimes = { 0f, 0.2f, 0.55f, 1f };
        private static readonly Vector3[] SlashPositions =
        {
            Vector3.zero, new Vector3(0.06f, 0.05f, -0.02f), new Vector3(-0.2f, -0.03f, 0.1f), Vector3.zero,
        };
        private static readonly Vector3[] SlashRotations =
        {
            Vector3.zero, new Vector3(0.3f, 30f, 25f), new Vector3(1f, -55f, -35f), Vector3.zero,
        };
        private static readonly float[] StabTimes = { 0f, 0.3f, 0.5f, 0.65f, 1f };
        private static readonly Vector3[] StabPositions =
        {
            Vector3.zero, new Vector3(0.02f, 0f, -0.07f), new Vector3(-0.06f, 0.05f, 0.12f), new Vector3(-0.06f, 0.05f, 0.12f), Vector3.zero,
        };
        private static readonly Vector3[] StabRotations =
        {
            Vector3.zero, new Vector3(0.3f, 0f, 0f), new Vector3(1f, -6f, 0f), new Vector3(1f, -6f, 0f), Vector3.zero,
        };

        private static readonly Vector3[] HookPositions =
        {
            Vector3.zero, new Vector3(0.03f, 0.09f, -0.04f), new Vector3(-0.07f, -0.06f, 0.1f), new Vector3(-0.07f, -0.07f, 0.1f), Vector3.zero,
        };
        private static readonly Vector3[] HookRotations =
        {
            Vector3.zero, new Vector3(0.6f, 0f, 10f), new Vector3(-1f, -10f, -15f), new Vector3(-1f, -10f, -15f), Vector3.zero,
        };

        private static void Keyframes(float t, float[] times, Vector3[] positions, Vector3[] rotations, float pitch, ref Vector3 position, ref Vector3 rotation)
        {
            t = Mathf.Clamp01(t);
            for (int k = 1; k < times.Length; k++)
            {
                if (t <= times[k])
                {
                    float u = Ease(Mathf.InverseLerp(times[k - 1], times[k], t));
                    Vector3 a = rotations[k - 1];
                    Vector3 b = rotations[k];
                    position += Vector3.Lerp(positions[k - 1], positions[k], u);
                    rotation += new Vector3(Mathf.Lerp(a.x, b.x, u) * pitch, Mathf.Lerp(a.y, b.y, u), Mathf.Lerp(a.z, b.z, u));
                    return;
                }
            }
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
