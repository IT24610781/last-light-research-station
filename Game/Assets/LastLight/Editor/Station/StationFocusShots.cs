using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.EditorTools
{
    /// <summary>Renders fixed review views (relay rooms, doors, robots) to Logs/QualityReport/focus.</summary>
    public static class StationFocusShots
    {
        [MenuItem("Last Light/Quality/Render Focus Shots")]
        public static void Render()
        {
            string folder = Path.Combine(StationQualityTools.ReportFolder, "focus");
            Directory.CreateDirectory(folder);
            foreach (var f in Directory.GetFiles(folder, "*.png")) File.Delete(f);
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            T[] All<T>() where T : Component => roots.SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
            Camera reference = All<Camera>().FirstOrDefault(c => c.CompareTag("MainCamera"));
            var go = new GameObject("FocusCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            if (reference) cam.CopyFrom(reference);
            cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            void Shot(string name, Vector3 from, Vector3 at)
            {
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            Shot("relayA-room", new Vector3(-5.5f, 1.7f, -1.5f), new Vector3(-13.5f, 1.4f, -0.5f));
            Shot("relayB-room", new Vector3(5.5f, 5.7f, 5.5f), new Vector3(13.5f, 5.4f, 6f));
            foreach (var d in All<StationSlidingDoor>())
            {
                Vector3 p = d.transform.position;
                float side = p.x < 0 ? 1f : -1f; // corridor side
                Shot("door-" + d.name.Substring(0, 2), new Vector3(p.x + side * 2.6f, p.y + 1.6f, p.z - 3.2f), new Vector3(p.x, p.y + 2.0f, p.z));
            }
            foreach (var s in All<LastLight.AI.Service.ServiceRobotPatrol>())
                Shot("service-robot", s.transform.position + new Vector3(1.8f, 0.6f, -1.2f), s.transform.position + Vector3.up * 0.2f);
            foreach (var b in All<CarryableObject>().Where(c => c.name.Contains("Barricade")))
                Shot("barricade", new Vector3(-2.6f, 1.8f, 10.5f), b.transform.position + Vector3.up * 0.6f);
            Shot("relayB-bank", new Vector3(9.2f, 5.7f, 6.0f), new Vector3(13.3f, 5.5f, 6.0f));
            Shot("relayA-bank", new Vector3(-9.2f, 1.7f, 0.0f), new Vector3(-13.3f, 1.5f, 0.0f));
            Shot("storage-room", new Vector3(-2.6f, 2.2f, 5.0f), new Vector3(-11f, 0.3f, 10f));
            Shot("corridor-ground", new Vector3(0f, 1.7f, -11f), new Vector3(0f, 2.0f, 2f));
            Shot("corridor-upper", new Vector3(0f, 5.7f, 11f), new Vector3(0f, 6.0f, -2f));
            foreach (var h in All<LastLight.AI.Hunter.HunterAgent>().Where(x => x.gameObject.activeInHierarchy))
                Shot("hunter", h.transform.position + new Vector3(2.2f, 0.8f, -1.5f), h.transform.position + Vector3.up * 0.3f);
            foreach (var r in All<RepairTargetSelector>())
                Shot("repair-" + r.name.Split(' ')[0], r.transform.position + new Vector3(1.8f, 0.6f, -1.2f), r.transform.position);
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            Debug.Log("Focus shots written to " + folder);
        }
    }
}
