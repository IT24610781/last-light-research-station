#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LastLight.AI.Movement;

// Editor-only integration scenarios. Run from the Verification menu, not normal gameplay.
public class RepairRobotScenarioTests : MonoBehaviour
{
    public static string Status = "Not run";
    public static readonly List<string> Results = new List<string>();
    private static readonly Vector3 Origin = new Vector3(100f, 0f, 100f);
    private GameObject fixture, robot;
    private RepairableAlly a, b;
    private RepairTargetSelector selector;
    private RepairRobotMovement movement;
    private RepairRobotRepair repair;
    private RepairRobotSensor sensor;
    private RepairRoutePlanner planner;
    private float savedTimeScale;

    private void Start()
    {
        savedTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        Status = "Running"; Results.Clear();
        StartCoroutine(Run());
    }
    private IEnumerator Run()
    {
        yield return Fixture();
        movement.enabled = false; repair.enabled = false;
        selector.EvaluateNow();
        Check(selector.CurrentTarget == null, "Healthy allies: no repair target");
        a.Health.TakeDamage(30f); selector.EvaluateNow();
        Check(selector.CurrentTarget == a, "Select injured ally");
        b.Health.TakeDamage(40f); selector.EvaluateNow();
        Check(selector.CurrentTarget == a, "Stability retains target for small improvement");
        b.Health.TakeDamage(10f); selector.EvaluateNow();
        Check(selector.CurrentTarget == b, "Larger utility improvement switches target");
        b.Health.Heal(200f); selector.EvaluateNow();
        Check(selector.CurrentTarget == a, "Full-health target is replaced");
        a.Health.TakeDamage(200f); selector.EvaluateNow();
        Check(selector.CurrentTarget == null, "Dead target excluded; no resurrection");

        yield return Fixture();
        movement.enabled = false; repair.enabled = false;
        var self = robot.AddComponent<RepairableAlly>();
        Set(selector, "allies", new[] { self, a, b });
        robot.GetComponent<Health>().TakeDamage(10f);
        Set(sensor, "emergencyUntil", 0f);
        selector.EvaluateNow();
        Check(selector.CurrentTarget == null, "Robot cannot select itself");

        yield return Fixture();
        movement.enabled = false; repair.enabled = false;
        a.Health.TakeDamage(50f); b.Health.TakeDamage(50f);
        var provider = robot.AddComponent<RepairRobotTestRoutes>();
        provider.AllyA = a.transform; provider.AllyB = b.transform;
        Set(planner, "pathProvider", provider);
        selector.EvaluateNow();
        Check(selector.CurrentTarget == b, "Uses supplied route cost, not straight-line distance");
        selector.ReportUnreachable(b, "Scenario blocked route");
        selector.EvaluateNow();
        Check(selector.CurrentTarget == a, "Blocked target cooldown selects another ally");

        yield return Fixture();
        movement.enabled = false; repair.enabled = false;
        a.Health.TakeDamage(40f);
        a.gameObject.SetActive(false); selector.EvaluateNow();
        Check(selector.CurrentTarget == null, "Disabled ally excluded");
        a.gameObject.SetActive(true);
        var wall = Cube("Unreachable wall", new Vector3(-1.5f, 1f, -3f), new Vector3(0.2f, 2f, 12f));
        Physics.SyncTransforms(); selector.EvaluateNow();
        Check(selector.CurrentTarget == null, "Checked direct fallback rejects obstructed repair routes");
        wall.SetActive(false); yield return Wait(3.6f);
        selector.EvaluateNow();
        Check(selector.CurrentTarget == a, "Unreachable target retried after obstacle removal/cooldown");

        yield return Fixture();
        // Close ally: test actual healing and physical line of sight.
        a.transform.position = robot.transform.position + Vector3.right * 1.7f;
        Physics.SyncTransforms(); a.Health.TakeDamage(50f);
        yield return Wait(0.9f);
        Check(a.Health.CurrentHealth > 50f && a.Health.CurrentHealth < 100f,
            "Repair increases health gradually, not instantly");
        float beforeWall = a.Health.CurrentHealth;
        wall = Cube("Repair LOS wall", new Vector3(-2.15f, 1f, -3f), new Vector3(0.15f, 2f, 3f));
        Physics.SyncTransforms(); yield return Wait(0.7f);
        Check(Mathf.Approximately(beforeWall, a.Health.CurrentHealth), "Wall stops repair immediately");
        wall.SetActive(false); yield return Wait(6f);
        Check(Mathf.Approximately(a.Health.CurrentHealth, 100f), "Repair resumes and clamps at maximum health");

        yield return Fixture();
        movement.enabled = false;
        a.Health.TakeDamage(40f); selector.EvaluateNow();
        float beforeRange = a.Health.CurrentHealth;
        yield return Wait(0.8f);
        Check(!repair.CanRepair(a) && Mathf.Approximately(beforeRange, a.Health.CurrentHealth),
            "No repair beyond range");
        a.Health.TakeDamage(200f); yield return Wait(0.4f);
        Check(a.Health.IsDead && a.Health.CurrentHealth == 0f, "Dead ally never repaired");

        yield return Fixture();
        a.Health.TakeDamage(40f);
        yield return Wait(3f);
        Check(Vector3.Distance(robot.GetComponent<CharacterController>().bounds.center,
            a.RepairPosition) <= 2.05f, "AgentMotor approaches an injured ally");
        Check(a.Health.CurrentHealth > 60f, "Approach transitions to repair");
        float stoppedHealth = a.Health.CurrentHealth;
        Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.5f);
        Check(Mathf.Approximately(stoppedHealth, a.Health.CurrentHealth), "Pause freezes repair");
        Time.timeScale = 1f;
        robot.GetComponent<Health>().TakeDamage(200f); yield return Wait(0.5f);
        Check(Mathf.Approximately(stoppedHealth, a.Health.CurrentHealth) && !repair.IsRepairing,
            "Dead repair robot cannot continue healing");

        yield return Fixture();
        movement.enabled = false; repair.enabled = false;
        GameObject threat = Capsule("Observed player", new Vector3(-6f, 1f, -1f));
        Set(sensor, "player", threat.transform);
        robot.transform.rotation = Quaternion.LookRotation(threat.transform.position - robot.transform.position);
        Physics.SyncTransforms(); yield return Wait(0.2f);
        Check(sensor.PlayerVisible && sensor.HasThreat, "Vision detects an exposed player in the view cone");
        Vector3 remembered = sensor.LastObservedThreat;
        wall = Cube("Sight wall", new Vector3(-4.5f, 1f, -2f), new Vector3(4f, 3f, 0.2f));
        threat.transform.position += Vector3.right;
        Physics.SyncTransforms(); yield return Wait(0.2f);
        Check(!sensor.PlayerVisible && Vector3.Distance(remembered, sensor.LastObservedThreat) < 0.01f,
            "Hidden player movement does not update remembered location");
        yield return Wait(4.2f);
        Check(!sensor.HasThreat, "Threat memory expires");
        NoiseSystem.Emit(robot.transform.position + Vector3.left * 4f, 8f, null);
        Check(sensor.HasThreat && sensor.Observation.Contains("Noise"), "Noise records an audible source observation");

        yield return Fixture();
        // Damage has no omniscient attacker position but still triggers self-preservation.
        Transform support = new GameObject("Support").transform;
        support.SetParent(fixture.transform); support.position = Origin + new Vector3(-3f, 1f, -6f);
        Set(movement, "supportPositions", new[] { support });
        Set(sensor, "memorySeconds", 2f);
        Vector3 start = robot.transform.position;
        robot.GetComponent<Health>().TakeDamage(5f);
        yield return Wait(1.2f);
        Check(movement.State == RepairRobotMovement.RepairState.Retreating &&
            Vector3.Distance(start, robot.transform.position) > 0.2f && !repair.IsRepairing,
            "Immediate danger interrupts repairs and retreats to reachable support");
        yield return Wait(4f);
        Check(movement.State == RepairRobotMovement.RepairState.Waiting,
            "No injured allies: waits at reachable support after danger expires");

        int failed = Results.FindAll(item => item.StartsWith("FAIL")).Count;
        Status = $"Complete: {Results.Count - failed} passed, {failed} failed";
        Debug.Log("[Repair verification] " + Status + "\n" + string.Join("\n", Results));
        Time.timeScale = savedTimeScale;
        if (fixture != null) Destroy(fixture);
    }
    private IEnumerator Fixture()
    {
        if (fixture != null) { Destroy(fixture); yield return null; }
        fixture = new GameObject("RepairVerificationFixture");
        Cube("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 16f));
        a = Capsule("Ally A", new Vector3(0f, 1f, -3f)).AddComponent<RepairableAlly>();
        b = Capsule("Ally B", new Vector3(-3f, 1f, 0f)).AddComponent<RepairableAlly>();
        robot = Capsule("Repair verification robot", new Vector3(-3f, 1f, -3f));
        robot.SetActive(false);
        DestroyImmediate(robot.GetComponent<CapsuleCollider>());
        var cc = robot.AddComponent<CharacterController>(); cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;
        robot.AddComponent<AgentMotor>();
        selector = robot.AddComponent<RepairTargetSelector>();
        movement = robot.AddComponent<RepairRobotMovement>();
        repair = robot.AddComponent<RepairRobotRepair>();
        planner = robot.GetComponent<RepairRoutePlanner>(); sensor = robot.GetComponent<RepairRobotSensor>();
        Set(selector, "allies", new[] { a, b });
        Set(selector, "logDecisions", false);
        robot.SetActive(true); Physics.SyncTransforms(); yield return null;
    }
    private GameObject Capsule(string name, Vector3 local)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        item.name = name; item.transform.SetParent(fixture.transform);
        item.transform.position = Origin + local; item.AddComponent<Health>(); return item;
    }
    private GameObject Cube(string name, Vector3 local, Vector3 scale)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name; item.transform.SetParent(fixture.transform);
        item.transform.position = Origin + local; item.transform.localScale = scale; return item;
    }
    public static void Set(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new InvalidOperationException(field);
        info.SetValue(target, value);
    }
    private static IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }
    private static void Check(bool condition, string name)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + name);
    }
    private void OnDestroy()
    {
        Time.timeScale = savedTimeScale;
        if (fixture != null) Destroy(fixture);
    }
}
#endif
