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

        GameSessionController session =
            FindFirstObjectByType<GameSessionController>();

        SetCursorCaptured(
            session == null ||
            session.State == GameSessionController.SessionState.Playing);
    }

    public void SetSensitivity(float value)
    {
        mouseSensitivity = Mathf.Clamp(value, 0.02f, 0.5f);
    }

    private void Update()
    {
        bool sessionOwnsPause =
            FindFirstObjectByType<GameSessionController>() != null;

        if (!sessionOwnsPause &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorCaptured(false);
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (!sessionOwnsPause &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorCaptured(true);
            }

            return;
        }

        Vector2 mouseDelta = lookAction.ReadValue<Vector2>();

        float horizontalRotation = mouseDelta.x * mouseSensitivity;
        float verticalRotation = mouseDelta.y * mouseSensitivity;

        transform.Rotate(0f, horizontalRotation, 0f);

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