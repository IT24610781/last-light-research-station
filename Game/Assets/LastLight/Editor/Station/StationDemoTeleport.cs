using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Editor-only demo shortcuts (Play mode): stand the player in front of a key location and face it,
    /// so each team member can reach their part of the game quickly during a demonstration.
    /// </summary>
    public static class StationDemoTeleport
    {
        [MenuItem("Last Light/Demo/Go To Armory Bench (weapons)")] private static void Armory() => Go(() => Object.FindObjectsByType<StationWeaponPickup>(FindObjectsSortMode.None).FirstOrDefault()?.transform);
        [MenuItem("Last Light/Demo/Go To Relay A (Power room)")] private static void RelayA() => Go(() => Object.FindObjectsByType<StationRelay>(FindObjectsSortMode.None).FirstOrDefault(r => r.Id == StationRelay.RelayId.A)?.transform);
        [MenuItem("Last Light/Demo/Go To Relay B (Main Lab)")] private static void RelayB() => Go(() => Object.FindObjectsByType<StationRelay>(FindObjectsSortMode.None).FirstOrDefault(r => r.Id == StationRelay.RelayId.B)?.transform);
        [MenuItem("Last Light/Demo/Go To Sound Hunter (Storage)")] private static void Hunter() => Go(() => Object.FindObjectsByType<LastLight.AI.Hunter.HunterAgent>(FindObjectsSortMode.None).FirstOrDefault()?.transform, 6f);
        [MenuItem("Last Light/Demo/Go To Tactical Guard (Security)")] private static void Guard() => Go(() => Object.FindFirstObjectByType<LastLight.AI.Guard.TacticalGuardAgent>()?.transform, 6f);
        [MenuItem("Last Light/Demo/Go To Repair Robot (Workshop)")] private static void Repair() => Go(() => Object.FindObjectsByType<RepairTargetSelector>(FindObjectsSortMode.None).OrderBy(r => r.name).FirstOrDefault()?.transform, 4f);
        [MenuItem("Last Light/Demo/Go To Medical Cases")] private static void Medical() => Go(() => Object.FindObjectsByType<StationMedkit>(FindObjectsSortMode.None).FirstOrDefault(m => m.name.StartsWith("Medical"))?.transform);
        [MenuItem("Last Light/Demo/Go To Throwable Crate (Storage)")] private static void Crate() => Go(() => Object.FindObjectsByType<CarryableObject>(FindObjectsSortMode.None).FirstOrDefault(c => c.name.StartsWith("Storage"))?.transform);
        [MenuItem("Last Light/Demo/Go To Evacuation Console")] private static void Evac() => Go(() => Object.FindFirstObjectByType<StationEvacuationTerminal>()?.transform);

        [MenuItem("Last Light/Demo/Go To Armory Bench (weapons)", true)]
        [MenuItem("Last Light/Demo/Go To Relay A (Power room)", true)]
        [MenuItem("Last Light/Demo/Go To Relay B (Main Lab)", true)]
        [MenuItem("Last Light/Demo/Go To Sound Hunter (Storage)", true)]
        [MenuItem("Last Light/Demo/Go To Tactical Guard (Security)", true)]
        [MenuItem("Last Light/Demo/Go To Repair Robot (Workshop)", true)]
        [MenuItem("Last Light/Demo/Go To Medical Cases", true)]
        [MenuItem("Last Light/Demo/Go To Throwable Crate (Storage)", true)]
        [MenuItem("Last Light/Demo/Go To Evacuation Console", true)]
        private static bool PlayingOnly() => EditorApplication.isPlaying;

        private static void Go(System.Func<Transform> find, float preferredDistance = 1.4f)
        {
            Transform target = find();
            PlayerShooter shooter = Object.FindFirstObjectByType<PlayerShooter>();
            if (target == null || shooter == null) { Debug.LogWarning("Demo teleport: target or player not found"); return; }
            GameObject player = shooter.gameObject;
            Camera cam = player.GetComponentInChildren<Camera>();
            var cc = player.GetComponent<CharacterController>();
            Vector3 focus = target.GetComponentsInChildren<Renderer>().Where(r => r.enabled)
                .Select(r => r.bounds).Aggregate(new Bounds(target.position, Vector3.zero), (a, b) => { a.Encapsulate(b); return a; }).center;

            for (float distance = preferredDistance; distance < preferredDistance + 3f; distance += 0.5f)
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * 22.5f * Mathf.Deg2Rad;
                    Vector3 flat = focus + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (!Physics.Raycast(new Vector3(flat.x, focus.y + 1.2f, flat.z), Vector3.down, out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (floor.transform.IsChildOf(target)) continue;
                    cc.enabled = false;
                    player.transform.position = floor.point + Vector3.up * ((cc.height * 0.5f - cc.center.y) * player.transform.lossyScale.y + 0.05f);
                    cc.enabled = true;
                    Physics.SyncTransforms();
                    Vector3 centre = player.transform.TransformPoint(cc.center);
                    float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius) * player.transform.lossyScale.y;
                    bool blocked = Physics.OverlapCapsule(centre - Vector3.up * (half - 0.12f), centre + Vector3.up * half, cc.radius * 0.85f, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(player.transform));
                    if (blocked) continue;
                    Vector3 toFocus = focus - cam.transform.position;
                    if (Physics.Raycast(cam.transform.position, toFocus.normalized, out RaycastHit los, toFocus.magnitude, ~0, QueryTriggerInteraction.Ignore)
                        && !los.transform.IsChildOf(target) && !los.transform.IsChildOf(player.transform)) continue;
                    player.transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(new Vector3(toFocus.x, 0f, toFocus.z)).eulerAngles.y, 0f);
                    float pitch = -Mathf.Asin(Mathf.Clamp(toFocus.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                    PlayerLook look = player.GetComponent<PlayerLook>();
                    typeof(PlayerLook).GetField("cameraPitch", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(look, pitch);
                    cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                    Debug.Log($"Demo teleport: in front of {target.name}");
                    return;
                }
            Debug.LogWarning("Demo teleport: no clear standing spot near " + target.name);
        }
    }
}
