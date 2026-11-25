using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>Play mode: reports how far each robot's visible body sits above/below the floor under it.</summary>
    public static class StationGroundingProbe
    {
        public static float LowestVisibleY(Transform root)
        {
            float minY = float.MaxValue;
            foreach (var rend in root.GetComponentsInChildren<Renderer>().Where(x => x.enabled && (x is MeshRenderer || x is SkinnedMeshRenderer)))
            {
                if (rend is SkinnedMeshRenderer smr)
                {
                    var mesh = new Mesh(); smr.BakeMesh(mesh, true);
                    foreach (var v in mesh.vertices) minY = Mathf.Min(minY, smr.transform.TransformPoint(v).y);
                    Object.DestroyImmediate(mesh);
                }
                else
                {
                    var mf = rend.GetComponent<MeshFilter>();
                    if (mf && mf.sharedMesh && mf.sharedMesh.isReadable)
                        foreach (var v in mf.sharedMesh.vertices) minY = Mathf.Min(minY, rend.transform.TransformPoint(v).y);
                    else minY = Mathf.Min(minY, rend.bounds.min.y);
                }
            }
            return minY;
        }

        public static float FloorBelow(Transform root)
        {
            foreach (var hit in Physics.RaycastAll(root.position + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!hit.transform.IsChildOf(root)) return hit.point.y;
            return float.NaN;
        }

        [MenuItem("Last Light/Quality/Probe Robot Grounding (Play mode)")]
        public static void Probe()
        {
            var r = new StringBuilder();
            foreach (var cc in Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Where(c => c.GetComponent<PlayerShooter>() == null))
            {
                float minY = LowestVisibleY(cc.transform), floor = FloorBelow(cc.transform);
                r.AppendLine($"{cc.name}: root y {cc.transform.position.y:0.000}, lowest visible vertex {minY:0.000}, floor {floor:0.000}, gap {(minY - floor):+0.000;-0.000} m");
            }
            File.WriteAllText(Path.Combine(StationQualityTools.ReportFolder, "grounding.txt"), r.ToString());
            Debug.Log(r.ToString());
        }
    }
}
