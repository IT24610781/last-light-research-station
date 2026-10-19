using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class RepairRobotDebugWindow : EditorWindow
{
    private ObjectField robotField;
    private Label output;

    [MenuItem("Last Light/Repair Robot Debug")]
    public static void Open() => GetWindow<RepairRobotDebugWindow>("Repair Robot Debug");

    public void CreateGUI()
    {
        rootVisualElement.style.paddingLeft = 12;
        rootVisualElement.style.paddingRight = 12;
        rootVisualElement.style.paddingTop = 10;
        var title = new Label("REPAIR ROBOT — DECISIONS & OBSERVATIONS");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        rootVisualElement.Add(title);
        robotField = new ObjectField("Robot selector")
        { objectType = typeof(RepairTargetSelector), allowSceneObjects = true };
        rootVisualElement.Add(robotField);
        var scroll = new ScrollView();
        scroll.style.flexGrow = 1;
        output = new Label();
        output.style.whiteSpace = WhiteSpace.Normal;
        output.style.marginTop = 10;
        output.style.fontSize = 13;
        scroll.Add(output); rootVisualElement.Add(scroll);
        rootVisualElement.schedule.Execute(Refresh).Every(250);
        Refresh();
    }
    private void Refresh()
    {
        if (robotField == null || output == null) return;
        RepairTargetSelector selector = robotField.value as RepairTargetSelector;
        if (selector == null && Selection.activeGameObject != null)
            selector = Selection.activeGameObject.GetComponent<RepairTargetSelector>();
        if (selector == null) selector = FindFirstObjectByType<RepairTargetSelector>();
        if (selector == null) { output.text = "Select a Repair Robot in the Hierarchy."; return; }
        var text = new StringBuilder();
        text.AppendLine($"Robot: {selector.name}");
        text.AppendLine(Application.isPlaying ? "PLAY MODE" : "Enter Play mode to see live scores.");
        RepairRoutePlanner planner = selector.GetComponent<RepairRoutePlanner>();
        RepairRobotSensor sensor = selector.GetComponent<RepairRobotSensor>();
        RepairRobotMovement movement = selector.GetComponent<RepairRobotMovement>();
        RepairRobotRepair repair = selector.GetComponent<RepairRobotRepair>();
        if (planner != null) text.AppendLine($"Routing: {planner.ProviderDescription}");
        if (movement != null) text.AppendLine($"State: {movement.State}\nMovement: {movement.MovementReason}");
        text.AppendLine($"Target: {(selector.CurrentTarget != null ? selector.CurrentTarget.name : "none")}");
        text.AppendLine($"Decision: {selector.Decision}");
        if (sensor != null)
            text.AppendLine($"Observed: {sensor.Observation}\nImmediate danger: {sensor.ImmediateDanger}");
        if (repair != null) text.AppendLine($"Repair active: {repair.IsRepairing}");
        text.AppendLine($"Switch margin: {selector.SwitchScoreMargin:F1}\n");
        text.AppendLine("ALLY | MISSING HP | ROUTE METRES | DANGER | UTILITY");
        foreach (RepairTargetSelector.Candidate item in selector.Candidates)
        {
            string name = item.Ally != null ? item.Ally.name : "Unassigned";
            text.AppendLine(item.Eligible
                ? $"{name} | {item.MissingHealth:F0} | {item.RouteCost:F1} | {item.Danger:F2} | {item.Score:F1}"
                : $"{name} | excluded");
            text.AppendLine($"  {item.Reason}");
        }
        if (movement != null && movement.SupportScores.Count > 0)
        {
            text.AppendLine("\nSUPPORT POSITION SCORES");
            foreach (string score in movement.SupportScores) text.AppendLine(score);
        }
        text.AppendLine("\nNo hidden-player tracking. Repair Robot never attacks.\n" +
            "Checked direct fallback cannot detour or change floors. Assign Member A's IPathProvider when available.");
        output.text = text.ToString();
    }
}
