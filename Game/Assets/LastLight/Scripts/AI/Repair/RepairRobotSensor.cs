using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-300)]
[RequireComponent(typeof(Health))]
public class RepairRobotSensor : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform eye;
    [SerializeField, Min(0.1f)] private float viewDistance = 12f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
    [SerializeField, Min(0.1f)] private float immediateDangerDistance = 3f;
    [SerializeField, Min(0.1f)] private float hearingRange = 12f;
    [SerializeField, Min(0.01f)] private float minimumHeardStrength = 2f;
    [SerializeField, Min(0.1f)] private float memorySeconds = 4f;
    [SerializeField] private LayerMask sightLayers = ~0;

    private Health ownHealth;
    private float previousHealth;
    private float threatTime = float.NegativeInfinity;
    private float threatStrength;
    private float emergencyUntil;
    private Vector3 lastThreatPosition;

    public bool PlayerVisible { get; private set; }
    public bool HasThreat => Time.time - threatTime < memorySeconds;
    // This is an observation/memory, NEVER the hidden player's live position.
    public Vector3 LastObservedThreat => lastThreatPosition;
    public string Observation { get; private set; } = "No threat observed";
    public bool ImmediateDanger => ownHealth != null && !ownHealth.IsDead &&
        (Time.time < emergencyUntil || (PlayerVisible &&
        Vector3.Distance(BodyPosition, lastThreatPosition) <= immediateDangerDistance));
    public Vector3 BodyPosition => GetComponent<Collider>() is Collider body
        ? body.bounds.center : transform.position;
    public Vector3 EyePosition => eye != null ? eye.position : BodyPosition + Vector3.up * 0.3f;

    private void Awake() { ownHealth = GetComponent<Health>(); }
    private void OnEnable()
    {
        threatTime = float.NegativeInfinity;
        emergencyUntil = 0f;
        PlayerVisible = false;
        NoiseSystem.NoiseProduced += HearNoise;
        ownHealth.HealthChanged += ObserveDamage;
    }
    private void Start()
    {
        previousHealth = ownHealth.CurrentHealth;
        if (player == null)
        {
            PlayerInput input = FindFirstObjectByType<PlayerInput>();
            if (input != null) player = input.transform;
        }
    }
    private void OnDisable()
    {
        NoiseSystem.NoiseProduced -= HearNoise;
        ownHealth.HealthChanged -= ObserveDamage;
        PlayerVisible = false;
    }
    private void Update()
    {
        if (Time.timeScale == 0f || ownHealth.IsDead) return;
        PlayerVisible = false;
        if (player != null && player.gameObject.activeInHierarchy)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth == null || !playerHealth.IsDead)
            {
                Collider body = player.GetComponent<Collider>();
                Vector3 aim = body != null ? body.bounds.center : player.position;
                Vector3 delta = aim - EyePosition;
                if (delta.magnitude <= viewDistance &&
                    Vector3.Angle(transform.forward, delta) <= fieldOfView * 0.5f &&
                    RepairVisibility.Clear(EyePosition, aim, transform, player, sightLayers))
                {
                    PlayerVisible = true;
                    Remember(aim, 1f, "Player seen");
                }
            }
        }
        if (!HasThreat && !ImmediateDanger) Observation = "No threat observed";
    }
    private void ObserveDamage(float current, float maximum)
    {
        if (current < previousHealth)
        {
            // Damage confirms danger, but does not reveal the attacker's location.
            emergencyUntil = Time.time + memorySeconds;
            Observation = "Taking damage (attacker location unknown unless observed)";
        }
        previousHealth = current;
    }
    private void HearNoise(NoiseEvent noise)
    {
        if (!isActiveAndEnabled || ownHealth.IsDead || Time.timeScale == 0f ||
            noise.Source == gameObject) return;
        if (noise.Source != null &&
            (noise.Source.transform.IsChildOf(transform) ||
             noise.Source.GetComponentInParent<RepairableAlly>() != null)) return;
        float distance = Vector3.Distance(BodyPosition, noise.Position);
        if (distance > hearingRange) return;
        float heard = noise.Strength * (1f - distance / hearingRange);
        if (heard < minimumHeardStrength) return;
        bool clear = RepairVisibility.Clear(EyePosition, noise.Position,
            transform, noise.Source != null ? noise.Source.transform : null, sightLayers);
        if (!clear) heard *= 0.25f; // Walls/floor slabs muffle sound.
        if (heard < minimumHeardStrength || PlayerVisible) return;
        Remember(noise.Position, Mathf.Clamp01(heard / 12f), "Noise heard (source position only)");
        if (distance <= immediateDangerDistance && heard >= 6f)
            emergencyUntil = Time.time + 1.5f;
    }
    private void Remember(Vector3 point, float strength, string description)
    {
        lastThreatPosition = point;
        threatStrength = strength;
        threatTime = Time.time;
        Observation = description;
    }
    public float DangerAt(Vector3 position)
    {
        if (!HasThreat) return 0f;
        float freshness = Mathf.Clamp01(1f - (Time.time - threatTime) / memorySeconds);
        float proximity = Mathf.Clamp01(1f -
            Vector3.Distance(position, lastThreatPosition) / viewDistance);
        return threatStrength * freshness * proximity;
    }
}
