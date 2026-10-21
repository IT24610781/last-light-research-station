using UnityEngine;
using LastLight.AI.Movement;

namespace LastLight.AI.Guard
{
    // Visual feedback only. Damage, ammunition and navigation remain owned by the agent.
    [RequireComponent(typeof(TacticalGuardAgent), typeof(Health), typeof(AgentMotor))]
    public sealed class TacticalGuardPresentation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LineRenderer tracer;
        private TacticalGuardAgent agent;
        private Health health;
        private AgentMotor motor;
        private float flashUntil;
        private Vector3 shotTarget;
        private string state;
        public string CurrentVisualState => state;
        public bool TracerVisible => tracer != null && tracer.enabled;

        private void Awake()
        {
            agent = GetComponent<TacticalGuardAgent>();
            health = GetComponent<Health>();
            motor = GetComponent<AgentMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable() { agent.AttackPerformed += OnShot; }
        private void OnDisable()
        {
            agent.AttackPerformed -= OnShot;
            if (tracer != null) tracer.enabled = false;
        }

        private void OnShot(TacticalGuardAgent sender, Vector3 target)
        {
            if (health.IsDead || agent.IsPaused) return;
            shotTarget = target + Vector3.up;
            flashUntil = Time.time + 0.07f;
            if (animator != null)
            {
                animator.Play("Attack", 0, 0f);
                state = "Attack";
            }
        }

        private void LateUpdate()
        {
            bool paused = agent.IsPaused || motor.IsPaused || Time.timeScale == 0f;
            if (animator != null)
            {
                animator.speed = paused ? 0f : 1f;
                if (!paused)
                {
                    string next = health.IsDead ? "Death" :
                        motor.MeasuredSpeed > 0.08f ? "Walk" :
                        agent.IsReloading ? "Reload" :
                        agent.LastDecision != null && agent.LastDecision.Action == GuardAction.Attack
                            ? "Attack" : "Idle";
                    if (state != next)
                    {
                        animator.CrossFadeInFixedTime(next, next == "Death" ? .08f : .15f);
                        state = next;
                    }
                    if (next == "Walk") animator.speed = Mathf.Clamp(motor.MeasuredSpeed / .8f, .5f, 2.5f);
                }
            }
            if (tracer == null) return;
            tracer.enabled = !paused && !health.IsDead && muzzle != null && Time.time < flashUntil;
            if (tracer.enabled)
            {
                tracer.SetPosition(0, muzzle.position);
                tracer.SetPosition(1, shotTarget);
            }
        }
    }
}
