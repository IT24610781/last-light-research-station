using UnityEngine;

// Add only to robots on the Repair Robot's team, never to the player.
[RequireComponent(typeof(Health))]
public class RepairableAlly : MonoBehaviour
{
    [Tooltip("TEST ONLY: simulated observed danger, from safe (0) to dangerous (1).")]
    [SerializeField, Range(0f, 1f)] private float testDanger;

    public Health Health { get; private set; }
    public float TestDanger => Mathf.Clamp01(testDanger);

    private void Awake()
    {
        Health = GetComponent<Health>();
    }
}
