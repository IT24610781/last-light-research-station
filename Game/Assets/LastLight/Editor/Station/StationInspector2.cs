using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.EditorTools
{
    public static class StationInspector2
    {
        [MenuItem("Last Light/Quality/Inspect Details")]
        public static void Inspect()
        {
            var r = new StringBuilder();
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            T[] All<T>() where T : Component => roots.SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
            string P(Transform t) => StationQualityTools.PathOf(t);
            string Src(GameObject g) { var s = PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetOutermostPrefabInstanceRoot(g) ?? g); return s ? AssetDatabase.GetAssetPath(s) : "-"; }

            foreach (var relay in All<StationRelay>())
            {
                r.AppendLine($"== RELAY {relay.Id} {P(relay.transform)} pos {relay.transform.position} rot {relay.transform.eulerAngles}");
                var seen = new System.Collections.Generic.HashSet<GameObject>();
                foreach (var rend in All<Renderer>().Where(x => x.bounds.SqrDistance(relay.transform.position) < 3.5f * 3.5f))
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(rend.gameObject) ?? rend.transform.parent?.gameObject ?? rend.gameObject;
                    if (!seen.Add(root)) continue;
                    var b = root.GetComponentsInChildren<Renderer>().Select(x => x.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                    r.AppendLine($"   {P(root.transform)} | src {Src(root)} | pos {root.transform.position} rot {root.transform.eulerAngles} scale {root.transform.lossyScale} | bounds c{b.center} s{b.size}");
                }
            }
            r.AppendLine("== TEXT");
            foreach (var t in All<TextMesh>().Take(6))
                r.AppendLine($"   TextMesh {P(t.transform)} '{t.text.Replace("\n", "|")}' font {t.font?.name} size {t.fontSize} char {t.characterSize} scale {t.transform.lossyScale} mat {t.GetComponent<MeshRenderer>().sharedMaterial?.name}/{t.GetComponent<MeshRenderer>().sharedMaterial?.shader.name} color {t.color}");
            r.AppendLine($"   TextMesh count {All<TextMesh>().Length}");
            var tmpType = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            r.AppendLine($"   TMP type {(tmpType != null)} count {(tmpType != null ? roots.SelectMany(g => g.GetComponentsInChildren(tmpType, true)).Count() : -1)}");

            r.AppendLine("== SERVICE");
            foreach (var s in All<LastLight.AI.Service.ServiceRobotPatrol>())
            {
                var cc = s.GetComponent<CharacterController>();
                var b = s.GetComponentsInChildren<Renderer>().Select(x => x.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                Physics.Raycast(s.transform.position + Vector3.up, Vector3.down, out RaycastHit floor, 5f, ~0, QueryTriggerInteraction.Ignore);
                r.AppendLine($"   {P(s.transform)} pos {s.transform.position} scale {s.transform.lossyScale} cc center {cc.center} h {cc.height} r {cc.radius} skin {cc.skinWidth} | render min y {b.min.y:0.000} max y {b.max.y:0.000} | floor y {floor.point.y:0.000} ({floor.collider?.name})");
                foreach (Transform ch in s.transform) r.AppendLine($"      child {ch.name} localPos {ch.localPosition} localScale {ch.localScale} src {Src(ch.gameObject)}");
                var so = new SerializedObject(s);
                var pts = so.FindProperty("patrolPoints");
                for (int i = 0; i < pts.arraySize; i++) { var t = pts.GetArrayElementAtIndex(i).objectReferenceValue as Transform; r.AppendLine($"      stop {i}: {(t ? P(t) + " " + t.position : "null")}"); }
                var anim = s.GetComponentInChildren<Animator>();
                r.AppendLine($"      animator {anim?.runtimeAnimatorController?.name} avatar {anim?.avatar?.name} rootMotion {anim?.applyRootMotion}");
            }
            foreach (var g in All<LastLight.AI.Hunter.HunterAgent>().Take(1).Select(x => x.gameObject).Concat(All<RepairTargetSelector>().Select(x => x.gameObject)).Concat(All<LastLight.AI.Guard.TacticalGuardAgent>().Select(x => x.gameObject)))
            {
                var cc = g.GetComponent<CharacterController>();
                var b = g.GetComponentsInChildren<Renderer>().Select(x => x.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                Physics.Raycast(g.transform.position + Vector3.up, Vector3.down, out RaycastHit floor, 5f, ~(1 << g.layer), QueryTriggerInteraction.Ignore);
                r.AppendLine($"   ROBOT {g.name} pos {g.transform.position} cc {(cc ? cc.center + " h" + cc.height : "none")} render min y {b.min.y:0.000} floor {floor.point.y:0.000}");
            }

            r.AppendLine("== REPAIR");
            foreach (var sel in All<RepairTargetSelector>())
            {
                r.AppendLine($"   {P(sel.transform)} pos {sel.transform.position}");
                foreach (var comp in sel.GetComponents<MonoBehaviour>())
                {
                    var so = new SerializedObject(comp); var it = so.GetIterator(); bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.name == "m_Script") continue;
                        if (it.propertyType == SerializedPropertyType.ObjectReference)
                            r.AppendLine($"      {comp.GetType().Name}.{it.name} = {(it.objectReferenceValue ? (it.objectReferenceValue is Component c ? P(c.transform) + " (" + c.transform.position + ")" : it.objectReferenceValue.name) : "null")}");
                        else if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.arraySize > 0 && it.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.ObjectReference)
                            for (int i = 0; i < it.arraySize; i++) { var o = it.GetArrayElementAtIndex(i).objectReferenceValue; r.AppendLine($"      {comp.GetType().Name}.{it.name}[{i}] = {(o is Component c ? P(c.transform) + " (" + c.transform.position + ")" : o?.name ?? "null")}"); }
                    }
                }
            }
            foreach (var ally in All<RepairableAlly>()) r.AppendLine($"   ALLY {P(ally.transform)} pos {ally.transform.position}");

            r.AppendLine("== BARRICADE");
            foreach (var c in All<CarryableObject>().Where(x => x.name.Contains("Barricade")))
            {
                var b = c.GetComponent<Renderer>().bounds;
                r.AppendLine($"   {P(c.transform)} pos {c.transform.position} rot {c.transform.eulerAngles} scale {c.transform.localScale} bounds {b.size} mass {c.GetComponent<Rigidbody>().mass} box {c.GetComponent<BoxCollider>().size}");
            }
            r.AppendLine("== GRABBER");
            var grab = All<PlayerGrabber>().FirstOrDefault();
            if (grab) { var so = new SerializedObject(grab); var it = so.GetIterator(); bool e = true; while (it.NextVisible(e)) { e = false; r.AppendLine($"   {it.name}: {(it.propertyType == SerializedPropertyType.Float ? it.floatValue.ToString() : it.propertyType.ToString())}"); } }
            File.WriteAllText(Path.Combine(StationQualityTools.ReportFolder, "details.txt"), r.ToString());
            Debug.Log("details written");
        }
    }
}
