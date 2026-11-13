using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Bomba: cine o cara, plantare (tine apasat F pe un BombSite),
    /// dezamorsare (tine apasat F langa bomba), scapare la moarte si explozie.
    /// </summary>
    public class BombController : MonoBehaviour
    {
        [Header("Plant / Defuse")]
        [SerializeField] private float plantDuration = 3.5f;
        [SerializeField] private float defuseDuration = 7f;
        [SerializeField] private float defuseRange = 2.5f;
        [SerializeField] private float pickupRange = 1.5f;

        [Header("Explozie")]
        [SerializeField] private float explosionRadius = 18f;
        [SerializeField] private float explosionDamage = 500f;

        private RoundManager roundManager;
        private BombSite[] sites;

        private GameObject bombVisual;
        private Light bombLight;
        private float blinkTimer;

        private bool isDropped;
        private FpsController lockedPlayer;

        public TeamMember Carrier { get; private set; }
        public bool IsPlanted { get; private set; }
        public bool IsDropped => isDropped;
        public bool IsPlanting { get; private set; }
        public bool IsDefusing { get; private set; }
        public BombSite PlantedSite { get; private set; }

        /// <summary>0..1 - progresul actiunii curente (plant sau defuse).</summary>
        public float ActionProgress { get; private set; }

        /// <summary>Mesaj pentru HUD ("Hold [F] to plant" etc.), gol daca nu e nimic de facut.</summary>
        public string Hint { get; private set; } = string.Empty;

        public Vector3 BombPosition =>
            bombVisual != null ? bombVisual.transform.position : Vector3.zero;

        private void Awake()
        {
            roundManager = GetComponent<RoundManager>();
            sites = FindObjectsByType<BombSite>();
            CreateVisual();
        }

        private void Start()
        {
            if (roundManager == null)
            {
                roundManager = RoundManager.Instance;
            }
        }

        public void ResetForRound(TeamMember carrier)
        {
            CancelAction();

            Carrier = carrier;
            IsPlanted = false;
            isDropped = false;
            PlantedSite = null;
            Hint = string.Empty;

            bombVisual.SetActive(false);
        }

        private void Update()
        {
            Hint = string.Empty;

            if (roundManager == null)
            {
                return;
            }

            RoundPhase phase = roundManager.Phase;

            if (phase != RoundPhase.Live && phase != RoundPhase.BombPlanted)
            {
                CancelAction();
                UpdateBlink(phase);
                return;
            }

            UpdateCarrier();

            TeamMember player = roundManager.LocalPlayer;
            bool holdingUse =
                Keyboard.current != null &&
                Keyboard.current.fKey.isPressed;

            bool actionThisFrame = false;

            if (player != null && player.IsAlive)
            {
                if (phase == RoundPhase.Live)
                {
                    actionThisFrame = UpdatePlanting(player, holdingUse);
                }
                else
                {
                    actionThisFrame = UpdateDefusing(player, holdingUse);
                }
            }

            if (!actionThisFrame)
            {
                CancelAction();
            }

            UpdateBlink(phase);
        }

        // ------------------------------------------------------------------
        // Carat / scapat / ridicat
        // ------------------------------------------------------------------
        private void UpdateCarrier()
        {
            if (IsPlanted)
            {
                return;
            }

            if (Carrier != null && !Carrier.IsAlive)
            {
                bombVisual.transform.position = Carrier.transform.position;
                bombVisual.SetActive(true);
                bombLight.enabled = false;

                Carrier = null;
                isDropped = true;
            }

            if (!isDropped)
            {
                return;
            }

            foreach (TeamMember member in TeamMember.All)
            {
                if (member.Team != Team.Attackers || !member.IsAlive)
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    member.transform.position,
                    bombVisual.transform.position
                );

                if (distance <= pickupRange)
                {
                    Carrier = member;
                    isDropped = false;
                    bombVisual.SetActive(false);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------
        // Plant
        // ------------------------------------------------------------------
        private bool UpdatePlanting(TeamMember player, bool holdingUse)
        {
            if (Carrier != player)
            {
                return false;
            }

            BombSite site = FindSiteContaining(player.FpsController);

            if (site == null)
            {
                Hint = "Ai bomba - du-o pe un bombsite";
                return false;
            }

            if (!player.FpsController.IsGrounded)
            {
                return false;
            }

            if (!holdingUse)
            {
                Hint = $"Tine apasat [F] pentru a planta pe {site.SiteId}";
                return false;
            }

            IsPlanting = true;
            LockPlayer(player.FpsController);

            ActionProgress += Time.deltaTime / plantDuration;
            Hint = $"Plantezi bomba pe {site.SiteId}...";

            if (ActionProgress >= 1f)
            {
                Plant(site, player.transform.position);
            }

            return true;
        }

        private void Plant(BombSite site, Vector3 position)
        {
            CancelAction();

            IsPlanted = true;
            PlantedSite = site;
            Carrier = null;
            isDropped = false;

            bombVisual.transform.position = position;
            bombVisual.SetActive(true);
            bombLight.enabled = true;
            blinkTimer = 0f;

            roundManager.NotifyBombPlanted();
        }

        // ------------------------------------------------------------------
        // Defuse
        // ------------------------------------------------------------------
        private bool UpdateDefusing(TeamMember player, bool holdingUse)
        {
            if (!IsPlanted || player.Team != Team.Defenders)
            {
                return false;
            }

            float distance = Vector3.Distance(
                player.transform.position,
                bombVisual.transform.position
            );

            if (distance > defuseRange)
            {
                return false;
            }

            if (!holdingUse)
            {
                Hint = "Tine apasat [F] pentru a dezamorsa";
                return false;
            }

            IsDefusing = true;
            LockPlayer(player.FpsController);

            ActionProgress += Time.deltaTime / defuseDuration;
            Hint = "Dezamorsezi bomba...";

            if (ActionProgress >= 1f)
            {
                CancelAction();

                IsPlanted = false;
                bombLight.enabled = false;

                roundManager.NotifyBombDefused();
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Explozie
        // ------------------------------------------------------------------
        public void Explode()
        {
            if (!IsPlanted)
            {
                return;
            }

            IsPlanted = false;
            Vector3 center = bombVisual.transform.position;
            bombVisual.SetActive(false);

            // Copie: TakeDamage poate declansa evenimente care citesc lista.
            TeamMember[] members = new TeamMember[TeamMember.All.Count];
            for (int i = 0; i < members.Length; i++)
            {
                members[i] = TeamMember.All[i];
            }

            foreach (TeamMember member in members)
            {
                if (!member.IsAlive)
                {
                    continue;
                }

                float distance = Vector3.Distance(member.transform.position, center);

                if (distance < explosionRadius)
                {
                    float falloff = 1f - distance / explosionRadius;
                    member.Health.TakeDamage(explosionDamage * falloff);
                }
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private BombSite FindSiteContaining(FpsController player)
        {
            foreach (BombSite site in sites)
            {
                if (site != null && site.IsPlayerInside(player))
                {
                    return site;
                }
            }

            return null;
        }

        private void LockPlayer(FpsController player)
        {
            if (lockedPlayer == player)
            {
                return;
            }

            lockedPlayer = player;
            player.SetInputLock(this, true);
        }

        private void CancelAction()
        {
            IsPlanting = false;
            IsDefusing = false;
            ActionProgress = 0f;

            if (lockedPlayer != null)
            {
                lockedPlayer.SetInputLock(this, false);
                lockedPlayer = null;
            }
        }

        private void UpdateBlink(RoundPhase phase)
        {
            if (!IsPlanted || phase != RoundPhase.BombPlanted)
            {
                return;
            }

            // Clipeste tot mai repede pe masura ce se apropie explozia.
            float remaining = roundManager.PhaseTimeRemaining;
            float interval = Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(remaining / 40f));

            blinkTimer += Time.deltaTime;

            if (blinkTimer >= interval)
            {
                blinkTimer = 0f;
                bombLight.enabled = !bombLight.enabled;
            }
        }

        private void CreateVisual()
        {
            // Placeholder greybox: o cutie rosie cu lumina. Inlocuieste cu modelul final.
            bombVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bombVisual.name = "Bomb (runtime)";
            bombVisual.transform.SetParent(transform, true);
            bombVisual.transform.localScale = new Vector3(0.45f, 0.25f, 0.3f);

            // Fara collider: bomba nu trebuie sa blocheze miscarea sau gloantele.
            Destroy(bombVisual.GetComponent<Collider>());

            Renderer bombRenderer = bombVisual.GetComponent<Renderer>();
            bombRenderer.material.color = new Color(0.8f, 0.1f, 0.1f);

            GameObject lightObject = new GameObject("BombLight");
            lightObject.transform.SetParent(bombVisual.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2f, 0f);

            bombLight = lightObject.AddComponent<Light>();
            bombLight.type = LightType.Point;
            bombLight.color = Color.red;
            bombLight.range = 6f;
            bombLight.intensity = 4f;
            bombLight.enabled = false;

            bombVisual.SetActive(false);
        }
    }
}
