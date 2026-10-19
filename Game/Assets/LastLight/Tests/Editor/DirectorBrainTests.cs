using System.Collections.Generic;
using LastLight.AI.Director;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>
    /// EditMode tests for the Encounter Director's decision logic.
    /// Covers the required cases: five-active cap, cooldown, four-activation
    /// budget, low player health, no safe bay, and reset.
    /// </summary>
    public class DirectorBrainTests
    {
        private DirectorSettings settings;
        private DirectorBrain brain;

        [SetUp]
        public void SetUp()
        {
            settings = new DirectorSettings();          // cap 5, budget 4, cooldown 20 s
            brain = new DirectorBrain(settings, startTime: 0f);
        }

        private static BayInfo SafeBay() => new BayInfo
        {
            Position = new Vector3(20f, 0f, 0f),
            VisibleToPlayer = false,
            DistanceToPlayer = 20f,
            HasRoute = true,
            RouteLength = 18f
        };

        /// <summary>A healthy, recovered player with no robots active, well after the cooldown.</summary>
        private static DirectorObservation Calm(float now = 100f, params BayInfo[] bays) => new DirectorObservation
        {
            Now = now,
            PlayerHealth01 = 1f,
            TimeSinceDamaged = 999f,
            RelaysOnline = 0,
            RelaysTotal = 2,
            ActiveByType = new int[3],
            AvailableByType = new[] { true, true, true },
            Bays = new List<BayInfo>(bays.Length > 0 ? bays : new[] { SafeBay() })
        };

        [Test]
        public void CalmSituation_ActivatesARobot()
        {
            DirectorDecision decision = brain.Decide(Calm());

            Assert.IsTrue(decision.Activate, decision.Reason);
        }

        [Test]
        public void RepairRobot_RejectedWhenNobodyToSupport()
        {
            brain.Decide(Calm());

            bool repairRejected = false;
            foreach (DirectorCandidate c in brain.Candidates)
                if (c.Type == RobotType.Repair && c.RejectedReason != null) repairRejected = true;
            Assert.IsTrue(repairRejected);
        }

        [Test]
        public void Cooldown_PreventsActivation()
        {
            DirectorDecision decision = brain.Decide(Calm(now: settings.cooldown - 1f));

            Assert.IsFalse(decision.Activate);
            StringAssert.Contains("Cooldown", decision.Reason);
        }

        [Test]
        public void ActiveCap_PreventsActivation()
        {
            DirectorObservation o = Calm();
            o.ActiveByType = new[] { 3, 2, 0 }; // five living robots

            DirectorDecision decision = brain.Decide(o);

            Assert.IsFalse(decision.Activate);
            StringAssert.Contains("Active cap", decision.Reason);
        }

        [Test]
        public void ExhaustedBudget_PreventsActivation_EvenAfterDeaths()
        {
            for (int i = 0; i < settings.activationBudget; i++)
                brain.RecordActivation(10f);

            // All activated robots have died (zero active), but the budget is never refunded.
            DirectorDecision decision = brain.Decide(Calm(now: 100f));

            Assert.IsFalse(decision.Activate);
            StringAssert.Contains("budget", decision.Reason);
        }

        [Test]
        public void LowHealthPlayer_DirectorWaits()
        {
            DirectorObservation o = Calm();
            o.PlayerHealth01 = 0.1f;
            o.TimeSinceDamaged = 1f;

            DirectorDecision decision = brain.Decide(o);

            Assert.IsFalse(decision.Activate, "Wait should outscore every activation for a struggling player.");
        }

        [Test]
        public void NoSafeBay_DirectorWaits()
        {
            BayInfo visible = SafeBay();
            visible.VisibleToPlayer = true;
            BayInfo tooClose = SafeBay();
            tooClose.DistanceToPlayer = 4f;
            BayInfo noRoute = SafeBay();
            noRoute.HasRoute = false;

            DirectorDecision decision = brain.Decide(Calm(100f, visible, tooClose, noRoute));

            Assert.IsFalse(decision.Activate);
            StringAssert.Contains("No safe bay", decision.Reason);
        }

        [Test]
        public void HighPressure_DirectorWaits()
        {
            DirectorObservation o = Calm();
            o.ActiveByType = new[] { 2, 1, 0 }; // pressure 0.8, above the desired 0.35

            DirectorDecision decision = brain.Decide(o);

            Assert.IsFalse(decision.Activate);
        }

        [Test]
        public void Reset_ClearsActivationCounter()
        {
            brain.RecordActivation(10f);
            brain.RecordActivation(40f);

            brain.Reset(now: 50f);

            Assert.AreEqual(0, brain.ActivationsUsed);
        }
    }
}
