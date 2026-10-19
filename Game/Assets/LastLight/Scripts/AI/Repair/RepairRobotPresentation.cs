using UnityEngine;
using LastLight.AI.Movement;

// Presentation follows authoritative AI state; it never applies healing or movement.
[RequireComponent(typeof(Health), typeof(AgentMotor), typeof(RepairRobotRepair))]
public sealed class RepairRobotPresentation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform toolTip;
    [SerializeField] private LineRenderer repairBeam;
    private Health health;
    private AgentMotor motor;
    private RepairRobotRepair repair;
    private string state;
    public string CurrentVisualState => state;
    public bool BeamVisible => repairBeam != null && repairBeam.enabled;

    private void Awake()
    {
        health = GetComponent<Health>();
        motor = GetComponent<AgentMotor>();
        repair = GetComponent<RepairRobotRepair>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        repair.RepairStateChanged += OnRepairChanged;
        health.Died += OnDeath;
    }

    private void LateUpdate()
    {
        if (animator == null) return;
        if (Time.timeScale == 0f || motor.IsPaused)
        {
            animator.speed = 0f;
            HideBeam();
            return;
        }
        animator.speed = 1f;
        bool repairing = !health.IsDead && repair.isActiveAndEnabled &&
            repair.IsRepairing && repair.RepairingTarget != null &&
            repair.CanRepair(repair.RepairingTarget);
        float speed = health.IsDead ? 0f : motor.MeasuredSpeed;
        animator.SetFloat("Speed", speed);
        animator.SetBool("Repairing", repairing);
        animator.SetBool("Dead", health.IsDead);
        string next = health.IsDead ? "Death" : repairing ? "Repair" : speed > 0.08f ? "Walk" : "Idle";
        if (next != state)
        {
            animator.CrossFadeInFixedTime(next, next == "Death" ? 0.08f : 0.15f);
            state = next;
        }
        if (repairBeam == null) return;
        repairBeam.enabled = repairing && toolTip != null;
        if (repairBeam.enabled)
        {
            repairBeam.SetPosition(0, toolTip.position);
            repairBeam.SetPosition(1, repair.RepairingTarget.RepairPosition);
        }
    }

    private void OnRepairChanged(bool active) { if (!active) HideBeam(); }
    private void OnDeath(GameObject source) { HideBeam(); }
    private void HideBeam() { if (repairBeam != null) repairBeam.enabled = false; }
    private void OnDisable()
    {
        if (repair != null) repair.RepairStateChanged -= OnRepairChanged;
        if (health != null) health.Died -= OnDeath;
        HideBeam();
    }
}
