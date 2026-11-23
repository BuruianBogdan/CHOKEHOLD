using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Miscarea jucatorului. Saritura si strafe-ul folosesc fizica pe viteza a shooterelor competitive clasice
    /// (frecare la sol, accelerare la sol si in aer, viteza limitata in aer), deci merg bunny hop, air strafe,
    /// counter-strafe si duck-jump. Restul e tactic: sprint doar inainte si fara foc, lean pe Q / E, crouch
    /// tinut (Ctrl) sau comutat (C).
    /// Privirea: grade pe "count" de mouse = sensibilitate x 0,022 (scara clasica), fara netezire.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsController : MonoBehaviour
    {
        [Header("Viteze (m/s)")]
        [Tooltip("Viteza de alergare cu cutitul; armele o inmultesc cu viteza lor (WeaponData.moveSpeed).")]
        [SerializeField] private float runSpeed = 5.2f;
        [SerializeField] private float sprintMultiplier = 1.3f;
        [SerializeField] private float crouchMultiplier = 0.45f;
        [SerializeField] private float ladderSideSpeed = 2f;

        [Header("Fizica de miscare")]
        [Tooltip("Accelerarea la sol: viteza maxima se atinge in ~1/accelerate secunde.")]
        [SerializeField] private float groundAccelerate = 5f;
        [Tooltip("Accelerarea in aer (air strafe). Viteza castigata pe directia dorita e limitata de airWishSpeedFactor.")]
        [SerializeField] private float airAccelerate = 10f;
        [SerializeField] private float friction = 4f;
        [Tooltip("Sub aceasta fractiune din viteza de alergare frecarea lucreaza ca la viteza aceasta (oprire rapida).")]
        [SerializeField] private float stopSpeedFactor = 0.3f;
        [Tooltip("Cat poate creste viteza pe directia dorita in aer (fractiune din viteza de alergare).")]
        [SerializeField] private float airWishSpeedFactor = 0.12f;
        [SerializeField] private float gravityAcceleration = 20.32f;
        [SerializeField] private float jumpApexHeight = 1.143f;
        [Tooltip("Peste aceasta fractiune din viteza maxima, saritura taie viteza (bunny hop limitat).")]
        [SerializeField] private float bunnyHopCapFactor = 1.7f;
        [SerializeField] private float bunnyHopCrop = 0.65f;
        [Tooltip("Oboseala dupa saritura (s): incetineste pe sol si scade inaltimea urmatoarei sarituri.")]
        [SerializeField] private float jumpStamina = 1.316f;
        [Tooltip("Fractiunea de viteza pierduta pe tick de 1/100 s, la oboseala maxima.")]
        [SerializeField] private float staminaSlowdown = 0.19f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float minimumPitch = -89f;
        [SerializeField] private float maximumPitch = 89f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private LayerMask groundMask;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchingHeight = 1.2f;
        [SerializeField] private float standingCameraHeight = 1.65f;
        [SerializeField] private float crouchingCameraHeight = 1.05f;
        [SerializeField] private float crouchTransitionSpeed = 10f;
        [SerializeField] private LayerMask ceilingMask;

        [Header("Lean (Q / E)")]
        [SerializeField] private float leanRoll = 15f;
        [SerializeField] private float leanShift = 0.32f;
        [SerializeField] private float leanSpeed = 10f;
        [SerializeField] private float leanCollisionRadius = 0.15f;
        [SerializeField] private LayerMask leanCollisionMask;

        private const float YawPerCount = 0.022f;      // grade pe count la sensibilitate 1

        private CharacterController controller;

        private Vector3 velocity;
        private bool isGrounded;
        private bool isCrouching;
        private bool crouchToggled;
        private float stamina;
        private float pitch;
        private Vector2 moveInput;
        private Vector2 punch;                 // recul (grade): x in sus, y spre dreapta

        private float currentLeanAngle;
        private float currentLeanOffset;
        private float ladderCooldown;

        // intrare simulata (teste automate, mai tarziu boti): inlocuieste tastatura cat e pornita
        private bool simulated;
        private Vector2 simulatedMove;
        private bool simulatedJump;
        private bool simulatedSprint;
        private bool simulatedCrouch;

        // Sisteme diferite (freeze time, plant/defuse, moarte) pot bloca miscarea
        // independent; miscarea revine doar cand niciun sistem nu o mai blocheaza.
        private readonly System.Collections.Generic.HashSet<object> inputLocks =
            new System.Collections.Generic.HashSet<object>();

        /// <summary>Viteza de deplasare cu arma curenta (lunetistele sunt mai grele).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Sensibilitatea mouse-ului cu luneta (zoom mare = rotire mai fina).</summary>
        public float SensitivityScale { get; set; } = 1f;

        /// <summary>Arma cere oprirea sprintului (ochire, foc).</summary>
        public bool SprintSuppressed { get; set; }

        /// <summary>Reculul curent peste privire (grade): x in sus, y spre dreapta. Scade singur in timp.</summary>
        public Vector2 Punch
        {
            get => punch;
            set => punch = value;
        }

        public Vector3 Velocity => velocity;
        public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;
        /// <summary>Viteza maxima la sol cu arma curenta, fara sprint si crouch.</summary>
        public float MaxGroundSpeed => runSpeed * SpeedMultiplier;

        public bool IsCrouching => isCrouching;
        public bool IsGrounded => isGrounded;
        public bool InputLocked => inputLocks.Count > 0;
        public bool IsClimbing { get; private set; }
        public float Pitch => pitch;
        public float Lean => leanRoll > 0f ? currentLeanAngle / leanRoll : 0f;

        public bool IsSprinting =>
            !InputLocked &&
            !SprintSuppressed &&
            (simulated ? simulatedSprint : Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) &&
            !isCrouching &&
            isGrounded &&
            moveInput.y > 0.1f;     // doar inainte (Shift pe loc, lateral sau in spate nu e alergare)

        /// <summary>Intrare din cod in locul tastaturii: miscare (x lateral, y inainte), saritura tinuta, sprint, crouch.</summary>
        public void SetSimulatedInput(Vector2 move, bool jump, bool sprint, bool crouch)
        {
            simulated = true;
            simulatedMove = move;
            simulatedJump = jump;
            simulatedSprint = sprint;
            simulatedCrouch = crouch;
        }

        public void ClearSimulatedInput()
        {
            simulated = false;
        }

        public void SetInputLock(object owner, bool locked)
        {
            if (owner == null)
            {
                return;
            }

            if (locked)
            {
                inputLocks.Add(owner);
            }
            else
            {
                inputLocks.Remove(owner);
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraPivot == null || groundCheck == null)
            {
                Debug.LogError("CameraPivot sau GroundCheck nu este asignat în FpsController.", this);
                enabled = false;
                return;
            }

            controller.height = standingHeight;
            controller.center = new Vector3(controller.center.x, standingHeight / 2f, controller.center.z);

            Vector3 cameraPosition = cameraPivot.localPosition;
            cameraPosition.y = standingCameraHeight;
            cameraPivot.localPosition = cameraPosition;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            velocity = Vector3.zero;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            CheckGround();
            UpdateCrouch(dt);
            MovePlayer(dt);
            DecayPunch(dt);
            UpdateLean(dt);
            LookAround();
        }

        // ------------------------------------------------------------------
        // Miscare
        // ------------------------------------------------------------------

        private Vector2 ReadMoveInput()
        {
            if (simulated)
            {
                return InputLocked ? Vector2.zero : simulatedMove;
            }

            Keyboard keyboard = Keyboard.current;

            if (InputLocked || keyboard == null)
            {
                return Vector2.zero;
            }

            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;

            return new Vector2(horizontal, vertical);
        }

        private bool JumpPressed()
        {
            if (InputLocked || PlayerInputGate.Blocked)
            {
                return false;
            }

            if (simulated)
            {
                return simulatedJump;
            }

            // Saritura doar la apasare (tinuta nu sare iar); rotita mouse-ului sare si ea (bunny hop), daca e pornita
            bool key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool wheel = GameSettings.ScrollWheelJump && Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f;
            return key || wheel;
        }

        private void MovePlayer(float dt)
        {
            // tastele merg direct, fara netezire: oprirea vine din frecare (counter-strafe)
            moveInput = ReadMoveInput();
            stamina = Mathf.Max(0f, stamina - dt);

            IsClimbing = ClimbLadder(dt);
            if (IsClimbing)
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 wishDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            wishDirection.y = 0f;
            float wishSpeed = 0f;
            if (wishDirection.sqrMagnitude > 0.0001f)
            {
                wishDirection.Normalize();     // pe diagonala nu se merge mai repede
                wishSpeed = CurrentMaxSpeed();
            }

            if (isGrounded && JumpPressed())
            {
                Jump();
            }

            if (isGrounded)
            {
                if (velocity.y < 0f)
                {
                    velocity.y = -2f;           // lipit de sol pe pante si trepte
                }

                ApplyFriction(dt);

                // dupa aterizare oboseala incetineste (doar o saritura imediata pastreaza viteza)
                if (stamina > 0f)
                {
                    float ratio = Mathf.Clamp01(1f - stamina * staminaSlowdown);
                    float scale = Mathf.Pow(ratio, dt * 100f);
                    velocity.x *= scale;
                    velocity.z *= scale;
                }

                Accelerate(wishDirection, wishSpeed, groundAccelerate, dt);
            }
            else
            {
                AirAccelerate(wishDirection, wishSpeed, dt);
                velocity.y -= gravityAcceleration * dt;
            }

            CollisionFlags flags = controller.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            {
                velocity.y = 0f;
            }
        }

        private float CurrentMaxSpeed()
        {
            float speed = runSpeed * SpeedMultiplier;
            if (isCrouching)
            {
                speed *= crouchMultiplier;
            }
            else if (IsSprinting)
            {
                speed *= sprintMultiplier;
            }
            return speed;
        }

        private void Jump()
        {
            // bunny hop: peste un prag de viteza, saritura taie din viteza orizontala
            float maxScaled = bunnyHopCapFactor * runSpeed * SpeedMultiplier;
            float speed = HorizontalSpeed;
            if (maxScaled > 0f && speed > maxScaled)
            {
                float fraction = maxScaled / speed * bunnyHopCrop;
                velocity.x *= fraction;
                velocity.z *= fraction;
            }

            float jumpVelocity = Mathf.Sqrt(2f * gravityAcceleration * jumpApexHeight);
            if (stamina > 0f)
            {
                jumpVelocity *= Mathf.Clamp01(1f - stamina * staminaSlowdown);
            }

            velocity.y = jumpVelocity;
            stamina = jumpStamina;
            isGrounded = false;
        }

        private void ApplyFriction(float dt)
        {
            float speed = HorizontalSpeed;
            if (speed < 0.01f)
            {
                velocity.x = 0f;
                velocity.z = 0f;
                return;
            }

            float stopSpeed = stopSpeedFactor * runSpeed;
            float control = speed < stopSpeed ? stopSpeed : speed;
            float newSpeed = Mathf.Max(0f, speed - control * friction * dt);
            float scale = newSpeed / speed;
            velocity.x *= scale;
            velocity.z *= scale;
        }

        private void Accelerate(Vector3 wishDirection, float wishSpeed, float accelerate, float dt)
        {
            if (wishSpeed <= 0f)
            {
                return;
            }

            float current = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDirection);
            float add = wishSpeed - current;
            if (add <= 0f)
            {
                return;
            }

            float accel = Mathf.Min(accelerate * wishSpeed * dt, add);
            velocity.x += accel * wishDirection.x;
            velocity.z += accel * wishDirection.z;
        }

        /// <summary>
        /// In aer viteza pe directia dorita creste doar pana la o limita mica, dar accelerarea foloseste toata viteza
        /// dorita: rotind mouse-ul odata cu strafe-ul (A / D), directia se schimba si viteza creste (air strafe).
        /// </summary>
        private void AirAccelerate(Vector3 wishDirection, float wishSpeed, float dt)
        {
            if (wishSpeed <= 0f)
            {
                return;
            }

            float capped = Mathf.Min(wishSpeed, airWishSpeedFactor * runSpeed);
            float current = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDirection);
            float add = capped - current;
            if (add <= 0f)
            {
                return;
            }

            float accel = Mathf.Min(airAccelerate * wishSpeed * dt, add);
            velocity.x += accel * wishDirection.x;
            velocity.z += accel * wishDirection.z;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 normal = hit.normal;

            // pereti (aproape verticali): viteza orizontala aluneca de-a lungul lor. Muchiile podelei si treptele
            // dau normale inclinate si nu trebuie sa opreasca miscarea sau saritura.
            if (Mathf.Abs(normal.y) < 0.3f)
            {
                Vector3 wall = new Vector3(normal.x, 0f, normal.z).normalized;
                float into = velocity.x * wall.x + velocity.z * wall.z;
                if (into < 0f)
                {
                    velocity.x -= wall.x * into;
                    velocity.z -= wall.z * into;
                }
            }
            else if (normal.y < -0.7f && velocity.y > 0f)
            {
                velocity.y = 0f;        // tavan
            }
        }

        private void CheckGround()
        {
            // urcand dupa saritura nu esti pe sol, chiar daca sfera atinge marginea
            if (velocity.y > 0.5f)
            {
                isGrounded = false;
                return;
            }

            bool sphereGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
            isGrounded = sphereGrounded || controller.isGrounded;
        }

        // ------------------------------------------------------------------
        // Scari de mana (acoperisuri)
        // ------------------------------------------------------------------

        /// <summary>
        /// Pe scara: W urca, S coboara, A/D te muta usor lateral; Space sare de pe scara.
        /// La baza, S te duce inapoi pe podea; sus, W te impinge pe acoperis.
        /// </summary>
        private bool ClimbLadder(float dt)
        {
            if (ladderCooldown > 0f)
            {
                ladderCooldown -= dt;
                return false;
            }

            Ladder ladder = Ladder.Find(transform.position + Vector3.up * 0.9f);

            if (ladder == null || (isGrounded && moveInput.y <= 0.05f))
            {
                return false;
            }

            Vector3 outward = ladder.Outward;
            Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;

            // Nu te desprinzi de scara mergand inapoi: componenta spre exterior se anuleaza.
            float away = Vector3.Dot(direction, outward);
            if (away > 0f)
            {
                direction -= outward * away;
            }

            direction = Vector3.ClampMagnitude(direction, 1f);

            // W / S urca si coboara indiferent unde privesti (ca in majoritatea shooterelor).
            float climb = moveInput.y * ladder.ClimbSpeed;

            controller.Move((direction * ladderSideSpeed + Vector3.up * climb) * dt);

            if (!InputLocked && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                // Sari de pe scara, cu spatele la perete.
                controller.Move(outward * 0.3f);
                velocity = outward * 2f + Vector3.up * Mathf.Sqrt(2f * gravityAcceleration * jumpApexHeight) * 0.6f;
                ladderCooldown = 0.4f;
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Crouch (Ctrl tinut sau C comutat); in aer ridica picioarele (duck-jump)
        // ------------------------------------------------------------------

        private void UpdateCrouch(float dt)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!InputLocked && keyboard.cKey.wasPressedThisFrame)
            {
                crouchToggled = !crouchToggled;
            }

            bool wantCrouch = !InputLocked && (simulated ? simulatedCrouch : keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed || crouchToggled);
            float delta = standingHeight - crouchingHeight;

            if (wantCrouch && !isCrouching)
            {
                isCrouching = true;
                if (!isGrounded)
                {
                    // in aer capsula se strange spre centru: picioarele urca (sari mai sus pe obstacole)
                    SetHeightInstant(crouchingHeight, delta * 0.5f);
                }
            }
            else if (!wantCrouch && isCrouching && !IsCeilingAbove())
            {
                if (isGrounded)
                {
                    isCrouching = false;
                }
                else if (!Physics.Raycast(transform.position + Vector3.up * 0.05f, Vector3.down, delta * 0.5f + 0.05f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    isCrouching = false;
                    SetHeightInstant(standingHeight, -delta * 0.5f);
                }
            }

            float targetHeight = isCrouching ? crouchingHeight : standingHeight;
            float targetCameraHeight = isCrouching ? crouchingCameraHeight : standingCameraHeight;
            float blend = 1f - Mathf.Exp(-crouchTransitionSpeed * dt);

            controller.height = Mathf.Lerp(controller.height, targetHeight, blend);
            Vector3 center = controller.center;
            center.y = controller.height / 2f;
            controller.center = center;

            Vector3 cameraPosition = cameraPivot.localPosition;
            cameraPosition.y = Mathf.Lerp(cameraPosition.y, targetCameraHeight, blend);
            cameraPivot.localPosition = cameraPosition;
        }

        private void SetHeightInstant(float height, float lift)
        {
            controller.enabled = false;
            transform.position += Vector3.up * lift;
            controller.height = height;
            controller.center = new Vector3(controller.center.x, height / 2f, controller.center.z);
            controller.enabled = true;

            // camera ramane la aceeasi inaltime in lume
            Vector3 cameraPosition = cameraPivot.localPosition;
            cameraPosition.y -= lift;
            cameraPivot.localPosition = cameraPosition;
        }

        private bool IsCeilingAbove()
        {
            Vector3 origin = transform.position + Vector3.up * crouchingHeight;
            float checkDistance = standingHeight - crouchingHeight;
            return Physics.Raycast(origin, Vector3.up, checkDistance, ceilingMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------
        // Lean (Q / E tinut)
        // ------------------------------------------------------------------

        private void UpdateLean(float dt)
        {
            if (Keyboard.current == null)
            {
                return;
            }

            float targetLeanAngle = 0f;
            float requestedLeanOffset = 0f;

            if (!InputLocked && Keyboard.current.qKey.isPressed)
            {
                targetLeanAngle = leanRoll;
                requestedLeanOffset = -leanShift;
            }
            else if (!InputLocked && Keyboard.current.eKey.isPressed)
            {
                targetLeanAngle = -leanRoll;
                requestedLeanOffset = leanShift;
            }

            float allowedLeanOffset = GetAllowedLeanOffset(requestedLeanOffset);
            if (Mathf.Abs(allowedLeanOffset) < Mathf.Abs(requestedLeanOffset))
            {
                targetLeanAngle *= leanShift > 0f ? Mathf.Abs(allowedLeanOffset) / leanShift : 0f;
            }

            float blend = 1f - Mathf.Exp(-leanSpeed * dt);
            currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLeanAngle, blend);
            currentLeanOffset = Mathf.Lerp(currentLeanOffset, allowedLeanOffset, blend);

            Vector3 cameraPosition = cameraPivot.localPosition;
            cameraPosition.x = currentLeanOffset;
            cameraPivot.localPosition = cameraPosition;
        }

        private float GetAllowedLeanOffset(float requestedOffset)
        {
            if (Mathf.Approximately(requestedOffset, 0f))
            {
                return 0f;
            }

            Transform frame = cameraPivot.parent != null ? cameraPivot.parent : transform;
            Vector3 direction = frame.right * Mathf.Sign(requestedOffset);
            Vector3 origin = frame.TransformPoint(new Vector3(0f, cameraPivot.localPosition.y, 0f));

            if (Physics.SphereCast(origin, leanCollisionRadius, direction, out RaycastHit hit, Mathf.Abs(requestedOffset),
                    leanCollisionMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0f, hit.distance - 0.02f) * Mathf.Sign(requestedOffset);
            }

            return requestedOffset;
        }

        // ------------------------------------------------------------------
        // Privire si recul
        // ------------------------------------------------------------------

        private void LookAround()
        {
            if (Mouse.current == null)
            {
                return;
            }

            // meniu deschis (cumparare, setari): cursorul e liber si camera sta pe loc
            Vector2 counts = PlayerInputGate.Blocked ? Vector2.zero : Mouse.current.delta.ReadValue();
            float degreesPerCount = GameSettings.Sensitivity * YawPerCount * SensitivityScale;

            transform.Rotate(Vector3.up, counts.x * degreesPerCount, Space.Self);
            pitch = Mathf.Clamp(pitch - counts.y * degreesPerCount, minimumPitch, maximumPitch);

            // Reculul e un unghi peste privire (nu se aduna in pitch): urca in rafala si revine dupa.
            cameraPivot.localRotation = Quaternion.Euler(
                Mathf.Max(pitch - punch.x, minimumPitch),
                punch.y,
                currentLeanAngle);
        }

        /// <summary>Reculul scade continuu: putin cand e mic, mai repede cand e mare.</summary>
        private void DecayPunch(float dt)
        {
            float length = punch.magnitude;
            if (length <= 0f)
            {
                return;
            }

            float newLength = Mathf.Max(0f, length - (10f + length * 0.5f) * dt);
            punch *= newLength / length;
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraPivot != null)
            {
                Gizmos.DrawWireSphere(cameraPivot.position, leanCollisionRadius);
            }
        }
    }
}
