using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpHeight = 1f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    [Header("Crouching")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float crouchSpeed = 2f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction crouchAction;

    private float verticalSpeed;
    private bool isCrouching;

    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingCameraPosition;
    public bool IsSprinting =>
    isActiveAndEnabled &&
    !isCrouching &&
    sprintAction != null &&
    sprintAction.IsPressed();

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        PlayerInput playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions.FindAction("Player/Move", true);
        sprintAction = playerInput.actions.FindAction("Player/Sprint", true);
        jumpAction = playerInput.actions.FindAction("Player/Jump", true);
        crouchAction = playerInput.actions.FindAction("Player/Crouch", true);

        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerMovement: assign Main Camera.", this);

            enabled = false;
            return;
        }

        standingHeight = controller.height;
        standingCenter = controller.center;
        standingCameraPosition = playerCamera.localPosition;

        crouchHeight = Mathf.Clamp(
            crouchHeight,
            controller.radius * 2f + 0.1f,
            standingHeight);
    }

    private void Update()
    {
        if (crouchAction.IsPressed())
        {
            SetCrouching(true);
        }
        else if (isCrouching && CanStandUp())
        {
            SetCrouching(false);
        }

        Vector2 input = Vector2.ClampMagnitude(
            moveAction.ReadValue<Vector2>(), 1f);

        Vector3 direction =
            transform.right * input.x +
            transform.forward * input.y;

        float speed = isCrouching
            ? crouchSpeed
            : sprintAction.IsPressed() ? sprintSpeed : walkSpeed;

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        if (!isCrouching &&
            controller.isGrounded &&
            jumpAction.WasPressedThisFrame())
        {
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 velocity = direction * speed;
        velocity.y = verticalSpeed;

        CollisionFlags collisions =
            controller.Move(velocity * Time.deltaTime);

        // Stop upward movement when the head hits a ceiling.
        if ((collisions & CollisionFlags.Above) != 0 &&
            verticalSpeed > 0f)
        {
            verticalSpeed = 0f;
        }
    }

    private void SetCrouching(bool crouching)
    {
        if (isCrouching == crouching)
        {
            return;
        }

        isCrouching = crouching;

        float height = crouching ? crouchHeight : standingHeight;
        float heightDifference = standingHeight - height;

        controller.height = height;

        // Lower the centre to keep the capsule's feet in place.
        controller.center = standingCenter -
            Vector3.up * (heightDifference * 0.5f);

        // Change camera position without changing mouse-look rotation.
        playerCamera.localPosition = standingCameraPosition -
            Vector3.up * heightDifference;
    }

    private bool CanStandUp()
    {
        // Assumes Player has scale 1,1,1 and remains upright.
        Vector3 center = transform.TransformPoint(standingCenter);

        float clearance = Mathf.Max(controller.skinWidth, 0.01f);
        float radius = Mathf.Max(
            0.01f, controller.radius - clearance);

        float endpointOffset =
            standingHeight * 0.5f - controller.radius;

        Vector3 bottom = center - transform.up * endpointOffset;
        Vector3 top = center + transform.up * endpointOffset;

        Collider[] obstacles = Physics.OverlapCapsule(
            bottom,
            top,
            radius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider obstacle in obstacles)
        {
            // Ignore the player's own colliders.
            if (obstacle.transform == transform ||
                obstacle.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        verticalSpeed = 0f;
    }
}