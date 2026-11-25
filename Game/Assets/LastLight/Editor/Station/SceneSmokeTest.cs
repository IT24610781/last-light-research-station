using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>Plays each demo scene for a few seconds and records errors/exceptions per scene.</summary>
    [InitializeOnLoad]
    public static class SceneSmokeTest
    {
        private const string QueueKey = "LL.Smoke.Queue", CurrentKey = "LL.Smoke.Current", ReportKey = "LL.Smoke.Report";
        private const string ErrorsKey = "LL.Smoke.Errors", FirstKey = "LL.Smoke.First", StartKey = "LL.Smoke.Start";
        private const float SecondsPerScene = 10f;

        static SceneSmokeTest()
        {
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Last Light/Quality/Smoke Test All Scenes (10 s each)")]
        public static void Run()
        {
            SessionState.SetString(QueueKey, "AgentTest;GuardTest;PlayerTest;ResearchStation");
            SessionState.SetString(ReportKey, "");
            Next();
        }

        private static void Next()
        {
            var queue = SessionState.GetString(QueueKey, "").Split(';').Where(s => s.Length > 0).ToList();
            if (queue.Count == 0)
            {
                SessionState.SetString(CurrentKey, "");
                string report = SessionState.GetString(ReportKey, "");
                File.WriteAllText(Path.Combine(StationQualityTools.ReportFolder, "smoke-test.txt"), report);
                Debug.Log("Scene smoke test complete\n" + report);
                EditorSceneManager.OpenScene(StationQualityTools.StationScene);
                return;
            }
            string scene = queue[0];
            SessionState.SetString(QueueKey, string.Join(";", queue.Skip(1)));
            SessionState.SetString(CurrentKey, scene);
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetString(FirstKey, "");
            EditorSceneManager.OpenScene($"Assets/LastLight/Scenes/{scene}.unity");
            EditorApplication.isPlaying = true;
        }

        private static void OnState(PlayModeStateChange state)
        {
            if (SessionState.GetString(CurrentKey, "") == "") return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            else if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += Next;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (!EditorApplication.isPlaying || SessionState.GetString(CurrentKey, "") == "") return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.Contains("Pipeline Server")) return; // editor tooling, not gameplay
            SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
            if (SessionState.GetString(FirstKey, "") == "") SessionState.SetString(FirstKey, message.Split('\n')[0]);
        }

        private static void Tick()
        {
            string scene = SessionState.GetString(CurrentKey, "");
            if (scene == "" || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, float.MaxValue) < SecondsPerScene) return;
            // Resume if a start screen froze time; agents need to run.
            var session = Object.FindFirstObjectByType<GameSessionController>();
            if (session != null && session.State == GameSessionController.SessionState.Start && SessionState.GetFloat(StartKey + scene, 0f) == 0f)
            {
                session.StartGame();
                SessionState.SetFloat(StartKey + scene, 1f);
                SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                return;
            }
            SessionState.SetFloat(StartKey + scene, 0f);
            int errors = SessionState.GetInt(ErrorsKey, 0);
            string line = $"{(errors == 0 ? "PASS" : "FAIL")} {scene}: {errors} error(s) in {SecondsPerScene:0} s of play" +
                          (errors > 0 ? "  first: " + SessionState.GetString(FirstKey, "") : "");
            SessionState.SetString(ReportKey, SessionState.GetString(ReportKey, "") + line + "\n");
            SessionState.SetString(CurrentKey, scene + "#done");
            EditorApplication.isPlaying = false;
        }
    }
}
