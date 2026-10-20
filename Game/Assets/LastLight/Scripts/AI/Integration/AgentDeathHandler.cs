using UnityEngine;

namespace LastLight.AI.Integration
{
    /// <summary>
    /// When a robot's Health reaches zero, it immediately stops thinking and
    /// moving, then is removed after a short delay (time for a death animation).
    /// Once removed it no longer counts as living for the Director, and the
    /// Director never reuses it. A full restart reloads the level.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class AgentDeathHandler : MonoBehaviour
    {
        [Tooltip("Seconds before the robot is removed (time for a death animation).")]
        [SerializeField] private float removeDelay = 1.5f;

        private Health health;

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable() => health.Died += OnDied;

        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(GameObject source)
        {
            // Stop decisions and movement now (brain, motor, sensor, attack).
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
                if (behaviour != this && behaviour is not Health) behaviour.enabled = false;

            Destroy(gameObject, removeDelay);
        }
    }
}
