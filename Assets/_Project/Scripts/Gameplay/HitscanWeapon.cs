using System.Collections;
using ChokeHold.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    public class HitscanWeapon : MonoBehaviour
    {
        /// <summary>Dupa fiecare foc; dupa inceperea reincarcarii (animatii, efecte).</summary>
        public event System.Action Fired;
        public event System.Action ReloadStarted;
        public int CurrentAmmo => currentAmmo;
        public bool IsReloading => isReloading;

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private HitMarkerUI hitMarker;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text reloadText;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private ParticleSystem impactEffectPrefab;
        [SerializeField] private FpsController fpsController;
        [SerializeField] private CrosshairUI crosshairUI;

        [Header("Weapon")]
        [SerializeField] private float range = 100f;
        [SerializeField] private float fireRate = 5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float damage = 25f;

        [Header("Ammo")]
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private float reloadTime = 1.5f;

        [Header("Recoil")]
        [SerializeField] private Transform weaponHolder;
        [SerializeField] private float recoilDistance = 0.08f;
        [SerializeField] private float recoilReturnSpeed = 12f;
        [SerializeField] private Vector3 reloadOffset = new Vector3(0f, -0.35f, 0f);
        [SerializeField] private float reloadMoveSpeed = 8f;
        [SerializeField] private float cameraRecoilVertical = 0.8f;
        [SerializeField] private float cameraRecoilHorizontal = 0.15f;

        [Header("Weapon Sway")]
        [SerializeField] private float swayAmount = 0.003f;
        [SerializeField] private float swaySmoothSpeed = 8f;
        [SerializeField] private float maximumSway = 0.08f;

        [Header("Weapon Bob")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private float walkBobSpeed = 8f;
        [SerializeField] private float sprintBobSpeed = 13f;
        [SerializeField] private float walkBobAmount = 0.025f;
        [SerializeField] private float sprintBobAmount = 0.045f;
        [SerializeField] private float bobSmoothSpeed = 10f;
        
        [Header("Aim Down Sights")]
        [SerializeField] private Vector3 aimPosition = new Vector3(0f, -0.15f, 0.35f);
        [SerializeField] private float aimSpeed = 12f;
        [SerializeField] private float normalFieldOfView = 60f;
        [SerializeField] private float aimFieldOfView = 48f;
        [SerializeField] private float aimSwayMultiplier = 0.35f;
        [SerializeField] private float aimBobMultiplier = 0.25f;
        
        [Header("Accuracy")]
        [SerializeField] private float hipFireSpread = 0.02f;
        [SerializeField] private float aimSpread = 0.003f;
        [SerializeField] private float crouchSpreadMultiplier = 0.65f;
        [SerializeField] private float movementSpreadMultiplier = 1.4f;
        [SerializeField] private float sprintSpreadMultiplier = 2f;
        [SerializeField] private float airSpreadMultiplier = 2.5f;

        private int currentAmmo;
        private bool isReloading;
        private float nextFireTime;

        private Vector3 weaponStartPosition;
        private Vector3 recoilTargetPosition;
        private Vector3 currentWeaponTargetPosition;

        private Vector3 swayOffset;

        private float bobTimer;
        private Vector3 bobOffset;
        
        private bool isAiming;
            
        [Header("Weapon Data")]
        [SerializeField] private WeaponData data;

        private WeaponSockets sockets;

        public WeaponData Data => data;
        public WeaponSockets Sockets => sockets;

        /// <summary>Statisticile si modelul de la persoana intai vin din WeaponData, daca e setat.</summary>
        private void ApplyWeaponData()
        {
            if (data == null)
            {
                return;
            }

            damage = data.damage;
            fireRate = data.fireRate;
            magazineSize = data.magazineSize;
            reloadTime = data.reloadTime;
            range = data.range;
            cameraRecoilVertical = data.recoilVertical;
            cameraRecoilHorizontal = data.recoilHorizontal;
            recoilDistance = data.recoilKick;
            hipFireSpread = data.hipSpread;
            aimSpread = data.aimSpread;
            aimFieldOfView = data.aimFieldOfView;

            if (weaponHolder != null && data.firstPersonPrefab != null)
            {
                // Modelul vechi (placeholder) se ascunde; modelul FP din kit se pune cu manerul in holder.
                foreach (Renderer old in weaponHolder.GetComponentsInChildren<Renderer>(true))
                {
                    old.enabled = false;
                }

                GameObject model = Instantiate(data.firstPersonPrefab, weaponHolder);
                model.name = data.firstPersonPrefab.name;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                sockets = model.GetComponent<WeaponSockets>();

                if (sockets != null && sockets.Muzzle != null && muzzleFlash != null)
                {
                    muzzleFlash.transform.SetParent(sockets.Muzzle, false);
                    muzzleFlash.transform.localPosition = Vector3.zero;
                    muzzleFlash.transform.localRotation = Quaternion.identity;
                }
            }
        }

        private void Awake()
        {
            ApplyWeaponData();

            if (weaponHolder == null)
            {
                Debug.LogError(
                    "WeaponHolder nu este asignat în HitscanWeapon.",
                    this
                );

                enabled = false;
                return;
            }

            weaponStartPosition = weaponHolder.localPosition;
            recoilTargetPosition = weaponStartPosition;
            currentWeaponTargetPosition = weaponStartPosition;

            currentAmmo = magazineSize;

            UpdateAmmoUI();

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }

            if (playerCamera != null)
            {
                normalFieldOfView = playerCamera.fieldOfView;
            }
        }

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null)
            {
                return;
            }
            
            isAiming = Mouse.current.rightButton.isPressed;

            UpdateCrosshair();
            UpdateWeaponSway();
            UpdateWeaponBob();
            UpdateWeaponPosition();
            UpdateAimFieldOfView();

            if (isReloading)
            {
                return;
            }

            // Freeze time, plant/defuse sau moarte: nu se trage si nu se reincarca.
            if (fpsController != null && fpsController.InputLocked)
            {
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartCoroutine(Reload());
                return;
            }

            if (currentAmmo <= 0)
            {
                StartCoroutine(Reload());
                return;
            }

            if (
                Mouse.current.leftButton.isPressed &&
                Time.time >= nextFireTime
            )
            {
                nextFireTime = Time.time + 1f / fireRate;
                Shoot();
            }
        }

        private void Shoot()
        {
            currentAmmo--;
            Fired?.Invoke();
            UpdateAmmoUI();

            recoilTargetPosition =
                weaponStartPosition +
                Vector3.back * recoilDistance;
            
            if (fpsController != null)
            {
                float recoilMultiplier =
                    isAiming ? 0.6f : 1f;

                fpsController.AddRecoil(
                    cameraRecoilVertical * recoilMultiplier,
                    cameraRecoilHorizontal * recoilMultiplier
                );
            }

            if (muzzleFlash != null)
            {
                muzzleFlash.Play();
            }

            if (audioSource != null && shootSound != null)
            {
                audioSource.PlayOneShot(shootSound);
            }

            Debug.Log($"Ammo: {currentAmmo}/{magazineSize}");

            float currentSpread = GetCurrentSpread();

            Vector3 spreadDirection =
                playerCamera.transform.forward +
                playerCamera.transform.right *
                Random.Range(-currentSpread, currentSpread) +
                playerCamera.transform.up *
                Random.Range(-currentSpread, currentSpread);

            spreadDirection.Normalize();

            Ray ray = new Ray(
                playerCamera.transform.position,
                spreadDirection
            );

            if (
                Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    range,
                    // Stratul "Ignore Raycast" tine barierele invizibile pentru jucatori (marginea acoperisurilor):
                    // gloantele trec prin ele.
                    hitMask & ~(1 << 2),
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                SpawnImpactEffect(hit);

                Debug.Log($"Lovit: {hit.collider.name}");

                Health targetHealth =
                    hit.collider.GetComponentInParent<Health>();

                if (targetHealth != null && !targetHealth.IsDead)
                {
                    // Zona de lovire (cap x4, membre x0,75); fara HitZone (capsulele vechi) ramane x1.
                    HitZone zone = hit.collider.GetComponent<HitZone>();
                    targetHealth.TakeDamage(damage * (zone != null ? zone.Multiplier : 1f));

                    if (hitMarker != null)
                    {
                        hitMarker.Show();
                    }
                }

                Debug.DrawLine(
                    ray.origin,
                    hit.point,
                    Color.red,
                    1f
                );
            }
            else
            {
                Debug.DrawRay(
                    ray.origin,
                    ray.direction * range,
                    Color.green,
                    1f
                );
            }
        }
        
        private void UpdateCrosshair()
        {
            if (crosshairUI == null)
            {
                return;
            }

            float currentSpread = GetCurrentSpread();
            crosshairUI.SetSpread(currentSpread);
        }

        private void SpawnImpactEffect(RaycastHit hit)
        {
            if (impactEffectPrefab == null)
            {
                return;
            }

            ParticleSystem impactEffect = Instantiate(
                impactEffectPrefab,
                hit.point + hit.normal * 0.01f,
                Quaternion.LookRotation(hit.normal)
            );

            impactEffect.Play();

            float destroyDelay =
                impactEffect.main.duration +
                impactEffect.main.startLifetime.constantMax;

            Destroy(
                impactEffect.gameObject,
                destroyDelay
            );
        }

        private IEnumerator Reload()
        {
            if (isReloading)
            {
                yield break;
            }

            if (currentAmmo == magazineSize)
            {
                yield break;
            }

            isReloading = true;
            ReloadStarted?.Invoke();

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(true);
            }

            Debug.Log("Reloading...");

            yield return new WaitForSeconds(reloadTime);

            currentAmmo = magazineSize;
            UpdateAmmoUI();

            isReloading = false;

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }

            Debug.Log("Reload complete.");
        }
        
        /// <summary>
        /// Incarcator plin instant (folosit la respawn / inceput de runda).
        /// </summary>
        public void RefillAmmo()
        {
            StopAllCoroutines();
            isReloading = false;
            currentAmmo = magazineSize;
            UpdateAmmoUI();

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }
        }

        private float GetCurrentSpread()
        {
            float spread = isAiming
                ? aimSpread
                : hipFireSpread;

            bool isMoving =
                Keyboard.current != null &&
                (
                    Keyboard.current.wKey.isPressed ||
                    Keyboard.current.aKey.isPressed ||
                    Keyboard.current.sKey.isPressed ||
                    Keyboard.current.dKey.isPressed
                );

            if (fpsController != null)
            {
                if (fpsController.IsCrouching)
                {
                    spread *= crouchSpreadMultiplier;
                }

                if (fpsController.IsSprinting)
                {
                    spread *= sprintSpreadMultiplier;
                }

                if (!fpsController.IsGrounded)
                {
                    spread *= airSpreadMultiplier;
                }
                else if (isMoving && !fpsController.IsSprinting)
                {
                    spread *= movementSpreadMultiplier;
                }
            }

            return spread;
        }

        private void UpdateAimFieldOfView()
        {
            if (playerCamera == null)
            {
                return;
            }
            
            float targetFieldOfView = isAiming
                ? aimFieldOfView
                : normalFieldOfView;

            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFieldOfView,
                aimSpeed * Time.deltaTime
            );
            
        }

        private void UpdateAmmoUI()
        {
            if (ammoText == null)
            {
                return;
            }

            ammoText.text = $"{currentAmmo}/{magazineSize}";
        }

        private void UpdateWeaponSway()
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            float swayX = Mathf.Clamp(
                -mouseDelta.x * swayAmount,
                -maximumSway,
                maximumSway
            );

            float swayY = Mathf.Clamp(
                -mouseDelta.y * swayAmount,
                -maximumSway,
                maximumSway
            );
            
            float swayMultiplier = isAiming
                ? aimSwayMultiplier
                : 1f;

            swayX *= swayMultiplier;
            swayY *= swayMultiplier;

            Vector3 targetSway = new Vector3(
                swayX,
                swayY,
                0f
            );

            swayOffset = Vector3.Lerp(
                swayOffset,
                targetSway,
                swaySmoothSpeed * Time.deltaTime
            );
        }

        private void UpdateWeaponBob()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            bool isMoving =
                Keyboard.current.wKey.isPressed ||
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.sKey.isPressed ||
                Keyboard.current.dKey.isPressed;

            bool isSprinting =
                isMoving &&
                Keyboard.current.leftShiftKey.isPressed;

            if (isMoving)
            {
                float bobSpeed = isSprinting
                    ? sprintBobSpeed
                    : walkBobSpeed;

                float bobAmount = isSprinting
                    ? sprintBobAmount
                    : walkBobAmount;

                if (isAiming)
                {
                    bobAmount *= aimBobMultiplier;
                }

                bobTimer += Time.deltaTime * bobSpeed;

                Vector3 targetBob = new Vector3(
                    Mathf.Sin(bobTimer) * bobAmount,
                    Mathf.Abs(Mathf.Cos(bobTimer)) * bobAmount,
                    0f
                );

                bobOffset = Vector3.Lerp(
                    bobOffset,
                    targetBob,
                    bobSmoothSpeed * Time.deltaTime
                );
            }
            else
            {
                bobTimer = 0f;

                bobOffset = Vector3.Lerp(
                    bobOffset,
                    Vector3.zero,
                    bobSmoothSpeed * Time.deltaTime
                );
            }
        }

        private void UpdateWeaponPosition()
        {
            Vector3 desiredPosition;

            if (isReloading)
            {
                desiredPosition = weaponStartPosition + reloadOffset;
            }
            else if (isAiming)
            {
                desiredPosition = aimPosition;
            }
            else
            {
                desiredPosition = recoilTargetPosition;
            }

            float movementSpeed = isAiming
                ? aimSpeed
                : reloadMoveSpeed;

            currentWeaponTargetPosition = Vector3.Lerp(
                currentWeaponTargetPosition,
                desiredPosition,
                movementSpeed * Time.deltaTime
            );

            Vector3 finalTargetPosition =
                currentWeaponTargetPosition +
                swayOffset +
                bobOffset;

            weaponHolder.localPosition = Vector3.Lerp(
                weaponHolder.localPosition,
                finalTargetPosition,
                recoilReturnSpeed * Time.deltaTime
            );

            if (!isReloading && !isAiming)
            {
                recoilTargetPosition = Vector3.Lerp(
                    recoilTargetPosition,
                    weaponStartPosition,
                    recoilReturnSpeed * Time.deltaTime
                );
            }
        }
    }
}