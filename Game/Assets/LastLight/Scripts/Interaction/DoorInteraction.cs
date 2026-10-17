using UnityEngine;

public class DoorInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 120f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;

    private void Awake()
    {
        closedRotation = transform.localRotation;

        openRotation = closedRotation *
            Quaternion.Euler(0f, openAngle, 0f);
    }

    public void Interact()
    {
        isOpen = !isOpen;
    }

    private void Update()
    {
        Quaternion targetRotation = isOpen
            ? openRotation
            : closedRotation;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }
}