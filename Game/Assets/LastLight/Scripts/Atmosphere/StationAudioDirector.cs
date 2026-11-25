using LastLight.AI.Guard;
using LastLight.AI.Hunter;
using UnityEngine;

/// <summary>Quiet station ambience and local feedback, generated at runtime.</summary>
public sealed class StationAudioDirector : MonoBehaviour
{
    public static StationAudioDirector Instance { get; private set; }

    private AudioSource ambience;
    private AudioClip ambientClip;
    private AudioClip relayClip;
    private AudioClip doorClip;
    private AudioClip attackClip;
    private AudioClip pickupClip;
    private AudioClip outcomeClip;
    private HunterAgent[] hunters;
    private TacticalGuardAgent[] guards;
    private Transform player;

    private void Awake()
    {
        Instance = this;
        ambientClip = CreateClip("Station machinery", 2f, t =>
            0.12f * Mathf.Sin(2f * Mathf.PI * 55f * t) +
            0.045f * Mathf.Sin(2f * Mathf.PI * 110f * t) +
            0.025f * Mathf.Sin(2f * Mathf.PI * 2f * t) *
            Mathf.Sin(2f * Mathf.PI * 220f * t));
        relayClip = CreateClip("Relay activation", 0.8f, t =>
            Mathf.Exp(-4f * t) * (0.32f * Mathf.Sin(2f * Mathf.PI * (460f + 350f * t) * t) +
            0.16f * Mathf.Sin(2f * Mathf.PI * 920f * t)));
        doorClip = CreateClip("Door motor", 0.55f, t =>
            Mathf.Sin(Mathf.PI * t / 0.55f) * 0.22f *
            (Mathf.Sin(2f * Mathf.PI * (75f + 100f * t) * t) +
             0.18f * Mathf.Sin(2f * Mathf.PI * 420f * t)));
        attackClip = CreateClip("Robot attack", 0.32f, t =>
            Mathf.Exp(-11f * t) * 0.48f *
            Mathf.Sin(2f * Mathf.PI * (680f - 900f * t) * t));
        pickupClip = CreateClip("Item collected", 0.3f, t =>
            Mathf.Exp(-12f * t) * 0.28f *
            Mathf.Sin(2f * Mathf.PI * (550f + 500f * t) * t));
        outcomeClip = CreateClip("Mission outcome", 1.3f, t =>
            Mathf.Exp(-1.7f * t) * 0.24f *
            (Mathf.Sin(2f * Mathf.PI * 440f * t) +
             Mathf.Sin(2f * Mathf.PI * 554f * t) +
             Mathf.Sin(2f * Mathf.PI * 659f * t)));

        ambience = gameObject.AddComponent<AudioSource>();
        ambience.clip = ambientClip;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.07f;
        ambience.Play();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null) player = playerObject.transform;
        hunters = FindObjectsByType<HunterAgent>(FindObjectsSortMode.None);
        guards = FindObjectsByType<TacticalGuardAgent>(FindObjectsSortMode.None);
        foreach (HunterAgent hunter in hunters) hunter.AttackPerformed += OnHunterAttack;
        foreach (TacticalGuardAgent guard in guards) guard.AttackPerformed += OnGuardAttack;
    }

    private void Update()
    {
        if (player == null) return;
        Vector3 p = player.position;
        // Power equipment has a stronger hum; upper labs have a faint higher whine.
        float targetVolume = p.x < -3f && Mathf.Abs(p.z) < 4f && p.y < 3f ? 0.13f : 0.075f;
        ambience.volume = Mathf.MoveTowards(ambience.volume, targetVolume, Time.unscaledDeltaTime * 0.15f);
        ambience.pitch = p.y > 3f ? 1.14f : 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (hunters != null)
            foreach (HunterAgent hunter in hunters)
                if (hunter != null) hunter.AttackPerformed -= OnHunterAttack;
        if (guards != null)
            foreach (TacticalGuardAgent guard in guards)
                if (guard != null) guard.AttackPerformed -= OnGuardAttack;
    }

    private void OnHunterAttack(HunterAgent hunter, Vector3 target) => PlayAt(attackClip, hunter.transform.position, 0.65f);
    private void OnGuardAttack(TacticalGuardAgent guard, Vector3 target) => PlayAt(attackClip, guard.transform.position, 0.5f);

    public void PlayRelay(Vector3 position) => PlayAt(relayClip, position, 0.8f);
    public void PlayDoor(Vector3 position) => PlayAt(doorClip, position, 0.35f);
    public void PlayPickup(Vector3 position) => PlayAt(pickupClip, position, 0.6f);
    public void PlayOutcome() => ambience.PlayOneShot(outcomeClip, 0.9f);

    private static void PlayAt(AudioClip clip, Vector3 position, float volume)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    private static AudioClip CreateClip(string name, float seconds, System.Func<float, float> sample)
    {
        const int rate = 22050;
        int count = Mathf.RoundToInt(rate * seconds);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++) samples[i] = Mathf.Clamp(sample(i / (float)rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
