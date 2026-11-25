using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.EditorTools
{
    /// <summary>Dumps layout facts (props, weapon views, room extents) used to place gameplay objects.</summary>
    public static class StationInspector
    {
        [MenuItem("Last Light/Quality/Inspect Station Layout")]
        public static void Inspect()
        {
            var r = new StringBuilder();
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            T[] All<T>() where T : Component => roots.SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

            foreach (var c in All<CarryableObject>())
            {
                var rend = c.GetComponentsInChildren<Renderer>();
                Bounds b = rend.Length > 0 ? rend[0].bounds : new Bounds(c.transform.position, Vector3.zero);
                foreach (var x in rend) b.Encapsulate(x.bounds);
                r.AppendLine($"CARRYABLE {StationQualityTools.PathOf(c.transform)} pos {c.transform.position} bounds {b.size} comps {string.Join(",", c.GetComponents<Component>().Select(k => k.GetType().Name))}");
            }
            foreach (var t in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name.ToLower().Contains("barricade")))
                r.AppendLine($"BARRICADE {StationQualityTools.PathOf(t)} pos {t.position} comps {string.Join(",", t.GetComponents<Component>().Select(k => k.GetType().Name))}");

            var shooter = All<PlayerShooter>().FirstOrDefault();
            if (shooter != null)
            {
                var so = new SerializedObject(shooter);
                foreach (string slot in new[] { "rifle", "pistol" })
                {
                    var view = so.FindProperty(slot + ".view")?.objectReferenceValue as GameObject;
                    if (view == null) { r.AppendLine("VIEW " + slot + " none"); continue; }
                    var rend = view.GetComponentsInChildren<Renderer>(true);
                    Bounds b = rend[0].bounds; foreach (var x in rend) b.Encapsulate(x.bounds);
                    r.AppendLine($"VIEW {slot} {StationQualityTools.PathOf(view.transform)} layer {LayerMask.LayerToName(view.layer)} renderers {rend.Length} worldSize {b.size} localScale {view.transform.lossyScale} mats {string.Join(",", rend.SelectMany(x => x.sharedMaterials).Where(m => m).Select(m => m.name).Distinct())}");
                    foreach (Transform ch in view.transform) r.AppendLine("   child " + ch.name + " comps " + string.Join(",", ch.GetComponents<Component>().Select(k => k.GetType().Name)));
                }
            }

            Physics.SyncTransforms();
            var probes = new (string, Vector3)[]
            {
                ("evacuation", new Vector3(8f, 1f, 8f)), ("evacuation-near-door", new Vector3(4f, 1f, 6f)),
                ("arrival", new Vector3(8f, 1f, -2f)), ("power", new Vector3(-8f, 1f, 0f)),
                ("storage", new Vector3(-8f, 1f, 8f)), ("armory", new Vector3(-8f, 1f, -8f)),
                ("corridor-ground", new Vector3(0f, 1f, 0f)), ("main-lab", new Vector3(8f, 5f, 3f)), ("medical", new Vector3(-8f, 5f, -7f)),
            };
            foreach (var (name, p) in probes)
            {
                string Dist(Vector3 d) => Physics.Raycast(p, d, out RaycastHit h, 30f, ~0, QueryTriggerInteraction.Ignore) ? $"{h.distance:0.00}({h.collider.name})" : "none";
                r.AppendLine($"PROBE {name} {p}: +x {Dist(Vector3.right)} -x {Dist(Vector3.left)} +z {Dist(Vector3.forward)} -z {Dist(Vector3.back)} down {Dist(Vector3.down)} up {Dist(Vector3.up)} inside {Physics.CheckSphere(p, 0.2f, ~0, QueryTriggerInteraction.Ignore)}");
            }
            foreach (var d in All<StationSlidingDoor>())
                r.AppendLine($"DOOR {d.name} req {d.Requirement} pos {d.transform.position} rot {d.transform.eulerAngles.y:0}");
            foreach (var x in All<StationRelay>()) r.AppendLine($"RELAY {x.Id} {StationQualityTools.PathOf(x.transform)} pos {x.transform.position}");
            foreach (var x in All<LastLight.AI.Hunter.HunterAgent>()) r.AppendLine($"HUNTER {x.name} active {x.gameObject.activeInHierarchy} pos {x.transform.position}");
            foreach (var x in All<LastLight.AI.Guard.TacticalGuardAgent>()) r.AppendLine($"GUARD {x.name} pos {x.transform.position}");
            foreach (var x in All<RepairTargetSelector>()) r.AppendLine($"REPAIR {x.name} active {x.gameObject.activeInHierarchy} pos {x.transform.position}");
            var player = All<PlayerShooter>().FirstOrDefault();
            if (player != null) r.AppendLine($"PLAYER pos {player.transform.position}");
            foreach (var m in new[] { "Station_MedicalGreen", "Station_DressedCeramic", "Station_DressedRed", "Station_EvacRed", "Station_SignalBlue", "StationControlMetal", "Station_DressedSteel", "Station_DressedGraphite" })
                r.AppendLine($"MAT {m}: {AssetDatabase.FindAssets(m + " t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault()}");
            File.WriteAllText(Path.Combine(StationQualityTools.ReportFolder, "layout.txt"), r.ToString());
            Debug.Log(r.ToString());
        }
    }
}
