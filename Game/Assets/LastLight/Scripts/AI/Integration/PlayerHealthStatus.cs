using LastLight.AI.Director;
using UnityEngine;

namespace LastLight.AI.Integration
{
    /// <summary>
    /// Connects the Systems Engineer's Health component to the Encounter Director.
    /// Put this on the Player (next to Health) and drag it into the Director's
    /// "Player Status Source" slot.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHealthStatus : MonoBehaviour, IPlayerStatus
    {
        private Health health;
        // Starts "infinite" because Health may not have run Awake yet when OnEnable runs.
        private float lastHealth = float.PositiveInfinity;
        private float lastDamageTime = -999f;

        public float Health01 =>
            health != null && health.MaximumHealth > 0f ? health.CurrentHealth / health.MaximumHealth : 1f;

        public float TimeSinceDamaged => Time.time - lastDamageTime;

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable() => health.HealthChanged += OnHealthChanged;

        private void OnDisable() => health.HealthChanged -= OnHealthChanged;

        private void OnHealthChanged(float current, float maximum)
        {
            // Only a drop counts as damage; healing does not reset the recovery timer.
            if (current < lastHealth) lastDamageTime = Time.time;
            lastHealth = current;
        }
    }
}
