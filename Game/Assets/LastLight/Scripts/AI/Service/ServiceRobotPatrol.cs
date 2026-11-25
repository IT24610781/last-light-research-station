using System.Collections.Generic;
using LastLight.AI.Movement;
using UnityEngine;

namespace LastLight.AI.Service
{
    [RequireComponent(typeof(CharacterController), typeof(AgentMotor))]
    public sealed class ServiceRobotPatrol : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour pathProviderComponent;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float waitAtStop = 1.5f;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.5f)] private float serviceDuration = 3f;
        public bool IsServicing { get; private set; }
        public int CompletedStops { get; private set; }
        public string StatusReason { get; private set; } = "Waiting for service route";

        private readonly List<Vector3> path = new List<Vector3>();
        private IPathProvider pathProvider;
        private AgentMotor motor;
        private int patrolIndex;
        private float resumeAt;
        private float serviceEndsAt;
        private Health health;
        private bool shutdown;

        private static readonly int SpeedId = Animator.StringToHash("Speed");

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            health = GetComponent<Health>();
            pathProvider = pathProviderComponent as IPathProvider ?? new StraightLinePathProvider();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            resumeAt = Time.time + 0.5f;
            IsServicing = false;
            shutdown = false;
        }

        private void Update()
        {
            bool paused = Time.timeScale == 0f || motor.IsPaused;
            if (animator != null) animator.speed = paused ? 0f : 1f;
            if (health != null && health.IsDead)
            {
                motor.Stop();
                IsServicing = false;
                StatusReason = "Shutdown";
                if (!shutdown && animator != null) animator.SetBool("Shutdown", true);
                shutdown = true;
                return;
            }
            if (paused) return;
            if (animator != null)
                animator.SetFloat(SpeedId, motor.MeasuredSpeed, 0.12f, Time.deltaTime);

            if (patrolPoints == null || patrolPoints.Length == 0) return;
            if (IsServicing)
            {
                if (Time.time < serviceEndsAt) return;
                IsServicing = false;
                CompletedStops++;
                AdvanceStop();
                return;
            }
            if (motor.Status == MovementStatus.Moving) return;

            if (motor.Status == MovementStatus.Arrived)
            {
                motor.Stop();
                Transform stop = patrolPoints[patrolIndex];
                if (stop != null) motor.FaceTowards(stop.position + stop.forward);
                IsServicing = true;
                serviceEndsAt = Time.time + serviceDuration;
                StatusReason = "Servicing " + (stop != null ? stop.name : "station");
                if (animator != null) animator.SetTrigger("Deliver");
                return;
            }
            if (motor.Status == MovementStatus.Blocked)
            {
                StatusReason = "Blocked service stop: selecting another station";
                AdvanceStop();
                return;
            }

            if (Time.time < resumeAt) return;
            Transform target = patrolPoints[patrolIndex];
            if (target == null)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                return;
            }

            if (pathProvider.TryFindPath(transform.position, target.position, path, out _))
            {
                StatusReason = "Travelling to " + target.name;
                motor.SetPath(path);
            }
            else
            {
                StatusReason = "Unreachable service stop: selecting another station";
                AdvanceStop();
            }
        }

        private void AdvanceStop()
        {
            motor.Stop();
            motor.ClearFaceTarget();
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            resumeAt = Time.time + waitAtStop;
        }

        public void Configure(MonoBehaviour provider, Transform[] points)
        {
            pathProviderComponent = provider;
            pathProvider = provider as IPathProvider ?? new StraightLinePathProvider();
            patrolPoints = points;
        }
    }
}
