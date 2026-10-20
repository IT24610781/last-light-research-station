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
    private void Update()
    {
        if (Time.timeScale == 0f) return;
        RepairableAlly target = selector.CurrentTarget;
        if (ownHealth.IsDead || motor.IsPaused || sensor.ImmediateDanger ||
            !selector.isActiveAndEnabled || !movement.ReadyToRepair || !CanRepair(target))
        { StopRepairing(); return; }
        if (RepairingTarget != target)
        {
            StopRepairing(); RepairingTarget = target; IsRepairing = true;
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
    private void StopRepairing()
    {
        bool wasRepairing = IsRepairing;
        IsRepairing = false; RepairingTarget = null; accumulatedTime = 0f;
        if (wasRepairing) RepairStateChanged?.Invoke(false);
    }
    private void OnDisable() { StopRepairing(); }
}
