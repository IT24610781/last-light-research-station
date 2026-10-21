using UnityEngine;

public sealed class StationRelay : MonoBehaviour, IInteractable
{
    public enum RelayId { A, B }

    [SerializeField] private RelayId relay;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Renderer[] linkedIndicators = new Renderer[0];
    [SerializeField] private Color offlineColor = new Color(0.65f, 0.08f, 0.03f);
    [SerializeField] private Color onlineColor = new Color(0.05f, 0.9f, 0.35f);
    private bool activated;

    public RelayId Id => relay;
    public bool Activated => activated;

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
        StationAudioDirector.Instance?.PlayRelay(transform.position);
    }

    private void RefreshIndicator()
    {
        if (indicator == null) indicator = GetComponentInChildren<Renderer>();
        if (!Application.isPlaying) return;
        SetColor(indicator);
        foreach (Renderer linked in linkedIndicators) SetColor(linked);
    }

    private void SetColor(Renderer target)
    {
        if (target == null) return;
        Material runtimeMaterial = target.material;
        Color color = activated ? onlineColor : offlineColor;
        if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", color);
        else if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.color = color;
        if (runtimeMaterial.HasProperty("_EmissionColor"))
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.SetColor("_EmissionColor", color * (activated ? 2f : 0.6f));
        }
    }
}
