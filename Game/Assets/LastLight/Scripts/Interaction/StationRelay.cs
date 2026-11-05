using UnityEngine;

public sealed class StationRelay : MonoBehaviour, IInteractable
{
    public enum RelayId { A, B }

    [SerializeField] private RelayId relay;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Color offlineColor = new Color(0.65f, 0.08f, 0.03f);
    [SerializeField] private Color onlineColor = new Color(0.05f, 0.85f, 1f);
    private bool activated;

    public void Configure(RelayId id, Renderer statusIndicator = null)
    {
        relay = id;
        indicator = statusIndicator;
        RefreshIndicator();
    }

    private void Awake() => RefreshIndicator();

    public void Interact()
    {
        if (activated) return;
        StationProgressionManager progression = StationProgressionManager.Instance;
        if (progression == null || !progression.ActivateRelay(relay)) return;
        activated = true;
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        if (indicator == null) indicator = GetComponentInChildren<Renderer>();
        if (indicator == null || !Application.isPlaying) return;

        Material runtimeMaterial = indicator.material;
        runtimeMaterial.color = activated ? onlineColor : offlineColor;
        if (runtimeMaterial.HasProperty("_EmissionColor"))
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.SetColor(
                "_EmissionColor", activated ? onlineColor * 2f : offlineColor);
        }
    }
}
