using UnityEngine;

/// <summary>
/// Exit trigger inside the evacuation bay. Entering it alive with both relays online wins the
/// mission; StationProgressionManager.TryEvacuate enforces the relay and alive checks.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class StationEvacuationZone : MonoBehaviour
{
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerShooter>() == null) return;
        StationProgressionManager progression = StationProgressionManager.Instance;
        if (progression != null) progression.TryEvacuate();
    }
}
