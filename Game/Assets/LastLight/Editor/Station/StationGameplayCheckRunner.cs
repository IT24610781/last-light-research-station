using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>Enters Play mode in ResearchStation and runs StationGameplayChecks.</summary>
    [InitializeOnLoad]
    public static class StationGameplayCheckRunner
    {
        private const string Pending = "LastLight.StationGameplayCheck.Pending";
        private const string Batch = "LastLight.StationGameplayCheck.Batch";
        private const string Started = "LastLight.StationGameplayCheck.Started";

        static StationGameplayCheckRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(Batch, false)) EditorApplication.update += WatchBatch;
        }

        [MenuItem("Last Light/Quality/Run Station Gameplay Check")]
        public static void Run() => Begin(false);

        public static void BatchRun() => Begin(true);

        private static void Begin(bool batch)
        {
            if (EditorApplication.isPlaying) { StartChecker(); return; }
            EditorSceneManager.OpenScene(StationQualityTools.StationScene, OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Batch, batch);
            SessionState.SetFloat(Started, (float)EditorApplication.timeSinceStartup);
            if (batch) EditorApplication.update += WatchBatch;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            EditorApplication.delayCall += StartChecker;
        }

        private static void StartChecker()
        {
            EditorApplication.isPaused = false;
            new GameObject("StationGameplayChecks").AddComponent<StationGameplayChecks>();
            Debug.Log("Station gameplay check started; results: " + Path.Combine(StationQualityTools.ReportFolder, "gameplay-check.txt"));
        }

        private static void WatchBatch()
        {
            bool done = StationGameplayChecks.Status == "Complete";
            bool timedOut = EditorApplication.timeSinceStartup - SessionState.GetFloat(Started, 0f) > 240f;
            if (!done && !timedOut) return;
            EditorApplication.update -= WatchBatch;
            SessionState.SetBool(Batch, false);
            if (timedOut && !done)
                File.AppendAllText(Path.Combine(StationQualityTools.ReportFolder, "gameplay-check.txt"), "\nTIMEOUT\n");
            EditorApplication.Exit(done && StationGameplayChecks.Failures == 0 ? 0 : 3);
        }
    }
}
