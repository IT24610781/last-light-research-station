using UnityEngine;

public sealed class StationRelay : MonoBehaviour, IInteractable
{
    public enum RelayId { A, B }

    [SerializeField] private RelayId relay;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Renderer[] linkedIndicators = new Renderer[0];
    [SerializeField] private Transform switchLever;
    [SerializeField] private TextMesh statusLabel;
    [SerializeField] private Color offlineColor = new Color(0.65f, 0.08f, 0.03f);
    [SerializeField] private Color onlineColor = new Color(0.05f, 0.9f, 0.35f);
    [SerializeField, Min(0.1f)] private float holdDuration = 2f;
    private float holdSeconds;
    private int lastHeldFrame = -1;
    private bool activated;
    public float HoldProgress => activated ? 1f : Mathf.Clamp01(holdSeconds / holdDuration);

    public void CancelHold() => holdSeconds = 0f;

    private void LateUpdate()
    {
        if (Time.timeScale == 0f || lastHeldFrame != Time.frameCount) CancelHold();
    }

    private void OnDisable() => CancelHold();

    public RelayId Id => relay;
    public bool Activated => activated;

    public void Configure(RelayId id, Renderer statusIndicator = null)
    {
        relay = id;
        indicator = statusIndicator;
        RefreshIndicator();
    }

    private void Awake() => RefreshIndicator();

    public void Interact() => AdvanceHold(Time.deltaTime);

    // Called only while the player's interaction ray still hits this console.
    public void AdvanceHold(float seconds)
    {
        if (activated || Time.timeScale == 0f || seconds <= 0f ||
            float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        lastHeldFrame = Time.frameCount;
        holdSeconds += seconds;
        if (holdSeconds < holdDuration) return;
        StationProgressionManager progression = StationProgressionManager.Instance;
        if (progression == null || !progression.ActivateRelay(relay)) return;
        activated = true;
        RefreshIndicator();
        StationAudioDirector.Instance?.PlayRelay(transform.position);
    }

    private void RefreshIndicator()
    {
        if (switchLever != null) switchLever.localRotation = Quaternion.Euler(activated ? -35f : 35f, 0f, 0f);
        if (statusLabel != null) statusLabel.text = activated ? "ONLINE" : "HOLD E  /  RESTORE";
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
