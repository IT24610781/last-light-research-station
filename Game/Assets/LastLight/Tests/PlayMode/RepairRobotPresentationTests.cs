#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using LastLight.AI.Movement;

public sealed class RepairRobotPresentationTests : MonoBehaviour
{
    public static string Status = "Not run";
    public static readonly List<string> Results = new List<string>();
    private GameObject fixture;
    private IEnumerator Start()
    {
        Status = "Running"; Results.Clear();
        fixture = new GameObject("RepairPresentationFixture");
        Vector3 origin = new Vector3(150, 0, 150);
        Cube("Floor", origin + Vector3.down * .5f, new Vector3(18,1,18));
        var robot = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/LastLight/Prefabs/AI/RepairRobot.prefab"), origin + new Vector3(0,1,-3), Quaternion.identity);
        robot.transform.SetParent(fixture.transform); robot.transform.position = origin + new Vector3(0,1,-3);
        var selector = robot.GetComponent<RepairTargetSelector>();
        var repair = robot.GetComponent<RepairRobotRepair>();
        var movement = robot.GetComponent<RepairRobotMovement>();
        var presentation = robot.GetComponent<RepairRobotPresentation>();
        var animator = robot.GetComponentInChildren<Animator>();
        yield return new WaitForSeconds(1f);
        Check(presentation.CurrentVisualState == "Idle" && !presentation.BeamVisible, "Healthy idle has no repair beam");
        Check(!animator.applyRootMotion, "Animation cannot move the AI root");
        Check(robot.GetComponentsInChildren<Renderer>().Length > 50, "Production mesh hierarchy is present");
        var allyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        allyGO.name = "Presentation ally"; allyGO.transform.SetParent(fixture.transform);
        allyGO.transform.position = origin + new Vector3(0,1,3);
        allyGO.AddComponent<Health>(); var ally = allyGO.AddComponent<RepairableAlly>();
        Set(selector,"allies",new[]{ally}); ally.Health.TakeDamage(95); Physics.SyncTransforms();
        selector.EvaluateNow(); yield return new WaitForSeconds(.7f);
        Check(presentation.CurrentVisualState == "Walk", "Measured movement plays walk: " + presentation.CurrentVisualState + ", " + movement.State + ", " + robot.transform.position + ", speed " + robot.GetComponent<AgentMotor>().MeasuredSpeed);
        var leg = System.Array.Find(robot.GetComponentsInChildren<Transform>(), t=>t.name == "Bone_Paw_L.001");
        Vector3 legPosition = robot.transform.InverseTransformPoint(leg.position); Quaternion legRotation = Quaternion.Inverse(robot.transform.rotation) * leg.rotation;
        yield return new WaitForSeconds(.17f);
        Check(Vector3.Distance(legPosition,robot.transform.InverseTransformPoint(leg.position)) > .0001f || Quaternion.Angle(legRotation,Quaternion.Inverse(robot.transform.rotation) * leg.rotation) > .1f, "Imported walk clip moves leg joints");
        yield return new WaitForSeconds(3f);
        Check(presentation.CurrentVisualState == "Repair" && presentation.BeamVisible && repair.IsRepairing,
            "Actual healing drives repair animation and beam: " + presentation.CurrentVisualState + ", HP " + ally.Health.CurrentHealth + ", " + movement.MovementReason);
        Vector3 mid = (robot.GetComponent<CharacterController>().bounds.center + ally.RepairPosition)*.5f;
        var wall = Cube("Beam occluder", mid, new Vector3(3,3,.15f));
        Physics.SyncTransforms(); yield return new WaitForSeconds(.15f);
        float blockedHealth = ally.Health.CurrentHealth;
        yield return new WaitForSeconds(.4f);
        Check(!presentation.BeamVisible && Mathf.Approximately(blockedHealth,ally.Health.CurrentHealth),
            "Wall stops both visible beam and healing");
        wall.SetActive(false); selector.EvaluateNow(); yield return new WaitForSeconds(.5f);
        robot.GetComponent<Health>().TakeDamage(200); yield return new WaitForSeconds(.2f);
        Check(presentation.CurrentVisualState == "Death" && !presentation.BeamVisible, "Death overrides locomotion and hides beam");
        yield return new WaitForSeconds(1.6f);
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death") &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f, "Death clip reaches and holds its final pose");

        // A fresh production instance traverses an actual 2m collider opening.
        var doorRobot = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/LastLight/Prefabs/AI/RepairRobot.prefab"), origin + new Vector3(0,1,-3), Quaternion.identity);
        doorRobot.transform.SetParent(fixture.transform); doorRobot.transform.position = origin + new Vector3(-5,1,-3);
        doorRobot.GetComponent<RepairTargetSelector>().enabled=false;
        doorRobot.GetComponent<RepairRobotMovement>().enabled=false;
        doorRobot.GetComponent<RepairRobotRepair>().enabled=false;
        Cube("Door left",origin + new Vector3(-6.5f,1,0),new Vector3(1,2,.5f));
        Cube("Door right",origin + new Vector3(-3.5f,1,0),new Vector3(1,2,.5f));
        Physics.SyncTransforms(); yield return null;
        doorRobot.GetComponent<AgentMotor>().SetPath(new List<Vector3>{origin+new Vector3(-5,1,3)});
        yield return new WaitForSeconds(4f);
        Check(doorRobot.transform.position.z > origin.z+1f, "Production robot passes through a 2m doorway");
        var dp=doorRobot.GetComponent<RepairRobotPresentation>();
        doorRobot.GetComponent<AgentMotor>().IsPaused=true; yield return null;
        Check(doorRobot.GetComponentInChildren<Animator>().speed==0f && !dp.BeamVisible,"Paused motor freezes visuals");
        int failed=Results.FindAll(s=>s.StartsWith("FAIL")).Count;
        Status=$"Complete: {Results.Count-failed} passed, {failed} failed";
        Debug.Log("[Repair presentation] "+Status+"\n"+string.Join("\n",Results));
        Destroy(fixture);
    }
    private GameObject Cube(string name,Vector3 position,Vector3 size)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;
        o.transform.SetParent(fixture.transform);o.transform.position=position;o.transform.localScale=size;return o;
    }
    private static void Set(object component,string field,object value)
    { component.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(component,value); }
    private static void Check(bool value,string name) { Results.Add((value?"PASS ":"FAIL ")+name); }
    private void OnDestroy() { if(fixture!=null)Destroy(fixture); }
}
#endif
