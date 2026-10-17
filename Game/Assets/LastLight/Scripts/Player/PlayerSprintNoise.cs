using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerSprintNoise : MonoBehaviour
{
    [SerializeField] private float noiseStrength = 5f;
    [SerializeField] private float noiseInterval = 0.4f;
    [SerializeField] private float minimumMovementSpeed = 1f;
    [SerializeField] private bool logNoise = true;

    private CharacterController controller;
    private PlayerMovement movement;
    private float nextNoiseTime;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
    }

    private void LateUpdate()
    {
        if (Time.timeScale == 0f ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        // Read actual movement after PlayerMovement has run.
        Vector3 horizontalVelocity = controller.velocity;
        horizontalVelocity.y = 0f;

        bool shouldProduceNoise =
            movement.IsSprinting &&
            controller.isGrounded &&
            horizontalVelocity.magnitude >= minimumMovementSpeed;

        if (!shouldProduceNoise)
        {
            nextNoiseTime = Time.time;
            return;
        }

        if (Time.time < nextNoiseTime)
        {
            return;
        }

        nextNoiseTime = Time.time + Mathf.Max(0.05f, noiseInterval);

        // Emit near the player's feet.
        Vector3 noisePosition = transform.TransformPoint(
            controller.center -
            Vector3.up * (controller.height * 0.5f));

        NoiseSystem.Emit(noisePosition, noiseStrength, gameObject);

        if (logNoise)
        {
            Debug.Log(
                $"{name}: sprint noise, strength {noiseStrength:F1}, " +
                $"position {noisePosition}", this);
        }
    }
}