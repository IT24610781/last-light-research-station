using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ImpactNoise : MonoBehaviour
{
    [SerializeField] private float minimumImpactSpeed = 2f;
    [SerializeField] private float noiseMultiplier = 1f;
    [SerializeField] private float cooldown = 0.4f;
    [SerializeField] private bool logNoise = true;

    private float nextNoiseTime;

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