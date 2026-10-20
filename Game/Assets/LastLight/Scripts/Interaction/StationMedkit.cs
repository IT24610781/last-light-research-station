using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Single-use medical case; full-health and dead players cannot consume it.</summary>
public sealed class StationMedkit : MonoBehaviour, IInteractable
{
    [SerializeField, Min(1f)] private float healing = 35f;

    public void Interact()
    {
        var player = FindFirstObjectByType<PlayerInput>();
        if (player == null || !player.TryGetComponent<Health>(out var health) ||
            health.IsDead || health.CurrentHealth >= health.MaximumHealth) return;
        if (Vector3.Distance(player.transform.position, transform.position) > 3.5f) return;
        health.Heal(healing);
        gameObject.SetActive(false);
    }
}
