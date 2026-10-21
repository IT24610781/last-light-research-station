using LastLight.AI.Movement;
using UnityEngine;

namespace LastLight.AI.Hunter
{
    /// <summary>
    /// Drives the Hunter's Animator from its real behaviour. It never moves the
    /// robot or makes decisions; it only shows what the motor and brain are doing.
    ///   Speed  = measured movement speed (a blocked robot does not look like it runs)
    ///   Attack = trigger fired by HunterAgent.AttackPerformed
    ///   Dead   = set when Health reports death
    /// Root motion is disabled: code moves the robot, animations play in place.
    /// </summary>
    [RequireComponent(typeof(AgentMotor), typeof(HunterAgent))]
    public class HunterPresentation : MonoBehaviour
    {
        [Tooltip("Leave empty to use the Animator on a child object.")]
        [SerializeField] private Animator animator;
        [Tooltip("Smoothing time (s) for the Speed parameter, so blends do not flicker.")]
        [SerializeField] private float speedDampTime = 0.1f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int DeadId = Animator.StringToHash("Dead");

        private AgentMotor motor;
        private HunterAgent hunter;
        private Health health; // optional

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            hunter = GetComponent<HunterAgent>();
            health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            hunter.AttackPerformed += OnAttack;
            if (health != null) health.Died += OnDied;

            // A pooled robot re-activated by the Director starts from a clean pose.
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private void OnDisable()
        {
            hunter.AttackPerformed -= OnAttack;
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            if (animator == null) return;

            bool paused = motor.IsPaused || Time.timeScale == 0f;
            animator.speed = paused ? 0f : 1f;
            if (paused) return;

            animator.SetFloat(SpeedId, motor.MeasuredSpeed, speedDampTime, Time.deltaTime);
        }

        private void OnAttack(HunterAgent attacker, Vector3 target)
        {
            if (animator != null) animator.SetTrigger(AttackId);
        }

        private void OnDied(GameObject source)
        {
            if (animator != null) animator.SetBool(DeadId, true);
        }
    }
}
