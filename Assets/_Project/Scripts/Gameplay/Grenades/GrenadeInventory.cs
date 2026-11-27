using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay.Grenades
{
    /// <summary>
    /// Grenadele jucatorului (maxim 4, ca in CS) si aruncarea rapida, ca in Rainbow:
    ///   [4] alege grenada urmatoare, tii [G] apasat = arma coboara si grenada vine in mana dreapta,
    ///   dai drumul la [G] = arunci tare (cu click dreapta tinut: arunci incet, pe jos).
    /// Cine moare pierde grenadele; cine supravietuieste le pastreaza.
    /// </summary>
    public class GrenadeInventory : MonoBehaviour
    {
        private const float RaiseTime = 0.22f;
        private const float ThrowTime = 0.4f;
        private const float ReleaseAt = 0.3f;
        private const float OverhandSpeed = 17f;
        private const float UnderhandSpeed = 7.5f;

        private enum Phase { Idle, Raise, Ready, Throw }

        private readonly int[] counts = new int[GrenadeSpec.All.Length];
        private int selected = -1;
        private Phase phase = Phase.Idle;
        private float phaseTime;
        private bool underhand;
        private bool released;
        private GrenadeType inHand;
        private GameObject heldModel;

        private TeamMember member;
        private HitscanWeapon weapon;
        private FpsController fps;
        private FirstPersonAnimator animator;
        private TMP_Text hud;

        public event Action Changed;

        public int Count(GrenadeType type) => counts[(int)type];

        public int Total
        {
            get
            {
                int total = 0;
                foreach (int c in counts) total += c;
                return total;
            }
        }

        public GrenadeType? Selected => selected >= 0 ? (GrenadeType)selected : (GrenadeType?)null;
        public bool IsThrowing => phase != Phase.Idle;

        /// <summary>Tine grenada in mana chiar fara [G] apasat (capturi, boti).</summary>
        public bool ForceHold { get; set; }

        public bool CanTake(GrenadeType type)
        {
            return counts[(int)type] < GrenadeSpec.Of(type).MaxCarry && Total < GrenadeSpec.MaxTotal;
        }

        /// <summary>Adauga o grenada (cumparare); o selecteaza daca nu era nimic selectat.</summary>
        public bool Give(GrenadeType type)
        {
            if (!CanTake(type))
            {
                return false;
            }

            counts[(int)type]++;
            if (selected < 0)
            {
                selected = (int)type;
            }
            Changed?.Invoke();
            return true;
        }

        public void OnRespawn(bool wasDead)
        {
            Cancel();
            if (wasDead)
            {
                Array.Clear(counts, 0, counts.Length);
                selected = -1;
            }
            Changed?.Invoke();
        }

        private void Start()
        {
            member = GetComponent<TeamMember>();
            weapon = GetComponentInChildren<HitscanWeapon>(true);
            fps = GetComponent<FpsController>();
            if (GetComponent<GrenadeScreenEffects>() == null)
            {
                gameObject.AddComponent<GrenadeScreenEffects>();
            }
            BuildHud();
            Changed += RefreshHud;
            RefreshHud();
        }

        private void Update()
        {
            animator ??= GetComponentInChildren<FirstPersonAnimator>(true);
            bool alive = member == null || member.IsAlive;
            if (!alive || PlayerInputGate.Blocked || Keyboard.current == null)
            {
                if (phase == Phase.Raise || phase == Phase.Ready)
                {
                    Cancel();
                }
                if (phase == Phase.Throw)
                {
                    StepThrow();
                }
                return;
            }

            if (phase == Phase.Idle && Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                SelectNext();
            }

            switch (phase)
            {
                case Phase.Idle:
                    if (Keyboard.current.gKey.wasPressedThisFrame && selected >= 0 && counts[selected] > 0 && (weapon == null || !weapon.IsReloading))
                    {
                        Begin((GrenadeType)selected);
                    }
                    break;

                case Phase.Raise:
                case Phase.Ready:
                    phaseTime += Time.deltaTime;
                    if (phase == Phase.Raise && phaseTime >= RaiseTime)
                    {
                        phase = Phase.Ready;
                    }
                    PoseHand();
                    if (!Keyboard.current.gKey.isPressed && !ForceHold && phaseTime >= 0.08f)
                    {
                        underhand = Mouse.current != null && Mouse.current.rightButton.isPressed;
                        phase = Phase.Throw;
                        phaseTime = 0f;
                        released = false;
                    }
                    break;

                case Phase.Throw:
                    StepThrow();
                    break;
            }
        }

        private void LateUpdate()
        {
            // modelul din mana urmeaza palma dupa ce mainile s-au asezat
            if (heldModel != null && animator != null && animator.Arms != null)
            {
                Transform cam = weapon != null && weapon.ViewCamera != null ? weapon.ViewCamera.transform : null;
                if (cam != null)
                {
                    Pose(out Vector3 position, out Quaternion rotation);
                    heldModel.transform.SetPositionAndRotation(position + rotation * HeldOffset, rotation * Quaternion.Euler(-15f, 0f, 0f));
                }
            }
        }

        // ------------------------------------------------------------------
        // Aruncarea
        // ------------------------------------------------------------------

        /// <summary>Unde sta grenada fata de palma (in cadrul mainii: x dreapta, y sus, z inainte).</summary>
        private static readonly Vector3 HeldOffset = new Vector3(-0.045f, 0.005f, 0.01f);

        private void Begin(GrenadeType type)
        {
            inHand = type;
            phase = Phase.Raise;
            phaseTime = 0f;
            if (weapon != null) weapon.Holstered = true;
            if (animator != null)
            {
                animator.Lowered = true;
                animator.SetWeaponHidden(true);
            }

            heldModel = GrenadeFx.CreateModel(type);
            heldModel.name = "HeldGrenade";
            GrenadeFx.SetLayer(heldModel, LayerMask.NameToLayer(FirstPersonAnimator.LayerName), false);
            PoseHand();
        }

        private void StepThrow()
        {
            phaseTime += Time.deltaTime;
            PoseHand();

            if (!released && phaseTime >= ReleaseAt * ThrowTime)
            {
                released = true;
                Release();
            }

            if (phaseTime >= ThrowTime)
            {
                Finish();
            }
        }

        private void Release()
        {
            if (heldModel != null)
            {
                Destroy(heldModel);
                heldModel = null;
            }

            Camera cam = weapon != null ? weapon.ViewCamera : null;
            if (cam == null)
            {
                return;
            }

            Transform eye = cam.transform;
            Vector3 dir = Quaternion.AngleAxis(-6f, eye.right) * eye.forward;
            Vector3 velocity = dir * (underhand ? UnderhandSpeed : OverhandSpeed);
            if (fps != null)
            {
                velocity += fps.Velocity * 0.9f;
            }

            // pornind din fata ochilor, dar niciodata prin perete
            Vector3 start = eye.position + eye.forward * 0.35f + eye.right * 0.12f - eye.up * (underhand ? 0.25f : 0.05f);
            Vector3 offset = start - eye.position;
            if (Physics.Raycast(eye.position, offset.normalized, out RaycastHit hit, offset.magnitude + 0.06f, GrenadeEffects.WorldMask, QueryTriggerInteraction.Ignore))
            {
                start = hit.point - offset.normalized * 0.08f;
            }

            GrenadeProjectile.Spawn(inHand, start, velocity, member);

            counts[(int)inHand] = Mathf.Max(0, counts[(int)inHand] - 1);
            if (counts[(int)inHand] == 0)
            {
                selected = -1;
                SelectNext();
            }
            Changed?.Invoke();
        }

        private void Finish()
        {
            phase = Phase.Idle;
            if (animator != null)
            {
                if (animator.Arms != null) animator.Arms.SetRightOverride(Vector3.zero, Quaternion.identity, 0f);
                animator.Lowered = false;
                animator.SetWeaponHidden(false);
                animator.PlayDraw();
            }
            if (weapon != null) weapon.Holstered = false;
        }

        private void Cancel()
        {
            if (heldModel != null)
            {
                Destroy(heldModel);
                heldModel = null;
            }
            if (phase != Phase.Idle)
            {
                Finish();
            }
        }

        private void SelectNext()
        {
            int start = selected < 0 ? -1 : selected;
            for (int i = 1; i <= counts.Length; i++)
            {
                int index = (start + i + counts.Length) % counts.Length;
                if (counts[index] > 0)
                {
                    selected = index;
                    Changed?.Invoke();
                    return;
                }
            }
            selected = -1;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------
        // Mana dreapta
        // ------------------------------------------------------------------

        private struct Key
        {
            public Vector3 Position;   // fata de ochi (x dreapta, y sus, z inainte)
            public Vector3 Euler;      // cadrul "armei" din mana
            public Key(Vector3 p, Vector3 e) { Position = p; Euler = e; }
        }

        private static readonly Key Low = new Key(new Vector3(0.22f, -0.5f, 0.3f), new Vector3(25f, -10f, 0f));
        private static readonly Key ReadyKey = new Key(new Vector3(0.15f, -0.1f, 0.38f), new Vector3(-10f, -12f, 0f));
        private static readonly Key Back = new Key(new Vector3(0.24f, -0.03f, 0.2f), new Vector3(-55f, -15f, 0f));
        private static readonly Key Forward = new Key(new Vector3(0.04f, -0.3f, 0.6f), new Vector3(40f, 0f, 0f));
        private static readonly Key UnderBack = new Key(new Vector3(0.2f, -0.45f, 0.28f), new Vector3(35f, -10f, 0f));
        private static readonly Key UnderForward = new Key(new Vector3(0.1f, -0.22f, 0.55f), new Vector3(-25f, -5f, 0f));

        private void PoseHand()
        {
            if (animator == null || animator.Arms == null)
            {
                return;
            }

            Pose(out Vector3 position, out Quaternion rotation);
            animator.Arms.SetRightOverride(position, rotation, 1f);
        }

        private void Pose(out Vector3 position, out Quaternion rotation)
        {
            Key a, b;
            float t;
            switch (phase)
            {
                case Phase.Raise:
                    a = Low; b = ReadyKey; t = phaseTime / RaiseTime;
                    break;
                case Phase.Throw:
                    float u = phaseTime / ThrowTime;
                    if (u < ReleaseAt)
                    {
                        a = ReadyKey; b = underhand ? UnderBack : Back; t = u / ReleaseAt;
                    }
                    else
                    {
                        a = underhand ? UnderBack : Back; b = underhand ? UnderForward : Forward; t = (u - ReleaseAt) / (1f - ReleaseAt);
                    }
                    break;
                default:
                    a = ReadyKey; b = ReadyKey; t = 1f;
                    break;
            }

            t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Transform cam = weapon != null && weapon.ViewCamera != null ? weapon.ViewCamera.transform : transform;
            position = cam.TransformPoint(Vector3.Lerp(a.Position, b.Position, t));
            rotation = cam.rotation * Quaternion.Slerp(Quaternion.Euler(a.Euler), Quaternion.Euler(b.Euler), t);
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            if (member != null && !member.IsLocalPlayer)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Grenades HUD");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject go = new GameObject("Grenades", typeof(RectTransform));
            go.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 150f);
            rect.sizeDelta = new Vector2(520f, 80f);
            hud = go.AddComponent<TextMeshProUGUI>();
            hud.fontSize = 24f;
            hud.alignment = TextAlignmentOptions.BottomRight;
            hud.fontStyle = FontStyles.Bold;
            hud.richText = true;
            hud.raycastTarget = false;
        }

        private void RefreshHud()
        {
            if (hud == null)
            {
                return;
            }

            if (Total == 0)
            {
                hud.text = string.Empty;
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                string name = GrenadeSpec.All[i].Name;
                string count = counts[i] > 1 ? $" x{counts[i]}" : string.Empty;
                sb.Append(i == selected ? $"<color=#FFD75A>[{name}{count}]</color>  " : $"<color=#BBBBBB>{name}{count}</color>  ");
            }
            sb.Append("\n<size=70%><color=#999999>[G] arunca  ·  [G] + click dreapta: pe jos  ·  [4] schimba</color></size>");
            hud.text = sb.ToString();
        }
    }
}
