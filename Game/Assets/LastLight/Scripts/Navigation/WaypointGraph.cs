using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Navigation
{
    /// <summary>
    /// Waypoint graph: nodes are safe positions, edges are traversable connections.
    /// Plain C# data so it can be unit tested without a scene.
    /// Edge cost = straight-line distance + optional non-negative penalty.
    /// That keeps straight-line distance a lower bound on the true cost,
    /// which is what makes the A* heuristic admissible.
    /// </summary>
    public class WaypointGraph
    {
        public class Edge
        {
            public int To;
            public float Cost;
            public bool Enabled = true;
        }

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<List<Edge>> adjacency = new List<List<Edge>>();

        /// <summary>Increases whenever nodes or connections change, so agents can detect stale paths.</summary>
        public int Revision { get; private set; }

        public int NodeCount { get { return positions.Count; } }

        public int AddNode(Vector3 position)
        {
            positions.Add(position);
            adjacency.Add(new List<Edge>());
            Revision++;
            return positions.Count - 1;
        }

        public bool IsValidNode(int id)
        {
            return id >= 0 && id < positions.Count;
        }

        public Vector3 GetPosition(int id)
        {
            return positions[id];
        }

        public List<Edge> GetEdges(int id)
        {
            return adjacency[id];
        }

        /// <summary>Adds a two-way connection. Negative penalties are clamped to 0.</summary>
        public void AddConnection(int a, int b, float penalty = 0f)
        {
            if (!IsValidNode(a) || !IsValidNode(b) || a == b) return;

            float cost = Vector3.Distance(positions[a], positions[b]) + Mathf.Max(0f, penalty);
            adjacency[a].Add(new Edge { To = b, Cost = cost });
            adjacency[b].Add(new Edge { To = a, Cost = cost });
            Revision++;
        }

        /// <summary>
        /// Opens or closes a connection (door, barricade). Returns false if no such connection exists.
        /// </summary>
        public bool SetConnectionEnabled(int a, int b, bool enabled)
        {
            if (!IsValidNode(a) || !IsValidNode(b)) return false;

            bool found = false;
            foreach (Edge e in adjacency[a])
            {
                if (e.To == b) { e.Enabled = enabled; found = true; }
            }
            foreach (Edge e in adjacency[b])
            {
                if (e.To == a) { e.Enabled = enabled; }
            }

            if (found) Revision++;
            return found;
        }
    }
}
