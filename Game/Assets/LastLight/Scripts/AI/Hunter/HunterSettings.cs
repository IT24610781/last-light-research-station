using System;
using UnityEngine;

namespace LastLight.AI.Hunter
{
    /// <summary>All tunable Hunter values in one place, editable in the Inspector.</summary>
    [Serializable]
    public class HunterSettings
    {
        [Header("Hearing")]
        [Tooltip("Noises further away than this (m) are not heard.")]
        public float hearingRange = 20f;
        [Tooltip("Perceived strength below this is ignored.")]
        public float minPerceivedStrength = 0.05f;
        [Tooltip("Strength treated as 'maximum loud' when scaling to 0-1.")]
        public float maxNoiseStrength = 1f;

        [Header("Vision")]
        public float viewDistance = 15f;
        [Tooltip("Total field-of-view angle in degrees.")]
        public float fieldOfView = 110f;

        [Header("Memory")]
        [Tooltip("Seconds a noise is remembered.")]
        public float noiseMemoryDuration = 15f;
        [Tooltip("Seconds the last seen player position is remembered.")]
        public float lastSeenMemoryDuration = 10f;
        public int maxRememberedNoises = 8;
        [Tooltip("Noises closer together than this (m) count as one.")]
        public float noiseMergeRadius = 2f;
        [Tooltip("Seconds before an unreachable noise is tried again.")]
        public float rejectRetryDelay = 5f;

        [Header("Noise scoring: score = wS*strength + wR*recency - wC*routeCost (all 0-1)")]
        public float weightStrength = 0.5f;
        public float weightRecency = 0.3f;
        public float weightRouteCost = 0.2f;
        [Tooltip("Route length (m) treated as cost 1.")]
        public float maxRouteCost = 40f;
        [Tooltip("A new noise must beat the current target by this much to switch (stops flip-flopping).")]
        public float switchMargin = 0.15f;

        [Header("Combat and chase")]
        public float attackRange = 8f;
        public float attackCooldown = 1f;
        [Tooltip("Replan the chase when the player has moved this far (m) from the current goal.")]
        public float pursueReplanDistance = 1.5f;

        [Header("Search")]
        public float searchRadius = 3f;
        public int searchPointCount = 4;
        [Tooltip("Maximum seconds spent searching before returning to patrol.")]
        public float maxSearchDuration = 12f;

        [Header("Speed multipliers (x motor max speed)")]
        public float patrolSpeed = 0.5f;
        public float investigateSpeed = 0.8f;
        public float pursueSpeed = 1f;
    }
}
