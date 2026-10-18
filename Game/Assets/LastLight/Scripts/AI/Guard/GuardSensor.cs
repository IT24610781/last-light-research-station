using UnityEngine;

namespace LastLight.AI.Guard
{
    /// <summary>
    /// The Guard's senses. It reports only what the Guard could genuinely know:
    /// whether it can see the player right now, and the last place the player was seen
    /// (kept for a limited time, then forgotten). It never exposes the hidden player's
    /// real position.
    /// Exposure and cover checks are measured against that remembered threat position.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GuardSensor : MonoBehaviour
    {
        [Tooltip("Point the Guard sees from. Defaults to this object, raised to eye height.")]
        [SerializeField] private Transform eye;
        [SerializeField] private float defaultEyeHeight = 1.5f;
        [Tooltip("Leave empty to find the object tagged 'Player' automatically.")]
        [SerializeField] private Transform player;
        [Tooltip("Height on the player the sight line aims at.")]
        [SerializeField] private float playerAimHeight = 1f;
        [Tooltip("Layers that block sight (walls, doors, props, and the player itself).")]
        [SerializeField] private LayerMask sightLayers = ~0;
        [SerializeField] private float viewDistance = 20f;
        [Tooltip("Total width of the view cone in degrees.")]
        [SerializeField] private float fieldOfView = 150f;
        [Tooltip("Seconds the last seen position is remembered after losing sight.")]
        [SerializeField] private float threatMemorySeconds = 5f;

        private Collider body;
        private bool hasThreat;
        private Vector3 lastKnownThreat;
        private float lastSeenTime;

        public float ViewDistance { get { return viewDistance; } }
        public Transform Player { get { return player; } }

        /// <summary>True if the player was seen on the latest sensing update.</summary>
        public bool HasVisibleTarget { get; private set; }

        /// <summary>The player's position, valid only while HasVisibleTarget is true.</summary>
        public Vector3 TargetPosition { get; private set; }

        private void Awake()
        {
            body = GetComponent<Collider>();
        }

        private void Start()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
            }
        }

        public Vector3 EyePosition
        {
            get { return eye != null ? eye.position : transform.position + Vector3.up * defaultEyeHeight; }
        }

        /// <summary>Call a few times per second with the gameplay clock.</summary>
        public void UpdateSensing(float now)
        {
            Vector3 seenAt;
            HasVisibleTarget = CanSeePlayer(out seenAt);

            if (HasVisibleTarget)
            {
                TargetPosition = seenAt;
                lastKnownThreat = seenAt;
                lastSeenTime = now;
                hasThreat = true;
            }
            else if (hasThreat && now - lastSeenTime > threatMemorySeconds)
            {
                hasThreat = false; // memory expired: the Guard no longer knows where the player is
            }
        }

        /// <summary>The last position the player was seen at, if still remembered.</summary>
        public bool TryGetThreat(out Vector3 position)
        {
            position = lastKnownThreat;
            return hasThreat;
        }

        public void ForgetThreat()
        {
            hasThreat = false;
            HasVisibleTarget = false;
        }

        /// <summary>True if the player is in range, inside the view cone and not behind cover.</summary>
        public bool CanSeePlayer(out Vector3 playerPosition)
        {
            playerPosition = default(Vector3);
            if (player == null || !player.gameObject.activeInHierarchy) return false;

            Vector3 eyePosition = EyePosition;
            Vector3 target = player.position + Vector3.up * playerAimHeight;
            Vector3 toTarget = target - eyePosition;

            if (toTarget.magnitude > viewDistance) return false;

            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            if (Vector3.Angle(transform.forward, flatToTarget) > fieldOfView * 0.5f) return false;

            // Line of sight: the first thing the ray hits must be the player.
            RaycastHit hit;
            if (Physics.Linecast(eyePosition, target, out hit, sightLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != player && !hit.transform.IsChildOf(player)) return false;
            }

            playerPosition = player.position;
            return true;
        }

        /// <summary>
        /// How exposed the Guard is to a threat at the given position: the fraction of three
        /// body points (low, middle, high) that the threat could see. 0 = fully covered, 1 = wide open.
        /// </summary>
        public float ComputeExposure(Vector3 threatPosition)
        {
            Vector3 from = threatPosition + Vector3.up * playerAimHeight;
            int visible = 0;
            if (BodyPointVisible(from, 0.15f)) visible++;
            if (BodyPointVisible(from, 0.5f)) visible++;
            if (BodyPointVisible(from, 0.85f)) visible++;
            return visible / 3f;
        }

        /// <summary>
        /// True if a wall or prop blocks the line from the threat to a robot standing at "point".
        /// Used to decide whether a cover point really protects.
        /// </summary>
        public bool IsPositionProtectedFrom(Vector3 point, Vector3 threatPosition)
        {
            Vector3 from = threatPosition + Vector3.up * playerAimHeight;
            Vector3 chest = point + Vector3.up * (body.bounds.size.y * 0.6f);
            return Physics.Linecast(from, chest, sightLayers, QueryTriggerInteraction.Ignore);
        }

        private bool BodyPointVisible(Vector3 from, float heightFraction)
        {
            Bounds bounds = body.bounds;
            Vector3 point = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, heightFraction), bounds.center.z);

            RaycastHit hit;
            if (Physics.Linecast(from, point, out hit, sightLayers, QueryTriggerInteraction.Ignore))
            {
                // Hitting the Guard's own body means nothing stood in between.
                return hit.transform == transform || hit.transform.IsChildOf(transform);
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePosition = EyePosition;
            Gizmos.color = Color.yellow;
            Quaternion left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f);
            Quaternion right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f);
            Gizmos.DrawLine(eyePosition, eyePosition + left * transform.forward * viewDistance);
            Gizmos.DrawLine(eyePosition, eyePosition + right * transform.forward * viewDistance);

            if (Application.isPlaying && hasThreat)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(lastKnownThreat, 0.4f);
            }
        }
    }
}
