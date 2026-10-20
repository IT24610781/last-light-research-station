#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using LastLight.AI.Movement;

// Controlled external-provider costs prove that the selector consumes IPathProvider.
public class RepairRobotTestRoutes : MonoBehaviour, IPathProvider
{
    public Transform AllyA, AllyB;
    public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> pathOut, out float pathLength)
    {
        pathOut.Clear(); pathOut.Add(goal);
        pathLength = Vector3.Distance(goal, AllyA.position) <
            Vector3.Distance(goal, AllyB.position) ? 100f : 1f;
        return true;
    }
}
#endif
