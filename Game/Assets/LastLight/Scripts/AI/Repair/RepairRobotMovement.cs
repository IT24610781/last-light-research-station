using System.Collections.Generic;
using UnityEngine;
using LastLight.AI.Movement;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Health), typeof(RepairTargetSelector), typeof(AgentMotor))]
public class RepairRobotMovement : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float targetMoveThreshold = 0.5f;
    [Tooltip("Known safe waiting/retreat positions. Empty uses the starting position.")]
    [SerializeField] private Transform[] supportPositions = new Transform[0];
    [SerializeField, Min(0.1f)] private float supportArrivalDistance = 0.4f;
    [SerializeField, Min(0.1f)] private float blockedRetrySeconds = 3f;
    [Header("Support utility weights")]
    [SerializeField, Min(0f)] private float separationWeight = 4f;
    [SerializeField, Min(0f)] private float supportDangerPenalty = 50f;
    [SerializeField, Min(0f)] private float supportRoutePenalty = 1f;

    public enum RepairState { Waiting, Approaching, ReadyToRepair, ReturningToSupport, Retreating, Blocked, Dead }
    public RepairState State { get; private set; }
    public bool ReadyToRepair => isActiveAndEnabled && State == RepairState.ReadyToRepair;
    public string MovementReason { get; private set; } = "Idle";
    public Vector3 Destination { get; private set; }
    public IReadOnlyList<string> SupportScores => supportScores;

    private Health health;
    private RepairTargetSelector selector;
    private RepairRoutePlanner planner;
    private RepairRobotSensor sensor;
    private AgentMotor motor;
    private readonly List<Vector3> route = new List<Vector3>();
    private readonly Dictionary<int, float> blockedSupports = new Dictionary<int, float>();
    private readonly List<string> supportScores = new List<string>();
    private RepairableAlly followedTarget;
    private Vector3 lastTargetPosition, home;
    private float nextRouteTime;
    private bool hasRoute, supporting;
    private int supportIndex = -1;

    private void Awake()
    {
        health = GetComponent<Health>(); selector = GetComponent<RepairTargetSelector>();
        planner = GetComponent<RepairRoutePlanner>(); sensor = GetComponent<RepairRobotSensor>();
        motor = GetComponent<AgentMotor>(); home = transform.position;
    }
    private void OnEnable() { nextRouteTime = 0f; hasRoute = false; }
    private void Update()
    {
        if (health.IsDead)
        {
            Stop(); State = RepairState.Dead; MovementReason = "Dead";
            motor.enabled = false; return;
        }
        if (Time.timeScale == 0f || motor.IsPaused) return;
        if (!selector.isActiveAndEnabled) { Stop(); State = RepairState.Waiting; return; }
        if (sensor.ImmediateDanger) { MoveToSupport(true); return; }
        RepairableAlly target = selector.CurrentTarget;
        if (!selector.Eligible(target)) { MoveToSupport(false); return; }
        if (supporting || target != followedTarget)
        {
            Stop(); supporting = false; followedTarget = target; nextRouteTime = 0f;
        }
        if (CanReachForRepair(target))
        {
            motor.Stop(); motor.FaceTowards(target.RepairPosition); hasRoute = false;
            State = RepairState.ReadyToRepair; MovementReason = "In range with clear line of sight";
            return;
        }
        if (motor.Status == MovementStatus.Blocked ||
            (hasRoute && motor.Status == MovementStatus.Arrived))
        {
            selector.ReportUnreachable(target, "Movement blocked or repair position invalid");
            Stop(); State = RepairState.Blocked; MovementReason = "Reconsidering other allies"; return;
        }
        bool moved = Vector3.Distance(lastTargetPosition, target.RepairPosition) >= targetMoveThreshold;
        if ((!hasRoute || moved) && Time.time >= nextRouteTime)
        {
            nextRouteTime = Time.time + repathInterval;
            if (!planner.TryPlanAlly(target, route, out float cost))
            {
                selector.ReportUnreachable(target, "Route no longer available"); Stop();
                State = RepairState.Blocked; return;
            }
            Destination = route[route.Count - 1]; motor.SetPath(route);
            hasRoute = true; lastTargetPosition = target.RepairPosition;
            MovementReason = $"Approaching {target.name}; route length {cost:F1}m";
        }
        State = RepairState.Approaching;
    }
    public bool CanReachForRepair(RepairableAlly target)
    {
        if (target == null) return false;
        RepairRobotRepair repair = GetComponent<RepairRobotRepair>();
        if (repair != null) return repair.CanRepair(target);
        return Vector3.Distance(sensor.BodyPosition, target.RepairPosition) <= 2f &&
            RepairVisibility.Clear(sensor.EyePosition, target.RepairPosition,
                transform, target.transform, ~0);
    }
    private void MoveToSupport(bool retreat)
    {
        if (!supporting)
        { Stop(); supporting = true; supportIndex = -1; nextRouteTime = 0f; }
        if (motor.Status == MovementStatus.Blocked)
        {
            blockedSupports[supportIndex] = Time.time + blockedRetrySeconds;
            Stop(); supporting = true; nextRouteTime = 0f;
        }
        if (hasRoute && Vector3.Distance(transform.position, Destination) <= supportArrivalDistance)
        {
            motor.Stop(); hasRoute = false;
            State = retreat ? RepairState.Retreating : RepairState.Waiting;
            MovementReason = retreat ? "Holding reachable support position while danger persists" :
                "Waiting at support; no ally needs reachable repairs";
            nextRouteTime = Mathf.Max(nextRouteTime, Time.time + repathInterval);
        }
        if (Time.time < nextRouteTime) return;
        nextRouteTime = Time.time + repathInterval;
        float bestScore = float.NegativeInfinity;
        supportScores.Clear();
        int bestIndex = -2;
        Vector3 bestPoint = transform.position;
        var bestPath = new List<Vector3>();
        int count = supportPositions.Length;
        for (int i = -1; i < count; i++)
        {
            if (i >= 0 && (supportPositions[i] == null || !supportPositions[i].gameObject.activeInHierarchy)) continue;
            if (blockedSupports.TryGetValue(i, out float retry) && Time.time < retry) continue;
            Vector3 point = i == -1 ? home : supportPositions[i].position;
            if (!planner.TryPlanPoint(point, route, out float cost))
            { supportScores.Add((i == -1 ? "Home" : supportPositions[i].name) + ": unreachable"); continue; }
            float danger = sensor.DangerAt(point + planner.BodyOffset);
            float separation = sensor.HasThreat ? Vector3.Distance(point + planner.BodyOffset,
                sensor.LastObservedThreat) : Vector3.Distance(point, home);
            float score = (retreat ? separation * separationWeight : 0f) -
                danger * supportDangerPenalty - cost * supportRoutePenalty;
            supportScores.Add($"{(i == -1 ? "Home" : supportPositions[i].name)}: " +
                $"score {score:F1}, route {cost:F1}m, danger {danger:F2}");
            if (score <= bestScore) continue;
            bestScore = score; bestIndex = i; bestPoint = point;
            bestPath.Clear(); bestPath.AddRange(route);
        }
        if (bestIndex == -2)
        {
            Stop(); supporting = true; State = RepairState.Blocked;
            MovementReason = "No reachable support position: hold and retry"; return;
        }
        bool different = supportIndex != bestIndex || !hasRoute ||
            Vector3.Distance(Destination, bestPoint) > targetMoveThreshold;
        supportIndex = bestIndex; Destination = bestPoint;
        State = retreat ? RepairState.Retreating : RepairState.ReturningToSupport;
        MovementReason = retreat ? "Retreating using observed danger and reachable support scores" :
            "Returning to reachable support position";
        MovementReason += $"; chosen score {bestScore:F1}";
        if (Vector3.Distance(transform.position, Destination) <= supportArrivalDistance)
        {
            motor.Stop(); hasRoute = false;
            State = retreat ? RepairState.Retreating : RepairState.Waiting; return;
        }
        if (different) { motor.SetPath(bestPath); hasRoute = true; }
    }
    private void Stop()
    {
        motor.Stop(); motor.ClearFaceTarget(); hasRoute = false; followedTarget = null;
    }
    private void OnDisable()
    {
        if (motor != null) Stop();
        supporting = false;
    }
}
