using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Movement
{
    /// <summary>
    /// What the motor is doing right now. AI decision code reads this
    /// (for example, the Hunter reacts when the status becomes Arrived or Blocked).
    /// </summary>
    public enum MovementStatus
    {
        Stopped,
        Moving,  
        Arrived,
        Blocked  
    }

    /// <summary>
    /// Turns a list of path points (later supplied by Member A's A* service) into
    /// smooth, collision-aware movement. It does NOT decide where to go; that is
    /// the AI's job. It only executes the route and reports its status.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AgentMotor : MonoBehaviour
    {
        [Header("Speed (metres per second)")]
        [SerializeField] private float maxSpeed = 3.5f;
        [Tooltip("Speed gained per second when speeding up.")]
        [SerializeField] private float acceleration = 6f;
        [Tooltip("Speed lost per second when slowing down.")]
        [SerializeField] private float deceleration = 10f;

        [Header("Turning")]
        [Tooltip("Maximum turn rate in degrees per second.")]
        [SerializeField] private float turnSpeed = 270f;
        [Tooltip("If the robot faces more than this many degrees away from where it must go, it stops and turns in place.")]
        [SerializeField] private float turnInPlaceAngle = 60f;

        [Header("Path following")]
        [Tooltip("How close (m) the robot must get to a point before moving on to the next one.")]
        [SerializeField] private float waypointTolerance = 0.25f;
        [Tooltip("Start slowing down when this close (m) to the final point.")]
        [SerializeField] private float slowDownDistance = 1.5f;

        [Header("Blocked detection")]
        [Tooltip("Seconds of little progress before the robot reports Blocked.")]
        [SerializeField] private float blockedTimeout = 1f;
        [Tooltip("Actual speed below this fraction of the intended speed counts as 'little progress'.")]
        [SerializeField, Range(0f, 1f)] private float minProgressRatio = 0.25f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;

        [Header("Debug")]
        [Tooltip("Abrupt movement (instant speed and rotation) for before/after comparison videos.")]
        [SerializeField] private bool snapMode = false;

        //Public information other scripts can read

        /// <summary>Current movement status.</summary>
        public MovementStatus Status { get; private set; } = MovementStatus.Stopped;

        /// <summary>
        /// Real horizontal speed measured from how far the robot actually moved.
        /// The Animator should use THIS (not the requested speed), so a blocked
        /// robot does not look like it is running.
        /// </summary>
        public float MeasuredSpeed { get; private set; }

        /// <summary>Fraction of max speed to use (e.g. 0.5 for patrol, 1 for chase).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>When true, the motor freezes (used by game pause).</summary>
        public bool IsPaused { get; set; }

        /// <summary>Switch between smooth and abrupt movement at runtime.</summary>
        public bool SnapMode
        {
            get => snapMode;
            set => snapMode = value;
        }

        /// <summary>Raised whenever the status changes (e.g. Moving -> Arrived).</summary>
        public event Action<MovementStatus> StatusChanged;

        // Internal state

        private CharacterController controller;
        private readonly List<Vector3> path = new List<Vector3>();
        private int currentIndex;           // which path point we are heading to
        private float currentSpeed;         // the speed we are trying to move at
        private float verticalVelocity;     // for gravity
        private float lowProgressTimer;     // how long we have been "stuck"

        private bool hasLookTarget;         // should we face something while standing still?
        private Vector3 lookTarget;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        // Commands (called by AI code) 

        /// <summary>
        /// Give the motor a new route. Any previous route is replaced.
        /// </summary>
        public void SetPath(IReadOnlyList<Vector3> points)
        {
            path.Clear();
            if (points == null || points.Count == 0)
            {
                Stop();
                return;
            }

            path.AddRange(points);
            currentIndex = 0;
            lowProgressTimer = 0f;
            hasLookTarget = false;
            SetStatus(MovementStatus.Moving);
        }

        /// <summary>Stop moving immediately and forget the route.</summary>
        public void Stop()
        {
            path.Clear();
            currentSpeed = 0f;
            lowProgressTimer = 0f;
            SetStatus(MovementStatus.Stopped);
        }

        /// <summary>
        /// While standing still, smoothly turn to face a point (e.g. the player when attacking).
        /// </summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            lookTarget = worldPoint;
            hasLookTarget = true;
        }

        /// <summary>Stop facing a point.</summary>
        public void ClearFaceTarget()
        {
            hasLookTarget = false;
        }

        /// <summary>Return to a clean state (used when the level restarts).</summary>
        public void ResetMotor()
        {
            Stop();
            verticalVelocity = 0f;
            MeasuredSpeed = 0f;
            hasLookTarget = false;
            IsPaused = false;
        }

        // Per-frame update

        private void Update()
        {
            if (IsPaused)
            {
                MeasuredSpeed = 0f;
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Work out the horizontal movement for this frame.
            Vector3 horizontalMove = Vector3.zero;
            if (Status == MovementStatus.Moving)
            {
                horizontalMove = FollowPath(dt);
            }
            else
            {
                currentSpeed = 0f;
                if (hasLookTarget) TurnTowards(lookTarget - transform.position, dt);
            }

            // 2. Add gravity so the robot stays on the floor.
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small push keeps it grounded
            verticalVelocity += gravity * dt;

            // 3. Move with collisions. CharacterController.Move stops us at walls.
            Vector3 positionBefore = transform.position;
            controller.Move(horizontalMove + Vector3.up * (verticalVelocity * dt));

            // 4. Measure how far we REALLY moved (horizontally).
            Vector3 actualDelta = transform.position - positionBefore;
            actualDelta.y = 0f;
            MeasuredSpeed = actualDelta.magnitude / dt;

            // 5. Detect being stuck against something.
            if (Status == MovementStatus.Moving)
                CheckBlocked(dt);
        }

        /// <summary>
        /// Works out how far to move this frame along the route.
        /// Returns a movement vector in world space (already multiplied by dt).
        /// </summary>
        private Vector3 FollowPath(float dt)
        {
            // Skip every point we are already close enough to.
            Vector3 toTarget;
            float distance;
            while (true)
            {
                toTarget = path[currentIndex] - transform.position;
                toTarget.y = 0f; // ignore height; robots move on a flat floor
                distance = toTarget.magnitude;

                if (distance > waypointTolerance) break;

                bool wasFinalPoint = currentIndex == path.Count - 1;
                if (wasFinalPoint)
                {
                    currentSpeed = 0f;
                    path.Clear();
                    SetStatus(MovementStatus.Arrived);
                    return Vector3.zero;
                }
                currentIndex++;
            }

            Vector3 direction = toTarget / distance; // unit vector towards the point
            bool isFinalPoint = currentIndex == path.Count - 1;
            float topSpeed = maxSpeed * Mathf.Max(0f, SpeedMultiplier);

            // Abrupt mode: for the "before" comparison video 
            if (snapMode)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                currentSpeed = topSpeed;
                return direction * (topSpeed * dt);
            }

            // Smooth mode

            // Turn gradually towards the direction of travel.
            TurnTowards(direction, dt);

            // The more we face away from our direction, the slower we go.
            // At turnInPlaceAngle or more, desired speed is 0: we turn on the spot.
            float angle = Vector3.Angle(transform.forward, direction);
            float facingFactor = Mathf.Clamp01(1f - angle / turnInPlaceAngle);
            float desiredSpeed = topSpeed * facingFactor;

            // Slow down when approaching the final point, so we stop smoothly.
            if (isFinalPoint)
            {
                float arrivalFactor = Mathf.Clamp01(distance / slowDownDistance);
                desiredSpeed = Mathf.Min(desiredSpeed, topSpeed * arrivalFactor);
            }

            // Change speed gradually (acceleration / deceleration).
            float rate = desiredSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * dt);

            return direction * (currentSpeed * dt);
        }

        /// <summary>Rotate around the vertical axis towards a direction, limited by turnSpeed.</summary>
        private void TurnTowards(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * dt);
        }

        /// <summary>
        /// If we are trying to move but actually moving much slower for too long,
        /// something is in the way: report Blocked so the AI can request a new path.
        /// </summary>
        private void CheckBlocked(float dt)
        {
            bool tryingToMove = currentSpeed > 0.3f;
            bool littleProgress = MeasuredSpeed < currentSpeed * minProgressRatio;

            if (tryingToMove && littleProgress)
                lowProgressTimer += dt;
            else
                lowProgressTimer = 0f;

            if (lowProgressTimer >= blockedTimeout)
            {
                currentSpeed = 0f;
                lowProgressTimer = 0f;
                SetStatus(MovementStatus.Blocked);
            }
        }

        private void SetStatus(MovementStatus newStatus)
        {
            if (Status == newStatus) return;
            Status = newStatus;
            StatusChanged?.Invoke(newStatus);
        }

        // Debug drawing (Scene view) 

        private void OnDrawGizmos()
        {
            if (path.Count == 0) return;

            Gizmos.color = Color.cyan;
            Vector3 previous = transform.position;
            for (int i = currentIndex; i < path.Count; i++)
            {
                Gizmos.DrawLine(previous, path[i]);
                Gizmos.DrawWireSphere(path[i], waypointTolerance);
                previous = path[i];
            }
        }
    }
}
