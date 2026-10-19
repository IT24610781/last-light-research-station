using UnityEngine;

namespace LastLight.AI.Guard
{
    /// <summary>
    /// Utility-based decision making for the Tactical Guard.
    /// Each decision has three steps:
    ///   1. Filter: remove actions that are impossible right now (no ammo, no cover...).
    ///   2. Score: give every remaining action a 0-1 usefulness score from the observation.
    ///   3. Commit rule: stick with the current action for a minimum time and only switch
    ///      if the new best action beats it by a margin (stops flip-flopping).
    /// This is plain C# with no scene access, so it is easy to test and the Unity component
    /// can stay thin (sense -> brain -> execute).
    /// The caller passes the gameplay clock (not Time.time) so pause and restart behave.
    /// </summary>
    public class GuardUtilityBrain
    {
        public const int ActionCount = 5;

        public GuardWeights Weights { get; private set; }
        public float MinActionDuration { get; private set; }
        public float SwitchMargin { get; private set; }

        public bool HasCurrent { get { return hasCurrent; } }
        public GuardAction Current { get { return current; } }

        private bool hasCurrent;
        private GuardAction current;
        private float currentSince;

        public GuardUtilityBrain(GuardWeights weights = null, float minActionDuration = 0.6f, float switchMargin = 0.15f)
        {
            Weights = weights ?? new GuardWeights();
            MinActionDuration = minActionDuration;
            SwitchMargin = switchMargin;
        }

        /// <summary>Forget the current action. Call on restart or when the Guard is re-activated.</summary>
        public void Reset()
        {
            hasCurrent = false;
            currentSince = 0f;
        }

        public GuardDecision Decide(GuardObservation obs, float now)
        {
            float health = Mathf.Clamp01(obs.Health01);
            float ammo = Mathf.Clamp01(obs.Ammo01);
            float exposure = Mathf.Clamp01(obs.Exposure01);
            float distance = Mathf.Clamp01(obs.TargetDistance01);
            float coverQuality = Mathf.Clamp01(obs.CoverQuality01);
            float retreatQuality = Mathf.Clamp01(obs.RetreatQuality01);

            float[] scores = new float[ActionCount];
            bool[] valid = new bool[ActionCount];
            GuardWeights w = Weights;

            // Step 1 and 2: validity filter, then score only valid actions.
            // Attack needs a visible target and ammunition.
            valid[(int)GuardAction.Attack] = obs.HasVisibleTarget && ammo > 0f;
            if (valid[(int)GuardAction.Attack])
            {
                scores[(int)GuardAction.Attack] =
                    w.AttackHealth * health +
                    w.AttackAmmo * ammo +
                    w.AttackProximity * (1f - distance) +
                    w.AttackCovered * (1f - exposure);
            }

            // Cover needs an unoccupied, reachable cover point.
            valid[(int)GuardAction.TakeCover] = obs.CoverAvailable;
            if (valid[(int)GuardAction.TakeCover])
            {
                scores[(int)GuardAction.TakeCover] =
                    w.CoverExposure * exposure +
                    w.CoverInjury * (1f - health) +
                    w.CoverQuality * coverQuality;
            }

            // Reload is valid whenever the magazine is not full, with or without cover.
            valid[(int)GuardAction.Reload] = ammo < 1f;
            if (valid[(int)GuardAction.Reload])
            {
                scores[(int)GuardAction.Reload] =
                    w.ReloadMissingAmmo * (1f - ammo) +
                    w.ReloadSafety * (1f - exposure);
            }

            // Retreat needs a reachable retreat point.
            valid[(int)GuardAction.Retreat] = obs.RetreatAvailable;
            if (valid[(int)GuardAction.Retreat])
            {
                scores[(int)GuardAction.Retreat] =
                    w.RetreatInjury * (1f - health) +
                    w.RetreatExposure * exposure +
                    w.RetreatQuality * retreatQuality;
            }

            // Hold is always valid, so the Guard never has nothing to do.
            valid[(int)GuardAction.Hold] = true;
            scores[(int)GuardAction.Hold] = w.HoldScore;

            // Best valid action (strict > means Hold, listed last, only wins if strictly better).
            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < ActionCount; i++)
            {
                if (!valid[i]) continue;
                if (scores[i] > bestScore)
                {
                    bestScore = scores[i];
                    best = i;
                }
            }
            GuardAction bestAction = (GuardAction)best;

            // Step 3: commit rule.
            bool switched = false;
            string reason;

            if (!hasCurrent)
            {
                Adopt(bestAction, now);
                switched = true;
                reason = "No current action: took the best-scoring valid action";
            }
            else if (!valid[(int)current])
            {
                // An invalid action overrides the hold time (for example the magazine just ran empty).
                Adopt(bestAction, now);
                switched = true;
                reason = "Current action is no longer valid: switched immediately";
            }
            else if (now - currentSince < MinActionDuration)
            {
                reason = "Holding current action: minimum duration not reached";
            }
            else if (bestAction != current && bestScore > scores[(int)current] + SwitchMargin)
            {
                Adopt(bestAction, now);
                switched = true;
                reason = "Switched: new action beats the current one by more than the margin";
            }
            else
            {
                reason = "Keeping current action: no advantage above the switch margin";
            }

            return new GuardDecision
            {
                Action = current,
                Switched = switched,
                Reason = reason,
                Scores = scores,
                Valid = valid
            };
        }

        private void Adopt(GuardAction action, float now)
        {
            current = action;
            currentSince = now;
            hasCurrent = true;
        }
    }
}
