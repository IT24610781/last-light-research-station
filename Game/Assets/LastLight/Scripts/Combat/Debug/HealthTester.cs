using UnityEngine;

[RequireComponent(typeof(Health))]
public class HealthTester : MonoBehaviour
{
    private Health health;

    private void OnEnable()
    {
        health = GetComponent<Health>();

        health.HealthChanged += ReportHealth;
        health.Died += ReportDeath;
    }

    private void OnDisable()
    {
        if (health == null)
        {
            return;
        }

        health.HealthChanged -= ReportHealth;
        health.Died -= ReportDeath;
    }

    [ContextMenu("Test/Damage 10")]
    private void DamageTen()
    {
        if (CanTest())
        {
            health.TakeDamage(10f);
        }
    }

    [ContextMenu("Test/Damage 200")]
    private void DamageTwoHundred()
    {
        if (CanTest())
        {
            health.TakeDamage(200f);
        }
    }

    [ContextMenu("Test/Heal 10")]
    private void HealTen()
    {
        if (CanTest())
        {
            health.Heal(10f);
        }
    }

    [ContextMenu("Test/Heal 200")]
    private void HealTwoHundred()
    {
        if (CanTest())
        {
            health.Heal(200f);
        }
    }

    private bool CanTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play mode to test health.", this);
            return false;
        }

        return health != null;
    }

    private void ReportHealth(float current, float maximum)
    {
        Debug.Log(
            $"{name}: health {current:F0}/{maximum:F0}", this);
    }

    private void ReportDeath(GameObject source)
    {
        Debug.Log($"{name}: died.", this);
    }
}