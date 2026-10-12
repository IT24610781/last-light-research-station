using System.Collections.Generic;
using LastLight.AI.Movement;
using UnityEngine;

namespace LastLight.AI.Hunter
{
    public enum HunterState { Patrol, Investigate, Search, Pursue, Attack, Return }

    public enum HunterAction { Hold, MoveTo, Attack }

    /// <summary>What the Hunter is allowed to know this decision tick (from its own senses only).</summary>
    public struct HunterObservation
    {
        public Vector3 SelfPosition;
        public bool PlayerVisible;
        public Vector3 PlayerPosition; // only meaningful when PlayerVisible is true
    }

    /// <summary>The brain's decision, carried out by the controller and motor.</summary>
    public struct HunterIntent
    {
        public HunterAction Action;
        public Vector3 Destination;
        public Vector3 LookAt;
        public float SpeedMultiplier;
        public int GoalVersion; // changes whenever a new destination is chosen
    }

    /// <summary>Score breakdown for one remembered noise, kept for the debug overlay and viva.</summary>
    public struct ScoredNoise
    {
        public NoiseMemory Noise;
        public bool Reachable;
        public bool Rejected;
        public float Strength01;
        public float Recency01;
        public float Cost01;
        public float Score;
    }

    /// <summary>
    /// Model-based, goal-driven decision maker for the Sound Hunter.
    /// </summary>
    public sealed class HunterBrain
    {
        private readonly HunterSettings settings;
        private readonly HunterMemory memory;
        private readonly IPathProvider paths;
        private readonly Vector3[] patrolPoints;

        private readonly List<ScoredNoise> scores = new List<ScoredNoise>();
        private readonly List<Vector3> searchPoints = new List<Vector3>();
        private readonly List<Vector3> scratchPath = new List<Vector3>();

        private int goalVersion;
        private int patrolIndex;
        private int searchIndex;
        private float searchStartTime;
        private float currentTargetScore;
        private Vector3 selfPosition;
        private MovementStatus? pendingMovement;

        public HunterState State { get; private set; } = HunterState.Patrol;
        public string Reason { get; private set; } = "Starting patrol";
        public bool HasGoal { get; private set; }
        public Vector3 CurrentGoal { get; private set; }
        public NoiseMemory TargetNoise { get; private set; }
        public IReadOnlyList<ScoredNoise> LastScores => scores;
        public IReadOnlyList<Vector3> SearchPoints => searchPoints;

        public HunterBrain(HunterSettings settings, HunterMemory memory, IPathProvider paths, Vector3[] patrolPoints)
        {
            this.settings = settings;
            this.memory = memory;
            this.paths = paths;
            this.patrolPoints = patrolPoints ?? new Vector3[0];
        }

        /// <summary>
        /// Called by the controller when the motor arrives or gets blocked.
        /// Results for an old goal are ignored, so a stale "Arrived" cannot trigger the wrong reaction.
        /// </summary>
        public void NotifyMovement(MovementStatus status, int forGoalVersion)
        {
            if (forGoalVersion != goalVersion) return;
            if (status == MovementStatus.Arrived || status == MovementStatus.Blocked)
                pendingMovement = status;
        }

        public void Reset()
        {
            State = HunterState.Patrol;
            Reason = "Reset";
            HasGoal = false;
            TargetNoise = null;
            patrolIndex = 0;
            searchIndex = 0;
            searchPoints.Clear();
            scores.Clear();
            pendingMovement = null;
            goalVersion++;
        }

        public HunterIntent Tick(in HunterObservation obs, float now)
        {
            selfPosition = obs.SelfPosition;
            memory.Forget(now, settings.noiseMemoryDuration, settings.lastSeenMemoryDuration);
            MovementStatus? movement = pendingMovement;
            pendingMovement = null;

            // 1. Direct sight beats every other piece of evidence.
            if (obs.PlayerVisible)
                return ReactToVisiblePlayer(obs, now);

            // 2. Advance the current state using the movement result.
            switch (State)
            {
                case HunterState.Attack:
                    // Sight was just lost: chase the remembered position, never the hidden real one.
                    if (memory.HasLastSeen)
                    {
                        Enter(HunterState.Pursue, "Lost sight: moving to last seen position");
                        SetGoal(memory.LastSeenPosition);
                    }
                    else StartReturn("Lost sight with no memory");
                    break;

                case HunterState.Pursue:
                    if (!HasGoal || CurrentGoal != memory.LastSeenPosition)
                    {
                        if (memory.HasLastSeen) SetGoal(memory.LastSeenPosition);
                    }
                    if (movement == MovementStatus.Arrived)
                    {
                        memory.ClearLastSeen();
                        StartSearch(CurrentGoal, obs.SelfPosition, now, "Reached last seen position: searching");
                    }
                    else if (movement == MovementStatus.Blocked)
                    {
                        memory.ClearLastSeen();
                        StartSearch(obs.SelfPosition, obs.SelfPosition, now, "Chase blocked: searching nearby");
                    }
                    break;

                case HunterState.Investigate:
                    if (movement == MovementStatus.Arrived && TargetNoise != null)
                    {
                        Vector3 noisePosition = TargetNoise.Position;
                        memory.Remove(TargetNoise); // evidence checked, no contact
                        TargetNoise = null;
                        StartSearch(noisePosition, obs.SelfPosition, now, "Reached noise without contact: searching");
                    }
                    else if (movement == MovementStatus.Blocked && TargetNoise != null)
                    {
                        TargetNoise.RejectedUntil = now + settings.rejectRetryDelay;
                        TargetNoise = null;
                        Reason = "Route to noise blocked: rejecting it for now";
                    }
                    else if (TargetNoise != null && !memory.Contains(TargetNoise))
                    {
                        TargetNoise = null; // evidence expired while travelling
                        Reason = "Noise evidence expired";
                    }
                    break;

                case HunterState.Search:
                    if (movement == MovementStatus.Arrived || movement == MovementStatus.Blocked)
                    {
                        searchIndex++;
                        GoToNextSearchPoint();
                    }
                    if (State == HunterState.Search && now - searchStartTime > settings.maxSearchDuration)
                        StartReturn("Search time limit reached");
                    break;

                case HunterState.Return:
                    if (movement == MovementStatus.Arrived || movement == MovementStatus.Blocked)
                    {
                        Enter(HunterState.Patrol, "Back on patrol route");
                        HasGoal = false;
                    }
                    break;

                case HunterState.Patrol:
                    if (movement == MovementStatus.Arrived || movement == MovementStatus.Blocked)
                    {
                        patrolIndex = (patrolIndex + 1) % Mathf.Max(1, patrolPoints.Length);
                        HasGoal = false;
                    }
                    break;
            }

            // 3. Weigh up remembered noises (not while chasing visual evidence).
            if (State != HunterState.Pursue)
                ConsiderNoises(obs.SelfPosition, now);

            // 4. Nothing to investigate after a failed target: go home.
            if (State == HunterState.Investigate && TargetNoise == null)
                StartReturn("No reachable evidence left");

            if (State == HunterState.Patrol && !HasGoal)
            {
                if (patrolPoints.Length == 0) return Hold();
                SetGoal(patrolPoints[patrolIndex]);
            }

            return HasGoal ? Move(SpeedFor(State)) : Hold();
        }

                               // Decisions 

        private HunterIntent ReactToVisiblePlayer(in HunterObservation obs, float now)
        {
            memory.SetLastSeen(obs.PlayerPosition, now);
            float distance = Flat(obs.PlayerPosition - obs.SelfPosition).magnitude;

            if (distance <= settings.attackRange)
            {
                Enter(HunterState.Attack, "Player visible and in range: attacking");
                HasGoal = false;
                return new HunterIntent
                {
                    Action = HunterAction.Attack,
                    LookAt = obs.PlayerPosition,
                    GoalVersion = goalVersion
                };
            }

            Enter(HunterState.Pursue, "Player visible but out of range: pursuing");
            TargetNoise = null;
            // Replan only when the player has moved meaningfully, not every tick.
            if (!HasGoal || Flat(CurrentGoal - obs.PlayerPosition).magnitude > settings.pursueReplanDistance)
                SetGoal(obs.PlayerPosition);
            return Move(settings.pursueSpeed);
        }

        /// <summary>
        /// Scores every remembered noise and investigates the best one.
        /// score = wS * strength + wR * recency - wC * routeCost, each scaled to 0-1.
        /// </summary>
        private void ConsiderNoises(Vector3 selfPosition, float now)
        {
            scores.Clear();
            ScoredNoise best = default;
            bool hasBest = false;
            bool currentTargetStillValid = false;

            foreach (NoiseMemory noise in memory.Noises)
            {
                var scored = new ScoredNoise { Noise = noise };

                if (noise.RejectedUntil > now)
                {
                    scored.Rejected = true;
                    scores.Add(scored);
                    continue;
                }

                if (!paths.TryFindPath(selfPosition, noise.Position, scratchPath, out float routeLength))
                {
                    // Unreachable: do not keep resubmitting it every tick.
                    noise.RejectedUntil = now + settings.rejectRetryDelay;
                    scored.Rejected = true;
                    scores.Add(scored);
                    continue;
                }

                scored.Reachable = true;
                scored.Strength01 = Mathf.Clamp01(noise.PerceivedStrength / Mathf.Max(0.0001f, settings.maxNoiseStrength));
                scored.Recency01 = Mathf.Clamp01(1f - (now - noise.TimeHeard) / settings.noiseMemoryDuration);
                scored.Cost01 = Mathf.Clamp01(routeLength / Mathf.Max(0.0001f, settings.maxRouteCost));
                scored.Score = settings.weightStrength * scored.Strength01
                             + settings.weightRecency * scored.Recency01
                             - settings.weightRouteCost * scored.Cost01;
                scores.Add(scored);

                if (noise == TargetNoise)
                {
                    currentTargetStillValid = true;
                    currentTargetScore = scored.Score;
                }
                if (!hasBest || scored.Score > best.Score)
                {
                    best = scored;
                    hasBest = true;
                }
            }

            if (!hasBest) return;

            bool investigating = State == HunterState.Investigate && TargetNoise != null && currentTargetStillValid;
            if (!investigating)
            {
                StartInvestigate(best, "Investigating best-scoring noise");
            }
            else if (best.Noise != TargetNoise && best.Score > currentTargetScore + settings.switchMargin)
            {
                // Hysteresis: only switch for a clearly better noise.
                StartInvestigate(best, "Switched to a clearly better noise");
            }
        }

        private void StartInvestigate(ScoredNoise target, string reason)
        {
            TargetNoise = target.Noise;
            currentTargetScore = target.Score;
            Enter(HunterState.Investigate, $"{reason} (score {target.Score:0.00})");
            SetGoal(target.Noise.Position);
        }

        private void StartSearch(Vector3 centre, Vector3 selfPosition, float now, string reason)
        {
            Enter(HunterState.Search, reason);
            searchStartTime = now;
            searchIndex = 0;
            searchPoints.Clear();

            // Points on a ring around the centre, starting on the far side from the Hunter.
            Vector3 away = Flat(centre - selfPosition);
            float startAngle = away.sqrMagnitude > 0.01f ? Mathf.Atan2(away.z, away.x) : 0f;
            int count = Mathf.Max(1, settings.searchPointCount);
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * (2f * Mathf.PI / count);
                Vector3 point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * settings.searchRadius;
                if (paths.TryFindPath(centre, point, scratchPath, out _))
                    searchPoints.Add(point); // skip points no route can reach
            }

            GoToNextSearchPoint();
        }

        private void GoToNextSearchPoint()
        {
            if (searchIndex < searchPoints.Count)
                SetGoal(searchPoints[searchIndex]);
            else
                StartReturn("Search finished without contact");
        }

        private void StartReturn(string reason)
        {
            TargetNoise = null;
            if (patrolPoints.Length == 0)
            {
                Enter(HunterState.Patrol, reason);
                HasGoal = false;
                return;
            }

            // Rejoin patrol at the patrol point nearest to the Hunter.
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                float d = Flat(patrolPoints[i] - selfPosition).sqrMagnitude;
                if (d < nearestDistance) { nearestDistance = d; nearest = i; }
            }
            patrolIndex = nearest;
            Enter(HunterState.Return, reason);
            SetGoal(patrolPoints[nearest]);
        }

        // Helpers 

        private void Enter(HunterState state, string reason)
        {
            State = state;
            Reason = reason;
        }

        private void SetGoal(Vector3 goal)
        {
            CurrentGoal = goal;
            HasGoal = true;
            goalVersion++;
            pendingMovement = null;
        }

        private HunterIntent Move(float speed) => new HunterIntent
        {
            Action = HunterAction.MoveTo,
            Destination = CurrentGoal,
            SpeedMultiplier = speed,
            GoalVersion = goalVersion
        };

        private HunterIntent Hold() => new HunterIntent { Action = HunterAction.Hold, GoalVersion = goalVersion };

        private float SpeedFor(HunterState state)
        {
            switch (state)
            {
                case HunterState.Pursue: return settings.pursueSpeed;
                case HunterState.Investigate: return settings.investigateSpeed;
                default: return settings.patrolSpeed;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
