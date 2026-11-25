using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Gives gameplay objects a physical, readable form in ResearchStation (idempotent):
    /// real weapon models on the Armory bench, visible medical cases, the evacuation console as the
    /// usable terminal plus an exit trigger, and small throwable crates for noise distraction.
    /// </summary>
    public static class StationGameplayDressing
    {
        private const string CratePrefab = "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Prefabs/Props/Crate_01.prefab";
        private static StringBuilder log;

        [MenuItem("Last Light/Quality/Apply Gameplay Dressing (open scene)")]
        public static void Apply()
        {
            log = new StringBuilder();
            Physics.SyncTransforms();
            WeaponModels();
            MedicalCases();
            EvacuationTerminalAndZone();
            ThrowableCrates();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(StationQualityTools.ReportFolder, "dressing.txt"), log.ToString());
            Debug.Log(log.ToString());
        }

        private static IEnumerable<T> All<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));

        private static Material Mat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"Assets/LastLight/Materials/ResearchStation/{name}.mat");

        private static bool WorldBounds(Transform root, out Bounds b, System.Func<Renderer, bool> filter = null)
        {
            b = default; bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (filter != null && !filter(r)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        private static void FitBoxCollider(GameObject go)
        {
            foreach (var old in go.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(old);
            if (!WorldBounds(go.transform, out Bounds b)) return;
            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            Vector3 local = go.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)) + Vector3.one * 0.03f;
        }

        /// <summary>Surface height directly below a point, ignoring the given object.</summary>
        private static bool SurfaceBelow(Vector3 from, Transform ignore, out float y)
        {
            y = 0f;
            var hits = Physics.RaycastAll(from, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
            foreach (RaycastHit h in hits)
            {
                if (ignore != null && h.transform.IsChildOf(ignore)) continue;
                y = h.point.y; return true;
            }
            return false;
        }

        // ---------------- Weapons ----------------

        private static void WeaponModels()
        {
            PlayerShooter shooter = All<PlayerShooter>().FirstOrDefault();
            if (shooter == null) { log.AppendLine("No PlayerShooter"); return; }
            var so = new SerializedObject(shooter);
            // Decorative block guns on the bench become real, takeable weapons too.
            foreach (Transform t in All<Transform>().Where(t => t.name.StartsWith("Rifle on armory bench") || t.name.StartsWith("Pistol on armory bench")).ToList())
            {
                if (t.GetComponent<StationWeaponPickup>() != null) continue;
                var added = Undo.AddComponent<StationWeaponPickup>(t.gameObject);
                added.Configure(t.name.StartsWith("Rifle") ? StationWeaponPickup.WeaponType.Rifle : StationWeaponPickup.WeaponType.Pistol);
                log.AppendLine($"{t.name}: decorative gun made takeable");
            }
            foreach (StationWeaponPickup pickup in All<StationWeaponPickup>())
            {
                string slot = pickup.Type == StationWeaponPickup.WeaponType.Rifle ? "rifle" : "pistol";
                var view = so.FindProperty(slot + ".view")?.objectReferenceValue as GameObject;
                if (view == null) { log.AppendLine("No view for " + slot); continue; }
                Transform existing = pickup.transform.Find("Display model");
                if (existing != null) { log.AppendLine($"{pickup.name}: model already present"); continue; }

                // Bench surface below the old placeholder.
                WorldBounds(pickup.transform, out Bounds old);
                foreach (var c in pickup.GetComponentsInChildren<Collider>()) c.enabled = false;
                Physics.SyncTransforms();
                bool surface = SurfaceBelow(old.center + Vector3.up * 0.3f, pickup.transform, out float benchY);
                foreach (var c in pickup.GetComponentsInChildren<Collider>()) c.enabled = true;
                if (!surface) benchY = old.min.y;

                // Retire the block placeholder parts.
                foreach (Transform child in pickup.transform.Cast<Transform>().ToList())
                    Undo.DestroyObjectImmediate(child.gameObject);

                GameObject model = Object.Instantiate(view, pickup.transform);
                Undo.RegisterCreatedObjectUndo(model, "Pickup model");
                model.name = "Display model";
                model.SetActive(true);
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(b);
                foreach (var l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
                // Hide any hidden placeholder body; keep the authored gun mesh.
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    if (r.name.EndsWith("Body")) r.enabled = false;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // lying on its side
                float targetLength = pickup.Type == StationWeaponPickup.WeaponType.Rifle ? 0.95f : 0.27f;
                WorldBounds(model.transform, out Bounds mb, r => r.enabled);
                float length = Mathf.Max(mb.size.x, mb.size.z);
                if (length > 0.01f) model.transform.localScale *= targetLength / length;
                Physics.SyncTransforms();
                WorldBounds(model.transform, out mb, r => r.enabled);
                Vector3 offset = new Vector3(old.center.x - mb.center.x, benchY + 0.004f - mb.min.y, old.center.z - mb.center.z);
                model.transform.position += offset;
                FitBoxCollider(pickup.gameObject);
                log.AppendLine($"{pickup.name}: real {slot} model placed on bench at y {benchY:0.00}");
            }
        }

        // ---------------- Medical cases ----------------

        private static void MedicalCases()
        {
            Material shell = Mat("Station_DressedCeramic");
            Material trim = Mat("Station_DressedGraphite");
            Material cross = Mat("Station_MedicalGreen") ?? Mat("Station_SignalBlue");
            var medkits = All<StationMedkit>().ToList();
            var targets = new Dictionary<StationMedkit, Vector3>();
            foreach (StationMedkit m in medkits)
            {
                Vector3 p = m.transform.position;
                float y = SurfaceBelow(p + Vector3.up * 0.2f, m.transform, out float s) ? s : p.y - 0.5f;
                targets[m] = new Vector3(p.x, y, p.z);
            }
            // Parents first so moving a parent never displaces an already-placed child.
            foreach (StationMedkit m in medkits.OrderBy(k => k.transform.hierarchyCount > 0 ? Depth(k.transform) : 0))
            {
                if (m.transform.Find("Medical case") != null) { log.AppendLine($"{m.name}: case already present"); continue; }
                Undo.RecordObject(m.transform, "Place medkit");
                // Move only this transform, keeping children (other medkits) where they were.
                var kids = m.transform.Cast<Transform>().Select(k => (k, k.position)).ToList();
                m.transform.position = targets[m] + Vector3.up * 0.1f;
                foreach (var (k, pos) in kids) k.position = pos;

                var root = new GameObject("Medical case");
                Undo.RegisterCreatedObjectUndo(root, "Medical case");
                root.transform.SetParent(m.transform, false);
                Part(root.transform, "Shell", new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.18f, 0.32f), shell);
                Part(root.transform, "Seam band", new Vector3(0f, 0.005f, 0f), new Vector3(0.47f, 0.025f, 0.33f), trim);
                Part(root.transform, "Handle", new Vector3(0f, 0.11f, 0f), new Vector3(0.16f, 0.03f, 0.035f), trim);
                Part(root.transform, "Latch L", new Vector3(-0.14f, 0.02f, 0.165f), new Vector3(0.04f, 0.05f, 0.012f), trim);
                Part(root.transform, "Latch R", new Vector3(0.14f, 0.02f, 0.165f), new Vector3(0.04f, 0.05f, 0.012f), trim);
                Part(root.transform, "Cross top V", new Vector3(0f, 0.0915f, -0.06f), new Vector3(0.04f, 0.004f, 0.13f), cross);
                Part(root.transform, "Cross top H", new Vector3(0f, 0.0915f, -0.06f), new Vector3(0.13f, 0.004f, 0.04f), cross);
                Part(root.transform, "Cross front V", new Vector3(0.12f, 0f, 0.1615f), new Vector3(0.035f, 0.11f, 0.004f), cross);
                Part(root.transform, "Cross front H", new Vector3(0.12f, 0f, 0.1615f), new Vector3(0.11f, 0.035f, 0.004f), cross);
                var box = Undo.AddComponent<BoxCollider>(m.gameObject);
                box.center = Vector3.zero;
                box.size = new Vector3(0.48f, 0.22f, 0.34f);
                log.AppendLine($"{m.name}: medical case placed at {m.transform.position}");
            }
        }

        private static int Depth(Transform t) { int d = 0; for (; t.parent != null; t = t.parent) d++; return d; }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---------------- Evacuation ----------------

        private static void EvacuationTerminalAndZone()
        {
            Transform controls = All<Transform>().FirstOrDefault(t => t.name.StartsWith("Evacuation pressure controls physical bounds"))
                              ?? All<Transform>().FirstOrDefault(t => t.name.StartsWith("Evacuation pressure controls"));
            var markers = All<StationEvacuationTerminal>().ToList();
            if (controls == null) { log.AppendLine("No evacuation pressure controls found"); }
            else if (controls.GetComponent<StationEvacuationTerminal>() == null)
            {
                if (controls.GetComponent<Collider>() == null) FitBoxCollider(controls.gameObject);
                Undo.AddComponent<StationEvacuationTerminal>(controls.gameObject);
                foreach (var m in markers) Undo.DestroyObjectImmediate(m);
                log.AppendLine($"Evacuation terminal moved onto {StationQualityTools.PathOf(controls)} at {controls.position}");
            }

            if (All<StationEvacuationZone>().Any()) { log.AppendLine("Evacuation zone already present"); return; }
            Transform door = All<StationSlidingDoor>().FirstOrDefault(d => d.Requirement == StationSlidingDoor.AccessRequirement.BothRelays)?.transform;
            var zone = new GameObject("Evacuation exit zone — enter with both relays online");
            Undo.RegisterCreatedObjectUndo(zone, "Evacuation zone");
            if (door != null && door.parent != null) zone.transform.SetParent(door.parent, true);
            // Evacuation bay interior measured from walls: x 1.6..13.0, z 0.1..11.9 (ground floor).
            zone.transform.position = new Vector3(8.6f, 1.2f, 6.0f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8.0f, 2.4f, 10.4f);
            zone.AddComponent<StationEvacuationZone>();
            log.AppendLine($"Evacuation exit zone created at {zone.transform.position} size {box.size}");
        }

        // ---------------- Throwable crates ----------------

        private static void ThrowableCrates()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefab);
            CarryableObject barricade = All<CarryableObject>().FirstOrDefault();
            if (prefab == null || barricade == null) { log.AppendLine("Crate prefab or barricade template missing"); return; }
            var spots = new (string, Vector3)[]
            {
                ("Arrival supply crate (throwable)", new Vector3(10.5f, 0f, -8.5f)),
                ("Power room spares crate (throwable)", new Vector3(-4.6f, 0f, -2.6f)),
                ("Storage supply crate A (throwable)", new Vector3(-4.0f, 0f, 10.6f)),
                ("Storage supply crate B (throwable)", new Vector3(-12.8f, 0f, 5.2f)),
            };
            GameObject parent = barricade.transform.parent != null ? barricade.transform.parent.gameObject : null;
            foreach (var (name, wanted) in spots)
            {
                if (All<Transform>().Any(t => t.name == name)) { log.AppendLine(name + " already present"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(go, "Throwable crate");
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                if (parent != null) go.transform.SetParent(parent.transform, true);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) if (StationQualityTools.IsUnsupported(mats[i])) mats[i] = StationQualityTools.GetUrpCopy(mats[i]);
                    r.sharedMaterials = mats;
                }
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
                WorldBounds(go.transform, out Bounds b);
                float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (largest > 0.01f) go.transform.localScale *= 0.42f / largest;
                Physics.SyncTransforms();
                WorldBounds(go.transform, out b);

                // Find a clear floor spot near the wanted point.
                Vector3 placed = wanted; bool ok = false;
                for (int ring = 0; ring < 4 && !ok; ring++)
                    for (int i = 0; i < 8 && !ok; i++)
                    {
                        Vector3 p = wanted + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (ring * 0.6f);
                        if (!SurfaceBelow(p + Vector3.up * 1.5f, go.transform, out float floor)) continue;
                        Vector3 centre = new Vector3(p.x, floor + b.extents.y + 0.02f, p.z);
                        if (Physics.CheckBox(centre, b.extents * 1.15f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                        placed = centre; ok = true;
                    }
                go.transform.position += placed - b.center;
                FitBoxCollider(go);
                var body = go.AddComponent<Rigidbody>();
                body.mass = 4f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                go.AddComponent<CarryableObject>();
                var noise = go.AddComponent<ImpactNoise>();
                EditorUtility.CopySerialized(barricade.GetComponent<ImpactNoise>(), noise);
                log.AppendLine($"{name}: {(ok ? "placed" : "NO CLEAR SPOT, placed anyway")} at {go.transform.position}");
            }
        }
    }
}
