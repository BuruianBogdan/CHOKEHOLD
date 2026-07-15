using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class FpsController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float jumpHeight = 1.1f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 0.08f;
        [SerializeField] private float minimumPitch = -80f;
        [SerializeField] private float maximumPitch = 80f;

        [Header("Jump")]
        [SerializeField] private float jumpBufferTime = 0.15f;
        [SerializeField] private float coyoteTime = 0.12f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private LayerMask groundMask;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchingHeight = 1.2f;
        [SerializeField] private float standingCameraHeight = 1.65f;
        [SerializeField] private float crouchingCameraHeight = 1.05f;
        [SerializeField] private float crouchTransitionSpeed = 8f;
        [SerializeField] private float crouchWalkSpeed = 2.5f;
        [SerializeField] private LayerMask ceilingMask;

        [Header("Camera Recoil")]
        [SerializeField] private float recoilSnappiness = 18f;
        [SerializeField] private float recoilReturnSpeed = 10f;
        
        [Header("Lean")]
        [SerializeField] private float leanAngle = 10f;
        [SerializeField] private float leanDistance = 0.15f;
        [SerializeField] private float leanSpeed = 8f;
        [SerializeField] private float leanCollisionRadius = 0.15f;
        [SerializeField] private LayerMask leanCollisionMask;

        private float currentLeanAngle;
        private float currentLeanOffset;
        
        private CharacterController controller;

        private bool isGrounded;
        private bool isCrouching;

        private float verticalVelocity;
        private float jumpBufferCounter;
        private float coyoteTimeCounter;

        private float pitch;

        private Vector2 currentRecoil;
        private Vector2 targetRecoil;

        public bool IsCrouching => isCrouching;
        public bool IsGrounded => isGrounded;

        public bool IsSprinting
        {
            get
            {
                return
                    Keyboard.current != null &&
                    Keyboard.current.leftShiftKey.isPressed &&
                    !isCrouching;
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraPivot == null)
            {
                Debug.LogError(
                    "CameraPivot nu este asignat în FpsController.",
                    this
                );

                enabled = false;
                return;
            }

            if (groundCheck == null)
            {
                Debug.LogError(
                    "GroundCheck nu este asignat în FpsController.",
                    this
                );

                enabled = false;
                return;
            }

            controller.height = standingHeight;

            controller.center = new Vector3(
                controller.center.x,
                standingHeight / 2f,
                controller.center.z
            );

            Vector3 cameraPosition = cameraPivot.localPosition;
            cameraPosition.y = standingCameraHeight;
            cameraPivot.localPosition = cameraPosition;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            CheckGround();
            UpdateCrouch();
            MovePlayer();
            UpdateCameraRecoil();
            UpdateLean();
            LookAround();
        }

        private void MovePlayer()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 direction =
                transform.right * horizontal +
                transform.forward * vertical;

            direction = Vector3.ClampMagnitude(direction, 1f);

            bool isSprinting =
                Keyboard.current != null &&
                Keyboard.current.leftShiftKey.isPressed &&
                !isCrouching;

            float currentSpeed;

            if (isCrouching)
            {
                currentSpeed = crouchWalkSpeed;
            }
            else if (isSprinting)
            {
                currentSpeed = sprintSpeed;
            }
            else
            {
                currentSpeed = walkSpeed;
            }

            controller.Move(
                direction *
                currentSpeed *
                Time.deltaTime
            );

            if (isGrounded)
            {
                coyoteTimeCounter = coyoteTime;

                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f;
                }
            }
            else
            {
                coyoteTimeCounter -= Time.deltaTime;
            }

            if (
                Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame
            )
            {
                jumpBufferCounter = jumpBufferTime;
            }
            else
            {
                jumpBufferCounter -= Time.deltaTime;
            }

            if (
                jumpBufferCounter > 0f &&
                coyoteTimeCounter > 0f &&
                !isCrouching
            )
            {
                verticalVelocity =
                    Mathf.Sqrt(
                        jumpHeight *
                        -2f *
                        gravity
                    );

                jumpBufferCounter = 0f;
                coyoteTimeCounter = 0f;
            }

            verticalVelocity += gravity * Time.deltaTime;

            controller.Move(
                Vector3.up *
                verticalVelocity *
                Time.deltaTime
            );
        }

        private void UpdateLean()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            float targetLeanAngle = 0f;
            float requestedLeanOffset = 0f;

            if (Keyboard.current.qKey.isPressed)
            {
                targetLeanAngle = leanAngle;
                requestedLeanOffset = -leanDistance;
            }
            else if (Keyboard.current.eKey.isPressed)
            {
                targetLeanAngle = -leanAngle;
                requestedLeanOffset = leanDistance;
            }

            float allowedLeanOffset = GetAllowedLeanOffset(
                requestedLeanOffset
            );

            if (Mathf.Abs(allowedLeanOffset) <
                Mathf.Abs(requestedLeanOffset))
            {
                float allowedPercentage =
                    leanDistance > 0f
                        ? Mathf.Abs(allowedLeanOffset) / leanDistance
                        : 0f;

                targetLeanAngle *= allowedPercentage;
            }

            currentLeanAngle = Mathf.Lerp(
                currentLeanAngle,
                targetLeanAngle,
                leanSpeed * Time.deltaTime
            );

            currentLeanOffset = Mathf.Lerp(
                currentLeanOffset,
                allowedLeanOffset,
                leanSpeed * Time.deltaTime
            );

            Vector3 cameraPosition = cameraPivot.localPosition;
            cameraPosition.x = currentLeanOffset;
            cameraPivot.localPosition = cameraPosition;
        }
        
        private float GetAllowedLeanOffset(
            float requestedOffset
        )
        {
            if (Mathf.Approximately(requestedOffset, 0f))
            {
                return 0f;
            }

            Vector3 direction =
                cameraPivot.parent != null
                    ? cameraPivot.parent.right *
                      Mathf.Sign(requestedOffset)
                    : transform.right *
                      Mathf.Sign(requestedOffset);

            Vector3 origin =
                cameraPivot.parent != null
                    ? cameraPivot.parent.TransformPoint(
                        new Vector3(
                            0f,
                            cameraPivot.localPosition.y,
                            0f
                        )
                    )
                    : transform.position +
                      Vector3.up * cameraPivot.localPosition.y;

            float requestedDistance =
                Mathf.Abs(requestedOffset);

            if (
                Physics.SphereCast(
                    origin,
                    leanCollisionRadius,
                    direction,
                    out RaycastHit hit,
                    requestedDistance,
                    leanCollisionMask,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                float safeDistance = Mathf.Max(
                    0f,
                    hit.distance - 0.02f
                );

                return safeDistance *
                       Mathf.Sign(requestedOffset);
            }

            return requestedOffset;
        }

        private void UpdateCrouch()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            bool crouchHeld =
                Keyboard.current.leftCtrlKey.isPressed ||
                Keyboard.current.rightCtrlKey.isPressed;

            if (crouchHeld)
            {
                isCrouching = true;
            }
            else if (!IsCeilingAbove())
            {
                isCrouching = false;
            }

            float targetHeight = isCrouching
                ? crouchingHeight
                : standingHeight;

            float targetCameraHeight = isCrouching
                ? crouchingCameraHeight
                : standingCameraHeight;

            controller.height = Mathf.Lerp(
                controller.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime
            );

            Vector3 controllerCenter = controller.center;

            controllerCenter.y = Mathf.Lerp(
                controllerCenter.y,
                targetHeight / 2f,
                crouchTransitionSpeed * Time.deltaTime
            );

            controller.center = controllerCenter;

            Vector3 cameraPosition = cameraPivot.localPosition;

            cameraPosition.y = Mathf.Lerp(
                cameraPosition.y,
                targetCameraHeight,
                crouchTransitionSpeed * Time.deltaTime
            );

            cameraPivot.localPosition = cameraPosition;
        }
        
        

        private bool IsCeilingAbove()
        {
            Vector3 origin =
                transform.position +
                Vector3.up * crouchingHeight;

            float checkDistance =
                standingHeight - crouchingHeight;

            return Physics.Raycast(
                origin,
                Vector3.up,
                checkDistance,
                ceilingMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private void LookAround()
        {
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 mouseDelta =
                Mouse.current.delta.ReadValue();

            float mouseX =
                mouseDelta.x * mouseSensitivity;

            float mouseY =
                mouseDelta.y * mouseSensitivity;

            float yawInput =
                mouseX +
                currentRecoil.y;

            transform.Rotate(
                Vector3.up,
                yawInput,
                Space.Self
            );

            pitch -= mouseY;
            pitch -= currentRecoil.x;

            pitch = Mathf.Clamp(
                pitch,
                minimumPitch,
                maximumPitch
            );

            cameraPivot.localRotation =
                Quaternion.Euler(
                    pitch,
                    0f,
                    currentLeanAngle
                );
        }

        private void UpdateCameraRecoil()
        {
            currentRecoil = Vector2.Lerp(
                currentRecoil,
                targetRecoil,
                recoilSnappiness * Time.deltaTime
            );

            targetRecoil = Vector2.Lerp(
                targetRecoil,
                Vector2.zero,
                recoilReturnSpeed * Time.deltaTime
            );
        }

        public void AddRecoil(
            float verticalRecoil,
            float horizontalRecoil
        )
        {
            float randomHorizontal =
                Random.Range(
                    -horizontalRecoil,
                    horizontalRecoil
                );

            targetRecoil += new Vector2(
                verticalRecoil,
                randomHorizontal
            );
        }

        private void CheckGround()
        {
            bool sphereGrounded =
                Physics.CheckSphere(
                    groundCheck.position,
                    groundCheckRadius,
                    groundMask,
                    QueryTriggerInteraction.Ignore
                );

            isGrounded =
                sphereGrounded ||
                controller.isGrounded;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }
            
            if (cameraPivot != null)
            {
                Gizmos.DrawWireSphere(
                    cameraPivot.position,
                    leanCollisionRadius
                );
            }
            
        }
    }
}