using System.Collections;
using ChokeHold.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Arma din mana jucatorului (persoana intai). O singura componenta pe WeaponHolder; PlayerLoadout ii da arma
    /// curenta prin Equip (model FP, statistici, munitie). Stie:
    ///   - foc automat sau semi-automat, incarcator + rezerva, reincarcare (mai lunga pe gol);
    ///   - ochire: hold click dreapta (catare / red dot / holo: socket-ul Sight pe axa camerei) sau luneta
    ///     (ACOG / sniper: click dreapta trece prin nivelurile de zoom, ecran de luneta, arma si mainile ascunse);
    ///   - cutit: click stanga taietura, click dreapta injunghiere, lovitura in spate inmultita;
    ///   - zone de lovire (cap x4, membre x0,75) si anuntul kill-urilor (economie).
    /// </summary>
    public class HitscanWeapon : MonoBehaviour
    {
        /// <summary>Dupa fiecare foc; dupa inceperea reincarcarii (animatii, efecte).</summary>
        public event System.Action Fired;
        public event System.Action ReloadStarted;
        /// <summary>Arma noua in mana (modelul FP e deja creat).</summary>
        public event System.Action<WeaponData> Equipped;
        /// <summary>Lovitura de cutit: true = injunghiere (atacul greu).</summary>
        public event System.Action<bool> MeleeSwing;
        /// <summary>O tinta a murit din lovitura noastra.</summary>
        public event System.Action<Health, WeaponData> TargetKilled;

        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;
        public bool IsAiming => isAiming;
        public bool IsScoped => scopeLevel > 0;
        public int ScopeLevel => scopeLevel;

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

        [Header("Weapon Data")]
        [SerializeField] private WeaponData data;

        private int currentAmmo;
        private int reserveAmmo;
        private bool isReloading;
        private float nextFireTime;
        private float reloadEndTime;

        private Vector3 weaponStartPosition;
        private Vector3 recoilTargetPosition;
        private Vector3 currentWeaponTargetPosition;
        private Vector3 swayOffset;
        private float bobTimer;
        private Vector3 bobOffset;

        private bool isAiming;
        private int shotsFired;                 // gloante trase in rafala curenta (reculul si dispersia cresc cu ele)
        private float nextShotsDecay;
        private int kickSide = 1;               // partea spre care merge reculul lateral
        private const float SprintToFireDelay = 0.15f;
        private const float ShotsDecayInterval = 0.0225f;
        private int scopeLevel;
        private int rescopeLevel;
        private float rescopeTime = -1f;

        private GameObject model;
        private WeaponSockets sockets;
        private ScopeOverlay scopeOverlay;
        private Coroutine reloadRoutine;

        public WeaponData Data => data;
        public WeaponSockets Sockets => sockets;

        // ------------------------------------------------------------------
        // Echipare
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (weaponHolder == null)
            {
                Debug.LogError("WeaponHolder nu este asignat în HitscanWeapon.", this);
                enabled = false;
                return;
            }

            // Modelul vechi (placeholder) din scena ramane ascuns cand vine un model FP din WeaponData.
            if (data != null && data.firstPersonPrefab != null)
            {
                foreach (Renderer old in weaponHolder.GetComponentsInChildren<Renderer>(true))
                {
                    old.enabled = false;
                }
            }

            if (playerCamera != null)
            {
                normalFieldOfView = playerCamera.fieldOfView;
            }

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }

            // Fara inventar (PlayerLoadout), arma din scena se echipeaza singura.
            if (GetComponentInParent<PlayerLoadout>() == null)
            {
                Equip(data, -1, -1);
            }
        }

        /// <summary>
        /// Pune arma in mana: model FP, socket-uri, statistici, pozitia de hip-fire si de ochire.
        /// ammo / reserve negative = incarcator plin si rezerva plina.
        /// </summary>
        public void Equip(WeaponData newData, int ammo, int reserve)
        {
            if (weaponHolder == null)
            {
                return;
            }

            CancelReload();
            SetScope(0);
            rescopeTime = -1f;
            isAiming = false;

            if (model != null)
            {
                // flacara de la teava e un obiect al scenei: se scoate de pe modelul vechi inainte de stergere
                if (muzzleFlash != null && muzzleFlash.transform.IsChildOf(model.transform))
                {
                    muzzleFlash.transform.SetParent(weaponHolder, false);
                }
                Destroy(model);
                model = null;
                sockets = null;
            }

            data = newData;

            if (data != null)
            {
                damage = data.damage;
                fireRate = data.fireRate;
                magazineSize = data.magazineSize;
                reloadTime = data.reloadTime;
                range = data.range;
                recoilDistance = data.recoilKick;
                hipFireSpread = data.hipSpread;
                aimSpread = data.aimSpread;
                aimFieldOfView = data.aimFieldOfView;

                if (data.firstPersonPrefab != null)
                {
                    model = Instantiate(data.firstPersonPrefab, weaponHolder);
                    model.name = data.firstPersonPrefab.name;
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.Euler(data.firstPersonRotation);
                    sockets = model.GetComponent<WeaponSockets>();
                }

                // Hip-fire: manerul la offset-ul armei. ADS: socket-ul Sight pe axa camerei, la distanta ochiului.
                weaponHolder.localPosition = data.firstPersonOffset;
                if (sockets != null && sockets.Sight != null)
                {
                    Vector3 sight = weaponHolder.InverseTransformPoint(sockets.Sight.position);
                    aimPosition = new Vector3(-sight.x, -sight.y, data.adsEyeRelief - sight.z);
                }

                // Reincarcarea se vede pe arma (FirstPersonAnimator); holder-ul doar coboara putin.
                if (GetComponent<FirstPersonAnimator>() != null)
                {
                    reloadOffset = new Vector3(0f, -0.02f, -0.02f);
                }

                if (sockets != null && sockets.Muzzle != null && muzzleFlash != null)
                {
                    muzzleFlash.transform.SetParent(sockets.Muzzle, false);
                    muzzleFlash.transform.localPosition = Vector3.zero;
                    muzzleFlash.transform.localRotation = Quaternion.identity;
                }

                nextFireTime = Time.time + data.drawTime;
            }

            weaponStartPosition = weaponHolder.localPosition;
            recoilTargetPosition = weaponStartPosition;
            currentWeaponTargetPosition = weaponStartPosition;

            currentAmmo = ammo >= 0 ? ammo : magazineSize;
            reserveAmmo = reserve >= 0 ? reserve : (data != null ? data.reserveAmmo : magazineSize * 3);
            UpdateAmmoUI();

            if (fpsController != null)
            {
                fpsController.SpeedMultiplier = data != null ? data.moveSpeed : 1f;
            }

            Equipped?.Invoke(data);
        }

        // ------------------------------------------------------------------
        // Bucla
        // ------------------------------------------------------------------

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null || data == null)
            {
                return;
            }

            bool blocked = PlayerInputGate.Blocked || (fpsController != null && fpsController.InputLocked);

            UpdateAim(blocked);

            if (fpsController != null)
            {
                // ochirea si focul opresc sprintul
                fpsController.SprintSuppressed = isAiming || IsScoped || (!blocked && Mouse.current.leftButton.isPressed);
            }

            // fara tragaci apasat rafala se "uita" repede (urmatorul glont e iar precis)
            if (!Mouse.current.leftButton.isPressed && shotsFired > 0 && Time.time >= nextShotsDecay)
            {
                shotsFired--;
                nextShotsDecay = Time.time + ShotsDecayInterval;
            }

            UpdateCrosshair();
            UpdateWeaponSway();
            UpdateWeaponBob();
            UpdateWeaponPosition();
            UpdateAimFieldOfView();

            if (rescopeTime >= 0f && Time.time >= rescopeTime)
            {
                rescopeTime = -1f;
                if (!isReloading && !blocked && currentAmmo > 0)
                {
                    SetScope(rescopeLevel);
                }
            }

            if (isReloading || blocked)
            {
                return;
            }

            if (data.IsKnife)
            {
                UpdateKnife();
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartReload();
                return;
            }

            bool trigger = data.automatic
                ? Mouse.current.leftButton.isPressed
                : Mouse.current.leftButton.wasPressedThisFrame;

            // din sprint arma are nevoie de o clipa ca sa ajunga in pozitia de tragere
            if (trigger && fpsController != null && fpsController.IsSprinting)
            {
                nextFireTime = Mathf.Max(nextFireTime, Time.time + SprintToFireDelay);
            }

            if (!trigger || Time.time < nextFireTime)
            {
                return;
            }

            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            nextFireTime = Time.time + 1f / fireRate;
            Shoot();
        }

        private void UpdateAim(bool blocked)
        {
            if (data.HasScope)
            {
                // Luneta: click dreapta trece prin nivelurile de zoom; reincarcarea iese din luneta.
                isAiming = scopeLevel > 0;
                if (!blocked && !isReloading && Mouse.current.rightButton.wasPressedThisFrame)
                {
                    int levels = data.zoomFieldOfView != null ? data.zoomFieldOfView.Length : 0;
                    SetScope(levels == 0 ? 0 : (scopeLevel + 1) % (levels + 1));
                    rescopeTime = -1f;
                }
                if (blocked && scopeLevel > 0)
                {
                    SetScope(0);
                }
                return;
            }

            isAiming = !blocked && !data.IsKnife && data.sight != SightType.None && Mouse.current.rightButton.isPressed;
        }

        private void SetScope(int level)
        {
            scopeLevel = level;
            bool scoped = level > 0 && data != null && data.HasScope;

            if (scoped && scopeOverlay == null)
            {
                scopeOverlay = ScopeOverlay.Create(transform);
            }
            if (scopeOverlay != null)
            {
                scopeOverlay.Show(scoped, data != null ? data.sight : SightType.None);
            }

            FirstPersonAnimator animator = GetComponent<FirstPersonAnimator>();
            if (animator != null)
            {
                animator.SetHidden(scoped);
            }

            if (fpsController != null && data != null)
            {
                // cu luneta pusa lunetistele merg mai incet
                fpsController.SpeedMultiplier = data.moveSpeed * (scoped && data.sight == SightType.SniperScope ? 0.72f : 1f);
            }

            if (fpsController != null)
            {
                fpsController.SensitivityScale = scoped && playerCamera != null
                    ? Mathf.Clamp(ScopedFieldOfView() / normalFieldOfView, 0.1f, 1f) * GameSettings.ZoomSensitivityRatio
                    : 1f;
            }
        }

        private float ScopedFieldOfView()
        {
            if (data == null || data.zoomFieldOfView == null || scopeLevel <= 0 || scopeLevel > data.zoomFieldOfView.Length)
            {
                return aimFieldOfView;
            }
            return data.zoomFieldOfView[scopeLevel - 1];
        }

        // ------------------------------------------------------------------
        // Foc
        // ------------------------------------------------------------------

        private void Shoot()
        {
            currentAmmo--;
            Fired?.Invoke();
            UpdateAmmoUI();

            // arma merge putin inapoi in mana (la ochire jumatate), apoi revine
            recoilTargetPosition = weaponStartPosition + Vector3.back * recoilDistance * (isAiming ? 0.5f : 1f);

            // dispersia se calculeaza inainte de recul: primul glont dintr-o rafala e cel mai precis
            float currentSpread = GetCurrentSpread();
            ApplyKick();

            if (muzzleFlash != null && !data.suppressed)
            {
                muzzleFlash.Play();
            }

            AudioClip clip = data.shootSound != null ? data.shootSound : shootSound;
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, data.suppressed ? 0.35f : 1f);
            }

            Vector3 direction =
                playerCamera.transform.forward +
                playerCamera.transform.right * Random.Range(-currentSpread, currentSpread) +
                playerCamera.transform.up * Random.Range(-currentSpread, currentSpread);

            Ray ray = new Ray(playerCamera.transform.position, direction.normalized);

            // Stratul "Ignore Raycast" tine barierele invizibile pentru jucatori (marginea acoperisurilor):
            // gloantele trec prin ele.
            if (FirstHit(ray, hitMask & ~(1 << 2), range, out RaycastHit hit))
            {
                SpawnImpactEffect(hit);
                ApplyDamage(hit, damage, false);
            }

            // Lunetistele cu inchizator ies din luneta dupa foc si revin dupa armare (ca in CS).
            if (data.sight == SightType.SniperScope && scopeLevel > 0)
            {
                rescopeLevel = scopeLevel;
                SetScope(0);
                rescopeTime = Time.time + Mathf.Min(1f / fireRate, 1.2f);
            }
        }

        private void UpdateKnife()
        {
            bool stab = Mouse.current.rightButton.wasPressedThisFrame;
            bool slash = Mouse.current.leftButton.isPressed;

            if (Time.time < nextFireTime || (!stab && !slash))
            {
                return;
            }

            nextFireTime = Time.time + 1f / (stab ? data.stabRate : data.slashRate);
            MeleeSwing?.Invoke(stab);
            Fired?.Invoke();

            // Lovitura ajunge la sfarsitul miscarii (taietura e rapida, injunghierea mai lenta)
            StartCoroutine(MeleeHit(stab, stab ? 0.18f : 0.08f));
        }

        private IEnumerator MeleeHit(bool stab, float delay)
        {
            yield return new WaitForSeconds(delay);

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!FirstHit(ray, hitMask & ~(1 << 2), data.meleeRange, out RaycastHit hit, 0.18f))
            {
                yield break;
            }

            float amount = stab ? data.stabDamage : data.slashDamage;
            Health target = hit.collider.GetComponentInParent<Health>();
            if (target != null)
            {
                // in spate: tinta priveste in aceeasi directie ca noi
                Vector3 attacker = playerCamera.transform.forward;
                attacker.y = 0f;
                Vector3 victim = target.transform.forward;
                victim.y = 0f;
                if (Vector3.Dot(attacker.normalized, victim.normalized) > 0.5f)
                {
                    amount *= data.backstabMultiplier;
                }
            }

            ApplyDamage(hit, amount, true);
        }

        private void ApplyDamage(RaycastHit hit, float amount, bool melee)
        {
            Health targetHealth = hit.collider.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead)
            {
                return;
            }

            // Zona de lovire (cap x4, membre x0,75); cutitul nu tine cont de zona.
            HitZone zone = hit.collider.GetComponent<HitZone>();
            float total = melee ? amount : amount * (zone != null ? zone.Multiplier : 1f);
            targetHealth.TakeDamage(total);

            Ragdoll ragdoll = targetHealth.GetComponentInChildren<Ragdoll>();
            if (ragdoll != null)
            {
                ragdoll.NoteHit(playerCamera.transform.forward);
            }

            if (hitMarker != null)
            {
                hitMarker.Show();
            }

            if (targetHealth.IsDead)
            {
                TargetKilled?.Invoke(targetHealth, data);
            }
        }

        private readonly RaycastHit[] hits = new RaycastHit[32];

        /// <summary>
        /// Cel mai apropiat obiect lovit care nu e al tragatorului: camera sta in capul propriului personaj,
        /// iar hitbox-urile lui (brate, piept, picioare) nu trebuie sa opreasca glontul.
        /// </summary>
        private bool FirstHit(Ray ray, int mask, float distance, out RaycastHit best, float radius = 0f)
        {
            best = default;
            Transform self = fpsController != null ? fpsController.transform : transform.root;
            int count = radius > 0f
                ? Physics.SphereCastNonAlloc(ray, radius, hits, distance, mask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(ray, hits, distance, mask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance < nearest && !hits[i].collider.transform.IsChildOf(self))
                {
                    nearest = hits[i].distance;
                    best = hits[i];
                }
            }

            return nearest < float.MaxValue;
        }

        private void SpawnImpactEffect(RaycastHit hit)
        {
            if (impactEffectPrefab == null)
            {
                return;
            }

            ParticleSystem impactEffect = Instantiate(impactEffectPrefab, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
            impactEffect.Play();
            Destroy(impactEffect.gameObject, impactEffect.main.duration + impactEffect.main.startLifetime.constantMax);
        }

        // ------------------------------------------------------------------
        // Munitie
        // ------------------------------------------------------------------

        private void StartReload()
        {
            if (isReloading || data == null || data.IsKnife || currentAmmo >= magazineSize || reserveAmmo <= 0)
            {
                return;
            }

            reloadRoutine = StartCoroutine(Reload());
        }

        private IEnumerator Reload()
        {
            isReloading = true;
            SetScope(0);
            rescopeTime = -1f;
            float duration = currentAmmo == 0 ? data.reloadEmptyTime : reloadTime;
            reloadEndTime = Time.time + duration;
            ReloadStarted?.Invoke();

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(duration);

            int needed = magazineSize - currentAmmo;
            int taken = Mathf.Min(needed, reserveAmmo);
            currentAmmo += taken;
            reserveAmmo -= taken;
            UpdateAmmoUI();
            FinishReload();
        }

        /// <summary>Durata reincarcarii in curs (animatia se incadreaza in ea).</summary>
        public float ReloadDuration => data != null ? (currentAmmo == 0 ? data.reloadEmptyTime : reloadTime) : reloadTime;

        private void CancelReload()
        {
            if (reloadRoutine != null)
            {
                StopCoroutine(reloadRoutine);
                reloadRoutine = null;
            }
            FinishReload();
        }

        private void FinishReload()
        {
            isReloading = false;
            reloadRoutine = null;
            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }
        }

        /// <summary>Incarcator si rezerva pline (respawn / inceput de runda).</summary>
        public void RefillAmmo()
        {
            StopAllCoroutines();
            FinishReload();
            currentAmmo = magazineSize;
            reserveAmmo = data != null ? data.reserveAmmo : magazineSize * 3;
            SetScope(0);
            rescopeTime = -1f;
            UpdateAmmoUI();
        }

        private void UpdateAmmoUI()
        {
            if (ammoText == null)
            {
                return;
            }

            ammoText.text = data != null && data.IsKnife ? string.Empty : $"{currentAmmo} / {reserveAmmo}";
        }

        // ------------------------------------------------------------------
        // Dispersie, sway, bob, pozitia armei
        // ------------------------------------------------------------------

        /// <summary>
        /// Reculul unui glont (grade), ca in shooterele competitive clasice: primul glont urca putin, fiecare
        /// glont din rafala urca mai mult, pana la un maxim; lateral reculul merge intr-o parte si o schimba din
        /// cand in cand. In aer si in miscare reculul e mai mare, ghemuit mai mic, la ochire putin mai mic.
        /// </summary>
        private void ApplyKick()
        {
            shotsFired++;
            if (fpsController == null || data == null)
            {
                return;
            }

            float stance = 1f;
            if (!fpsController.IsGrounded)
            {
                stance = 2f;
            }
            else if (fpsController.HorizontalSpeed > MovingFraction * fpsController.MaxGroundSpeed)
            {
                stance = 1.5f;
            }
            else if (fpsController.IsCrouching)
            {
                stance = 0.85f;
            }

            if (isAiming && !IsScoped)
            {
                stance *= 0.85f;
            }

            int extra = shotsFired > 1 ? shotsFired : 0;
            float up = (data.kickBase.x + data.kickPerShot.x * extra) * stance;
            float side = (data.kickBase.y + data.kickPerShot.y * extra) * stance;

            Vector2 punch = fpsController.Punch;
            punch.x = Mathf.Min(punch.x + up, data.kickMax.x);
            punch.y = Mathf.Clamp(punch.y + side * kickSide, -data.kickMax.y, data.kickMax.y);
            fpsController.Punch = punch;

            if (Random.Range(0, data.kickFlip + 1) == 0)
            {
                kickSide = -kickSide;
            }
        }

        private const float MovingFraction = 0.34f;     // sub aceasta fractiune din viteza maxima esti "oprit" (counter-strafe)

        /// <summary>
        /// Dispersia dupa viteza reala, nu dupa taste: oprit (sau dupa un counter-strafe) primul glont e precis;
        /// alergand, in aer sau in sprint dispersia creste mult; in rafala creste cu fiecare glont.
        /// </summary>
        private float GetCurrentSpread()
        {
            bool sniper = data != null && data.sight == SightType.SniperScope;
            float spread = isAiming ? aimSpread : hipFireSpread;

            if (fpsController != null)
            {
                float speedFraction = fpsController.HorizontalSpeed / Mathf.Max(0.1f, fpsController.MaxGroundSpeed);

                if (!fpsController.IsGrounded)
                {
                    spread = Mathf.Max(spread, hipFireSpread) * airSpreadMultiplier;
                }
                else if (speedFraction > MovingFraction)
                {
                    float moving = Mathf.InverseLerp(MovingFraction, 1f, speedFraction);
                    // cu luneta, miscarea strica tinta mult mai tare
                    float worst = IsScoped ? movementSpreadMultiplier * 12f : movementSpreadMultiplier * 2.5f;
                    spread *= Mathf.Lerp(1.2f, worst, moving);
                    if (fpsController.IsSprinting)
                    {
                        spread *= sprintSpreadMultiplier;
                    }
                }
                else if (!(sniper && !IsScoped))
                {
                    // oprit: primul glont e precis (lunetistele fara luneta raman imprecise)
                    spread *= StandingAccuracy;
                }

                if (fpsController.IsCrouching && fpsController.IsGrounded)
                {
                    spread *= crouchSpreadMultiplier;
                }
            }

            if (data != null && data.automatic)
            {
                spread *= 1f + Mathf.Min(shotsFired, 10) * 0.18f;
            }

            return spread;
        }

        private const float StandingAccuracy = 0.3f;

        private void UpdateCrosshair()
        {
            if (crosshairUI == null)
            {
                return;
            }

            // Lunetistele n-au cruce fara luneta (ca in CS); cu luneta se vede reticulul lunetei.
            bool show = !IsScoped && (data == null || data.sight != SightType.SniperScope);
            crosshairUI.SetVisible(show);
            if (show)
            {
                crosshairUI.SetSpread(GetCurrentSpread());
            }
        }

        private void UpdateAimFieldOfView()
        {
            if (playerCamera == null)
            {
                return;
            }

            float target = IsScoped ? ScopedFieldOfView() : (isAiming ? aimFieldOfView : normalFieldOfView);

            // luneta intra instant (ca in CS); ochirea prin catare se face lin
            playerCamera.fieldOfView = IsScoped
                ? target
                : Mathf.Lerp(playerCamera.fieldOfView, target, aimSpeed * Time.deltaTime);
        }

        private void UpdateWeaponSway()
        {
            Vector2 mouseDelta = PlayerInputGate.Blocked ? Vector2.zero : Mouse.current.delta.ReadValue();

            float swayX = Mathf.Clamp(-mouseDelta.x * swayAmount, -maximumSway, maximumSway);
            float swayY = Mathf.Clamp(-mouseDelta.y * swayAmount, -maximumSway, maximumSway);
            float swayMultiplier = isAiming ? aimSwayMultiplier : 1f;

            swayOffset = Vector3.Lerp(swayOffset, new Vector3(swayX, swayY, 0f) * swayMultiplier, swaySmoothSpeed * Time.deltaTime);
        }

        private void UpdateWeaponBob()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            // balansul urmeaza viteza reala pe sol (nu tastele): in aer si la counter-strafe arma sta linistita
            bool isMoving = fpsController != null
                ? fpsController.IsGrounded && fpsController.HorizontalSpeed > 0.8f
                : Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed;
            bool isSprinting = fpsController != null && fpsController.IsSprinting;

            if (isMoving)
            {
                float bobSpeed = isSprinting ? sprintBobSpeed : walkBobSpeed;
                float bobAmount = isSprinting ? sprintBobAmount : walkBobAmount;

                if (isAiming)
                {
                    bobAmount *= aimBobMultiplier;
                }

                bobTimer += Time.deltaTime * bobSpeed;
                Vector3 targetBob = new Vector3(Mathf.Sin(bobTimer) * bobAmount, Mathf.Abs(Mathf.Cos(bobTimer)) * bobAmount, 0f);
                bobOffset = Vector3.Lerp(bobOffset, targetBob, bobSmoothSpeed * Time.deltaTime);
            }
            else
            {
                bobTimer = 0f;
                bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, bobSmoothSpeed * Time.deltaTime);
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

            float movementSpeed = isAiming ? aimSpeed : reloadMoveSpeed;

            currentWeaponTargetPosition = Vector3.Lerp(currentWeaponTargetPosition, desiredPosition, movementSpeed * Time.deltaTime);
            Vector3 finalTargetPosition = currentWeaponTargetPosition + swayOffset + bobOffset;
            weaponHolder.localPosition = Vector3.Lerp(weaponHolder.localPosition, finalTargetPosition, recoilReturnSpeed * Time.deltaTime);

            if (!isReloading && !isAiming)
            {
                recoilTargetPosition = Vector3.Lerp(recoilTargetPosition, weaponStartPosition, recoilReturnSpeed * Time.deltaTime);
            }
        }
    }
}
