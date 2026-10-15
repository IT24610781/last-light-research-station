using UnityEngine;

[RequireComponent(typeof(Health))]
public class RepairTargetSelector : MonoBehaviour
{
    [Tooltip("Explicitly list allied robots. Do not include the player or this robot.")]
    [SerializeField] private RepairableAlly[] allies = new RepairableAlly[0];
    [SerializeField, Min(0.1f)] private float reconsiderInterval = 0.5f;
    [SerializeField, Min(0f)] private float missingHealthWeight = 100f;
    [SerializeField, Min(0f)] private float distancePenaltyPerMetre = 2f;
    [SerializeField, Min(0f)] private float dangerPenalty = 30f;
    [Tooltip("Another ally must beat the current target by this many points.")]
    [SerializeField, Min(0f)] private float switchScoreMargin = 15f;

    public RepairableAlly CurrentTarget { get; private set; }

    private Health ownHealth;
    private float nextSelectionTime;
    private bool hasReported;
    private bool wasKeepingTarget;

    private void Awake()
    {
        ownHealth = GetComponent<Health>();
    }

    private void OnEnable()
    {
        nextSelectionTime = 0f;
        hasReported = false;
        ownHealth.Died += HandleDeath;
    }

    private void OnDisable()
    {
        ownHealth.Died -= HandleDeath;
        CurrentTarget = null;
    }

    private void HandleDeath(GameObject source)
    {
        CurrentTarget = null;
        Debug.Log($"{name}: dead; repair target cleared.", this);
        enabled = false;
    }

    private void Update()
    {
        if (ownHealth.IsDead || Time.timeScale == 0f ||
            Time.time < nextSelectionTime)
        {
            return;
        }

        nextSelectionTime = Time.time + Mathf.Max(0.1f, reconsiderInterval);
        SelectTarget();
    }

    private void SelectTarget()
    {
        RepairableAlly best = null;
        float bestScore = float.NegativeInfinity;
        string bestReason = "";

        bool currentIsValid = false;
        float currentScore = float.NegativeInfinity;
        string currentReason = "";

        foreach (RepairableAlly ally in allies)
        {
            if (ally == null || !ally.isActiveAndEnabled ||
                ally.Health == null || !ally.Health.isActiveAndEnabled ||
                ally.Health == ownHealth || ally.Health.IsDead)
            {
                continue;
            }

            float missing =
                ally.Health.MaximumHealth - ally.Health.CurrentHealth;

            if (missing <= 0f)
            {
                continue;
            }

            float missingFraction = missing / ally.Health.MaximumHealth;

            // Temporary distance estimate; actual route cost comes later.
            float distance = Vector3.Distance(
                transform.position, ally.transform.position);

            float healthPoints = missingFraction * missingHealthWeight;
            float distancePoints = distance * distancePenaltyPerMetre;
            float dangerPoints = ally.TestDanger * dangerPenalty;
            float score = healthPoints - distancePoints - dangerPoints;

            string reason =
                $"missing health {missing:F0}/{ally.Health.MaximumHealth:F0} " +
                $"(+{healthPoints:F1}), distance estimate {distance:F1}m " +
                $"(-{distancePoints:F1}), TEST danger {ally.TestDanger:F2} " +
                $"(-{dangerPoints:F1}), score {score:F1}";

            // Recalculate the current target's score each time.
            if (ally == CurrentTarget)
            {
                currentIsValid = true;
                currentScore = score;
                currentReason = reason;
            }

            if (score > bestScore)
            {
                best = ally;
                bestScore = score;
                bestReason = reason;
            }
        }

        bool keepingTarget = false;

        // Apply stability only while the current target remains eligible.
        if (currentIsValid && best != null && best != CurrentTarget)
        {
            float improvement = bestScore - currentScore;
            float requiredImprovement = Mathf.Max(0f, switchScoreMargin);

            if (improvement <= requiredImprovement)
            {
                keepingTarget = true;

                bestReason =
                    $"Keeping current target. {best.name} scores " +
                    $"{bestScore:F1}, only {improvement:F1} points better; " +
                    $"switching requires more than {requiredImprovement:F1}. " +
                    $"Current target: {currentReason}";

                best = CurrentTarget;
            }
        }

        bool changed = CurrentTarget != best;
        bool stabilityChanged = keepingTarget != wasKeepingTarget;

        CurrentTarget = best;
        wasKeepingTarget = keepingTarget;

        if (!hasReported || changed || stabilityChanged)
        {
            hasReported = true;

            Debug.Log(best != null
                ? $"{name}: target {best.name}. {bestReason}"
                : $"{name}: waiting; no living, active, injured allies.",
                this);
        }
    }
}
