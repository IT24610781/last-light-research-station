using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Station quality pass used before demonstrations:
    /// 1. Replaces built-in (non-URP) kit materials, which URP renders magenta, with tracked URP Lit copies.
    /// 2. Gives every interactable a non-trigger collider so the player's centre-screen raycast can hit it.
    /// 3. Audits scenes for missing scripts, unsupported materials and unreachable interactables.
    /// 4. Renders review screenshots around key gameplay locations.
    /// </summary>
    public static class StationQualityTools
    {
        public const string StationScene = "Assets/LastLight/Scenes/ResearchStation.unity";
        private static readonly string[] Scenes =
        {
            StationScene,
            "Assets/LastLight/Scenes/PlayerTest.unity",
            "Assets/LastLight/Scenes/AgentTest.unity",
            "Assets/LastLight/Scenes/GuardTest.unity",
        };
        private const string MaterialFolder = "Assets/LastLight/Materials/ResearchStation";
        private static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

        public static string ReportFolder
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("LASTLIGHT_REPORT_DIR");
                string folder = string.IsNullOrEmpty(configured)
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QualityReport"))
                    : configured;
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        // ---------------- Menu entries ----------------

        [MenuItem("Last Light/Quality/Fix Materials And Interactables (open scene)")]
        public static void FixOpenSceneMenu()
        {
            var report = new StringBuilder();
            FixActiveScene(report);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(ReportFolder, "fix-open-scene.txt"), report.ToString());
            Debug.Log(report.ToString());
        }

        [MenuItem("Last Light/Quality/Audit Open Scene")]
        public static void AuditOpenSceneMenu()
        {
            var report = new StringBuilder();
            int problems = Audit(report);
            File.WriteAllText(Path.Combine(ReportFolder, "audit-open-scene.txt"), $"PROBLEMS: {problems}\n{report}");
            Debug.Log($"Audit found {problems} problem(s)\n{report}");
        }

        // ---------------- Batch entries ----------------

        [MenuItem("Last Light/Quality/Fix And Audit All Scenes")]
        public static void BatchFixAndAudit() => RunBatch(true);
        public static void BatchAuditOnly() => RunBatch(false);

        private static void RunBatch(bool fix)
        {
            var report = new StringBuilder();
            int exitCode = 0;
            try
            {
                foreach (string path in Scenes)
                {
                    if (!File.Exists(path)) { report.AppendLine("SKIP (missing) " + path); continue; }
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    report.AppendLine();
                    report.AppendLine("==== " + path);
                    if (fix)
                    {
                        FixActiveScene(report);
                        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    }
                    int problems = Audit(report);
                    report.AppendLine($"PROBLEMS: {problems}");
                    if (problems > 0) exitCode = 2;
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                report.AppendLine("EXCEPTION " + e);
                exitCode = 1;
            }
            File.WriteAllText(Path.Combine(ReportFolder, fix ? "fix-and-audit.txt" : "audit.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        // ---------------- Fixes ----------------

        public static void FixActiveScene(StringBuilder report)
        {
            int slots = ConvertUnsupportedMaterials(report);
            int colliders = EnsureInteractableColliders(report);
            report.AppendLine($"Fixed: {slots} material slot(s) converted to URP, {colliders} collider(s) added");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static IEnumerable<T> SceneComponents<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true));

        public static bool IsUnsupported(Material material)
        {
            if (material == null) return false;
            Shader shader = material.shader;
            if (shader == null) return true;
            string n = shader.name;
            return n == "Standard" || n == "Standard (Specular setup)" ||
                   n.StartsWith("Legacy Shaders/") || n.StartsWith("Autodesk") ||
                   n == "Hidden/InternalErrorShader" || !shader.isSupported;
        }

        private static int ConvertUnsupportedMaterials(StringBuilder report)
        {
            int count = 0;
            var names = new SortedSet<string>();
            foreach (Renderer renderer in SceneComponents<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!IsUnsupported(materials[i])) continue;
                    names.Add(materials[i].name);
                    materials[i] = GetUrpCopy(materials[i]);
                    changed = true;
                    count++;
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Convert kit materials to URP");
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (names.Count > 0) report.AppendLine("  converted materials: " + string.Join(", ", names));
            return count;
        }

        public static Material GetUrpCopy(Material source)
        {
            if (converted.TryGetValue(source, out Material cached) && cached != null) return cached;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string guid = string.IsNullOrEmpty(sourcePath) ? "00000000" : AssetDatabase.AssetPathToGUID(sourcePath);
            string safeName = new string(source.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            string path = $"{MaterialFolder}/Kit_{safeName}_{guid.Substring(0, 8)}.mat";
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null || IsUnsupported(copy))
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) throw new InvalidOperationException("URP Lit shader not found");
                copy = new Material(lit) { name = Path.GetFileNameWithoutExtension(path) };
                CopyStandardToUrp(source, copy);
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.CreateAsset(copy, path);
            }
            converted[source] = copy;
            return copy;
        }

        private static void CopyTexture(Material s, string from, Material d, string to)
        {
            if (!s.HasProperty(from) || !d.HasProperty(to)) return;
            Texture texture = s.GetTexture(from);
            if (texture == null) return;
            d.SetTexture(to, texture);
            d.SetTextureScale(to, s.GetTextureScale(from));
            d.SetTextureOffset(to, s.GetTextureOffset(from));
        }

        private static void CopyStandardToUrp(Material s, Material d)
        {
            CopyTexture(s, "_MainTex", d, "_BaseMap");
            if (s.HasProperty("_Color")) d.SetColor("_BaseColor", s.GetColor("_Color"));

            if (s.HasProperty("_BumpMap") && s.GetTexture("_BumpMap") != null)
            {
                CopyTexture(s, "_BumpMap", d, "_BumpMap");
                d.SetFloat("_BumpScale", s.HasProperty("_BumpScale") ? s.GetFloat("_BumpScale") : 1f);
                d.EnableKeyword("_NORMALMAP");
            }

            bool metallicMap = s.HasProperty("_MetallicGlossMap") && s.GetTexture("_MetallicGlossMap") != null;
            if (metallicMap)
            {
                CopyTexture(s, "_MetallicGlossMap", d, "_MetallicGlossMap");
                d.SetFloat("_Metallic", 1f);
                d.SetFloat("_Smoothness", s.HasProperty("_GlossMapScale") ? s.GetFloat("_GlossMapScale") : 0.5f);
                d.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                if (s.HasProperty("_Metallic")) d.SetFloat("_Metallic", s.GetFloat("_Metallic"));
                if (s.HasProperty("_Glossiness")) d.SetFloat("_Smoothness", s.GetFloat("_Glossiness"));
            }

            if (s.HasProperty("_OcclusionMap") && s.GetTexture("_OcclusionMap") != null)
            {
                CopyTexture(s, "_OcclusionMap", d, "_OcclusionMap");
                d.SetFloat("_OcclusionStrength", s.HasProperty("_OcclusionStrength") ? s.GetFloat("_OcclusionStrength") : 1f);
                d.EnableKeyword("_OCCLUSIONMAP");
            }

            if (s.HasProperty("_EmissionColor") && s.IsKeywordEnabled("_EMISSION"))
            {
                Color emission = s.GetColor("_EmissionColor");
                if (emission.maxColorComponent > 0.001f)
                {
                    d.SetColor("_EmissionColor", emission);
                    CopyTexture(s, "_EmissionMap", d, "_EmissionMap");
                    d.EnableKeyword("_EMISSION");
                    d.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
            }

            float mode = s.HasProperty("_Mode") ? s.GetFloat("_Mode") : 0f;
            if (Mathf.Approximately(mode, 1f))
            {
                d.SetFloat("_AlphaClip", 1f);
                d.SetFloat("_Cutoff", s.HasProperty("_Cutoff") ? s.GetFloat("_Cutoff") : 0.5f);
                d.EnableKeyword("_ALPHATEST_ON");
                d.SetOverrideTag("RenderType", "TransparentCutout");
                d.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (mode >= 2f)
            {
                d.SetFloat("_Surface", 1f);
                d.SetFloat("_Blend", 0f);
                d.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                d.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                d.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                d.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                d.SetFloat("_ZWrite", 0f);
                d.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                d.SetOverrideTag("RenderType", "Transparent");
                d.renderQueue = (int)RenderQueue.Transparent;
                d.SetShaderPassEnabled("DepthOnly", false);
                d.SetShaderPassEnabled("ShadowCaster", false);
            }
            d.enableInstancing = true;
        }

        /// <summary>Every object the player can use must be hittable by the centre-screen raycast.</summary>
        private static IEnumerable<MonoBehaviour> Interactables() =>
            SceneComponents<MonoBehaviour>().Where(m => m != null &&
                (m is IInteractable || m is CarryableObject || m is StationRelay));

        private static bool HasHittableCollider(Component target) =>
            target.GetComponentsInChildren<Collider>(true)
                .Any(c => c.enabled && !c.isTrigger && c.gameObject.layer != 2);

        private static int EnsureInteractableColliders(StringBuilder report)
        {
            int added = 0;
            foreach (MonoBehaviour target in Interactables())
            {
                if (HasHittableCollider(target)) continue;
                if (!TryLocalRendererBounds(target.transform, out Bounds bounds))
                {
                    report.AppendLine("  WARNING no renderer to size a collider: " + PathOf(target.transform));
                    continue;
                }
                var box = Undo.AddComponent<BoxCollider>(target.gameObject);
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size + Vector3.one * 0.04f, Vector3.one * 0.12f);
                added++;
                report.AppendLine($"  collider added: {PathOf(target.transform)} ({target.GetType().Name}) size {box.size}");
            }
            return added;
        }

        private static bool TryLocalRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                Bounds local = r.localBounds;
                Vector3 min = local.min, max = local.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        // ---------------- Audit ----------------

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static int Audit(StringBuilder report)
        {
            int problems = 0;
            void Problem(string message) { problems++; report.AppendLine("  PROBLEM " + message); }

            int missingScripts = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missingScripts > 0) Problem($"{missingScripts} missing script reference(s)");

            int badSlots = 0, nullSlots = 0;
            foreach (Renderer r in SceneComponents<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) { nullSlots++; if (nullSlots <= 5) report.AppendLine("  empty material slot: " + PathOf(r.transform)); }
                    else if (IsUnsupported(m)) { badSlots++; if (badSlots <= 10) report.AppendLine($"  unsupported material: {PathOf(r.transform)} -> {m.name} ({m.shader?.name})"); }
                }
            }
            if (badSlots > 0) Problem($"{badSlots} renderer slot(s) use a non-URP shader (render magenta)");
            if (nullSlots > 0) Problem($"{nullSlots} renderer slot(s) have no material (render magenta)");

            Renderer[] allRenderers = SceneComponents<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            foreach (MonoBehaviour target in Interactables())
            {
                if (HasHittableCollider(target)) continue;
                Problem($"not hittable by interaction ray: {PathOf(target.transform)} ({target.GetType().Name})");
                Vector3 p = target.transform.position;
                report.AppendLine($"      at {p}  rot {target.transform.eulerAngles}  scale {target.transform.lossyScale}  active {target.gameObject.activeInHierarchy}");
                report.AppendLine("      components: " + string.Join(", ", target.GetComponents<Component>().Select(c => c.GetType().Name)));
                foreach (Renderer near in allRenderers.Where(r => r.bounds.SqrDistance(p) < 0.6f * 0.6f).Take(6))
                    report.AppendLine($"      nearby renderer: {PathOf(near.transform)}  bounds {near.bounds.center} size {near.bounds.size}");
            }

            var pickups = SceneComponents<StationWeaponPickup>().ToList();
            var relays = SceneComponents<StationRelay>().ToList();
            var doors = SceneComponents<StationSlidingDoor>().ToList();
            report.AppendLine($"  weapon pickups: {string.Join(", ", pickups.Select(p => p.Type + " @ " + p.name))}");
            report.AppendLine($"  relays: {string.Join(", ", relays.Select(r => r.Id.ToString()))}");
            report.AppendLine($"  doors: {doors.Count} ({string.Join(", ", doors.GroupBy(d => d.Requirement).Select(g => g.Key + " x" + g.Count()))})");
            report.AppendLine($"  medkits: {SceneComponents<StationMedkit>().Count()}, carryables: {SceneComponents<CarryableObject>().Count()}, " +
                              $"evacuation terminals: {SceneComponents<StationEvacuationTerminal>().Count()}");
            report.AppendLine($"  agents: Guard {SceneComponents<LastLight.AI.Guard.TacticalGuardAgent>().Count()}, " +
                              $"Hunter {SceneComponents<LastLight.AI.Hunter.HunterAgent>().Count()}, " +
                              $"Repair {SceneComponents<RepairTargetSelector>().Count()}, " +
                              $"Director {SceneComponents<LastLight.AI.Director.EncounterDirector>().Count()}, " +
                              $"Service {SceneComponents<LastLight.AI.Service.ServiceRobotPatrol>().Count()}");

            if (SceneManager.GetActiveScene().path == StationScene)
            {
                if (!pickups.Any(p => p.Type == StationWeaponPickup.WeaponType.Pistol) ||
                    !pickups.Any(p => p.Type == StationWeaponPickup.WeaponType.Rifle))
                    Problem("station needs at least one pistol and one rifle pickup");
                if (relays.Count(r => r.Id == StationRelay.RelayId.A) != 1 || relays.Count(r => r.Id == StationRelay.RelayId.B) != 1)
                    Problem("station needs Relay A and Relay B");
                if (!SceneComponents<StationEvacuationTerminal>().Any()) Problem("no evacuation terminal");
                if (!SceneComponents<GameSessionController>().Any()) Problem("no GameSessionController");
                if (!SceneComponents<StationProgressionManager>().Any()) Problem("no StationProgressionManager");
            }
            return problems;
        }

        // ---------------- Review screenshots ----------------

        [MenuItem("Last Light/Quality/Render Review Screenshots")]
        public static void BatchScreenshots()
        {
            int exitCode = 0;
            var log = new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(StationScene, OpenSceneMode.Single);
                Physics.SyncTransforms();
                string folder = Path.Combine(ReportFolder, "shots");
                Directory.CreateDirectory(folder);

                var anchors = new List<(string, Transform)>();
                foreach (var p in SceneComponents<StationWeaponPickup>()) anchors.Add(("pickup-" + p.Type, p.transform));
                foreach (var r in SceneComponents<StationRelay>()) anchors.Add(("relay-" + r.Id, r.transform));
                foreach (var m in SceneComponents<StationMedkit>().Take(2)) anchors.Add(("medkit-" + m.name, m.transform));
                foreach (var t in SceneComponents<StationEvacuationTerminal>()) anchors.Add(("evacuation", t.transform));
                foreach (var g in SceneComponents<LastLight.AI.Guard.TacticalGuardAgent>().Take(1)) anchors.Add(("guard", g.transform));
                foreach (var h in SceneComponents<LastLight.AI.Hunter.HunterAgent>().Where(h => h.gameObject.activeInHierarchy).Take(1)) anchors.Add(("hunter", h.transform));
                foreach (var r in SceneComponents<RepairTargetSelector>().Take(2)) anchors.Add(("repair-" + r.name, r.transform));
                foreach (var s in SceneComponents<LastLight.AI.Service.ServiceRobotPatrol>().Take(1)) anchors.Add(("service", s.transform));
                foreach (Transform t in SceneComponents<Transform>())
                {
                    string n = t.name;
                    if (n.Contains("bench") || n.Contains("worktop") || n.Contains("operator desk") || n.Contains("coolant piping") || n.StartsWith("Wall_Cube_01_Thin") || n.StartsWith("Wall_Gear_02_Half"))
                        anchors.Add(("kit-" + n, t));
                }

                Camera reference = SceneComponents<Camera>().FirstOrDefault(c => c.CompareTag("MainCamera"));
                var go = new GameObject("ReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = go.AddComponent<Camera>();
                if (reference != null) { camera.CopyFrom(reference); }
                camera.fieldOfView = 70f;
                camera.nearClipPlane = 0.05f;
                var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
                camera.targetTexture = rt;

                if (reference != null) Shoot(camera, rt, reference.transform.position, reference.transform.rotation, Path.Combine(folder, "00-player-start.png"), log);

                int index = 1;
                foreach (var (label, t) in anchors)
                {
                    Vector3 focus = t.position + Vector3.up * 0.6f;
                    if (TryLocalRendererBounds(t, out Bounds b)) focus = t.TransformPoint(b.center);
                    Vector3 best = Vector3.zero; bool found = false;
                    for (int i = 0; i < 16 && !found; i++)
                    {
                        float angle = i * 45f * Mathf.Deg2Rad;
                        float distance = i < 8 ? 2.6f : 1.6f;
                        Vector3 candidate = focus + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance + Vector3.up * 0.9f;
                        Vector3 toFocus = focus - candidate;
                        if (Physics.CheckSphere(candidate, 0.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Raycast(candidate, toFocus.normalized, out RaycastHit hit, toFocus.magnitude - 0.3f, ~0, QueryTriggerInteraction.Ignore)
                            && !hit.transform.IsChildOf(t)) continue;
                        best = candidate; found = true;
                    }
                    if (!found) { log.AppendLine("no clear viewpoint: " + label); continue; }
                    string file = Path.Combine(folder, $"{index++:00}-{Sanitize(label)}.png");
                    Shoot(camera, rt, best, Quaternion.LookRotation(focus - best), file, log);
                }
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
            catch (Exception e) { log.AppendLine("EXCEPTION " + e); exitCode = 1; }
            File.WriteAllText(Path.Combine(ReportFolder, "screenshots.txt"), log.ToString());
            Debug.Log("Review screenshots written to " + ReportFolder + "\n" + log);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        private static string Sanitize(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

        private static void Shoot(Camera camera, RenderTexture rt, Vector3 position, Quaternion rotation, string file, StringBuilder log)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(file, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            log.AppendLine("saved " + file);
        }
    }
}
