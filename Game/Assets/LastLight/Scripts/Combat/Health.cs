using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maximumHealth = 100f;

    public float MaximumHealth => maximumHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    // Sends current and maximum health to interested systems.
    public event Action<float, float> HealthChanged;

    // Sends the source of the lethal damage.
    public event Action<GameObject> Died;

    private void Awake()
    {
        maximumHealth = Mathf.Max(1f, maximumHealth);
        CurrentHealth = maximumHealth;
    }

    private void OnValidate()
    {
        maximumHealth = Mathf.Max(1f, maximumHealth);
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || !IsValidAmount(amount))
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        HealthChanged?.Invoke(CurrentHealth, maximumHealth);

        if (IsDead)
        {
            Died?.Invoke(source);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead ||
            CurrentHealth >= maximumHealth ||
            !IsValidAmount(amount))
        {
            return;
        }

        CurrentHealth = Mathf.Min(
            maximumHealth, CurrentHealth + amount);

        HealthChanged?.Invoke(CurrentHealth, maximumHealth);
    }

    private bool IsValidAmount(float amount)
    {
        return amount > 0f &&
            !float.IsNaN(amount) &&
            !float.IsInfinity(amount);
    }
}