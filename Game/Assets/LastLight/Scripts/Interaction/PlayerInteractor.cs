using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerGrabber))]
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionRange = 3f;

    private InputAction interactAction;
    private InputAction throwAction;
    private PlayerGrabber grabber;

    private void Start()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();

        interactAction = playerInput.actions.FindAction(
            "Player/Interact", true);

        throwAction = playerInput.actions.FindAction(
            "Player/Throw", true);

        grabber = GetComponent<PlayerGrabber>();

        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerInteractor: assign Main Camera.", this);

            enabled = false;
        }
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        if (grabber.IsHolding)
        {
            if (throwAction.WasPressedThisFrame())
            {
                grabber.Throw();
            }
            else if (interactAction.WasPressedThisFrame())
            {
                grabber.Drop();
            }

            return;
        }

        if (!interactAction.WasPressedThisFrame())
        {
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        CarryableObject carryable =
            hit.collider.GetComponentInParent<CarryableObject>();

        if (carryable != null)
        {
            grabber.TryGrab(carryable);
            return;
        }

        IInteractable target =
            hit.collider.GetComponentInParent<IInteractable>();

        target?.Interact();
    }
}