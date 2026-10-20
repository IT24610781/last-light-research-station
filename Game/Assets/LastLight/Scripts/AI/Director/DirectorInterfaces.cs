using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Director
{
    public enum RobotType { Hunter, Guard, Repair }

    /// <summary>Player condition the Director may observe (its declared sensor model). Implemented by the player's health code.</summary>
    public interface IPlayerStatus
    {
        /// <summary>Current health scaled to 0-1.</summary>
        float Health01 { get; }

        /// <summary>Seconds since the player last took damage.</summary>
        float TimeSinceDamaged { get; }
    }

    /// <summary>Mission progress the Director may observe. Implemented by Member A's relay/objective code.</summary>
    public interface IMissionProgress
    {
        int RelaysOnline { get; }
        int RelaysTotal { get; }
    }

    /// <summary>A robot the Director can activate at a service bay. Each agent owner implements this.</summary>
    public interface ISpawnableAgent
    {
        /// <summary>Clears health, memory, movement and effects, then places the robot.</summary>
        void ResetAgentAt(Vector3 position, Quaternion rotation);
    }
}
