using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Pregatirea aparatorilor: tinand apasat F, jucatorul local (daca e aparator)
    /// intareste pereti moi / trape si monteaza baricade pe deschideri.
    /// Numarul de intariri si baricade este limitat pe runda.
    /// </summary>
    public class DefenderPrepController : MonoBehaviour
    {
        [Header("Resurse pe runda")]
        [SerializeField] private int reinforcementsPerRound = 6;
        [SerializeField] private int barricadesPerRound = 4;

        [Header("Actiuni")]
        [SerializeField] private float reinforceDuration = 2.5f;
        [SerializeField] private float barricadeDuration = 1.5f;
        [SerializeField] private float reach = 3f;

        private RoundManager roundManager;
        private BombController bomb;

        private Camera playerCamera;
        private TeamMember cameraOwner;

        private Component currentTarget;
        private FpsController lockedPlayer;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        public int ReinforcementsLeft { get; private set; }
        public int BarricadesLeft { get; private set; }
        public bool IsActing { get; private set; }

        /// <summary>0..1 - progresul actiunii curente.</summary>
        public float ActionProgress { get; private set; }

        /// <summary>Mesaj pentru HUD, gol daca nu e nimic de facut.</summary>
        public string Hint { get; private set; } = string.Empty;

        private void Awake()
        {
            roundManager = GetComponent<RoundManager>();
            bomb = GetComponent<BombController>();

            ReinforcementsLeft = reinforcementsPerRound;
            BarricadesLeft = barricadesPerRound;
        }

        public void ResetForRound()
        {
            CancelAction();

            ReinforcementsLeft = reinforcementsPerRound;
            BarricadesLeft = barricadesPerRound;
            Hint = string.Empty;
        }

        private void Update()
        {
            Hint = string.Empty;

            if (!CanPrepare(out TeamMember player))
            {
                CancelAction();
                return;
            }

            Component target = FindTarget(player);

            if (target == null)
            {
                CancelAction();
                return;
            }

            bool isWall = target is Destructible;
            int left = isWall ? ReinforcementsLeft : BarricadesLeft;

            if (left <= 0)
            {
                Hint = isWall ? "Nu mai ai intariri" : "Nu mai ai baricade";
                CancelAction();
                return;
            }

            bool holdingUse =
                Keyboard.current != null &&
                Keyboard.current.fKey.isPressed;

            if (!holdingUse)
            {
                Hint = isWall
                    ? $"Tine apasat [F] pentru a intari ({left} ramase)"
                    : $"Tine apasat [F] pentru a baricada ({left} ramase)";

                CancelAction();
                return;
            }

            // Progresul se pierde daca muti privirea pe alt obiect.
            if (target != currentTarget)
            {
                ActionProgress = 0f;
                currentTarget = target;
            }

            IsActing = true;
            LockPlayer(player.FpsController);

            float duration = isWall ? reinforceDuration : barricadeDuration;
            ActionProgress += Time.deltaTime / duration;
            Hint = isWall ? "Intaresti..." : "Baricadezi...";

            if (ActionProgress < 1f)
            {
                return;
            }

            if (target is Destructible wall)
            {
                wall.Reinforce();
                ReinforcementsLeft--;
            }
            else if (target is BarricadePoint point)
            {
                point.Build();
                BarricadesLeft--;
            }

            CancelAction();
        }

        private bool CanPrepare(out TeamMember player)
        {
            player = roundManager != null ? roundManager.LocalPlayer : null;

            if (player == null || !player.IsAlive || player.Team != Team.Defenders)
            {
                return false;
            }

            // Dezamorsarea foloseste aceeasi tasta si are prioritate.
            if (bomb != null && bomb.IsDefusing)
            {
                return false;
            }

            RoundPhase phase = roundManager.Phase;

            return
                phase == RoundPhase.PreRound ||
                phase == RoundPhase.Live ||
                phase == RoundPhase.BombPlanted;
        }

        /// <summary>
        /// Primul obiect din fata jucatorului care poate fi intarit sau baricadat.
        /// Un perete plin (sau unul deja intarit) aflat mai aproape blocheaza cautarea.
        /// </summary>
        private Component FindTarget(TeamMember player)
        {
            if (cameraOwner != player || playerCamera == null)
            {
                cameraOwner = player;
                playerCamera = player.GetComponentInChildren<Camera>();
            }

            if (playerCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            int count = Physics.RaycastNonAlloc(
                ray, hits, reach, ~0, QueryTriggerInteraction.Collide);

            Array.Sort(hits, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = hits[i].collider;

                // Capsula proprie a jucatorului.
                if (hitCollider.transform.IsChildOf(player.transform))
                {
                    continue;
                }

                Destructible wall = hitCollider.GetComponentInParent<Destructible>();

                if (wall != null)
                {
                    return wall.CanBeReinforced ? wall : null;
                }

                BarricadePoint point = hitCollider.GetComponent<BarricadePoint>();

                if (point != null)
                {
                    if (!point.IsBuilt)
                    {
                        return point;
                    }

                    continue;
                }

                // Alt trigger (ex. zona de bombsite) nu blocheaza; un obiect solid, da.
                if (!hitCollider.isTrigger)
                {
                    return null;
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
            IsActing = false;
            ActionProgress = 0f;
            currentTarget = null;

            if (lockedPlayer != null)
            {
                lockedPlayer.SetInputLock(this, false);
                lockedPlayer = null;
            }
        }

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            public int Compare(RaycastHit a, RaycastHit b)
            {
                return a.distance.CompareTo(b.distance);
            }
        }
    }
}
