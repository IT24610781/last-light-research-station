using UnityEngine;

namespace LastLight.AI.Service
{
    // Presentation only: the service route owns arrival and work timing.
    public sealed class ServiceRobotArm : MonoBehaviour
    {
        [SerializeField] private ServiceRobotPatrol service;
        [SerializeField] private Transform shoulder;
        [SerializeField] private Transform elbow;
        [SerializeField] private Transform leftFinger;
        [SerializeField] private Transform rightFinger;
        private Health health;
        private void Awake() { health = GetComponentInParent<Health>(); }
        private void LateUpdate()
        {
            if (Time.timeScale == 0f || service == null) return;
            bool working = service.IsServicing && (health == null || !health.IsDead);
            float cycle = Mathf.Sin(Time.time * 3f);
            float blend = 1f - Mathf.Exp(-8f * Time.deltaTime);
            if (shoulder != null) shoulder.localRotation = Quaternion.Slerp(shoulder.localRotation,
                Quaternion.Euler(working ? 40f + cycle * 12f : -20f, 0f, 0f), blend);
            if (elbow != null) elbow.localRotation = Quaternion.Slerp(elbow.localRotation,
                Quaternion.Euler(working ? -50f - cycle * 15f : -110f, 0f, 0f), blend);
            float opening = working ? 12f + 10f * (cycle + 1f) : 8f;
            if (leftFinger != null) leftFinger.localRotation = Quaternion.Euler(0f, 0f, opening);
            if (rightFinger != null) rightFinger.localRotation = Quaternion.Euler(0f, 0f, -opening);
        }
    }
}
