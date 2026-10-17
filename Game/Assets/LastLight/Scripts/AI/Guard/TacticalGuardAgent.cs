using System;
using System.Collections.Generic;
using System.Text;
using LastLight.AI.Movement;
using UnityEngine;

namespace LastLight.AI.Guard
{
    /// <summary>
    /// Unity-side wiring for the Tactical Guard. It senses, builds a GuardObservation,
    /// asks GuardUtilityBrain for a decision a few times per second, then turns the decision
    /// into path requests and AgentMotor commands. All decision logic lives in the brain.
    /// Uses its own gameplay clock, which stops while paused.
    /// </summary>
    [RequireComponent(typeof(AgentMotor), typeof(Health), typeof(GuardSensor))]
    public class TacticalGuardAgent : MonoBehaviour
    {
        // Cover quality = share for being protected + share for a short route.
        private const float CoverProtectionShare = 0.6f;
        // Small bonus for the cover already reserved, so the Guard does not swap between two similar spots.
        private const float CurrentCoverBonus = 0.15f;

        [Header("Decision")]
        [SerializeField] private GuardWeights weights = new GuardWeights();
        [Tooltip("An action is kept at least this long before another can replace it.")]
        [SerializeField] private float minActionDuration = 0.6f;
        [Tooltip("A new action must beat the current one's score by this much to replace it.")]
        [SerializeField] private float switchMargin = 0.15f;
        [Tooltip("Seconds between decisions. Movement stays smooth every frame regardless.")]
        [SerializeField] private float decisionInterval = 0.2f;

        [Header("Navigation")]
        [Tooltip("Optional: a component implementing IPathProvider (WaypointNavigationService). Empty = straight lines.")]
        [SerializeField] private MonoBehaviour pathProviderComponent;
        [Tooltip("Safe positions the Guard can fall back to when badly hurt.")]
        [SerializeField] private Transform[] retreatPoints;
        [SerializeField] private float coverSearchRadius = 15f;
        [Tooltip("Route length (m) treated as 'far' when scoring route cost.")]
        [SerializeField] private float routeCostReference = 25f;
        [Tooltip("Counts as arrived when this close (m) to the destination.")]
        [SerializeField] private float arriveRadius = 0.6f;
        [Tooltip("How long a blocked or unreachable destination is ignored.")]
        [SerializeField] private float blockedRetrySeconds = 3f;

        [Header("Weapon")]
        [SerializeField] private int magazineSize = 12;
        [SerializeField] private float fireInterval = 0.5f;
        [SerializeField] private float reloadDuration = 2f;
        [SerializeField] private float damage = 8f;
        [SerializeField, Range(0f, 1f)] private float accuracy = 0.65f;

        [Header("Debug")]
        [SerializeField] private bool showOverlay = true;

        /// <summary>Raised when the Guard fires (combat feedback such as sound can listen here).</summary>
        public event Action<TacticalGuardAgent, Vector3> AttackPerformed;

        public GuardDecision LastDecision { get; private set; }
        public bool IsPaused { get; private set; }
        public int Ammo { get { return ammo; } }
        public bool IsReloading { get { return reloading; } }

        private AgentMotor motor;
        private Health health;
        private GuardSensor sensor;
        private IPathProvider paths;
        private GuardUtilityBrain brain;

        private readonly List<Vector3> pathBuffer = new List<Vector3>();
        private readonly List<Vector3> scratchPath = new List<Vector3>();
        private readonly Dictionary<int, float> blockedUntil = new Dictionary<int, float>();

        private float gameplayTime;
        private float decisionTimer;

        private int ammo;
        private bool reloading;
        private float reloadEndTime;
        private float lastShotTime = -999f;

        private GuardAction currentAction = GuardAction.Hold;
        private CoverPoint reservedCover;
        private CoverPoint bestCover;
        private Transform bestRetreat;

        private bool hasGoal;
        private Vector3 goal;
        private Transform goalKey;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            health = GetComponent<Health>();
            sensor = GetComponent<GuardSensor>();

            paths = pathProviderComponent as IPathProvider;
            if (paths == null)
            {
                if (pathProviderComponent != null)
                    Debug.LogWarning(name + ": path provider does not implement IPathProvider; using straight lines.", this);
                paths = new StraightLinePathProvider();
            }

            brain = new GuardUtilityBrain(weights, minActionDuration, switchMargin);
            ammo = magazineSize;
            startPosition = transform.position;
            startRotation = transform.rotation;

            // Stagger decisions so several robots do not all think on the same frame.
            decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
        }

        private void OnEnable()
        {
            motor.StatusChanged += OnMotorStatusChanged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            motor.StatusChanged -= OnMotorStatusChanged;
            health.Died -= OnDied;
            ReleaseCover();
        }

        private void Update()
        {
            if (IsPaused || health.IsDead) return;

            gameplayTime += Time.deltaTime;
            UpdateReload();

            // Firing is checked every frame so the fire interval is exact, not rounded to decision ticks.
            if (currentAction == GuardAction.Attack) TryFire();

            decisionTimer -= Time.deltaTime;
            if (decisionTimer > 0f) return;
            decisionTimer = decisionInterval;

            sensor.UpdateSensing(gameplayTime);
            GuardObservation observation = BuildObservation();
            LastDecision = brain.Decide(observation, gameplayTime);
            currentAction = LastDecision.Action;
            Apply(currentAction);
        }

        // ---------- Observation ----------

        private GuardObservation BuildObservation()
        {
            GuardObservation obs = new GuardObservation();
            obs.HasVisibleTarget = sensor.HasVisibleTarget;
            obs.Health01 = health.MaximumHealth > 0f ? health.CurrentHealth / health.MaximumHealth : 0f;
            obs.Ammo01 = magazineSize > 0 ? (float)ammo / magazineSize : 0f;
            obs.TargetDistance01 = sensor.HasVisibleTarget
                ? Mathf.Clamp01(Vector3.Distance(transform.position, sensor.TargetPosition) / sensor.ViewDistance)
                : 1f;

            bestCover = null;
            bestRetreat = null;

            // Cover and exposure only make sense against a threat the Guard actually remembers.
            Vector3 threat;
            if (sensor.TryGetThreat(out threat))
            {
                obs.Exposure01 = sensor.ComputeExposure(threat);

                float coverQuality;
                bestCover = FindBestCover(threat, out coverQuality);
                obs.CoverAvailable = bestCover != null;
                obs.CoverQuality01 = coverQuality;

                float retreatQuality;
                bestRetreat = FindBestRetreat(threat, out retreatQuality);
                obs.RetreatAvailable = bestRetreat != null;
                obs.RetreatQuality01 = retreatQuality;
            }

            return obs;
        }

        private CoverPoint FindBestCover(Vector3 threat, out float quality)
        {
            CoverPoint best = null;
            float bestSelection = float.NegativeInfinity;
            quality = 0f;

            IReadOnlyList<CoverPoint> all = CoverPoint.All;
            for (int i = 0; i < all.Count; i++)
            {
                CoverPoint cover = all[i];
                if (!cover.IsAvailableTo(gameObject)) continue;        // occupied by another Guard
                if (IsBlocked(cover.transform)) continue;               // recently unreachable
                if (Vector3.Distance(transform.position, cover.Position) > coverSearchRadius) continue;
                if (!sensor.IsPositionProtectedFrom(cover.Position, threat)) continue; // not actually cover

                float length;
                if (!paths.TryFindPath(transform.position, cover.Position, scratchPath, out length)) continue; // unreachable

                float cost01 = Mathf.Clamp01(length / routeCostReference);
                float q = CoverProtectionShare + (1f - CoverProtectionShare) * (1f - cost01);
                float selection = q + (cover == reservedCover ? CurrentCoverBonus : 0f);

                if (selection > bestSelection)
                {
                    bestSelection = selection;
                    best = cover;
                    quality = q;
                }
            }
            return best;
        }

        private Transform FindBestRetreat(Vector3 threat, out float quality)
        {
            Transform best = null;
            float bestQuality = -1f;
            quality = 0f;
            if (retreatPoints == null) return null;

            float myDistance = Vector3.Distance(transform.position, threat);
            for (int i = 0; i < retreatPoints.Length; i++)
            {
                Transform point = retreatPoints[i];
                if (point == null || IsBlocked(point)) continue;

                float distanceFromThreat = Vector3.Distance(point.position, threat);
                if (distanceFromThreat <= myDistance) continue; // a retreat must lead away from the threat

                float length;
                if (!paths.TryFindPath(transform.position, point.position, scratchPath, out length)) continue;

                float away01 = Mathf.Clamp01(distanceFromThreat / (coverSearchRadius * 2f));
                float cost01 = Mathf.Clamp01(length / routeCostReference);
                float q = 0.5f * away01 + 0.5f * (1f - cost01);

                if (q > bestQuality)
                {
                    bestQuality = q;
                    best = point;
                    quality = q;
                }
            }
            return best;
        }

        // ---------- Acting on the decision ----------

        private void Apply(GuardAction action)
        {
            switch (action)
            {
                case GuardAction.Attack:
                    if (motor.Status == MovementStatus.Moving) motor.Stop();
                    hasGoal = false;
                    motor.FaceTowards(sensor.TargetPosition);
                    break;

                case GuardAction.TakeCover:
                    if (bestCover != null)
                    {
                        if (reservedCover != bestCover)
                        {
                            ReleaseCover();
                            if (bestCover.TryReserve(gameObject)) reservedCover = bestCover;
                        }

                        if (reservedCover != null)
                        {
                            bool arrived = MoveToward(reservedCover.Position, reservedCover.transform);
                            Vector3 threat;
                            if (arrived && sensor.TryGetThreat(out threat)) motor.FaceTowards(threat);
                        }
                    }
                    break;

                case GuardAction.Reload:
                    // Reloading runs on a timer and does not interrupt movement.
                    StartReload();
                    break;

                case GuardAction.Retreat:
                    if (bestRetreat != null)
                    {
                        ReleaseCover(); // leaving this cover point
                        MoveToward(bestRetreat.position, bestRetreat);
                    }
                    break;

                default: // Hold
                    if (motor.Status == MovementStatus.Moving) motor.Stop();
                    hasGoal = false;
                    Vector3 lastKnown;
                    if (sensor.TryGetThreat(out lastKnown)) motor.FaceTowards(lastKnown);
                    else motor.ClearFaceTarget();
                    break;
            }
        }

        /// <summary>Moves toward a destination. Returns true once the Guard is close enough.</summary>
        private bool MoveToward(Vector3 destination, Transform key)
        {
            motor.ClearFaceTarget();
            motor.SpeedMultiplier = 1f;

            Vector3 flat = destination - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= arriveRadius)
            {
                if (motor.Status == MovementStatus.Moving) motor.Stop();
                hasGoal = false;
                return true;
            }

            bool needsPath = !hasGoal
                || (goal - destination).sqrMagnitude > 1f
                || motor.Status == MovementStatus.Stopped;

            if (needsPath)
            {
                if (paths.TryFindPath(transform.position, destination, pathBuffer, out _))
                {
                    motor.SetPath(pathBuffer);
                    goal = destination;
                    goalKey = key;
                    hasGoal = true;
                }
                else
                {
                    MarkBlocked(key); // no route at all: ignore this destination for a while
                    hasGoal = false;
                }
            }
            return false;
        }

        private void OnMotorStatusChanged(MovementStatus status)
        {
            if (status == MovementStatus.Blocked && hasGoal)
            {
                MarkBlocked(goalKey);
                hasGoal = false;
                motor.Stop();
            }
        }

        private bool IsBlocked(Transform key)
        {
            if (key == null) return false;
            float until;
            return blockedUntil.TryGetValue(key.GetInstanceID(), out until) && gameplayTime < until;
        }

        private void MarkBlocked(Transform key)
        {
            if (key == null) return;
            blockedUntil[key.GetInstanceID()] = gameplayTime + blockedRetrySeconds;
        }

        // ---------- Weapon ----------

        private void StartReload()
        {
            if (reloading || ammo >= magazineSize) return;
            reloading = true;
            reloadEndTime = gameplayTime + reloadDuration;
        }

        private void UpdateReload()
        {
            if (reloading && gameplayTime >= reloadEndTime)
            {
                ammo = magazineSize;
                reloading = false;
            }
        }

        private void TryFire()
        {
            if (reloading || ammo <= 0 || !sensor.HasVisibleTarget) return;
            if (gameplayTime - lastShotTime < fireInterval) return;

            lastShotTime = gameplayTime;
            ammo--;
            motor.FaceTowards(sensor.TargetPosition);

            if (UnityEngine.Random.value <= accuracy && sensor.Player != null)
            {
                IDamageable target = sensor.Player.GetComponentInParent<IDamageable>();
                if (target == null) target = sensor.Player.GetComponentInChildren<IDamageable>();
                if (target != null) target.TakeDamage(damage, gameObject);
            }

            if (AttackPerformed != null) AttackPerformed(this, sensor.TargetPosition);
        }

        // ---------- Death, pause, reset ----------

        private void OnDied(GameObject source)
        {
            motor.Stop();
            hasGoal = false;
            ReleaseCover();
        }

        private void ReleaseCover()
        {
            if (reservedCover != null)
            {
                reservedCover.Release(gameObject);
                reservedCover = null;
            }
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            motor.IsPaused = paused;
        }

        /// <summary>Returns the Guard to its starting state for a level restart.</summary>
        public void ResetAgent()
        {
            ResetAgentAt(startPosition, startRotation);
        }

        public void ResetAgentAt(Vector3 position, Quaternion rotation)
        {
            startPosition = position;
            startRotation = rotation;
            if (brain == null) return; // Awake has not run yet; it will use this position

            brain.Reset();
            sensor.ForgetThreat();
            motor.ResetMotor();
            ReleaseCover();
            blockedUntil.Clear();

            ammo = magazineSize;
            reloading = false;
            lastShotTime = -999f;
            hasGoal = false;
            currentAction = GuardAction.Hold;
            LastDecision = null;

            // CharacterController overrides position changes while enabled.
            CharacterController controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;

            SetPaused(false);
        }

        // ---------- Debug ----------

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            if (reservedCover != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(reservedCover.Position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
            }
            if (hasGoal)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position + Vector3.up, goal + Vector3.up);
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || LastDecision == null) return;

            StringBuilder text = new StringBuilder();
            text.AppendLine("<b>" + name + "</b>  Action: " + LastDecision.Action);
            text.AppendLine("Reason: " + LastDecision.Reason);
            for (int i = 0; i < GuardUtilityBrain.ActionCount; i++)
            {
                GuardAction action = (GuardAction)i;
                string marker = action == LastDecision.Action ? "> " : "  ";
                text.AppendLine(LastDecision.IsValid(action)
                    ? marker + action + "  " + LastDecision.ScoreOf(action).ToString("0.00")
                    : marker + action + "  (invalid)");
            }
            text.AppendLine("HP " + health.CurrentHealth.ToString("0") + "/" + health.MaximumHealth.ToString("0")
                + "   Ammo " + ammo + "/" + magazineSize + (reloading ? " (reloading)" : ""));

            Rect area = new Rect(Screen.width - 440f, 10f, 430f, 190f);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);
            GUIStyle style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(area.x + 8f, area.y + 4f, area.width - 16f, area.height), text.ToString(), style);
        }
    }
}
