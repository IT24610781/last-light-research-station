using NUnit.Framework;
using UnityEngine;

public sealed class StationProgressionTests
{
    private GameObject root;
    private StationProgressionManager progression;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Station Progression Test");
        progression = root.AddComponent<StationProgressionManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void RelayLockedDoorsFollowMissionProgress()
    {
        Assert.That(
            progression.CanOpen(StationSlidingDoor.AccessRequirement.None),
            Is.True);
        Assert.That(
            progression.CanOpen(StationSlidingDoor.AccessRequirement.RelayA),
            Is.False);
        Assert.That(
            progression.CanOpen(StationSlidingDoor.AccessRequirement.BothRelays),
            Is.False);

        Assert.That(progression.ActivateRelay(StationRelay.RelayId.A), Is.True);
        Assert.That(
            progression.CanOpen(StationSlidingDoor.AccessRequirement.RelayA),
            Is.True);
        Assert.That(
            progression.CanOpen(StationSlidingDoor.AccessRequirement.BothRelays),
            Is.False);

        Assert.That(progression.ActivateRelay(StationRelay.RelayId.B), Is.True);
        Assert.That(
            progression.CanOpen(StationSlidingDoor.AccessRequirement.BothRelays),
            Is.True);
    }

    [Test]
    public void RelayCanOnlyActivateOnce()
    {
        Assert.That(progression.ActivateRelay(StationRelay.RelayId.A), Is.True);
        Assert.That(progression.ActivateRelay(StationRelay.RelayId.A), Is.False);
    }
}
