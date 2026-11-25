using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Second dressing pass (idempotent): Main Lab console bank matching the Power room, ground-floor
    /// Repair Robot, Service robot grounding, larger barricade, room name plates above doors, HUD map.
    /// </summary>
    public static class StationDressingRound2
    {
        private const float ServiceVisualLift = 0.62f; // measured in Play mode: lowest vertex was 0.567 m below the floor
        private const string MatFolder = "Assets/LastLight/Materials/ResearchStation";
        private static StringBuilder log;

        [MenuItem("Last Light/Quality/Apply Round 2 Dressing (open scene)")]
        public static void Apply()
        {
            log = new StringBuilder();
            Physics.SyncTransforms();
            RelayBConsole();
            GroundFloorRepairRobot();
            ServiceRobotGrounding();
            BiggerBarricade();
            DoorSigns();
            HudMap();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(StationQualityTools.ReportFolder, "dressing-round2.txt"), log.ToString());
            Debug.Log(log.ToString());
        }

        private static IEnumerable<T> All<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
        private static Transform Find(string name) => All<Transform>().FirstOrDefault(t => t.name == name);
        private static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is LineRenderer) && !(r is ParticleSystemRenderer)).ToArray();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------------- Relay B console bank ----------------

        private static void RelayBConsole()
        {
            if (Find("Relay B control bank") is Transform existing)
            {
                MirrorPowerRoom(existing, Find("Main Lab conduits"));
                return;
            }
            Transform bankA = Find("Relay A control bank");
            Transform conduitA = Find("Power conduits");
            StationRelay relayB = All<StationRelay>().FirstOrDefault(r => r.Id == StationRelay.RelayId.B);
            if (bankA == null || relayB == null) { log.AppendLine("Relay A bank or Relay B missing"); return; }

            Transform old = Find("Relay B laboratory controls");
            if (old != null) { Undo.RecordObject(old.gameObject, "Hide old bank"); old.gameObject.SetActive(false); old.name += " (replaced by Relay B control bank)"; }

            var bank = Object.Instantiate(bankA.gameObject, bankA.parent.parent.Find("Floor 2 — Upper") ?? bankA.parent);
            Undo.RegisterCreatedObjectUndo(bank, "Relay B bank");
            bank.name = "Relay B control bank";
            bank.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            bank.transform.localScale = bankA.lossyScale;
            bank.transform.position = new Vector3(13f, bankA.position.y + 4f, 6f);
            Physics.SyncTransforms();

            // East wall face behind Relay B.
            float wallX = 14.6f;
            foreach (var hit in Physics.RaycastAll(new Vector3(12.4f, 5.6f, 6f), Vector3.right, 4f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (!hit.transform.IsChildOf(bank.transform) && !hit.transform.IsChildOf(relayB.transform) && hit.collider.bounds.size.y > 2f) { wallX = hit.point.x; break; }
            Bounds b = BoundsOf(bank.transform);
            bank.transform.position += new Vector3(wallX - 0.02f - b.max.x, 0f, relayB.transform.position.z - b.center.z);
            Physics.SyncTransforms();
            b = BoundsOf(bank.transform);
            log.AppendLine($"Relay B control bank placed against wall x {wallX:0.00}: bounds {b.center} size {b.size}");

            if (conduitA != null && Find("Main Lab conduits") == null)
            {
                Bounds ca = BoundsOf(conduitA), ba = BoundsOf(bankA);
                var conduit = Object.Instantiate(conduitA.gameObject, bank.transform.parent);
                Undo.RegisterCreatedObjectUndo(conduit, "Main Lab conduits");
                conduit.name = "Main Lab conduits";
                conduit.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * conduitA.rotation;
                Physics.SyncTransforms();
                Bounds cb = BoundsOf(conduit.transform);
                // Mirror the Power room offset (pipe relative to bank) across the room.
                Vector3 offset = ca.center - ba.center;
                Vector3 target = b.center + new Vector3(-offset.x, offset.y, -offset.z);
                conduit.transform.position += target - cb.center;
                log.AppendLine($"Main Lab conduits placed at {BoundsOf(conduit.transform).center}");
            }

            MirrorPowerRoom(bank.transform, Find("Main Lab conduits"));
            b = BoundsOf(bank.transform);
            foreach (var r in All<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(bank.transform)))
            {
                if (!r.bounds.Intersects(new Bounds(b.center, b.size * 0.9f))) continue;
                string n = r.name.ToLower();
                if (n.Contains("floor") || n.Contains("roof") || n.Contains("wall") || n.Contains("cladding") || r.bounds.size.x > 6f || r.bounds.size.z > 6f) continue;
                log.AppendLine("   overlaps bank: " + StationQualityTools.PathOf(r.transform) + " bounds " + r.bounds.center + " " + r.bounds.size);
            }
        }

        /// <summary>Place the Main Lab bank and pipes as the mirror image (across x = 0) of the Power room set.</summary>
        private static void MirrorPowerRoom(Transform bank, Transform conduit)
        {
            Transform bankA = Find("Relay A control bank");
            Transform conduitA = Find("Power conduits");
            StationRelay relayB = All<StationRelay>().FirstOrDefault(r => r.Id == StationRelay.RelayId.B);
            Bounds ba = BoundsOf(bankA), bb = BoundsOf(bank);
            Undo.RecordObject(bank, "Mirror bank");
            bank.position += new Vector3(-ba.center.x - bb.center.x, ba.center.y + 4f - bb.center.y, relayB.transform.position.z - bb.center.z);
            Physics.SyncTransforms();
            bb = BoundsOf(bank);
            log.AppendLine($"Relay B control bank mirrored from Power room: bounds {bb.center} size {bb.size}");
            if (conduit != null && conduitA != null)
            {
                Bounds ca = BoundsOf(conduitA), cb = BoundsOf(conduit);
                Vector3 target = new Vector3(-ca.center.x, ca.center.y + 4f, bb.center.z - (ca.center.z - ba.center.z));
                Undo.RecordObject(conduit, "Mirror conduits");
                conduit.position += target - cb.center;
                Physics.SyncTransforms();
                log.AppendLine($"Main Lab conduits mirrored: {BoundsOf(conduit).center}");
            }
        }

        // ---------------- Repair robot on ground floor ----------------

        private static Vector3? Snap(Vector3 wanted, float clearance)
        {
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return null;
            if (Physics.CheckSphere(hit.position + Vector3.up * 0.7f, clearance, ~0, QueryTriggerInteraction.Ignore)) return null;
            return hit.position;
        }

        private static void GroundFloorRepairRobot()
        {
            Transform robot = Find("Security Repair Robot") ?? Find("Storage Repair Robot");
            Transform supports = Find("Security repair support positions") ?? Find("Storage repair support positions");
            if (robot == null || supports == null) { log.AppendLine("Security Repair Robot or its support positions not found"); return; }
            if (robot.position.y < 2f) { log.AppendLine("Repair robot already on the ground floor"); return; }
            float rootOffset = robot.position.y - 4f; // root height above floor upstairs
            var cc = robot.GetComponent<CharacterController>();
            bool ccWas = cc != null && cc.enabled;
            if (cc) cc.enabled = false;
            Physics.SyncTransforms();

            var spawn = new[] { new Vector3(-11.5f, 0f, 9.8f), new Vector3(-10.5f, 0f, 10.6f), new Vector3(-12.5f, 0f, 8.5f) }
                .Select(p => Snap(p, 0.45f)).FirstOrDefault(p => p.HasValue);
            if (!spawn.HasValue) { log.AppendLine("No clear NavMesh spot for repair robot in Storage"); if (cc) cc.enabled = ccWas; return; }
            Undo.RecordObject(robot, "Move repair robot");
            robot.position = spawn.Value + Vector3.up * rootOffset;
            robot.name = "Storage Repair Robot";
            if (cc) cc.enabled = ccWas;

            var candidates = new[] { new Vector3(-12.6f, 0f, 10.8f), new Vector3(-9.6f, 0f, 11.0f), new Vector3(-12.8f, 0f, 6.6f),
                                     new Vector3(-4.2f, 0f, 4.8f), new Vector3(-9.0f, 0f, 4.8f), new Vector3(-2.8f, 0f, 10.9f) };
            var chosen = new List<Vector3>();
            foreach (var c in candidates)
            {
                var s = Snap(c, 0.4f);
                if (s.HasValue && chosen.All(o => Vector3.Distance(o, s.Value) > 2f)) chosen.Add(s.Value);
                if (chosen.Count == supports.childCount) break;
            }
            Undo.RecordObject(supports, "Move support positions");
            supports.name = "Storage repair support positions";
            int i = 0;
            foreach (Transform child in supports)
            {
                if (i >= chosen.Count) break;
                Undo.RecordObject(child, "Move support");
                child.position = chosen[i] + Vector3.up * 0.18f;
                child.name = "Storage support " + (i + 1);
                i++;
            }
            log.AppendLine($"Repair robot moved to Storage at {robot.position}; support positions: {string.Join(", ", chosen.Select(c => c.ToString()))}");
        }

        // ---------------- Service robot ----------------

        private static void ServiceRobotGrounding()
        {
            foreach (var service in All<LastLight.AI.Service.ServiceRobotPatrol>())
            {
                if (service.transform.Find("Service Robot Visual") is Transform visual)
                {
                    if (visual.localPosition.y > 0.3f) { log.AppendLine("Service robot visual already raised"); continue; }
                    Undo.RecordObject(visual, "Raise service visual");
                    visual.localPosition += Vector3.up * ServiceVisualLift;
                    if (service.transform.Find("Service manipulator") is Transform arm)
                    {
                        Undo.RecordObject(arm, "Raise service arm");
                        arm.localPosition += Vector3.up * ServiceVisualLift;
                    }
                    log.AppendLine($"Service robot visual raised by {ServiceVisualLift} m");
                }
            }
        }

        // ---------------- Barricade ----------------

        private static void BiggerBarricade()
        {
            var barricade = All<CarryableObject>().FirstOrDefault(c => c.name.Contains("Barricade"));
            if (barricade == null) return;
            if (barricade.transform.localScale.x > 1.2f) { log.AppendLine("Barricade already enlarged"); return; }
            Undo.RecordObject(barricade.transform, "Enlarge barricade");
            barricade.transform.localScale = Vector3.one * 1.35f;
            var body = barricade.GetComponent<Rigidbody>();
            Undo.RecordObject(body, "Barricade mass");
            body.mass = 30f;
            Physics.SyncTransforms();
            Bounds b = BoundsOf(barricade.transform);
            barricade.transform.position += Vector3.up * (0.01f - b.min.y);
            Physics.SyncTransforms();
            var box = barricade.GetComponent<BoxCollider>();
            bool overlaps = Physics.OverlapBox(box.bounds.center, box.bounds.extents * 0.95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => !c.transform.IsChildOf(barricade.transform) && c.bounds.size.y > 0.2f && !c.name.Contains("Floor"));
            log.AppendLine($"Barricade scaled to 1.35 (size {BoundsOf(barricade.transform).size}), mass 30, overlap {overlaps}");
        }

        // ---------------- Door signs ----------------

        private static readonly Dictionary<string, string> RoomNames = new Dictionary<string, string>
        {
            { "01", "01  ARRIVAL" }, { "02", "02  ARMORY" }, { "03", "03  MEDICAL" }, { "04", "04  POWER  ·  RELAY A" },
            { "05", "05  STORAGE" }, { "06", "06  MAIN LAB  ·  RELAY B" }, { "07", "07  ROBOT WORKSHOP" },
            { "08", "08  SECURITY" }, { "09", "09  CREW QUARTERS" }, { "10", "10  EVACUATION" },
        };

        private static Material EmissiveMat(string name, Color color)
        {
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color * 0.35f);
            m.SetColor("_EmissionColor", color * 2.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Smoothness", 0.4f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void DoorSigns()
        {
            Transform parent = Find("Room name plates (above doors)");
            if (parent != null) Undo.DestroyObjectImmediate(parent.gameObject); // rebuild with current settings
            var root = new GameObject("Room name plates (above doors)");
            Undo.RegisterCreatedObjectUndo(root, "Room plates");
            Transform station = Find("Research Station — two floors");
            if (station != null) root.transform.SetParent(station, false);

            TextMesh template = All<TextMesh>().FirstOrDefault(t => t.name.Contains("corridor sign")) ?? All<TextMesh>().FirstOrDefault();
            Material backing = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/Station_DressedGraphite.mat");
            var cyan = new Color(0.30f, 0.83f, 0.90f);
            var amber = new Color(1.00f, 0.70f, 0.18f);
            var red = new Color(1.00f, 0.22f, 0.16f);
            Material stripCyan = EmissiveMat("Station_SignStrip_Cyan", cyan);
            Material stripAmber = EmissiveMat("Station_SignStrip_Amber", amber);
            Material stripRed = EmissiveMat("Station_SignStrip_Red", red);

            foreach (StationSlidingDoor door in All<StationSlidingDoor>())
            {
                string key = door.name.Substring(0, 2);
                if (!RoomNames.TryGetValue(key, out string label)) continue;
                Vector3 d = door.transform.position;
                float floorY = d.y;
                float towardCorridor = d.x < 0f ? 1f : -1f;
                // Mount on the corridor face of the door-frame arch (or the wall if no frame is found).
                Vector3 probe = new Vector3(d.x, floorY + 2.55f, d.z);
                var frames = All<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && r.name.Contains("DoorWay")
                                                         && r.bounds.SqrDistance(probe) < 0.25f).ToList();
                float faceX = d.x + towardCorridor * 0.12f;
                if (frames.Count > 0)
                    faceX = towardCorridor > 0f ? frames.Max(r => r.bounds.max.x) : frames.Min(r => r.bounds.min.x);
                else
                    foreach (var hit in Physics.RaycastAll(new Vector3(0f, probe.y, d.z), new Vector3(-towardCorridor, 0f, 0f), 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    { faceX = hit.point.x; break; }

                Color color = door.Requirement == StationSlidingDoor.AccessRequirement.RelayA ? amber
                    : door.Requirement == StationSlidingDoor.AccessRequirement.BothRelays ? red : cyan;
                Material strip = color == amber ? stripAmber : color == red ? stripRed : stripCyan;

                var plate = new GameObject("Room plate — " + label.Replace("  ", " "));
                plate.transform.SetParent(root.transform, false);
                plate.transform.position = new Vector3(faceX + towardCorridor * 0.03f, floorY + 2.5f, d.z);
                // Text faces the corridor (forward points into the wall).
                plate.transform.rotation = Quaternion.LookRotation(new Vector3(-towardCorridor, 0f, 0f), Vector3.up);

                var textGo = new GameObject("Name");
                textGo.transform.SetParent(plate.transform, false);
                textGo.transform.localPosition = new Vector3(0f, 0f, -0.035f);
                var text = textGo.AddComponent<TextMesh>();
                text.text = label;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.characterSize = label.Length > 16 ? 0.021f : 0.0235f; // ~18 cm capitals
                text.fontStyle = FontStyle.Bold;
                text.color = Color.Lerp(color, Color.white, 0.25f);
                if (template != null)
                {
                    text.font = template.font;
                    textGo.GetComponent<MeshRenderer>().sharedMaterial = template.GetComponent<MeshRenderer>().sharedMaterial;
                }
                Bounds tb = textGo.GetComponent<MeshRenderer>().bounds;
                float measured = Mathf.Max(tb.size.x, tb.size.z);
                float width = measured > 0.05f ? Mathf.Clamp(measured + 0.4f, 1.0f, 2.15f)
                                               : Mathf.Clamp(label.Length * 0.07f + 0.4f, 1.0f, 2.1f);

                PlatePart(plate.transform, "Backing", new Vector3(0f, 0f, 0f), new Vector3(width, 0.38f, 0.03f), backing);
                PlatePart(plate.transform, "Top light strip", new Vector3(0f, 0.18f, -0.02f), new Vector3(width, 0.022f, 0.012f), strip);
                PlatePart(plate.transform, "Bottom light strip", new Vector3(0f, -0.18f, -0.02f), new Vector3(width, 0.022f, 0.012f), strip);
                log.AppendLine($"Plate '{label}' at {plate.transform.position} width {width:0.00} (frame found: {frames.Count > 0}) text {textGo.GetComponent<MeshRenderer>().bounds.size}");
            }
        }

        private static void PlatePart(Transform parent, string name, Vector3 local, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------- HUD map ----------------

        private static void HudMap()
        {
            if (All<StationMinimap>().Any()) { log.AppendLine("HUD map already present"); return; }
            var go = new GameObject("Station Map (HUD)");
            Undo.RegisterCreatedObjectUndo(go, "HUD map");
            go.AddComponent<StationMinimap>();
            log.AppendLine("HUD map added");
        }
    }
}
