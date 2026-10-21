using UnityEngine;

public sealed class StationWeaponPickup : MonoBehaviour, IInteractable
{
    public enum WeaponType { Pistol, Rifle }

    [SerializeField] private WeaponType weapon;

    public void Configure(WeaponType type) => weapon = type;

    public void Interact()
    {
        PlayerShooter shooter = FindFirstObjectByType<PlayerShooter>();
        if (shooter == null) return;

        bool acquired = weapon == WeaponType.Rifle
            ? shooter.AcquireRifle()
            : shooter.AcquirePistol();

        if (acquired) gameObject.SetActive(false);
    }
}
