using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Director
{
    /// <summary>
    /// Marks a robot's type and keeps a live registry of active robots, so the
    /// Director can count the population without searching the scene. A robot
    /// counts as living while its GameObject is active; death disables it.
    /// </summary>
    public class DirectorRobotTag : MonoBehaviour
    {
        [SerializeField] private RobotType type;

        public RobotType Type => type;

        private static readonly List<DirectorRobotTag> active = new List<DirectorRobotTag>();

        public static IReadOnlyList<DirectorRobotTag> Active => active;

        public static int CountActive(RobotType robotType)
        {
            int count = 0;
            foreach (DirectorRobotTag tag in active)
                if (tag.type == robotType) count++;
            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        private void OnEnable() => active.Add(this);

        private void OnDisable() => active.Remove(this);
    }
}
