using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Director
{
    /// <summary>One service bay as the Director sees it this decision.</summary>
    public struct BayInfo
    {
        public Vector3 Position;
        public bool VisibleToPlayer;
        public float DistanceToPlayer;
        public bool HasRoute;
        public float RouteLength;
    }

    /// <summary>Everything the Director may know when deciding (its sensors).</summary>
    public struct DirectorObservation
    {
        public float Now;
        public float PlayerHealth01;
        public float TimeSinceDamaged;
        public int RelaysOnline;
        public int RelaysTotal;
        public int[] ActiveByType;      // indexed by (int)RobotType
        public bool[] AvailableByType;  // an inactive pooled instance exists
        public IReadOnlyList<BayInfo> Bays;
    }

    /// <summary>One option considered, kept for the debug overlay and viva.</summary>
    public struct DirectorCandidate
    {
        public string Label;
        public bool IsWait;
        public RobotType Type;
        public int BayIndex;
        public float Score;
        public string RejectedReason; // null when the option was valid and scored
    }

    public struct DirectorDecision
    {
        public bool Activate;
        public RobotType Type;
        public int BayIndex;
        public float Score;
        public string Reason;
    }

    /// <summary>
    /// Utility-based pacing agent. Hard constraints (population cap, budget,
    /// cooldown, unsafe bays) are filtered first; every remaining (robot type, bay)
    /// option is then scored against the pressure the player can handle, and
    /// compared with an explicit "wait" option. Plain C#, so it is testable on its own.
    /// </summary>
    public sealed class DirectorBrain
    {
        private static readonly RobotType[] AllTypes = { RobotType.Hunter, RobotType.Guard, RobotType.Repair };

        private readonly DirectorSettings settings;
        private readonly List<DirectorCandidate> candidates = new List<DirectorCandidate>();

        public int ActivationsUsed { get; private set; }
        public float LastActivationTime { get; private set; }
        public float DesiredPressure { get; private set; }
        public float CurrentPressure { get; private set; }
        public float PlayerReadiness { get; private set; }
        public string LastReason { get; private set; } = "Starting";
        public IReadOnlyList<DirectorCandidate> Candidates => candidates;

        public DirectorBrain(DirectorSettings settings, float startTime)
        {
            this.settings = settings;
            LastActivationTime = startTime; // no reinforcements in the first cooldown period
        }

        public void Reset(float now)
        {
            ActivationsUsed = 0;
            LastActivationTime = now;
            candidates.Clear();
            LastReason = "Reset";
        }

        /// <summary>Counts an activation only after it actually succeeded.</summary>
        public void RecordActivation(float now)
        {
            ActivationsUsed++;
            LastActivationTime = now;
        }

        public float CooldownRemaining(float now) => Mathf.Max(0f, settings.cooldown - (now - LastActivationTime));

        public DirectorDecision Decide(in DirectorObservation o)
        {
            candidates.Clear();

            int hunters = o.ActiveByType[(int)RobotType.Hunter];
            int guards = o.ActiveByType[(int)RobotType.Guard];
            int repairs = o.ActiveByType[(int)RobotType.Repair];
            int active = hunters + guards + repairs;

            // Pressure model 
            float progress = o.RelaysTotal > 0 ? Mathf.Clamp01((float)o.RelaysOnline / o.RelaysTotal) : 0f;
            DesiredPressure = Mathf.Lerp(settings.basePressure, settings.maxPressure, progress);

            float recovery = Mathf.Clamp01(o.TimeSinceDamaged / Mathf.Max(0.01f, settings.recoveryTime));
            PlayerReadiness = Mathf.Clamp01(o.PlayerHealth01) * recovery;

            CurrentPressure = Mathf.Clamp01(hunters * settings.hunterPressure
                                          + guards * settings.guardPressure
                                          + repairs * settings.repairPressure);

            // Positive gap = room for more challenge; negative = already too much.
            float gap = DesiredPressure * PlayerReadiness - CurrentPressure;

            float waitScore = settings.waitBase
                            + settings.waitLowReadiness * (1f - PlayerReadiness)
                            + settings.waitOverPressure * Mathf.Max(0f, -gap);
            candidates.Add(new DirectorCandidate { Label = "Wait", IsWait = true, Score = waitScore });

            //Hard constraints: checked before any scoring
            if (active >= settings.maxActiveRobots)
                return Wait($"Active cap reached ({active}/{settings.maxActiveRobots})", waitScore);
            if (ActivationsUsed >= settings.activationBudget)
                return Wait($"Activation budget used ({ActivationsUsed}/{settings.activationBudget})", waitScore);
            float cooldownLeft = CooldownRemaining(o.Now);
            if (cooldownLeft > 0f)
                return Wait($"Cooldown: {cooldownLeft:0}s left", waitScore);

            //Filter bays (independent of robot type)
            var bayValid = new bool[o.Bays.Count];
            var bayFit = new float[o.Bays.Count];
            int validBays = 0;
            for (int i = 0; i < o.Bays.Count; i++)
            {
                BayInfo bay = o.Bays[i];
                string reject = null;
                if (bay.VisibleToPlayer) reject = "in player's view";
                else if (bay.DistanceToPlayer < settings.minSpawnDistance) reject = $"too close ({bay.DistanceToPlayer:0}m)";
                else if (!bay.HasRoute) reject = "no route to player";

                if (reject != null)
                {
                    candidates.Add(new DirectorCandidate { Label = $"Bay {i}", BayIndex = i, RejectedReason = reject });
                    continue;
                }

                bayValid[i] = true;
                validBays++;
                // Prefer bays a medium route length away: far enough to be fair, close enough to matter.
                float ideal = Mathf.Max(0.01f, settings.idealRouteLength);
                bayFit[i] = Mathf.Clamp01(1f - Mathf.Abs(bay.RouteLength - ideal) / ideal);
            }

            if (validBays == 0)
                return Wait("No safe bay: waiting", waitScore);

            // Score every valid (type, bay) option 
            DirectorDecision best = Wait("Waiting scores highest", waitScore);
            foreach (RobotType type in AllTypes)
            {
                if (!o.AvailableByType[(int)type])
                {
                    candidates.Add(new DirectorCandidate { Label = type.ToString(), Type = type, RejectedReason = "none left in pool" });
                    continue;
                }

                float typeFit = TypeFit(type, hunters, guards, repairs, active, progress, out string typeReject);
                if (typeReject != null)
                {
                    candidates.Add(new DirectorCandidate { Label = type.ToString(), Type = type, RejectedReason = typeReject });
                    continue;
                }

                for (int i = 0; i < o.Bays.Count; i++)
                {
                    if (!bayValid[i]) continue;

                    float score = settings.weightPressureGap * gap
                                + settings.weightTypeFit * typeFit
                                + settings.weightBayFit * bayFit[i];
                    candidates.Add(new DirectorCandidate { Label = $"{type} @ Bay {i}", Type = type, BayIndex = i, Score = score });

                    if (score > best.Score)
                    {
                        best = new DirectorDecision
                        {
                            Activate = true,
                            Type = type,
                            BayIndex = i,
                            Score = score,
                            Reason = $"Activate {type} at bay {i} (score {score:0.00} > wait {waitScore:0.00})"
                        };
                    }
                }
            }

            LastReason = best.Reason;
            return best;
        }

        /// <summary>
        /// How well a robot type suits the current encounter (0-1).
        /// Favours types that are missing from the current mix; Repair is only
        /// useful when there are other robots to support.
        /// </summary>
        private static float TypeFit(RobotType type, int hunters, int guards, int repairs, int active,
                                     float progress, out string reject)
        {
            reject = null;
            int sameType = type == RobotType.Hunter ? hunters : type == RobotType.Guard ? guards : repairs;
            float diversity = active > 0 ? 1f - (float)sameType / active : 1f;

            switch (type)
            {
                case RobotType.Repair:
                    if (hunters + guards == 0)
                    {
                        reject = "no robots to support";
                        return 0f;
                    }
                    return diversity;

                case RobotType.Guard:
                    // Defenders become more relevant as the player restores relays.
                    return Mathf.Clamp01(diversity + 0.3f * progress);

                default:
                    return diversity;
            }
        }

        private DirectorDecision Wait(string reason, float score)
        {
            LastReason = reason;
            return new DirectorDecision { Activate = false, Score = score, Reason = reason };
        }
    }
}
