using UnityEngine;

/// <summary>A two-leaf room door operated by the player's existing Interact action.</summary>
public sealed class StationSlidingDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform leftLeaf;
    [SerializeField] private Transform rightLeaf;
    [SerializeField, Min(0.1f)] private float travel = 1.2f;
    [SerializeField, Min(0.1f)] private float speed = 2.5f;
    private Vector3 leftClosed;
    private Vector3 rightClosed;
    private bool open;
    private float fraction;

    public bool IsOpen => open;
    public float OpenFraction => fraction;
    public AudioSource doorAudio;
    public AudioClip doorOpenSound;
    public enum AccessRequirement { None, RelayA, RelayB, BothRelays }
    public AccessRequirement requiredAccess = AccessRequirement.None;
    public AccessRequirement Requirement => requiredAccess;
    public bool CanOpen => requiredAccess == AccessRequirement.None ||
        (StationProgressionManager.Instance != null &&
         StationProgressionManager.Instance.CanOpen(requiredAccess));

    public void ConfigureAccess(AccessRequirement requirement) => requiredAccess = requirement;

    public void Configure(Transform left, Transform right)
    {
        leftLeaf = left;
        rightLeaf = right;
        CacheClosedPositions();
    }

    private void Awake() => CacheClosedPositions();

    private void CacheClosedPositions()
    {
        if (leftLeaf != null) leftClosed = leftLeaf.localPosition;
        if (rightLeaf != null) rightClosed = rightLeaf.localPosition;
    }

    public void Interact() 
       {
        if (!CanOpen)
        {
            StationProgressionManager.Instance?.ReportLocked(requiredAccess);
            return;
        }
        open = !open;
        
        if (doorAudio != null && doorOpenSound != null) 
        {
            doorAudio.PlayOneShot(doorOpenSound);
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        if (leftLeaf == null || rightLeaf == null) return;
        // Robots use the same access rules as the player. A closed unlocked door
        // must not leave an otherwise valid route permanently blocked.
        if (!open && CanOpen)
        {
            foreach (Collider nearby in Physics.OverlapSphere(transform.position, 2f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var motor = nearby.GetComponentInParent<LastLight.AI.Movement.AgentMotor>();
                var health = nearby.GetComponentInParent<Health>();
                if (motor != null && motor.isActiveAndEnabled &&
                    (health == null || !health.IsDead)) { open = true; break; }
            }
        }
        // Reopen rather than closing onto a character or a carried physics object.
        if (!open && fraction > 0f)
        {
            var occupants = Physics.OverlapBox(transform.position + Vector3.up * 1.25f,
                new Vector3(0.65f, 1.2f, 1.05f), transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var occupant in occupants)
            {
                if (occupant.transform.IsChildOf(transform)) continue;
                if (occupant is CharacterController || occupant.attachedRigidbody != null)
                {
                    open = true;
                    break;
                }
            }
        }
        fraction = Mathf.MoveTowards(fraction, open ? 1f : 0f,
            speed / travel * Time.deltaTime);
        leftLeaf.localPosition = leftClosed + Vector3.back * (travel * fraction);
        rightLeaf.localPosition = rightClosed + Vector3.forward * (travel * fraction);
    }
}
