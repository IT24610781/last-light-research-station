using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.AI.Movement
{
    /// <summary>
    /// TEST ONLY: sends hand-placed waypoints to an AgentMotor so movement can be
    /// tested before Member A's A* service exists. Later, the AI will call
    /// motor.SetPath(...) with real A* results instead.
    ///
    /// Controls in Play mode:
    ///   Space = send the route again
    ///   T     = toggle smooth / abrupt (snap) movement
    ///   X     = stop
    /// </summary>
    public class AgentMotorTester : MonoBehaviour
    {
        [SerializeField] private AgentMotor motor;
        [SerializeField] private Transform[] waypoints;
        [Tooltip("Start the route again automatically after arriving.")]
        [SerializeField] private bool loop = true;

        private void OnEnable()
        {
            if (motor != null) motor.StatusChanged += OnStatusChanged;
        }

        private void OnDisable()
        {
            // Always unsubscribe, otherwise old listeners stay alive after restart.
            if (motor != null) motor.StatusChanged -= OnStatusChanged;
        }

        private void Start()
        {
            SendRoute();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) SendRoute();
            if (keyboard.xKey.wasPressedThisFrame) motor.Stop();
            if (keyboard.tKey.wasPressedThisFrame)
            {
                motor.SnapMode = !motor.SnapMode;
                Debug.Log($"[AgentMotorTester] Snap mode: {motor.SnapMode}");
            }
        }

        private void SendRoute()
        {
            if (motor == null || waypoints == null || waypoints.Length == 0)
            {
                Debug.LogWarning("[AgentMotorTester] Assign the motor and waypoints in the Inspector.");
                return;
            }

            var points = new List<Vector3>();
            foreach (Transform waypoint in waypoints)
            {
                if (waypoint != null) points.Add(waypoint.position);
            }
            motor.SetPath(points);
        }

        private void OnStatusChanged(MovementStatus status)
        {
            Debug.Log($"[AgentMotorTester] Status: {status}");

            if (status == MovementStatus.Arrived && loop)
                SendRoute();
        }
    }
}
