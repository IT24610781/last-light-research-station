using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Navigation
{
    /// <summary>
    /// A* over a WaypointGraph: f(n) = g(n) + h(n).
    ///   g(n) = cheapest known cost from start to n
    ///   h(n) = straight-line distance from n to the goal
    /// With useHeuristic = false, h is 0 and this becomes Dijkstra (uniform-cost search),
    /// which we use as the correctness baseline in tests.
    /// The open list is a plain List with a linear scan: with ~40-60 nodes this is
    /// fast enough and easy to explain.
    /// </summary>
    public static class PathFinder
    {
        public static PathResult FindPath(WaypointGraph graph, int start, int goal, bool useHeuristic = true)
        {
            if (graph == null)
                return PathResult.Failure(0, "No graph");

            if (!graph.IsValidNode(start) || !graph.IsValidNode(goal))
                return PathResult.Failure(graph.Revision, "Invalid start or goal node");

            if (start == goal)
            {
                return new PathResult
                {
                    Success = true,
                    NodeIds = new List<int> { start },
                    Points = new List<Vector3> { graph.GetPosition(start) },
                    Cost = 0f,
                    GraphRevision = graph.Revision,
                    NodesExpanded = 0
                };
            }

            int n = graph.NodeCount;
            float[] g = new float[n];
            int[] cameFrom = new int[n];
            bool[] inOpen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                g[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }

            Vector3 goalPos = graph.GetPosition(goal);
            float H(int id) { return useHeuristic ? Vector3.Distance(graph.GetPosition(id), goalPos) : 0f; }

            List<int> open = new List<int>();
            g[start] = 0f;
            open.Add(start);
            inOpen[start] = true;
            int expanded = 0;

            while (open.Count > 0)
            {
                // Pick the open node with the lowest f = g + h.
                int bestIndex = 0;
                float bestF = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = g[open[i]] + H(open[i]);
                    if (f < bestF)
                    {
                        bestF = f;
                        bestIndex = i;
                    }
                }

                int current = open[bestIndex];
                open.RemoveAt(bestIndex);
                inOpen[current] = false;
                expanded++;

                if (current == goal)
                    return BuildResult(graph, cameFrom, start, goal, g[goal], expanded);

                foreach (WaypointGraph.Edge edge in graph.GetEdges(current))
                {
                    if (!edge.Enabled) continue; // closed door / barricade

                    float tentative = g[current] + edge.Cost;
                    if (tentative < g[edge.To])
                    {
                        g[edge.To] = tentative;
                        cameFrom[edge.To] = current;
                        if (!inOpen[edge.To])
                        {
                            open.Add(edge.To);
                            inOpen[edge.To] = true;
                        }
                    }
                }
            }

            return PathResult.Failure(graph.Revision, "No route between start and goal");
        }

        private static PathResult BuildResult(WaypointGraph graph, int[] cameFrom, int start, int goal, float cost, int expanded)
        {
            List<int> ids = new List<int>();
            for (int node = goal; node != -1; node = cameFrom[node])
            {
                ids.Add(node);
                if (node == start) break;
            }
            ids.Reverse();

            List<Vector3> points = new List<Vector3>();
            foreach (int id in ids)
                points.Add(graph.GetPosition(id));

            return new PathResult
            {
                Success = true,
                NodeIds = ids,
                Points = points,
                Cost = cost,
                GraphRevision = graph.Revision,
                NodesExpanded = expanded
            };
        }
    }
}
