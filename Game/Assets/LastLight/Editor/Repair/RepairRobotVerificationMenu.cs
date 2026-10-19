using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RepairRobotVerificationMenu
{
    private const string Pending = "LastLight.RepairVerification.Pending";
    static RepairRobotVerificationMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    [MenuItem("Last Light/Run Repair Robot Verification")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { StartRunner(); return; }
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        EditorApplication.delayCall += StartRunner;
    }
    private static void StartRunner()
    {
        EditorApplication.isPaused = false;
        RepairRobotScenarioTests existing = Object.FindFirstObjectByType<RepairRobotScenarioTests>();
        if (existing != null)
        {
            if (RepairRobotScenarioTests.Status == "Running") return;
            Object.DestroyImmediate(existing.gameObject);
        }
        new GameObject("RepairRobotVerificationRunner").AddComponent<RepairRobotScenarioTests>();
        Debug.Log("Repair Robot verification started. Results appear in Console in about 35 seconds. Stop Play mode when finished.");
    }
}
