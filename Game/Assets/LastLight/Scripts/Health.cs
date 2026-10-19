using UnityEngine;

public class Health : MonoBehaviour
{
    public float MaximumHealth = 100f;
    public float CurrentHealth = 100f;
    
    public bool IsDead => CurrentHealth <= 0;

    public void Heal(float amount)
    {
        if (IsDead) return;
        
        CurrentHealth += amount;
        if (CurrentHealth > MaximumHealth) 
        {
            CurrentHealth = MaximumHealth;
        }
        Debug.Log("Player healed! Current Health: " + CurrentHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        
        CurrentHealth -= amount;
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            Debug.Log("Player is Dead!");
        }
    }
}