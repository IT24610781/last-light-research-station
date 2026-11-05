#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LastLight.AI.Guard;
using LastLight.AI.Movement;

public sealed class TacticalGuardPresentationTests : MonoBehaviour
{
    public static string Status = "Not run";
    public static readonly List<string> Results = new List<string>();
    private GameObject fixture;
    private IEnumerator Start()
    {
        Status = "Running"; Results.Clear();
        fixture = new GameObject("GuardVerificationFixture");
        Vector3 origin = new Vector3(150, 0, 150);
        Cube("Floor", origin + Vector3.down * .5f, new Vector3(20, 1, 20));
        var robot = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/LastLight/Prefabs/AI/TacticalGuard.prefab"), origin + Vector3.up, Quaternion.identity);
        robot.transform.SetParent(fixture.transform);
        var agent = robot.GetComponent<TacticalGuardAgent>();
        var sensor = robot.GetComponent<GuardSensor>();
        var motor = robot.GetComponent<AgentMotor>();
        var visual = robot.GetComponent<TacticalGuardPresentation>();
        var animator = robot.GetComponentInChildren<Animator>();
        Set(agent, "showOverlay", false);
        yield return new WaitForSeconds(.5f);
        Check(visual.CurrentVisualState == "Idle", "Idle without a visible target");
        Check(!animator.applyRootMotion, "Animation does not drive the AI root");
        Check(robot.GetComponentsInChildren<Renderer>().Length >= 57, "Production mesh and armor present");
        agent.enabled = false;
        motor.SetPath(new List<Vector3> { origin + new Vector3(0, 1, 6) });
        yield return new WaitForSeconds(.5f);
        Check(motor.MeasuredSpeed > .1f && visual.CurrentVisualState == "Walk", "Measured motion drives walking");
        var leg = System.Array.Find(robot.GetComponentsInChildren<Transform>(), t => t.name == "DEF-shin.L");
        Quaternion before = leg.localRotation;
        yield return new WaitForSeconds(.18f);
        Check(Quaternion.Angle(before, leg.localRotation) > .1f, "Imported walk animation moves the leg");
        motor.Stop();
        agent.enabled = true;
        var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.name = "Guard test target";
        target.transform.SetParent(fixture.transform);
        target.transform.position = robot.transform.position + Vector3.forward * 5;
        var targetHealth = target.AddComponent<Health>();
        Set(targetHealth, "maximumHealth", 10000f); targetHealth.Heal(10000);
        Set(sensor, "player", target.transform);
        Set(agent, "accuracy", 1f);
        Set(agent, "magazineSize", 3); Set(agent, "ammo", 3);
        Set(agent, "reloadDuration", 1.2f);
        int shots = 0;
        agent.AttackPerformed += (a, p) => shots++;
        Physics.SyncTransforms();
        bool attackSeen = false, reloadSeen = false, tracerSeen = false;
        float until = Time.time + 4.5f;
        while (Time.time < until)
        {
            attackSeen |= visual.CurrentVisualState == "Attack";
            reloadSeen |= visual.CurrentVisualState == "Reload" && agent.IsReloading;
            tracerSeen |= visual.TracerVisible;
            yield return null;
        }
        Check(shots >= 3 && targetHealth.CurrentHealth < 10000, "Real AI attacks consume ammunition and damage target");
        Check(attackSeen && tracerSeen, "Actual shots drive attack animation and tracer");
        Check(reloadSeen, "Empty magazine drives reload animation");
        var wall = Cube("Occluder", (robot.transform.position + target.transform.position) * .5f,
            new Vector3(4, 3, .3f));
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.4f);
        float hp = targetHealth.CurrentHealth;
        int count = shots;
        yield return new WaitForSeconds(.8f);
        Check(shots == count && targetHealth.CurrentHealth == hp && !visual.TracerVisible,
            "Wall prevents shots and damage after sensing updates");
        agent.SetPaused(true);
        yield return null;
        Check(animator.speed == 0 && !visual.TracerVisible, "Pause freezes animation and hides tracer");
        agent.SetPaused(false);
        robot.GetComponent<Health>().TakeDamage(1000);
        yield return new WaitForSeconds(1.8f);
        Check(visual.CurrentVisualState == "Death" && !visual.TracerVisible &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1,
            "Death stops combat and holds final animation pose");
        wall.SetActive(false);

        var walker = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/LastLight/Prefabs/AI/TacticalGuard.prefab"), origin + new Vector3(-6, 1, -3), Quaternion.identity);
        walker.transform.SetParent(fixture.transform);
        walker.GetComponent<TacticalGuardAgent>().enabled = false;
        Cube("Door left", origin + new Vector3(-7.15f, 1.1f, 0), new Vector3(1, 2.2f, .5f));
        Cube("Door right", origin + new Vector3(-4.85f, 1.1f, 0), new Vector3(1, 2.2f, .5f));
        Cube("Door lintel", origin + new Vector3(-6, 2.45f, 0), new Vector3(3.3f, .5f, .5f));
        Physics.SyncTransforms();
        walker.GetComponent<AgentMotor>().SetPath(new List<Vector3> { origin + new Vector3(-6, 1, 3) });
        yield return new WaitForSeconds(4);
        Check(walker.transform.position.z > origin.z + 1, "Passes a 1.3m wide, 2.2m high doorway");
        Cube("Blocking wall", origin + new Vector3(-6, 1.5f, 5), new Vector3(4, 3, .5f));
        Physics.SyncTransforms();
        walker.GetComponent<AgentMotor>().SetPath(new List<Vector3> { origin + new Vector3(-6, 1, 7) });
        yield return new WaitForSeconds(3);
        Check(walker.transform.position.z < origin.z + 4.5f, "Controller cannot walk through a wall");
        int failed = Results.FindAll(s => s.StartsWith("FAIL")).Count;
        Status = $"Complete: {Results.Count - failed} passed, {failed} failed";
        Debug.Log("[Guard presentation] " + Status + "\n" + string.Join("\n", Results));
        Destroy(fixture);
    }
    private GameObject Cube(string name, Vector3 position, Vector3 size)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name;
        o.transform.SetParent(fixture.transform); o.transform.position = position;
        o.transform.localScale = size; return o;
    }
    private static void Set(object o, string name, object value)
    { o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value); }
    private static void Check(bool value, string name) { Results.Add((value ? "PASS " : "FAIL ") + name); }
    private void OnDestroy() { if (fixture != null) Destroy(fixture); }
}
#endif
