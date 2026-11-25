using System;
using UnityEngine;
using LastLight.AI.Movement;

[RequireComponent(typeof(Health), typeof(RepairTargetSelector), typeof(RepairRobotMovement))]
public class RepairRobotRepair : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float repairRange = 2f;
    [SerializeField, Min(0.1f)] private float healthPerSecond = 20f;
    [SerializeField, Min(0.05f)] private float repairInterval = 0.25f;
    [SerializeField] private LayerMask repairBlockingLayers = ~0;
    public bool IsRepairing { get; private set; }
    public float RepairRange => repairRange;
    public string StatusReason { get; private set; } = "Waiting for a valid repair target";
    public RepairableAlly RepairingTarget { get; private set; }
    // Member B can use this event for the repair animation/beam.
    public event Action<bool> RepairStateChanged;
    private Health ownHealth;
    private RepairTargetSelector selector;
    private RepairRobotMovement movement;
    private RepairRobotSensor sensor;
    private AgentMotor motor;
    private float accumulatedTime;

    private void Awake()
    {
        ownHealth = GetComponent<Health>(); selector = GetComponent<RepairTargetSelector>();
        movement = GetComponent<RepairRobotMovement>(); sensor = GetComponent<RepairRobotSensor>();
        motor = GetComponent<AgentMotor>();
    }
    private void OnEnable() { ownHealth.Died += HandleDeath; }
    private void HandleDeath(GameObject source) { StopRepairing("Repair robot died"); }
    private void Update()
    {
        if (Time.timeScale == 0f || motor.IsPaused) { StopRepairing("Paused"); return; }
        RepairableAlly target = selector.CurrentTarget;
        if (ownHealth.IsDead) { StopRepairing("Repair robot died"); return; }
        if (sensor.ImmediateDanger) { StopRepairing("Immediate observed danger"); return; }
        if (!selector.isActiveAndEnabled || !movement.ReadyToRepair || !CanRepair(target))
        { StopRepairing("Target unavailable, approaching, out of range or line of sight blocked"); return; }
        if (RepairingTarget != target)
        {
            StopRepairing(); RepairingTarget = target; IsRepairing = true;
            target.Health.HealthChanged += OnTargetHealthChanged;
            StatusReason = "Repairing " + target.name;
            RepairStateChanged?.Invoke(true);
        }
        accumulatedTime += Time.deltaTime;
        if (accumulatedTime < repairInterval) return;
        // One accumulated amount, revalidated immediately before the health effect.
        float amount = accumulatedTime * healthPerSecond;
        accumulatedTime = 0f;
        if (CanRepair(target) && !ownHealth.IsDead && !sensor.ImmediateDanger)
            target.Health.Heal(amount);
    }
    public bool CanRepair(RepairableAlly target)
    {
        if (!selector.Eligible(target)) return false;
        return Vector3.Distance(sensor.BodyPosition, target.RepairPosition) <= repairRange &&
            RepairVisibility.Clear(sensor.EyePosition, target.RepairPosition,
                transform, target.transform, repairBlockingLayers);
    }
    private void OnTargetHealthChanged(float current, float maximum)
    {
        if (current <= 0f || current >= maximum)
            StopRepairing(current <= 0f ? "Target died" : "Target fully repaired");
    }
    private void StopRepairing(string reason = "Repair stopped")
    {
        bool wasRepairing = IsRepairing;
        if (RepairingTarget != null) RepairingTarget.Health.HealthChanged -= OnTargetHealthChanged;
        IsRepairing = false; RepairingTarget = null; accumulatedTime = 0f;
        StatusReason = reason;
        if (wasRepairing) RepairStateChanged?.Invoke(false);
    }
    private void OnDisable()
    {
        if (ownHealth != null) ownHealth.Died -= HandleDeath;
        StopRepairing("Repair component disabled");
    }
}
