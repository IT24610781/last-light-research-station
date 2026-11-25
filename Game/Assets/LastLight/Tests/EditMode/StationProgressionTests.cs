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
        // EditMode does not invoke the runtime Awake lifecycle automatically.
        typeof(StationProgressionManager).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(progression, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (progression != null) typeof(StationProgressionManager).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(progression, null);
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

    [TestCase(StationSlidingDoor.AccessRequirement.RelayA)]
    [TestCase(StationSlidingDoor.AccessRequirement.RelayB)]
    [TestCase(StationSlidingDoor.AccessRequirement.BothRelays)]
    public void DoorInteractionCannotBypassRelayRequirements(StationSlidingDoor.AccessRequirement requirement)
    {
        var door = root.AddComponent<StationSlidingDoor>();
        door.ConfigureAccess(requirement);
        door.Interact();
        Assert.That(door.IsOpen, Is.False);
        progression.ActivateRelay(StationRelay.RelayId.B);
        door.Interact();
        Assert.That(door.IsOpen, Is.EqualTo(requirement == StationSlidingDoor.AccessRequirement.RelayB));
        if (door.IsOpen) door.Interact();
        progression.ActivateRelay(StationRelay.RelayId.A);
        door.Interact();
        Assert.That(door.IsOpen, Is.True);
    }

    [Test]
    public void LockedDoorFailsClosedWithoutProgressionManager()
    {
        typeof(StationProgressionManager).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(progression, null);
        Object.DestroyImmediate(progression);
        var door = root.AddComponent<StationSlidingDoor>();
        door.ConfigureAccess(StationSlidingDoor.AccessRequirement.BothRelays);
        door.Interact();
        Assert.That(door.IsOpen, Is.False);
    }

    [Test]
    public void InterruptedRelayHoldDoesNotActivateOrRetainProgress()
    {
        float scale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            var relay = root.AddComponent<StationRelay>();
            relay.Configure(StationRelay.RelayId.A);
            relay.AdvanceHold(1f);
            Assert.That(progression.RelayAOnline, Is.False);
            relay.CancelHold();
            Assert.That(relay.HoldProgress, Is.Zero);
            relay.AdvanceHold(1.2f);
            Assert.That(progression.RelayAOnline, Is.False);
            relay.AdvanceHold(1f);
            Assert.That(progression.RelayAOnline, Is.True);
            Assert.That(progression.RelaysOnline, Is.EqualTo(1));
        }
        finally { Time.timeScale = scale; }
    }
}
