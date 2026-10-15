using System;
using UnityEngine;

namespace LastLight.AI.Hunter
{
    /// <summary>
    /// The Hunter's senses. Hearing listens to NoiseSystem and applies range and
    /// distance attenuation. Vision checks distance, field of view and line of sight.
    /// It only reports what the Hunter could genuinely sense; it never reveals a hidden player.
    /// </summary>
    public class HunterSensor : MonoBehaviour
    {
        [Tooltip("Point the Hunter sees from. Defaults to this object, raised to eye height.")]
        [SerializeField] private Transform eye;
        [SerializeField] private float defaultEyeHeight = 1.5f;

        [Tooltip("Leave empty to find the object tagged 'Player' automatically.")]
        [SerializeField] private Transform player;
        [Tooltip("Height on the player the Hunter aims its sight line at.")]
        [SerializeField] private float playerAimHeight = 1f;

        [Tooltip("Layers that block sight (walls, doors, props, and the player itself).")]
        [SerializeField] private LayerMask sightLayers = ~0;

        /// <summary>Set by HunterAgent so all tuning lives in one settings block.</summary>
        public HunterSettings Settings { get; set; }

        /// <summary>Raised with (noise position, perceived strength, time heard).</summary>
        public event Action<Vector3, float, float> NoiseHeard;

        private void Start()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
            }
        }

        private void OnEnable() => NoiseSystem.NoiseProduced += OnNoiseProduced;

        private void OnDisable() => NoiseSystem.NoiseProduced -= OnNoiseProduced;

        private void OnNoiseProduced(NoiseEvent noise)
        {
            if (Settings == null || noise.Source == gameObject) return;

            float distance = Vector3.Distance(transform.position, noise.Position);
            if (distance > Settings.hearingRange) return;

            // Linear attenuation: full strength at the source, zero at the edge of hearing range.
            float perceived = noise.Strength * (1f - distance / Settings.hearingRange);
            if (perceived < Settings.minPerceivedStrength) return;

            NoiseHeard?.Invoke(noise.Position, perceived, noise.TimeCreated);
        }

        public Vector3 EyePosition =>
            eye != null ? eye.position : transform.position + Vector3.up * defaultEyeHeight;

        /// <summary>True if the player is in range, inside the view cone and not behind cover.</summary>
        public bool CanSeePlayer(out Vector3 playerPosition)
        {
            playerPosition = default;
            if (player == null || Settings == null || !player.gameObject.activeInHierarchy) return false;

            Vector3 eyePosition = EyePosition;
            Vector3 target = player.position + Vector3.up * playerAimHeight;
            Vector3 toTarget = target - eyePosition;

            if (toTarget.magnitude > Settings.viewDistance) return false;

            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            if (Vector3.Angle(transform.forward, flatToTarget) > Settings.fieldOfView * 0.5f) return false;

            // Line of sight: the first thing the ray hits must be the player.
            if (Physics.Linecast(eyePosition, target, out RaycastHit hit, sightLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != player && !hit.transform.IsChildOf(player)) return false;
            }

            playerPosition = player.position;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (Settings == null) return;

            // View cone edges and hearing range.
            Vector3 eyePosition = EyePosition;
            Gizmos.color = Color.yellow;
            Quaternion left = Quaternion.Euler(0f, -Settings.fieldOfView * 0.5f, 0f);
            Quaternion right = Quaternion.Euler(0f, Settings.fieldOfView * 0.5f, 0f);
            Gizmos.DrawLine(eyePosition, eyePosition + left * transform.forward * Settings.viewDistance);
            Gizmos.DrawLine(eyePosition, eyePosition + right * transform.forward * Settings.viewDistance);

            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, Settings.hearingRange);
        }
    }
}
