using UnityEngine;

/// <summary>A two-leaf room door operated by the player's existing Interact action.</summary>
public sealed class StationSlidingDoor : MonoBehaviour, IInteractable
{
    public enum AccessRequirement { None, RelayA, BothRelays }

    [SerializeField] private Transform leftLeaf;
    [SerializeField] private Transform rightLeaf;
    [SerializeField, Min(0.1f)] private float travel = 1.2f;
    [SerializeField, Min(0.1f)] private float speed = 2.5f;
    [SerializeField] private AccessRequirement accessRequirement;
    private Vector3 leftClosed;
    private Vector3 rightClosed;
    private bool open;
    private float fraction;

    public bool IsOpen => open;
    public float OpenFraction => fraction;
    public AccessRequirement Requirement => accessRequirement;

    public void Configure(Transform left, Transform right)
    {
        leftLeaf = left;
        rightLeaf = right;
        CacheClosedPositions();
    }

    public void ConfigureAccess(AccessRequirement requirement)
    {
        accessRequirement = requirement;
    }

    private void Awake()
    {
        CacheClosedPositions();
        if (accessRequirement == AccessRequirement.None)
        {
            open = true;
            fraction = 1f;
            ApplyLeafPositions();
        }
    }

    private void CacheClosedPositions()
    {
        if (leftLeaf != null) leftClosed = leftLeaf.localPosition;
        if (rightLeaf != null) rightClosed = rightLeaf.localPosition;
    }

    public void Interact()
    {
        StationProgressionManager progression = StationProgressionManager.Instance;
        if (progression != null && !progression.CanOpen(accessRequirement))
        {
            progression.ReportLocked(accessRequirement);
            return;
        }

        open = !open;
        StationAudioDirector.Instance?.PlayDoor(transform.position);
    }

    private void Update()
    {
        if (leftLeaf == null || rightLeaf == null) return;
        if (!open && NearbyAgentCanOpen())
        {
            open = true;
            StationAudioDirector.Instance?.PlayDoor(transform.position);
        }

        // Reopen rather than closing onto a character or a carried physics object.
        if (!open && fraction > 0f)
        {
            Collider[] occupants = Physics.OverlapBox(transform.position + Vector3.up * 1.25f,
                new Vector3(0.65f, 1.2f, 1.05f), transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider occupant in occupants)
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
        ApplyLeafPositions();
    }
    private bool NearbyAgentCanOpen()
    {
        StationProgressionManager progression = StationProgressionManager.Instance;
        if (progression != null && !progression.CanOpen(accessRequirement)) return false;

        Collider[] nearby = Physics.OverlapBox(transform.position + Vector3.up * 1.1f,
            new Vector3(3f, 1.1f, 1.2f), transform.rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider item in nearby)
        {
            if (item.transform.IsChildOf(transform)) continue;
            if (item.GetComponentInParent<LastLight.AI.Movement.AgentMotor>() != null)
                return true;
        }
        return false;
    }

    private void ApplyLeafPositions()
    {
        if (leftLeaf != null) leftLeaf.localPosition = leftClosed + Vector3.back * (travel * fraction);
        if (rightLeaf != null) rightLeaf.localPosition = rightClosed + Vector3.forward * (travel * fraction);
    }
}
