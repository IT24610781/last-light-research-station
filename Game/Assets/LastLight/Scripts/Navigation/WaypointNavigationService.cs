using System.Collections.Generic;
using UnityEngine;
using LastLight.AI.Movement;

namespace LastLight.Navigation
{
    /// <summary>
    /// Member A's A* service, wired into the scene. Put empty child objects under
    /// "waypointRoot" (one per corridor turn, doorway, cover and objective). At start the
    /// service links every pair of waypoints that are close enough and have a clear path
    /// between them (checked with a sphere sweep, so the robot's width is respected).
    /// It implements IPathProvider, so Hunter, Guard and Repair Robot can all use it by
    /// dragging this component into their "Path Provider Component" slot.
    /// </summary>
    public class WaypointNavigationService : MonoBehaviour, IPathProvider
    {
        [Tooltip("Parent object whose direct children are the waypoints.")]
        [SerializeField] private Transform waypointRoot;
        [Tooltip("Waypoints further apart than this are never linked.")]
        [SerializeField] private float maxLinkDistance = 8f;
        [Tooltip("Robot half-width used when checking that a link is wide enough.")]
        [SerializeField] private float clearanceRadius = 0.4f;
        [Tooltip("Height above the floor of the sweep, so the floor itself is not hit.")]
        [SerializeField] private float castHeight = 0.6f;
        [Tooltip("Layers that block movement (walls, props). Characters are ignored automatically.")]
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [SerializeField] private bool drawGizmos = true;

        private readonly List<Transform> nodeTransforms = new List<Transform>();
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        public WaypointGraph Graph { get; private set; }

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>Rebuilds the whole graph from the waypoint markers.</summary>
        public void Rebuild()
        {
            Graph = new WaypointGraph();
            nodeTransforms.Clear();

            if (waypointRoot == null)
            {
                Debug.LogWarning($"{name}: no waypoint root assigned, navigation graph is empty.", this);
                return;
            }

            foreach (Transform child in waypointRoot)
            {
                nodeTransforms.Add(child);
                Graph.AddNode(child.position);
            }

            for (int i = 0; i < Graph.NodeCount; i++)
            {
                for (int j = i + 1; j < Graph.NodeCount; j++)
                {
                    Vector3 a = Graph.GetPosition(i);
                    Vector3 b = Graph.GetPosition(j);
                    if (Vector3.Distance(a, b) > maxLinkDistance) continue;
                    if (!IsClear(a, b)) continue;
                    Graph.AddConnection(i, j);
                }
            }
        }

        /// <summary>Closes or reopens the link between two waypoints (door, barricade).</summary>
        public bool SetLinkEnabled(Transform a, Transform b, bool enabled)
        {
            if (Graph == null) return false;
            int ia = nodeTransforms.IndexOf(a);
            int ib = nodeTransforms.IndexOf(b);
            if (ia < 0 || ib < 0) return false;
            return Graph.SetConnectionEnabled(ia, ib, enabled);
        }

        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> pathOut, out float pathLength)
        {
            pathOut.Clear();
            pathLength = 0f;
            if (Graph == null || Graph.NodeCount == 0) return false;

            // The nearest waypoint might be behind a wall, so only accept ones we can reach in a straight line.
            int startNode = NearestVisibleNode(start);
            int goalNode = NearestVisibleNode(goal);
            if (startNode < 0 || goalNode < 0) return false;

            PathResult result = PathFinder.FindPath(Graph, startNode, goalNode);
            if (!result.Success) return false;

            // Skip the first waypoint if we can already walk straight to the second one.
            int firstIndex = 0;
            if (result.Points.Count >= 2 && IsClear(start, result.Points[1])) firstIndex = 1;

            for (int i = firstIndex; i < result.Points.Count; i++)
                pathOut.Add(result.Points[i]);
            pathOut.Add(goal);

            Vector3 previous = start;
            for (int i = 0; i < pathOut.Count; i++)
            {
                pathLength += Vector3.Distance(previous, pathOut[i]);
                previous = pathOut[i];
            }
            return true;
        }

        private int NearestVisibleNode(Vector3 point)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Graph.NodeCount; i++)
            {
                Vector3 nodePosition = Graph.GetPosition(i);
                float distance = Vector3.Distance(point, nodePosition);
                if (distance >= bestDistance) continue;
                if (!IsClear(point, nodePosition)) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>True if a robot-wide sweep from a to b hits no wall or prop.</summary>
        private bool IsClear(Vector3 a, Vector3 b)
        {
            Vector3 from = a + Vector3.up * castHeight;
            Vector3 to = b + Vector3.up * castHeight;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) return true;

            int count = Physics.SphereCastNonAlloc(from, clearanceRadius, delta / distance,
                hitBuffer, distance, obstacleLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                // Robots and the player standing in the way do not make a route permanently invalid.
                if (hitBuffer[i].collider.GetComponentInParent<CharacterController>() != null) continue;
                return false;
            }
            return true;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || waypointRoot == null) return;

            Gizmos.color = Color.cyan;
            foreach (Transform child in waypointRoot)
                Gizmos.DrawSphere(child.position, 0.15f);

            if (Graph == null) return;
            for (int i = 0; i < Graph.NodeCount; i++)
            {
                foreach (WaypointGraph.Edge edge in Graph.GetEdges(i))
                {
                    if (edge.To < i) continue; // draw each two-way link once
                    Gizmos.color = edge.Enabled ? Color.green : Color.red;
                    Gizmos.DrawLine(Graph.GetPosition(i), Graph.GetPosition(edge.To));
                }
            }
        }
    }
}
