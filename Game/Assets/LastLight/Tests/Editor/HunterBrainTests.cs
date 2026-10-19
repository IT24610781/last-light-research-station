using System.Collections.Generic;
using LastLight.AI.Hunter;
using LastLight.AI.Movement;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>
    /// EditMode tests for the Sound Hunter's decision logic (plain C#, no scene needed).
    /// Covers the required cases: competing, stale and unreachable evidence,
    /// lost sight / hidden-player relocation, and target hysteresis.
    /// </summary>
    public class HunterBrainTests
    {
        /// <summary>Straight-line routes, except to positions marked unreachable.</summary>
        private sealed class FakePaths : IPathProvider
        {
            public readonly List<Vector3> Unreachable = new List<Vector3>();

            public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> pathOut, out float pathLength)
            {
                pathOut.Clear();
                pathLength = 0f;
                foreach (Vector3 blocked in Unreachable)
                    if ((blocked - goal).sqrMagnitude < 0.01f) return false;

                pathOut.Add(goal);
                pathLength = Vector3.Distance(start, goal);
                return true;
            }
        }

        private HunterSettings settings;
        private HunterMemory memory;
        private FakePaths paths;
        private HunterBrain brain;

        private static readonly Vector3 Self = Vector3.zero;

        [SetUp]
        public void SetUp()
        {
            settings = new HunterSettings();
            memory = new HunterMemory(settings.maxRememberedNoises, settings.noiseMergeRadius);
            paths = new FakePaths();
            brain = new HunterBrain(settings, memory, paths, new Vector3[0]);
        }

        private HunterIntent TickBlind(float now) =>
            brain.Tick(new HunterObservation { SelfPosition = Self, PlayerVisible = false }, now);

        [Test]
        public void CompetingNoises_ChoosesStrongerWhenEquallyRecentAndFar()
        {
            memory.AddNoise(new Vector3(5f, 0f, 0f), 0.3f, 0f);  // quiet
            memory.AddNoise(new Vector3(-5f, 0f, 0f), 0.9f, 0f); // loud, same distance

            TickBlind(0f);

            Assert.AreEqual(HunterState.Investigate, brain.State);
            Assert.AreEqual(new Vector3(-5f, 0f, 0f), brain.TargetNoise.Position);
        }

        [Test]
        public void CompetingNoises_NearerNoiseCanBeatSlightlyLouderFarNoise()
        {
            memory.AddNoise(new Vector3(2f, 0f, 0f), 0.6f, 0f);   // near
            memory.AddNoise(new Vector3(-38f, 0f, 0f), 0.7f, 0f); // far: route cost ~0.95

            TickBlind(0f);

            Assert.AreEqual(new Vector3(2f, 0f, 0f), brain.TargetNoise.Position,
                "Route cost should outweigh a small strength advantage.");
        }

        [Test]
        public void StaleEvidence_IsForgottenAfterMemoryDuration()
        {
            memory.AddNoise(new Vector3(5f, 0f, 0f), 0.9f, 0f);

            TickBlind(settings.noiseMemoryDuration + 1f);

            Assert.AreEqual(0, memory.Noises.Count);
            Assert.IsNull(brain.TargetNoise);
            Assert.AreNotEqual(HunterState.Investigate, brain.State);
        }

        [Test]
        public void UnreachableNoise_IsRejectedInsteadOfRetried()
        {
            var unreachable = new Vector3(6f, 0f, 0f);
            paths.Unreachable.Add(unreachable);
            memory.AddNoise(unreachable, 0.9f, 0f);

            TickBlind(0f);

            Assert.IsNull(brain.TargetNoise);
            Assert.AreNotEqual(HunterState.Investigate, brain.State);
            Assert.IsTrue(brain.LastScores[0].Rejected);
            Assert.Greater(memory.Noises[0].RejectedUntil, 0f, "It should wait before retrying.");
        }

        [Test]
        public void LostSight_ChasesLastSeenPositionNotHiddenPlayer()
        {
            var seenAt = new Vector3(0f, 0f, 12f); // beyond attack range, so it pursues
            brain.Tick(new HunterObservation { SelfPosition = Self, PlayerVisible = true, PlayerPosition = seenAt }, 0f);
            Assert.AreEqual(HunterState.Pursue, brain.State);

            // Player hides and moves somewhere else. The brain must not learn the new position.
            var hiddenAt = new Vector3(10f, 0f, -8f);
            brain.Tick(new HunterObservation { SelfPosition = Self, PlayerVisible = false, PlayerPosition = hiddenAt }, 0.2f);

            Assert.AreEqual(HunterState.Pursue, brain.State);
            Assert.AreEqual(seenAt, brain.CurrentGoal);
            Assert.AreNotEqual(hiddenAt, brain.CurrentGoal);
        }

        [Test]
        public void VisiblePlayerInRange_Attacks()
        {
            var playerPosition = new Vector3(0f, 0f, 3f);

            HunterIntent intent = brain.Tick(
                new HunterObservation { SelfPosition = Self, PlayerVisible = true, PlayerPosition = playerPosition }, 0f);

            Assert.AreEqual(HunterState.Attack, brain.State);
            Assert.AreEqual(HunterAction.Attack, intent.Action);
        }

        [Test]
        public void Hysteresis_DoesNotSwitchForSlightlyBetterNoise()
        {
            memory.AddNoise(new Vector3(5f, 0f, 0f), 0.6f, 0f);
            TickBlind(0f);
            NoiseMemory first = brain.TargetNoise;

            // Slightly louder noise elsewhere: better score, but within the switch margin.
            memory.AddNoise(new Vector3(-5f, 0f, 0f), 0.7f, 0f);
            TickBlind(0.2f);

            Assert.AreSame(first, brain.TargetNoise);
        }

        [Test]
        public void Reset_ClearsStateAndTarget()
        {
            memory.AddNoise(new Vector3(5f, 0f, 0f), 0.9f, 0f);
            TickBlind(0f);

            brain.Reset();

            Assert.AreEqual(HunterState.Patrol, brain.State);
            Assert.IsNull(brain.TargetNoise);
            Assert.IsFalse(brain.HasGoal);
        }
    }

    /// <summary>EditMode tests for the Hunter's bounded, forgetting memory.</summary>
    public class HunterMemoryTests
    {
        [Test]
        public void NearbyNoises_AreMergedIntoOne()
        {
            var memory = new HunterMemory(capacity: 8, mergeRadius: 2f);
            memory.AddNoise(new Vector3(0f, 0f, 0f), 0.3f, 0f);
            memory.AddNoise(new Vector3(1f, 0f, 0f), 0.8f, 1f);

            Assert.AreEqual(1, memory.Noises.Count);
            Assert.AreEqual(0.8f, memory.Noises[0].PerceivedStrength, 1e-5f);
            Assert.AreEqual(1f, memory.Noises[0].TimeHeard, 1e-5f);
        }

        [Test]
        public void FullMemory_ForgetsOldestNoise()
        {
            var memory = new HunterMemory(capacity: 2, mergeRadius: 1f);
            memory.AddNoise(new Vector3(0f, 0f, 0f), 0.5f, 0f);
            memory.AddNoise(new Vector3(10f, 0f, 0f), 0.5f, 1f);
            memory.AddNoise(new Vector3(20f, 0f, 0f), 0.5f, 2f);

            Assert.AreEqual(2, memory.Noises.Count);
            foreach (NoiseMemory noise in memory.Noises)
                Assert.AreNotEqual(0f, noise.TimeHeard, "The oldest noise should have been removed.");
        }

        [Test]
        public void LastSeen_ExpiresAfterDuration()
        {
            var memory = new HunterMemory(8, 2f);
            memory.SetLastSeen(Vector3.one, 0f);

            memory.Forget(now: 11f, noiseDuration: 15f, lastSeenDuration: 10f);

            Assert.IsFalse(memory.HasLastSeen);
        }
    }
}
