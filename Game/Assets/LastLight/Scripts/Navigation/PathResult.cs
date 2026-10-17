using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Navigation
{
    /// <summary>
    /// What the A* service returns. A failed path is normal information, not an exception:
    /// agents check Success and choose another goal or wait.
    /// </summary>
    public class PathResult
    {
        public bool Success;
        public List<int> NodeIds = new List<int>();
        public List<Vector3> Points = new List<Vector3>();
        public float Cost;
        public int GraphRevision;
        public int NodesExpanded;
        public string FailureReason;

        public static PathResult Failure(int graphRevision, string reason)
        {
            return new PathResult
            {
                Success = false,
                GraphRevision = graphRevision,
                FailureReason = reason
            };
        }
    }
}
