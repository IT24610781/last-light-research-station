using System;
using UnityEngine;

public sealed class StationProgressionManager : MonoBehaviour
{
    public static StationProgressionManager Instance { get; private set; }

    public bool RelayAOnline { get; private set; }
    public bool RelayBOnline { get; private set; }
    public event Action ProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool ActivateRelay(StationRelay.RelayId relay)
    {
        if (relay == StationRelay.RelayId.A)
        {
            if (RelayAOnline) return false;
            RelayAOnline = true;
        }
        else
        {
            if (RelayBOnline) return false;
            RelayBOnline = true;
        }

        Debug.Log($"Relay {relay} is online.", this);
        ProgressChanged?.Invoke();
        return true;
    }

    public bool CanOpen(StationSlidingDoor.AccessRequirement requirement)
    {
        switch (requirement)
        {
            case StationSlidingDoor.AccessRequirement.RelayA:
                return RelayAOnline;
            case StationSlidingDoor.AccessRequirement.BothRelays:
                return RelayAOnline && RelayBOnline;
            default:
                return true;
        }
    }

    public void ReportLocked(StationSlidingDoor.AccessRequirement requirement)
    {
        string reason = requirement == StationSlidingDoor.AccessRequirement.RelayA
            ? "Relay A must be activated first."
            : "Both station relays must be activated first.";
        Debug.Log(reason, this);
    }

    public bool TryEvacuate()
    {
        if (!RelayAOnline || !RelayBOnline)
        {
            ReportLocked(StationSlidingDoor.AccessRequirement.BothRelays);
            return false;
        }

        GameSessionController session = FindFirstObjectByType<GameSessionController>();
        if (session == null) return false;
        session.CompleteGame();
        return true;
    }
}
