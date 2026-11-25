using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ImpactNoise : MonoBehaviour
{
    [SerializeField] private float minimumImpactSpeed = 2f;
    [SerializeField] private float noiseMultiplier = 1f;
    [SerializeField] private float cooldown = 0.4f;
    [SerializeField] private bool logNoise = true;
    [Tooltip("Ignore impacts for this long after the prop is enabled, so a prop settling " +
             "onto the floor at level start or restart does not produce a fake noise.")]
    [SerializeField, Min(0f)] private float settleTime = 1f;

    private float nextNoiseTime;

    private void OnEnable() => nextNoiseTime = Time.time + settleTime;

    private void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minimumImpactSpeed ||
            Time.time < nextNoiseTime)
        {
            return;
        }

        nextNoiseTime = Time.time + cooldown;

        Vector3 position = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        float strength = impactSpeed * noiseMultiplier;

        NoiseSystem.Emit(position, strength, gameObject);

        if (logNoise)
        {
            Debug.Log(
                $"{name}: impact noise, strength {strength:F1}, " +
                $"position {position}", this);
        }
    }
}