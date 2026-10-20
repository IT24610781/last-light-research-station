using System;
using System.Collections.Generic;
using System.Linq;
using LastLight.AI.Movement;
using UnityEngine;

namespace LastLight.AI.Director
{
    /// <summary>
    /// Unity-side wiring for the invisible Encounter Director: gathers observations
    /// (player condition, mission progress, robot population, bay safety), asks the
    /// brain for a decision, and activates a pooled robot when the brain chooses to.
    /// </summary>
    public class EncounterDirector : MonoBehaviour
    {
        [Serializable]
        public class PoolEntry
        {
            public RobotType type;
            [Tooltip("Pre-placed robots of this type, inactive at start.")]
            public GameObject[] instances;
        }

        [SerializeField] private DirectorSettings settings = new DirectorSettings();

        [Tooltip("Service bays where robots may emerge. Never include the arrival room.")]
        [SerializeField] private Transform[] bays;
        [SerializeField] private PoolEntry[] pool;

        [Tooltip("Leave empty to find the object tagged 'Player'.")]
        [SerializeField] private Transform player;
        [SerializeField] private float playerEyeHeight = 0.6f;
        [Tooltip("Layers that block the player's view of a bay.")]
        [SerializeField] private LayerMask viewBlockers = ~0;

        [Header("Data sources (optional until teammates' systems exist)")]
        [Tooltip("Component implementing IPlayerStatus.")]
        [SerializeField] private MonoBehaviour playerStatusSource;
        [Tooltip("Component implementing IMissionProgress.")]
        [SerializeField] private MonoBehaviour missionProgressSource;
        [Tooltip("Component implementing IPathProvider (Member A's A*). Empty = straight lines.")]
        [SerializeField] private MonoBehaviour pathProviderComponent;

        [SerializeField] private float decisionInterval = 1f;

        [Header("Debug")]
        [SerializeField] private bool showOverlay = true;

        /// <summary>Raised after a successful activation (UI can play a bay sound or indicator).</summary>
        public event Action<RobotType, Vector3> RobotActivated;

        public DirectorBrain Brain { get; private set; }
        public bool IsPaused { get; private set; }

        private IPlayerStatus playerStatus;
        private IMissionProgress missionProgress;
        private IPathProvider paths;
        private readonly List<BayInfo> bayInfos = new List<BayInfo>();
        private readonly List<Vector3> pathBuffer = new List<Vector3>();
        private float decisionTimer;

        private void Awake()
        {
            playerStatus = playerStatusSource as IPlayerStatus;
            missionProgress = missionProgressSource as IMissionProgress;
            paths = pathProviderComponent as IPathProvider ?? new StraightLinePathProvider();

            // Pooled robots wait inactive until the Director chooses them.
            foreach (PoolEntry entry in pool ?? new PoolEntry[0])
                foreach (GameObject instance in entry.instances)
                    if (instance != null) instance.SetActive(false);
        }

        private void Start()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) player = found.transform;
            }
            Brain = new DirectorBrain(settings, Time.time);
        }

        private void Update()
        {
            if (IsPaused || Brain == null || player == null) return;

            decisionTimer -= Time.deltaTime;
            if (decisionTimer > 0f) return;
            decisionTimer = decisionInterval;

            DirectorObservation observation = Observe();
            DirectorDecision decision = Brain.Decide(observation);
            if (decision.Activate) TryActivate(decision);
        }

        private DirectorObservation Observe()
        {
            int typeCount = Enum.GetValues(typeof(RobotType)).Length;
            var activeByType = new int[typeCount];
            var availableByType = new bool[typeCount];
            for (int t = 0; t < typeCount; t++)
            {
                activeByType[t] = DirectorRobotTag.CountActive((RobotType)t);
                availableByType[t] = FindInactive((RobotType)t) != null;
            }

            bayInfos.Clear();
            foreach (Transform bay in bays ?? new Transform[0])
            {
                if (bay == null) continue;
                Vector3 flat = bay.position - player.position;
                flat.y = 0f;
                bool hasRoute = paths.TryFindPath(bay.position, player.position, pathBuffer, out float routeLength);
                bayInfos.Add(new BayInfo
                {
                    Position = bay.position,
                    VisibleToPlayer = IsVisibleToPlayer(bay.position),
                    DistanceToPlayer = flat.magnitude,
                    HasRoute = hasRoute,
                    RouteLength = routeLength
                });
            }

            return new DirectorObservation
            {
                Now = Time.time,
                PlayerHealth01 = playerStatus != null ? playerStatus.Health01 : 1f,
                TimeSinceDamaged = playerStatus != null ? playerStatus.TimeSinceDamaged : 999f,
                RelaysOnline = missionProgress != null ? missionProgress.RelaysOnline : 0,
                RelaysTotal = missionProgress != null ? missionProgress.RelaysTotal : 2,
                ActiveByType = activeByType,
                AvailableByType = availableByType,
                Bays = bayInfos
            };
        }

        /// <summary>A bay is in view if it is inside the player's view angle and nothing blocks the sight line.</summary>
        private bool IsVisibleToPlayer(Vector3 bayPosition)
        {
            Vector3 eye = player.position + Vector3.up * playerEyeHeight;
            Vector3 target = bayPosition + Vector3.up;
            Vector3 flatDirection = target - eye;
            flatDirection.y = 0f;

            if (Vector3.Angle(player.forward, flatDirection) > settings.playerViewAngle * 0.5f) return false;
            return !Physics.Linecast(eye, target, viewBlockers, QueryTriggerInteraction.Ignore);
        }

        private GameObject FindInactive(RobotType type)
        {
            foreach (PoolEntry entry in pool ?? new PoolEntry[0])
            {
                if (entry.type != type) continue;
                foreach (GameObject instance in entry.instances)
                    if (instance != null && !instance.activeSelf) return instance;
            }
            return null;
        }

        private void TryActivate(in DirectorDecision decision)
        {
            GameObject instance = FindInactive(decision.Type);
            if (instance == null || decision.BayIndex < 0 || decision.BayIndex >= bayInfos.Count) return;

            Vector3 position = bayInfos[decision.BayIndex].Position;
            Vector3 towardsPlayer = player.position - position;
            towardsPlayer.y = 0f;
            Quaternion rotation = towardsPlayer.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(towardsPlayer)
                : Quaternion.identity;

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            if (instance.TryGetComponent(out ISpawnableAgent spawnable))
                spawnable.ResetAgentAt(position, rotation);

            // Count only after the activation actually happened; deaths never refund it.
            Brain.RecordActivation(Time.time);
            RobotActivated?.Invoke(decision.Type, position);
            Debug.Log($"[Director] {decision.Reason}");
        }

        // Pause and reset

        public void SetPaused(bool paused) => IsPaused = paused;

        /// <summary>Clears counters and returns all pooled robots to the inactive pool.</summary>
        public void ResetDirector()
        {
            foreach (PoolEntry entry in pool ?? new PoolEntry[0])
                foreach (GameObject instance in entry.instances)
                    if (instance != null) instance.SetActive(false);

            Brain?.Reset(Time.time);
            decisionTimer = 0f;
            IsPaused = false;
        }

        // Debug

        private void OnDrawGizmos()
        {
            if (bays == null) return;
            for (int i = 0; i < bays.Length; i++)
            {
                if (bays[i] == null) continue;
                bool rejected = Brain != null && Brain.Candidates.Any(c => c.Label == $"Bay {i}" && c.RejectedReason != null);
                Gizmos.color = rejected ? Color.red : Color.green;
                Gizmos.DrawWireCube(bays[i].position + Vector3.up, new Vector3(1.5f, 2f, 1.5f));
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || Brain == null) return;

            var text = new System.Text.StringBuilder();
            text.AppendLine("<b>Encounter Director</b>");
            text.AppendLine($"Activations {Brain.ActivationsUsed}/{settings.activationBudget}   " +
                            $"Active {DirectorRobotTag.Active.Count}/{settings.maxActiveRobots}   " +
                            $"Cooldown {Brain.CooldownRemaining(Time.time):0}s");
            text.AppendLine($"Pressure desired {Brain.DesiredPressure:0.00}  current {Brain.CurrentPressure:0.00}  readiness {Brain.PlayerReadiness:0.00}");
            text.AppendLine($"Decision: {Brain.LastReason}");

            // Valid options sorted by score, then rejected ones with their reasons.
            foreach (DirectorCandidate c in Brain.Candidates.Where(c => c.RejectedReason == null).OrderByDescending(c => c.Score).Take(6))
                text.AppendLine($"  {c.Label}: {c.Score:0.00}");
            foreach (DirectorCandidate c in Brain.Candidates.Where(c => c.RejectedReason != null).Take(6))
                text.AppendLine($"  {c.Label}: rejected ({c.RejectedReason})");

            var area = new Rect(Screen.width - 470, 10, 460, 90 + 18 * Mathf.Min(12, Brain.Candidates.Count));
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);
            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(area.x + 8, area.y + 4, area.width - 16, area.height), text.ToString(), style);
        }
    }
}
