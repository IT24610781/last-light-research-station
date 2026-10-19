using System;

namespace LastLight.AI.Guard
{
    /// <summary>
    /// Starting weights for each action's utility score. These are tuning values, not measured
    /// truths: record why each was chosen and adjust them from playtests.
    /// The weights inside each action sum to 1.0, so every score stays on a 0-1 scale and the
    /// scores of different actions can be compared fairly.
    /// </summary>
    [Serializable]
    public class GuardWeights
    {
        // Attack = health + ammo + closeness + being covered (the opposite of exposure)
        public float AttackHealth = 0.30f;
        public float AttackAmmo = 0.25f;
        public float AttackProximity = 0.15f;
        public float AttackCovered = 0.30f;

        // Cover = how exposed + how hurt + how good the best cover point is
        public float CoverExposure = 0.55f;
        public float CoverInjury = 0.20f;
        public float CoverQuality = 0.25f;

        // Reload = how empty the magazine is + how safe it is to look away
        public float ReloadMissingAmmo = 0.60f;
        public float ReloadSafety = 0.40f;

        // Retreat = how hurt + how exposed + how good the retreat point is
        public float RetreatInjury = 0.60f;
        public float RetreatExposure = 0.20f;
        public float RetreatQuality = 0.20f;

        // Fallback so the Guard never has zero valid actions
        public float HoldScore = 0.10f;
    }
}
