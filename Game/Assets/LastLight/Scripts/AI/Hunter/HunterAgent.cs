using System;
using System.Collections.Generic;
using LastLight.AI.Movement;
using UnityEngine;

namespace LastLight.AI.Hunter
{
    /// <summary>
    /// Unity-side wiring for the Sound Hunter: collects observations from the sensor,
    /// asks the brain for a decision a few times per second, then turns that decision
    /// into a path request and motor commands. Contains no decision logic itself.
    /// </summary>
    [RequireComponent(typeof(AgentMotor), typeof(HunterSensor))]
    public class HunterAgent : MonoBehaviour
    {
        [SerializeField] private HunterSettings settings = new HunterSettings();
        [SerializeField] private Transform[] patrolPoints;

        [Tooltip("Optional: a component implementing IPathProvider (Member A's A*). Empty = straight-line fallback.")]
        [SerializeField] private MonoBehaviour pathProviderComponent;

        [Tooltip("Seconds between decisions. Movement stays smooth every frame regardless.")]
        [SerializeField] private float decisionInterval = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool showOverlay = true;

        /// <summary>Raised when the Hunter attacks (combat code can apply damage here).</summary>
        public event Action<HunterAgent, Vector3> AttackPerformed;

        public HunterBrain Brain { get; private set; }
        public HunterMemory Memory { get; private set; }
        public bool IsPaused { get; private set; }

        private AgentMotor motor;
        private HunterSensor sensor;
        private IPathProvider paths;
        private readonly List<Vector3> pathBuffer = new List<Vector3>();

        private int appliedGoalVersion = -1;
        private float decisionTimer;
        private float lastAttackTime = -999f;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            sensor = GetComponent<HunterSensor>();
            sensor.Settings = settings;

            paths = pathProviderComponent as IPathProvider;
            if (paths == null)
            {
                if (pathProviderComponent != null)
                    Debug.LogWarning($"{name}: path provider does not implement IPathProvider; using straight lines.", this);
                paths = new StraightLinePathProvider();
            }

            var patrolPositions = new List<Vector3>();
            if (patrolPoints != null)
                foreach (Transform point in patrolPoints)
                    if (point != null) patrolPositions.Add(point.position);

            Memory = new HunterMemory(settings.maxRememberedNoises, settings.noiseMergeRadius);
            Brain = new HunterBrain(settings, Memory, paths, patrolPositions.ToArray());

            startPosition = transform.position;
            startRotation = transform.rotation;
            // Stagger decisions so several robots do not all think on the same frame.
            decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
        }

        private void OnEnable()
        {
            sensor.NoiseHeard += OnNoiseHeard;
            motor.StatusChanged += OnMotorStatusChanged;
        }

        private void OnDisable()
        {
            sensor.NoiseHeard -= OnNoiseHeard;
            motor.StatusChanged -= OnMotorStatusChanged;
        }

        private void OnNoiseHeard(Vector3 position, float perceivedStrength, float timeHeard)
        {
            if (!IsPaused) Memory.AddNoise(position, perceivedStrength, timeHeard);
        }

        private void OnMotorStatusChanged(MovementStatus status)
        {
            Brain.NotifyMovement(status, appliedGoalVersion);
        }

        private void Update()
        {
            if (IsPaused) return;

            decisionTimer -= Time.deltaTime;
            if (decisionTimer > 0f) return;
            decisionTimer = decisionInterval;

            var observation = new HunterObservation { SelfPosition = transform.position };
            observation.PlayerVisible = sensor.CanSeePlayer(out observation.PlayerPosition);

            HunterIntent intent = Brain.Tick(observation, Time.time);
            Apply(intent);
        }

        private void Apply(in HunterIntent intent)
        {
            switch (intent.Action)
            {
                case HunterAction.MoveTo:
                    motor.ClearFaceTarget();
                    motor.SpeedMultiplier = intent.SpeedMultiplier;
                    if (intent.GoalVersion != appliedGoalVersion)
                    {
                        appliedGoalVersion = intent.GoalVersion;
                        if (paths.TryFindPath(transform.position, intent.Destination, pathBuffer, out _))
                            motor.SetPath(pathBuffer);
                        else
                            Brain.NotifyMovement(MovementStatus.Blocked, appliedGoalVersion); // no route at all
                    }
                    break;

                case HunterAction.Attack:
                    if (motor.Status == MovementStatus.Moving) motor.Stop();
                    motor.FaceTowards(intent.LookAt);
                    appliedGoalVersion = -1; // the next MoveTo must request a fresh path
                    if (Time.time - lastAttackTime >= settings.attackCooldown)
                    {
                        lastAttackTime = Time.time;
                        AttackPerformed?.Invoke(this, intent.LookAt);
                        Debug.Log($"[Hunter] {name} attacks at {intent.LookAt}");
                    }
                    break;

                default:
                    if (motor.Status == MovementStatus.Moving) motor.Stop();
                    appliedGoalVersion = -1;
                    break;
            }
        }

                                              // Pause and reset 

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            motor.IsPaused = paused;
        }

        /// <summary>Returns the Hunter to its starting state for a level restart.</summary>
        public void ResetAgent()
        {
            Memory.Clear();
            Brain.Reset();
            motor.ResetMotor();
            appliedGoalVersion = -1;
            lastAttackTime = -999f;

            // CharacterController overrides position changes while enabled.
            var controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;
            SetPaused(false);
        }

                                                    // Debug

        private void OnDrawGizmos()
        {
            if (Brain == null) return;

            foreach (ScoredNoise scored in Brain.LastScores)
            {
                if (scored.Noise == null) continue;
                Gizmos.color = scored.Rejected ? Color.grey
                             : scored.Noise == Brain.TargetNoise ? Color.green
                             : Color.yellow;
                Gizmos.DrawWireSphere(scored.Noise.Position, 0.3f + Mathf.Max(0f, scored.Score));
            }

            if (Memory.HasLastSeen)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(Memory.LastSeenPosition + Vector3.up * 0.1f, Vector3.one * 0.4f);
            }

            if (Brain.State == HunterState.Search)
            {
                Gizmos.color = Color.magenta;
                foreach (Vector3 point in Brain.SearchPoints) Gizmos.DrawWireSphere(point, 0.25f);
            }

            if (Brain.HasGoal)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position + Vector3.up, Brain.CurrentGoal + Vector3.up);
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || Brain == null) return;

            var text = new System.Text.StringBuilder();
            text.AppendLine($"<b>{name}</b>  State: {Brain.State}");
            text.AppendLine($"Reason: {Brain.Reason}");
            foreach (ScoredNoise s in Brain.LastScores)
            {
                if (s.Noise == null) continue;
                string marker = s.Noise == Brain.TargetNoise ? "> " : "  ";
                text.AppendLine(s.Rejected
                    ? $"{marker}noise REJECTED (unreachable)"
                    : $"{marker}noise S {s.Strength01:0.00}  R {s.Recency01:0.00}  C {s.Cost01:0.00}  = {s.Score:0.00}");
            }

            GUI.Label(new Rect(10, 10, 520, 220), text.ToString(), new GUIStyle(GUI.skin.label) { richText = true });
        }
    }
}
