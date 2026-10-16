using System.Collections.Generic;
using UnityEngine;
using LastLight.AI.Movement;

[RequireComponent(typeof(CharacterController))]
public class RepairRoutePlanner : MonoBehaviour
{
    [Tooltip("Member A's MonoBehaviour implementing IPathProvider. Empty = checked direct test routes.")]
    [SerializeField] private MonoBehaviour pathProvider;
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField, Min(0.1f)] private float approachDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float repairRange = 2f;
    private CharacterController controller;
    public bool IsFallback => !(pathProvider is IPathProvider) || !pathProvider.isActiveAndEnabled;
    public string ProviderDescription => IsFallback
        ? "Checked direct routes (fallback; cannot detour around walls)"
        : pathProvider.GetType().Name;
    public Vector3 BodyOffset => transform.TransformVector(Controller.center);
    private CharacterController Controller => controller != null ? controller :
        controller = GetComponent<CharacterController>();

    public bool TryPlanAlly(RepairableAlly ally, List<Vector3> path, out float cost)
    {
        path.Clear(); cost = float.PositiveInfinity;
        if (ally == null) return false;
        Vector3 body = Controller.bounds.center;
        RepairRobotRepair repair = GetComponent<RepairRobotRepair>();
        float range = repair != null ? repair.RepairRange : repairRange;
        if (Vector3.Distance(body, ally.RepairPosition) <= range &&
            RepairVisibility.Clear(body + Vector3.up * 0.3f, ally.RepairPosition,
                transform, ally.transform, obstacleLayers))
        {
            path.Add(transform.position); cost = 0f; return true;
        }

        // Try several repair positions, not the ally's occupied collider centre.
        Vector3 towardUs = transform.position - ally.transform.position;
        towardUs.y = 0f;
        if (towardUs.sqrMagnitude < 0.001f) towardUs = Vector3.back;
        towardUs.Normalize();
        var candidatePath = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            Vector3 radial = Quaternion.Euler(0f, i * 45f, 0f) * towardUs;
            float standoff = Mathf.Min(approachDistance, Mathf.Max(0.1f, range - 0.3f));
            Vector3 goal = ally.RepairPosition + radial * standoff - BodyOffset;
            if (!RepairVisibility.Clear(goal + BodyOffset + Vector3.up * 0.3f,
                ally.RepairPosition, transform, ally.transform, obstacleLayers)) continue;
            if (!TryPlanPoint(goal, candidatePath, out float candidateCost)) continue;
            if (candidateCost < cost)
            {
                cost = candidateCost; path.Clear(); path.AddRange(candidatePath);
            }
        }
        return path.Count > 0;
    }

    public bool TryPlanPoint(Vector3 goal, List<Vector3> path, out float cost)
    {
        path.Clear(); cost = float.PositiveInfinity;
        if (!Finite(goal) || Occupied(goal)) return false;
        if (!IsFallback)
        {
            bool found = ((IPathProvider)pathProvider).TryFindPath(
                transform.position, goal, path, out cost);
            if (!found || path.Count == 0 || float.IsNaN(cost) ||
                float.IsInfinity(cost) || cost < 0f) { path.Clear(); return false; }
            foreach (Vector3 point in path)
                if (!Finite(point)) { path.Clear(); return false; }
            if (Vector3.Distance(path[path.Count - 1], goal) > 0.5f)
            { path.Clear(); return false; }
            return true;
        }

        // Explicitly a single-floor test fallback, not an A* replacement.
        if (Mathf.Abs(goal.y - transform.position.y) > 0.4f) return false;
        Vector3 delta = goal - transform.position;
        Capsule(transform.position, out Vector3 bottom, out Vector3 top, out float radius);
        if (delta.magnitude > 0.001f)
        {
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius,
                delta.normalized, delta.magnitude, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                if (!RepairVisibility.Ignored(hit.collider.transform, transform, null)) return false;
            }
        }
        path.Add(goal); cost = delta.magnitude; return true;
    }
    private bool Occupied(Vector3 position)
    {
        Capsule(position, out Vector3 bottom, out Vector3 top, out float radius);
        foreach (Collider item in Physics.OverlapCapsule(bottom, top, radius,
            obstacleLayers, QueryTriggerInteraction.Ignore))
            if (!RepairVisibility.Ignored(item.transform, transform, null)) return true;
        return false;
    }
    private void Capsule(Vector3 position, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        radius = Mathf.Max(0.05f, Controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) - 0.02f);
        float half = Mathf.Max(0f, Controller.height * Mathf.Abs(scale.y) * 0.5f - Controller.radius);
        Vector3 centre = position + BodyOffset;
        bottom = centre - Vector3.up * half;
        top = centre + Vector3.up * half;
    }
    private static bool Finite(Vector3 point) =>
        !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
        !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);
}
