using System;
using UnityEngine;

namespace LastLight.AI.Director
{
    /// <summary>All tunable Director values in one place, editable in the Inspector.</summary>
    [Serializable]
    public class DirectorSettings
    {
        [Header("Hard limits (never overridden by scores)")]
        public int maxActiveRobots = 5;
        [Tooltip("Successful activations allowed per run.")]
        public int activationBudget = 4;
        [Tooltip("Seconds between activations (also applied after level start).")]
        public float cooldown = 20f;
        [Tooltip("Bays closer than this (m) to the player are rejected.")]
        public float minSpawnDistance = 10f;
        [Tooltip("Player's horizontal view angle (degrees) used for the 'bay in view' check.")]
        public float playerViewAngle = 100f;

        [Header("Pressure model (all 0-1)")]
        [Tooltip("Desired pressure at the start of the mission.")]
        public float basePressure = 0.35f;
        [Tooltip("Desired pressure when every relay is online.")]
        public float maxPressure = 0.8f;
        [Tooltip("Seconds without damage before the player counts as fully recovered.")]
        public float recoveryTime = 10f;
        [Tooltip("Pressure each living robot type contributes.")]
        public float hunterPressure = 0.25f;
        public float guardPressure = 0.3f;
        public float repairPressure = 0.15f;

        [Header("Activation score = wGap*pressureGap + wType*typeFit + wBay*bayFit")]
        public float weightPressureGap = 1f;
        public float weightTypeFit = 0.3f;
        public float weightBayFit = 0.2f;
        [Tooltip("Route length (m) from bay to player that gives the best bay fit.")]
        public float idealRouteLength = 18f;

        [Header("Wait score = base + wLow*(1 - readiness) + wOver*overPressure")]
        public float waitBase = 0.15f;
        public float waitLowReadiness = 0.6f;
        public float waitOverPressure = 0.8f;
    }
}
