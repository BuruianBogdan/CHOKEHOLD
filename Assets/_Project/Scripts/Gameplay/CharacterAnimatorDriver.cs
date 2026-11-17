using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Scrie in Animator ce se intampla cu personajul: viteza si directia (din CharacterController), crouch,
    /// sprint, sol, scara si lean (din FpsController), tinta (din unghiul camerei), tragere si reincarcare
    /// (din HitscanWeapon), plantare / dezamorsare (BombController), lovituri (Health).
    /// Sta pe prefab-ul personajului; cauta sistemele in parinte, deci merge si pe tinte fara jucator (idle).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float smoothing = 12f;
        [SerializeField] private int weaponKind;          // 0 = pusca, 1 = pistol

        private Animator animator;
        private FpsController fps;
        private CharacterController controller;
        private HitscanWeapon weapon;
        private Health health;
        private TeamMember member;
        private BombController bomb;
        private ThirdPersonWeaponIK ik;

        private Vector2 move;
        private Vector3 lastPosition;
        private bool wasGrounded = true;
        private int leanLayer = -1;

        public int WeaponKind
        {
            get => weaponKind;
            set => weaponKind = value;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ik = GetComponent<ThirdPersonWeaponIK>();
            fps = GetComponentInParent<FpsController>();
            controller = GetComponentInParent<CharacterController>();
            health = GetComponentInParent<Health>();
            member = GetComponentInParent<TeamMember>();
            weapon = fps != null ? fps.GetComponentInChildren<HitscanWeapon>(true) : null;
            lastPosition = transform.position;
            // indexul stratului Lean se citeste in Start: in Awake Animator-ul nu e inca legat la controller
        }

        private void OnEnable()
        {
            if (weapon != null)
            {
                weapon.Fired += OnFired;
                weapon.ReloadStarted += OnReloadStarted;
            }

            if (health != null)
            {
                health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.Fired -= OnFired;
                weapon.ReloadStarted -= OnReloadStarted;
            }

            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        private void Start()
        {
            bomb = FindAnyObjectByType<BombController>();
            leanLayer = animator.runtimeAnimatorController != null ? animator.GetLayerIndex("Lean") : -1;
        }

        private void Update()
        {
            if (animator.runtimeAnimatorController == null)
            {
                return;
            }

            Vector3 velocity;
            if (controller != null && controller.enabled)
            {
                velocity = controller.velocity;
            }
            else
            {
                velocity = Time.deltaTime > 0f ? (transform.position - lastPosition) / Time.deltaTime : Vector3.zero;
            }
            lastPosition = transform.position;

            Vector3 local = transform.InverseTransformDirection(velocity);
            Vector2 target = new Vector2(local.x, local.z) / Mathf.Max(walkSpeed, 0.01f);
            target = Vector2.ClampMagnitude(target, 1.5f);
            move = Vector2.Lerp(move, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            bool grounded = fps == null || fps.IsGrounded;
            bool crouch = fps != null && fps.IsCrouching;
            bool sprint = fps != null && fps.IsSprinting && target.magnitude > 0.5f;
            bool climb = fps != null && fps.IsClimbing;

            animator.SetFloat("MoveX", move.x);
            animator.SetFloat("MoveZ", move.y);
            animator.SetFloat("Speed", Mathf.Clamp(target.magnitude, 0.2f, 1.5f));
            animator.SetBool("Crouch", crouch);
            animator.SetBool("Sprint", sprint);
            animator.SetBool("Grounded", grounded || climb);
            animator.SetBool("Climb", climb);
            animator.SetFloat("WeaponKind", weaponKind);

            float pitch = fps != null ? -fps.Pitch : 0f;
            animator.SetFloat("AimPitch", Mathf.Clamp(pitch, -60f, 60f));

            // capul spre unde se uita (IK, rigging): pitch-ul camerei peste directia corpului
            if (ik != null)
            {
                ik.LookAlong(Quaternion.AngleAxis(-pitch, transform.right) * transform.forward);
            }
            animator.SetFloat("AimYaw", 0f);

            float lean = fps != null ? fps.Lean : 0f;
            animator.SetFloat("Lean", lean);
            if (leanLayer >= 0)
            {
                animator.SetLayerWeight(leanLayer, Mathf.Abs(lean));
            }

            if (wasGrounded && !grounded && !climb && velocity.y > 1f)
            {
                animator.SetTrigger("Jump");
            }
            wasGrounded = grounded;

            if (bomb != null && member != null)
            {
                animator.SetBool("Plant", bomb.IsPlanting && bomb.Carrier == member);
                animator.SetBool("Defuse", bomb.IsDefusing && member.IsLocalPlayer);
            }
        }

        private void OnFired()
        {
            animator.SetTrigger("Fire");
        }

        private void OnReloadStarted()
        {
            animator.SetTrigger(weapon != null && weapon.CurrentAmmo == 0 ? "ReloadEmpty" : "Reload");
        }

        private void OnDamaged(Health _, float damage)
        {
            if (damage > 0f && health != null && !health.IsDead)
            {
                animator.SetTrigger("HitF");
            }
        }
    }
}
