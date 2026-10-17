namespace LastLight.AI.Guard
{
    /// <summary>
    /// Actions the Tactical Guard can choose between.
    /// Hold is last so it only wins when it is strictly better than every real action.
    /// </summary>
    public enum GuardAction
    {
        Attack = 0,
        TakeCover = 1,
        Reload = 2,
        Retreat = 3,
        Hold = 4
    }

    /// <summary>
    /// A snapshot of what the Guard is allowed to know. The Unity component fills this in
    /// from its senses (sight, own health/ammo, reachable cover); the brain never reads the
    /// scene. Values marked 0-1 are normalised so weights are comparable.
    /// </summary>
    public struct GuardObservation
    {
        public bool HasVisibleTarget;

        /// <summary>Current health divided by max health (0 = dead, 1 = full).</summary>
        public float Health01;

        /// <summary>Rounds in magazine divided by magazine size (0 = empty, 1 = full).</summary>
        public float Ammo01;

        /// <summary>How exposed the Guard is to the last known threat (0 = fully covered, 1 = wide open).</summary>
        public float Exposure01;

        /// <summary>Distance to the target divided by max engagement range (0 = adjacent, 1 = far).</summary>
        public float TargetDistance01;

        /// <summary>True only if an unoccupied, reachable cover point exists.</summary>
        public bool CoverAvailable;

        /// <summary>Quality of the best cover point (protection, firing chance, short route): 0-1.</summary>
        public float CoverQuality01;

        /// <summary>True only if a reachable retreat point exists.</summary>
        public bool RetreatAvailable;

        /// <summary>Quality of the best retreat point (safe, short route): 0-1.</summary>
        public float RetreatQuality01;
    }

    /// <summary>
    /// The brain's answer, plus everything the debug overlay needs to explain it:
    /// every action's score, which actions were valid, and why this action was chosen.
    /// </summary>
    public class GuardDecision
    {
        public GuardAction Action;
        public bool Switched;
        public string Reason;
        public float[] Scores;
        public bool[] Valid;

        public float ScoreOf(GuardAction action)
        {
            return Scores[(int)action];
        }

        public bool IsValid(GuardAction action)
        {
            return Valid[(int)action];
        }
    }
}
