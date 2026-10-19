using NUnit.Framework;
using LastLight.AI.Guard;

public class GuardUtilityBrainTests
{
    private static GuardObservation Obs(
        float health, float ammo, float exposure, float distance,
        bool visible, bool cover, float coverQuality, bool retreat, float retreatQuality)
    {
        return new GuardObservation
        {
            Health01 = health,
            Ammo01 = ammo,
            Exposure01 = exposure,
            TargetDistance01 = distance,
            HasVisibleTarget = visible,
            CoverAvailable = cover,
            CoverQuality01 = coverQuality,
            RetreatAvailable = retreat,
            RetreatQuality01 = retreatQuality
        };
    }

    // Healthy, full magazine, lightly exposed, target visible: Attack is clearly best.
    private static GuardObservation Healthy()
    {
        return Obs(1f, 1f, 0.1f, 0.5f, true, true, 0.8f, true, 0.5f);
    }

    // Hurt and fully exposed with great cover available: cover is far better than attacking.
    private static GuardObservation HurtAndExposedWithCover()
    {
        return Obs(0.3f, 1f, 1f, 0.5f, true, true, 1f, false, 0f);
    }

    [Test]
    public void HealthyWithVisibleTarget_ChoosesAttack()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        GuardDecision d = brain.Decide(Healthy(), 0f);
        Assert.AreEqual(GuardAction.Attack, d.Action);
    }

    [Test]
    public void EmptyMagazine_ReloadsAndNeverAttacks_EvenWithoutCover()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        GuardObservation o = Obs(1f, 0f, 0.7f, 0.5f, true, false, 0f, false, 0f);
        GuardDecision d = brain.Decide(o, 0f);

        Assert.IsFalse(d.IsValid(GuardAction.Attack));
        Assert.IsFalse(d.IsValid(GuardAction.TakeCover));
        Assert.AreEqual(GuardAction.Reload, d.Action);
    }

    [Test]
    public void LowHealthWithRetreatPoint_ChoosesRetreat()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        GuardObservation o = Obs(0.15f, 0.8f, 0.7f, 0.5f, true, true, 0.5f, true, 0.9f);
        GuardDecision d = brain.Decide(o, 0f);
        Assert.AreEqual(GuardAction.Retreat, d.Action);
    }

    [Test]
    public void NoCoverAvailable_CoverIsInvalidAndGuardStillActs()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        GuardObservation o = Obs(0.6f, 1f, 0.9f, 0.5f, true, false, 0f, false, 0f);
        GuardDecision d = brain.Decide(o, 0f);

        Assert.IsFalse(d.IsValid(GuardAction.TakeCover));
        Assert.AreNotEqual(GuardAction.TakeCover, d.Action);
        Assert.AreEqual(GuardAction.Attack, d.Action, "Should attack rather than freeze");
    }

    [Test]
    public void NothingUsefulAvailable_FallsBackToHold()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        GuardObservation o = Obs(1f, 1f, 0.1f, 0.5f, false, false, 0f, false, 0f);
        GuardDecision d = brain.Decide(o, 0f);
        Assert.AreEqual(GuardAction.Hold, d.Action);
    }

    [Test]
    public void MinimumDuration_HoldsCurrentActionEvenIfAnotherScoresHigher()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        brain.Decide(Healthy(), 0f); // commits to Attack at t = 0

        GuardDecision early = brain.Decide(HurtAndExposedWithCover(), 0.3f);
        Assert.AreEqual(GuardAction.Attack, early.Action);
        Assert.IsFalse(early.Switched);
        Assert.Greater(early.ScoreOf(GuardAction.TakeCover), early.ScoreOf(GuardAction.Attack));

        GuardDecision later = brain.Decide(HurtAndExposedWithCover(), 0.7f);
        Assert.AreEqual(GuardAction.TakeCover, later.Action);
        Assert.IsTrue(later.Switched);
    }

    [Test]
    public void SwitchMargin_SmallAdvantageDoesNotSwitch_LargeAdvantageDoes()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        brain.Decide(Healthy(), 0f); // Attack

        // Full health, fully exposed: cover leads attack by less than the 0.15 margin.
        GuardObservation small = Obs(1f, 1f, 1f, 0.5f, true, true, 0.8f, false, 0f);
        GuardDecision keep = brain.Decide(small, 1.0f);
        Assert.AreEqual(GuardAction.Attack, keep.Action);
        Assert.Greater(keep.ScoreOf(GuardAction.TakeCover), keep.ScoreOf(GuardAction.Attack));

        // Hurt and exposed: cover leads by far more than the margin.
        GuardObservation large = Obs(0.6f, 1f, 1f, 0.5f, true, true, 0.8f, false, 0f);
        GuardDecision change = brain.Decide(large, 2.0f);
        Assert.AreEqual(GuardAction.TakeCover, change.Action);
    }

    [Test]
    public void InvalidCurrentAction_OverridesHoldTimeImmediately()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        brain.Decide(Healthy(), 0f); // Attack

        // 0.1 s later the magazine is empty: Attack is invalid, so switch right away.
        GuardObservation empty = Obs(1f, 0f, 0.1f, 0.5f, true, true, 0.8f, true, 0.5f);
        GuardDecision d = brain.Decide(empty, 0.1f);

        Assert.AreEqual(GuardAction.Reload, d.Action);
        Assert.IsTrue(d.Switched);
    }

    [Test]
    public void Reset_ClearsCurrentAction()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        brain.Decide(Healthy(), 0f);
        Assert.IsTrue(brain.HasCurrent);

        brain.Reset();
        Assert.IsFalse(brain.HasCurrent);

        // With no current action there is no hold time: the best action is taken at once.
        GuardDecision d = brain.Decide(HurtAndExposedWithCover(), 0.1f);
        Assert.AreEqual(GuardAction.TakeCover, d.Action);
    }

    [Test]
    public void OutOfRangeInputs_AreClampedAndScoresStayInZeroToOne()
    {
        GuardUtilityBrain brain = new GuardUtilityBrain();
        GuardObservation o = Obs(5f, -2f, 9f, -1f, true, true, 3f, true, -3f);
        GuardDecision d = brain.Decide(o, 0f);

        for (int i = 0; i < d.Scores.Length; i++)
        {
            Assert.GreaterOrEqual(d.Scores[i], 0f);
            Assert.LessOrEqual(d.Scores[i], 1.0001f);
        }
    }
}
