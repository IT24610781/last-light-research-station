using LastLight.AI.Hunter;
using UnityEngine;

namespace LastLight.AI.Integration
{
    /// <summary>
    /// Turns the Hunter's attack decision into real damage on the player.
    /// The brain only attacks when the player is visible and in range, and the
    /// agent applies the attack cooldown, so this component only delivers the hit.
    /// </summary>
    [RequireComponent(typeof(HunterAgent))]
    public class HunterAttackDamage : MonoBehaviour
    {
        [SerializeField] private float damagePerHit = 10f;

        [Tooltip("Leave empty to find the object tagged 'Player'.")]
        [SerializeField] private GameObject target;

        private HunterAgent hunter;
        private IDamageable targetDamageable;

        private void Awake() => hunter = GetComponent<HunterAgent>();

        private void Start()
        {
            if (target == null) target = GameObject.FindWithTag("Player");
            if (target != null) targetDamageable = target.GetComponentInParent<IDamageable>();
        }

        private void OnEnable() => hunter.AttackPerformed += OnAttack;

        private void OnDisable() => hunter.AttackPerformed -= OnAttack;

        private void OnAttack(HunterAgent attacker, Vector3 targetPosition)
        {
            targetDamageable?.TakeDamage(damagePerHit, gameObject);
        }
    }
}
