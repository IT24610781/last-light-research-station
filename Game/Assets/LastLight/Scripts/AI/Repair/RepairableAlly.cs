using UnityEngine;

[RequireComponent(typeof(Health))]
public class RepairableAlly : MonoBehaviour
{
    [Tooltip("Optional point at which the repair beam should end.")]
    [SerializeField] private Transform repairPoint;
    [Tooltip("Legacy test value. Used only when explicitly enabled on the selector.")]
    [SerializeField, Range(0f, 1f)] private float testDanger;
    private Health health;

    public Health Health => health != null ? health : health = GetComponent<Health>();
    public float TestDanger => Mathf.Clamp01(testDanger);
    public Vector3 RepairPosition
    {
        get
        {
            if (repairPoint != null) return repairPoint.position;
            Collider body = GetComponent<Collider>();
            return body != null ? body.bounds.center : transform.position;
        }
    }
}
