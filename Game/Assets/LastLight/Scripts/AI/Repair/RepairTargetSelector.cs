using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Health), typeof(RepairRoutePlanner), typeof(RepairRobotSensor))]
public class RepairTargetSelector : MonoBehaviour
{
    [SerializeField] private RepairableAlly[] allies = new RepairableAlly[0];
    [Tooltip("Team health reports are available only inside this support radius. This grants no player tracking.")]
    [SerializeField, Min(0.1f)] private float supportRange = 25f;
    [SerializeField, Min(0.1f)] private float reconsiderInterval = 0.5f;
    [SerializeField, Min(0f)] private float missingHealthWeight = 100f;
    [Tooltip("Penalty per metre of the returned route, not straight-line target distance.")]
    [SerializeField, Min(0f)] private float distancePenaltyPerMetre = 2f;
    [SerializeField, Min(0f)] private float dangerPenalty = 30f;
    [SerializeField, Min(0f)] private float switchScoreMargin = 15f;
    [SerializeField, Min(0.1f)] private float unreachableRetrySeconds = 3f;
    [Tooltip("Debug only. Normal decisions use the sensor's observations.")]
    [SerializeField] private bool useTestDanger;
    [SerializeField] private bool logDecisions = true;

    public sealed class Candidate
    {
        public RepairableAlly Ally;
        public bool Eligible;
        public float MissingHealth, RouteCost, Danger, Score;
        public string Reason;
    }
    public RepairableAlly CurrentTarget { get; private set; }
    public string Decision { get; private set; } = "Waiting for first observation";
    public IReadOnlyList<Candidate> Candidates => candidates;
    public float SwitchScoreMargin => switchScoreMargin;
    public float SupportRange => supportRange;

    private readonly List<Candidate> candidates = new List<Candidate>();
    private readonly List<Vector3> scratchRoute = new List<Vector3>();
    private readonly Dictionary<RepairableAlly, float> retryAfter = new Dictionary<RepairableAlly, float>();
    private Health ownHealth;
    private RepairRoutePlanner planner;
    private RepairRobotSensor sensor;
    private float nextSelectionTime;
    private string lastLogKey;

    private void Awake()
    {
        ownHealth = GetComponent<Health>();
        planner = GetComponent<RepairRoutePlanner>();
        sensor = GetComponent<RepairRobotSensor>();
    }
    private void OnEnable()
    {
        nextSelectionTime = 0f; lastLogKey = null; retryAfter.Clear();
        ownHealth.Died += HandleDeath;
        if (!overlayOrder.Contains(this)) overlayOrder.Add(this);
    }
    private void OnDisable()
    {
        ownHealth.Died -= HandleDeath;
        overlayOrder.Remove(this);
        CurrentTarget = null;
    }
    private void HandleDeath(GameObject source)
    {
        CurrentTarget = null; Decision = "Dead: no decisions or repairs";
        enabled = false;
    }
    private void Update()
    {
        if (ownHealth.IsDead || Time.timeScale == 0f) return;
        if (sensor.ImmediateDanger)
        {
            CurrentTarget = null;
            Decision = "Immediate observed danger: retreat to a reachable support position";
            return;
        }
        if (CurrentTarget != null && !Eligible(CurrentTarget))
        { CurrentTarget = null; nextSelectionTime = 0f; }
        if (Time.time < nextSelectionTime) return;
        nextSelectionTime = Time.time + Mathf.Max(0.1f, reconsiderInterval);
        EvaluateNow();
    }
    public bool Eligible(RepairableAlly ally) => ally != null && ally.isActiveAndEnabled &&
        ally.Health != null && ally.Health.isActiveAndEnabled && ally.Health != ownHealth &&
        !ally.transform.IsChildOf(transform) && !transform.IsChildOf(ally.transform) &&
        Vector3.Distance(sensor.BodyPosition, ally.RepairPosition) <= supportRange &&
        !ally.Health.IsDead && ally.Health.CurrentHealth < ally.Health.MaximumHealth;

    public void ReportUnreachable(RepairableAlly ally, string reason)
    {
        if (ally == null) return;
        retryAfter[ally] = Time.time + Mathf.Max(0.1f, unreachableRetrySeconds);
        if (CurrentTarget == ally) CurrentTarget = null;
        nextSelectionTime = 0f;
        Decision = $"{ally.name}: {reason}; retry in {unreachableRetrySeconds:F1}s";
        if (logDecisions) Debug.Log($"{name}: {Decision}", this);
    }
    public void EvaluateNow()
    {
        if (!isActiveAndEnabled || ownHealth.IsDead || Time.timeScale == 0f) return;
        if (sensor.ImmediateDanger)
        {
            CurrentTarget = null;
            Decision = "Immediate observed danger: retreat to a reachable support position";
            return;
        }
        candidates.Clear();
        Candidate best = null, current = null;
        foreach (RepairableAlly ally in allies)
        {
            Candidate item = new Candidate { Ally = ally, Score = float.NegativeInfinity };
            candidates.Add(item);
            if (!Eligible(ally))
            {
                item.Reason = ally != null && Vector3.Distance(sensor.BodyPosition, ally.RepairPosition) > supportRange
                    ? $"Outside team report range ({supportRange:F1}m)"
                    : "Self, dead, full health, disabled or missing";
                continue;
            }
            if (retryAfter.TryGetValue(ally, out float retry) && Time.time < retry)
            { item.Reason = $"Blocked/unreachable: retry in {retry - Time.time:F1}s"; continue; }
            item.MissingHealth = ally.Health.MaximumHealth - ally.Health.CurrentHealth;
            if (!planner.TryPlanAlly(ally, scratchRoute, out float cost))
            {
                item.Reason = "No reachable repair position with clear line of sight";
                retryAfter[ally] = Time.time + unreachableRetrySeconds;
                continue;
            }
            item.RouteCost = cost;
            item.Danger = useTestDanger ? ally.TestDanger : sensor.DangerAt(ally.RepairPosition);
            item.Score = Score(item.MissingHealth / ally.Health.MaximumHealth, cost,
                item.Danger, missingHealthWeight, distancePenaltyPerMetre, dangerPenalty);
            item.Eligible = true;
            item.Reason = $"injury +{item.MissingHealth / ally.Health.MaximumHealth * missingHealthWeight:F1}, " +
                $"route -{cost * distancePenaltyPerMetre:F1}, danger -{item.Danger * dangerPenalty:F1}";
            if (ally == CurrentTarget) current = item;
            if (best == null || item.Score > best.Score) best = item;
        }
        bool retained = current != null && best != null && best != current &&
            !ShouldSwitch(current.Score, best.Score, switchScoreMargin);
        Candidate chosen = retained ? current : best;
        CurrentTarget = chosen != null ? chosen.Ally : null;
        Decision = chosen == null ? "No eligible reachable injured ally: wait at support" : retained
            ? $"Keep {current.Ally.name}: challenger improvement {best.Score - current.Score:F1} " +
                $"does not exceed {switchScoreMargin:F1}" :
            $"Choose {chosen.Ally.name}: score {chosen.Score:F1}; {chosen.Reason}";
        string key = (chosen != null ? chosen.Ally.GetInstanceID().ToString() : "none") + "/" + retained;
        if (logDecisions && key != lastLogKey) Debug.Log($"{name}: {Decision}", this);
        lastLogKey = key;
    }
    public static float Score(float missingFraction, float routeCost, float danger,
        float injuryWeight, float routeWeight, float dangerWeight) =>
        Mathf.Clamp01(missingFraction) * Mathf.Max(0f, injuryWeight) -
        Mathf.Max(0f, routeCost) * Mathf.Max(0f, routeWeight) -
        Mathf.Clamp01(danger) * Mathf.Max(0f, dangerWeight);
    public static bool ShouldSwitch(float current, float challenger, float margin) =>
        challenger - current > Mathf.Max(0f, margin);

    // ---------- In-game decision overlay (F3) ----------
    private static readonly List<RepairTargetSelector> overlayOrder = new List<RepairTargetSelector>();

    private void OnGUI()
    {
        if (!LastLight.Core.DebugOverlays.Repair || ownHealth == null) return;

        RepairRobotRepair repair = GetComponent<RepairRobotRepair>();
        var text = new System.Text.StringBuilder();
        text.AppendLine($"<b>{name}</b>  HP {ownHealth.CurrentHealth:0}/{ownHealth.MaximumHealth:0}" +
            $"   Target: {(CurrentTarget != null ? CurrentTarget.name : "none")}");
        text.AppendLine($"U = {missingHealthWeight:0}×missing − {distancePenaltyPerMetre:0}×route m − {dangerPenalty:0}×danger" +
            $"   (switch margin {switchScoreMargin:0}, range {supportRange:0} m)");
        text.AppendLine("Decision: " + Decision);
        if (repair != null)
            text.AppendLine((repair.IsRepairing ? "<color=#5CF2E0>REPAIRING</color>  " : "") + repair.StatusReason);
        int rows = 0;
        foreach (Candidate c in candidates)
        {
            if (c.Ally == null || rows >= 5) continue;
            string marker = c.Ally == CurrentTarget ? "> " : "  ";
            text.AppendLine(c.Eligible
                ? $"{marker}{c.Ally.name}: U {c.Score:0.0}  (missing {c.MissingHealth:0} HP, route {c.RouteCost:0.0} m, danger {c.Danger:0.00})"
                : $"{marker}{c.Ally.name}: rejected ({c.Reason})");
            rows++;
        }

        float height = 100f + 18f * rows;
        float top = Screen.height * 0.38f;
        foreach (RepairTargetSelector other in overlayOrder)
        {
            if (other == this) break;
            if (other.isActiveAndEnabled) top += 110f + 18f * Mathf.Min(5, other.candidates.Count);
        }
        Rect area = new Rect(10f, top, 640f, height);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        GUIStyle style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(area.x + 8f, area.y + 4f, area.width - 16f, area.height), text.ToString(), style);
    }
}
