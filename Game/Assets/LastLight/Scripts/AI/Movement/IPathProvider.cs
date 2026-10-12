using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Movement
{
    /// <summary>
    /// Anything that can find a route between two points.
    /// Member A's A* will implement this interface, so agents never depend
    /// on one particular pathfinding implementation.
    /// </summary>
    public interface IPathProvider
    {
        /// <summary>
        /// Finds a route from start to goal.
        /// Returns false if no route exists (the goal is unreachable).
        /// </summary>
        /// <param name="pathOut">Filled with the route points (the list is cleared first).</param>
        /// <param name="pathLength">Total route length in metres.</param>
        bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> pathOut, out float pathLength);
    }

    /// <summary>
    /// TEMPORARY stand-in until Member A's A* exists: returns a single straight
    /// line to the goal. It cannot route around walls; the motor's Blocked
    /// detection handles that case for now.
    /// </summary>
    public sealed class StraightLinePathProvider : IPathProvider
    {
        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> pathOut, out float pathLength)
        {
            pathOut.Clear();
            pathOut.Add(goal);

            Vector3 flat = goal - start;
            flat.y = 0f; // robots move on a flat floor
            pathLength = flat.magnitude;
            return true;
        }
    }
}
