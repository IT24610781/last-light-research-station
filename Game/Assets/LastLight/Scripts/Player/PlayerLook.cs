using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maximumLookAngle = 85f;

    private InputAction lookAction;
    private float cameraPitch;

    private void Start()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();

        lookAction = playerInput.actions.FindAction("Player/Look", true);

        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerLook: assign Main Camera in the Inspector.", this);

            enabled = false;
            return;
        }

        SetCursorCaptured(true);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorCaptured(false);
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorCaptured(true);
            }

            return;
        }

        Vector2 mouseDelta = lookAction.ReadValue<Vector2>();

        float horizontalRotation = mouseDelta.x * mouseSensitivity;
        float verticalRotation = mouseDelta.y * mouseSensitivity;

        // Turn the whole player left and right.
        transform.Rotate(0f, horizontalRotation, 0f);

        // Tilt only the camera up and down.
        cameraPitch -= verticalRotation;
        cameraPitch = Mathf.Clamp(
            cameraPitch, -maximumLookAngle, maximumLookAngle);

        playerCamera.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void OnDisable()
    {
        SetCursorCaptured(false);
    }

    private void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !captured;
    }
}