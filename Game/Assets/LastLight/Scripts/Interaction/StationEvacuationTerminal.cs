using UnityEngine;

public sealed class StationEvacuationTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        StationProgressionManager progression = StationProgressionManager.Instance;
        if (progression != null) progression.TryEvacuate();
    }
}
