using UnityEngine;

public class PlayerGrabber : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float holdDistance = 1.8f;
    [SerializeField] private float followStrength = 10f;
    [SerializeField] private float maximumCarrySpeed = 8f;
    [SerializeField] private float throwSpeed = 10f;
    [SerializeField] private float releaseDistance = 3f;

    private CarryableObject heldObject;
    private bool originalGravity;
    private RigidbodyConstraints originalConstraints;

    public bool IsHolding => heldObject != null;

    public void TryGrab(CarryableObject target)
    {
        if (IsHolding || target == null || playerCamera == null)
        {
            return;
        }

        Rigidbody body = target.Body;

        // This version carries only dynamic physics objects.
        if (body == null || body.isKinematic)
        {
            return;
        }

        heldObject = target;
        originalGravity = body.useGravity;
        originalConstraints = body.constraints;

        body.useGravity = false;
        body.constraints =
            originalConstraints | RigidbodyConstraints.FreezeRotation;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
    }

    private void FixedUpdate()
    {
        if (!IsHolding || playerCamera == null)
        {
            return;
        }

        Rigidbody body = heldObject.Body;

        // Release if an obstruction leaves the object too far behind.
        if (Vector3.Distance(
            body.position, playerCamera.transform.position)
            > releaseDistance)
        {
            Drop();
            return;
        }

        Vector3 targetPosition =
            playerCamera.transform.position +
            playerCamera.transform.forward * holdDistance;

        Vector3 offset = targetPosition - body.position;

        // Move using physics rather than teleporting the Transform.
        body.linearVelocity = Vector3.ClampMagnitude(
            offset * followStrength, maximumCarrySpeed);
    }

    public void Drop()
    {
        Release(false);
    }

    public void Throw()
    {
        Release(true);
    }

    private void Release(bool shouldThrow)
    {
        if (!IsHolding)
        {
            return;
        }

        Rigidbody body = heldObject.Body;

        body.useGravity = originalGravity;
        body.constraints = originalConstraints;
        body.angularVelocity = Vector3.zero;

        body.linearVelocity = shouldThrow && playerCamera != null
            ? playerCamera.transform.forward * throwSpeed
            : Vector3.zero;

        heldObject = null;
    }

    private void OnDisable()
    {
        Drop();
    }
}