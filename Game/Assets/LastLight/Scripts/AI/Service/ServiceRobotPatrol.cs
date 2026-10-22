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

        private readonly List<Vector3> path = new List<Vector3>();
        private IPathProvider pathProvider;
        private AgentMotor motor;
        private int patrolIndex;
        private float resumeAt;

        private static readonly int SpeedId = Animator.StringToHash("Speed");

        private void Awake()
        {
            motor = GetComponent<AgentMotor>();
            pathProvider = pathProviderComponent as IPathProvider ?? new StraightLinePathProvider();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            resumeAt = Time.time + 0.5f;
        }

        private void Update()
        {
            if (animator != null)
                animator.SetFloat(SpeedId, motor.MeasuredSpeed, 0.12f, Time.deltaTime);

            if (patrolPoints == null || patrolPoints.Length == 0) return;
            if (motor.Status == MovementStatus.Moving) return;

            if (motor.Status == MovementStatus.Arrived)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                resumeAt = Time.time + waitAtStop;
                motor.Stop();
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
                motor.SetPath(path);
            else
                resumeAt = Time.time + 1f;
        }

        public void Configure(MonoBehaviour provider, Transform[] points)
        {
            pathProviderComponent = provider;
            patrolPoints = points;
        }
    }
}