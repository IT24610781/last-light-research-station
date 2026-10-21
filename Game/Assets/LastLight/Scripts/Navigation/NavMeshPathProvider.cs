using System.Collections.Generic;
using LastLight.AI.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace LastLight.Navigation
{
    public sealed class NavMeshPathProvider : MonoBehaviour, IPathProvider
    {
        [SerializeField, Min(0.1f)] private float sampleRadius = 2f;
        [SerializeField] private int areaMask = NavMesh.AllAreas;

        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> pathOut, out float pathLength)
        {
            pathOut.Clear();
            pathLength = 0f;

            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, sampleRadius, areaMask) ||
                !NavMesh.SamplePosition(goal, out NavMeshHit goalHit, sampleRadius, areaMask))
                return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, goalHit.position, areaMask, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners.Length == 0)
                return false;

            Vector3 previous = start;
            for (int i = 1; i < path.corners.Length; i++)
            {
                Vector3 point = path.corners[i];
                pathOut.Add(point);
                pathLength += Vector3.Distance(previous, point);
                previous = point;
            }

            if (pathOut.Count == 0 || Vector3.Distance(pathOut[pathOut.Count - 1], goal) > 0.1f)
            {
                pathOut.Add(goal);
                pathLength += Vector3.Distance(previous, goal);
            }

            return true;
        }
    }
}
