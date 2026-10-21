using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerGrabber))]
public class PlayerShooter : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        public GunDefinition definition;
        public GameObject view;
        public Transform muzzle;

        // Runtime state belongs to this weapon, not its definition asset.
        [System.NonSerialized] public int ammo;
        [System.NonSerialized] public float nextShotTime;
    }

    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponSlot rifle = new WeaponSlot();
    [SerializeField] private WeaponSlot pistol = new WeaponSlot();
    [SerializeField] private LayerMask hittableLayers = ~0;

    private InputAction attackAction;
    private InputAction reloadAction;
    private InputAction selectRifleAction;
    private InputAction selectPistolAction;
    private PlayerGrabber grabber;

    private WeaponSlot equipped;
    private bool ownsRifle;
    private bool ownsPistol;
    private float reloadTimeRemaining;
    private bool readyToFire;

    public int CurrentAmmo => equipped != null ? equipped.ammo : 0;
    public bool IsReloading { get; private set; }
    public GunDefinition CurrentDefinition =>
        equipped != null ? equipped.definition : null;
    public bool HasRifle => ownsRifle;
    public bool HasPistol => ownsPistol;

    private string WeaponName => equipped == rifle ? "Rifle" : "Pistol";

    private void Start()
    {
        if (playerCamera == null ||
            !IsConfigured(rifle) ||
            !IsConfigured(pistol) ||
            rifle.view == pistol.view)
        {
            Debug.LogError(
                "PlayerShooter: assign the camera and both weapons. " +
                "Each weapon needs a definition, its own view and muzzle.",
                this);

            enabled = false;
            return;
        }

        PlayerInput input = GetComponent<PlayerInput>();

        attackAction = input.actions.FindAction("Player/Attack", true);
        reloadAction = input.actions.FindAction("Player/Reload", true);
        selectRifleAction = input.actions.FindAction(
            "Player/SelectRifle", true);
        selectPistolAction = input.actions.FindAction(
            "Player/SelectPistol", true);

        grabber = GetComponent<PlayerGrabber>();

        rifle.ammo = rifle.definition.magazineCapacity;
        pistol.ammo = pistol.definition.magazineCapacity;

        equipped = null;
        rifle.view.SetActive(false);
        pistol.view.SetActive(false);
    }

    private bool IsConfigured(WeaponSlot slot)
    {
        return slot != null &&
            slot.definition != null &&
            slot.view != null &&
            slot.muzzle != null;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
        {
            readyToFire = false;
            return;
        }

        bool controlsActive =
            Application.isFocused &&
            Cursor.lockState == CursorLockMode.Locked;

        // Switching happens before advancing the reload timer.
        if (controlsActive && !grabber.IsHolding)
        {
            if (ownsRifle && selectRifleAction.WasPressedThisFrame() &&
                equipped != rifle)
            {
                Equip(rifle);
                return;
            }

            if (ownsPistol && selectPistolAction.WasPressedThisFrame() &&
                equipped != pistol)
            {
                Equip(pistol);
                return;
            }
        }

        if (grabber.IsHolding || equipped == null)
        {
            CancelReload();
            readyToFire = false;
            return;
        }

        if (IsReloading)
        {
            reloadTimeRemaining -= Time.deltaTime;

            if (reloadTimeRemaining <= 0f)
            {
                IsReloading = false;
                equipped.ammo = equipped.definition.magazineCapacity;

                Debug.Log($"{WeaponName}: reload complete.", this);
                ReportAmmo();
            }
        }

        if (!controlsActive)
        {
            readyToFire = false;
            return;
        }

        // Restart an empty-magazine reload after dropping a prop
        // or switching back to an empty weapon.
        if (equipped.ammo == 0 && !IsReloading)
        {
            BeginReload();
        }

        if (reloadAction.WasPressedThisFrame())
        {
            BeginReload();
        }

        if (IsReloading)
        {
            return;
        }

        // Require release after cursor capture, carrying or switching.
        if (!readyToFire)
        {
            if (!attackAction.IsPressed())
            {
                readyToFire = true;
            }

            return;
        }

        bool wantsToFire = equipped.definition.automatic
            ? attackAction.IsPressed()
            : attackAction.WasPressedThisFrame();

        if (!wantsToFire ||
            equipped.ammo <= 0 ||
            Time.time < equipped.nextShotTime)
        {
            return;
        }

        equipped.nextShotTime = Time.time +
            Mathf.Max(0.01f, equipped.definition.firingInterval);

        equipped.ammo--;
        Fire();
        ReportAmmo();

        if (equipped.ammo == 0)
        {
            BeginReload();
        }
    }

    private void Equip(WeaponSlot selected)
    {
        CancelReload();

        equipped = selected;

        rifle.view.SetActive(selected == rifle);
        pistol.view.SetActive(selected == pistol);

        readyToFire = false;

        Debug.Log($"Selected {WeaponName}.", this);
        ReportAmmo();
    }

    public bool AcquireRifle()
    {
        if (ownsRifle) return false;
        ownsRifle = true;
        rifle.ammo = rifle.definition.magazineCapacity;
        Equip(rifle);
        return true;
    }

    public bool AcquirePistol()
    {
        if (ownsPistol) return false;
        ownsPistol = true;
        pistol.ammo = pistol.definition.magazineCapacity;
        Equip(pistol);
        return true;
    }

    private void BeginReload()
    {
        if (IsReloading ||
            equipped.ammo >= equipped.definition.magazineCapacity)
        {
            return;
        }

        IsReloading = true;
        reloadTimeRemaining = Mathf.Max(
            0.1f, equipped.definition.reloadDuration);

        Debug.Log(
            $"{WeaponName}: reloading for " +
            $"{reloadTimeRemaining:F1} seconds.", this);
    }

    private void CancelReload()
    {
        if (!IsReloading)
        {
            return;
        }

        IsReloading = false;
        reloadTimeRemaining = 0f;

        // No ammunition is awarded for an unfinished reload.
        Debug.Log($"{WeaponName}: reload cancelled.", this);
    }

    private void ReportAmmo()
    {
        Debug.Log(
            $"{WeaponName} ammunition: " +
            $"{equipped.ammo}/{equipped.definition.magazineCapacity}",
            this);
    }

    private void OnDisable()
    {
        CancelReload();
        readyToFire = false;
    }

    private void Fire()
    {
        GunDefinition gun = equipped.definition;
        Transform muzzle = equipped.muzzle;

        Ray aimRay = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        Vector3 aimPoint = aimRay.GetPoint(gun.range);

        if (Physics.Raycast(
            aimRay,
            out RaycastHit aimHit,
            gun.range,
            hittableLayers,
            QueryTriggerInteraction.Ignore))
        {
            aimPoint = aimHit.point;
        }

        NoiseSystem.Emit(muzzle.position, gun.noiseStrength, gameObject);

        Vector3 shotDirection = aimPoint - muzzle.position;
        float shotDistance = shotDirection.magnitude;

        if (shotDistance < 0.001f)
        {
            return;
        }

        if (Physics.Raycast(
            muzzle.position,
            shotDirection.normalized,
            out RaycastHit hit,
            Mathf.Min(shotDistance + 0.02f, gun.range),
            hittableLayers,
            QueryTriggerInteraction.Ignore))
        {
            IDamageable target =
                hit.collider.GetComponentInParent<IDamageable>();

            target?.TakeDamage(gun.damage, gameObject);

            Debug.Log(
                $"{WeaponName}: shot hit {hit.collider.name}.", this);
        }
        else
        {
            Debug.Log($"{WeaponName}: shot missed.", this);
        }
    }
}
