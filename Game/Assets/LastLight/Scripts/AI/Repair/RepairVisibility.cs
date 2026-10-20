using UnityEngine;

// Shared by sensing, approach planning and the final repair check.
public static class RepairVisibility
{
    public static bool Clear(Vector3 from, Vector3 to, Transform observer,
        Transform recipient, LayerMask layers)
    {
        // Raycasts alone miss obstacles containing an endpoint.
        if (EndpointBlocked(from, observer, recipient, layers) ||
            EndpointBlocked(to, observer, recipient, layers)) return false;

        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.000001f) return true;
        foreach (RaycastHit hit in Physics.RaycastAll(from, delta.normalized,
            delta.magnitude, layers, QueryTriggerInteraction.Ignore))
        {
            if (!Ignored(hit.collider.transform, observer, recipient)) return false;
        }
        return true;
    }

    private static bool EndpointBlocked(Vector3 point, Transform observer,
        Transform recipient, LayerMask layers)
    {
        foreach (Collider collider in Physics.OverlapSphere(point, 0.02f,
            layers, QueryTriggerInteraction.Ignore))
        {
            if (!Ignored(collider.transform, observer, recipient)) return true;
        }
        return false;
    }

    public static bool Ignored(Transform item, Transform observer, Transform recipient)
    {
        return (observer != null && (item == observer || item.IsChildOf(observer))) ||
            (recipient != null && (item == recipient || item.IsChildOf(recipient)));
    }
}
