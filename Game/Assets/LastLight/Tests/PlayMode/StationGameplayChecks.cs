#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Editor-only live check of the ResearchStation mission. It aims the real player camera at each
/// interactable and resolves the hit exactly like PlayerInteractor (centre-screen ray, 3 m, triggers
/// ignored), then drives pickups, medkit, props, doors, relays, evacuation, pause, restart and defeat.
/// Run with: Last Light > Quality > Run Station Gameplay Check.
/// </summary>
public sealed class StationGameplayChecks : MonoBehaviour
{
    public static string Status = "Not run";
    public static int Failures;
    private readonly List<string> results = new List<string>();
    private readonly List<string> errors = new List<string>();
    private string resultPath;
    private GameObject player;
    private Camera cam;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        string folder = Environment.GetEnvironmentVariable("LASTLIGHT_REPORT_DIR");
        if (string.IsNullOrEmpty(folder)) folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/QualityReport"));
        Directory.CreateDirectory(folder);
        resultPath = Path.Combine(folder, "gameplay-check.txt");
        Application.logMessageReceived += OnLog;
        Status = "Running"; Failures = 0;
    }

    private void OnDestroy() => Application.logMessageReceived -= OnLog;

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(type + ": " + condition.Split('\n')[0]);
    }

    private void Check(bool ok, string name)
    {
        results.Add((ok ? "PASS " : "FAIL ") + name);
        if (!ok) Failures++;
        File.WriteAllLines(resultPath, results);
    }

    private void Note(string text) { results.Add("     " + text); File.WriteAllLines(resultPath, results); }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        IEnumerator run = Run();
        while (true)
        {
            object current;
            try
            {
                if (!run.MoveNext()) break;
                current = run.Current;
            }
            catch (Exception e)
            {
                Check(false, "Check aborted by exception: " + e.Message);
                Debug.LogException(e);
                break;
            }
            yield return current;
        }
        Check(errors.Count == 0, $"No errors or exceptions logged during the run ({errors.Count})");
        foreach (string e in errors.Distinct().Take(15)) Note(e);
        results.Add(Failures == 0 ? "RESULT: ALL PASSED" : $"RESULT: {Failures} FAILED");
        results.Add("COMPLETE");
        File.WriteAllLines(resultPath, results);
        Status = "Complete";
    }

    private GameSessionController Session => FindFirstObjectByType<GameSessionController>();

    private void BindPlayer()
    {
        player = FindFirstObjectByType<PlayerShooter>().gameObject;
        cam = player.GetComponentInChildren<Camera>();
        player.GetComponent<PlayerMovement>().enabled = false;
        player.GetComponent<PlayerLook>().enabled = false;
    }

    private void QuietAgents()
    {
        foreach (var g in FindObjectsByType<LastLight.AI.Guard.TacticalGuardAgent>(FindObjectsSortMode.None)) g.SetPaused(true);
        foreach (var h in FindObjectsByType<LastLight.AI.Hunter.HunterAgent>(FindObjectsSortMode.None)) h.SetPaused(true);
        var director = FindFirstObjectByType<LastLight.AI.Director.EncounterDirector>();
        if (director != null) director.SetPaused(true);
    }

    private static Vector3 Focus(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return t.position;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b.center;
    }

    /// <summary>Stand the player near the target and aim the camera at it; succeed only if the
    /// gameplay interaction ray actually hits the target.</summary>
    private bool Aim(Transform target, out RaycastHit hit)
    {
        hit = default;
        Vector3 focus = Focus(target);
        int noFloor = 0, blockedCount = 0, misses = 0; string lastHit = "nothing";
        var cc = player.GetComponent<CharacterController>();
        for (int ring = 0; ring < 3; ring++)
        {
            float distance = 1.2f + ring * 0.6f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                Vector3 flat = focus + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance;
                if (!Physics.Raycast(new Vector3(flat.x, focus.y + 1.2f, flat.z), Vector3.down, out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore))
                { noFloor++; continue; }
                if (floor.transform.IsChildOf(target)) continue;
                Vector3 feet = floor.point;
                cc.enabled = false;
                player.transform.position = feet + Vector3.up * ((cc.height * 0.5f - cc.center.y) * player.transform.lossyScale.y + 0.05f);
                cc.enabled = true;
                Physics.SyncTransforms();
                float scaleY = player.transform.lossyScale.y;
                Vector3 centre = player.transform.TransformPoint(cc.center);
                float half = Mathf.Max(0f, cc.height * 0.5f * scaleY - cc.radius * scaleY);
                Vector3 top = centre + Vector3.up * half;
                Vector3 bottom = centre - Vector3.up * half + Vector3.up * 0.12f;
                Collider blocker = Physics.OverlapCapsule(bottom, top, cc.radius * scaleY * 0.85f, ~0, QueryTriggerInteraction.Ignore)
                    .FirstOrDefault(c => !c.transform.IsChildOf(player.transform));
                if (blocker != null) { blockedCount++; lastHit = "blocked by " + blocker.name; continue; }
                Vector3 dir = focus - cam.transform.position;
                player.transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)).eulerAngles.y, 0f);
                cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position);
                Physics.SyncTransforms();
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    hit.transform.IsChildOf(target))
                    return true;
                misses++;
                lastHit = Physics.Raycast(ray, out RaycastHit other, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? $"{other.collider.name} (layer {LayerMask.LayerToName(other.collider.gameObject.layer)}, {other.distance:0.00} m)" : "nothing within 3 m";
            }
        }
        Note($"aim {target.name}: focus {focus}, noFloor {noFloor}, blocked {blockedCount}, rayMiss {misses}, last ray hit {lastHit}");
        return false;
    }

    private string Prompt()
    {
        MethodInfo method = typeof(GameSessionController).GetMethod("GetInteractionPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
        return method != null ? (string)method.Invoke(Session, null) : "";
    }

    private IEnumerator Run()
    {
        GameSessionController session = Session;
        Check(session != null && session.State == GameSessionController.SessionState.Start, "Game opens on the start screen");
        session.StartGame();
        yield return null;
        Check(session.State == GameSessionController.SessionState.Playing && Time.timeScale > 0f, "ENTER STATION starts play");
        BindPlayer();
        QuietAgents();
        Health hp = player.GetComponent<Health>();
        PlayerShooter shooter = player.GetComponent<PlayerShooter>();
        Check(shooter.CurrentDefinition == null && !shooter.HasPistol && !shooter.HasRifle, "Player starts unarmed (weapons are collected in the Armory)");

        // Rendering: no magenta materials at runtime.
        int magenta = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .SelectMany(r => r.sharedMaterials)
            .Count(m => m == null || m.shader == null || m.shader.name == "Standard" || m.shader.name.StartsWith("Legacy Shaders/") || m.shader.name == "Hidden/InternalErrorShader");
        Check(magenta == 0, $"No unsupported/missing materials on active renderers ({magenta})");

        // Weapons.
        int pickupCount = FindObjectsByType<StationWeaponPickup>(FindObjectsSortMode.None).Length;
        Note($"bench weapons available: {pickupCount}");
        foreach (StationWeaponPickup pickup in FindObjectsByType<StationWeaponPickup>(FindObjectsSortMode.None)
                     .GroupBy(p => p.Type).Select(g => g.First()).OrderBy(p => p.Type))
        {
            bool aimed = Aim(pickup.transform, out RaycastHit hit);
            Check(aimed, $"Interaction ray can hit the {pickup.Type} on the Armory bench");
            if (!aimed) continue;
            string prompt = Prompt();
            Check(prompt.Contains("TAKE " + pickup.Type.ToString().ToUpperInvariant()), $"HUD prompt shows \"{prompt}\"");
            hit.collider.GetComponentInParent<IInteractable>().Interact();
            yield return null;
            bool owned = pickup.Type == StationWeaponPickup.WeaponType.Pistol ? shooter.HasPistol : shooter.HasRifle;
            Check(owned && !pickup.gameObject.activeSelf, $"{pickup.Type} collected and removed from the bench");
            Check(shooter.CurrentDefinition != null && shooter.CurrentAmmo == shooter.CurrentDefinition.magazineCapacity,
                $"{pickup.Type} equipped with a full magazine ({shooter.CurrentAmmo})");
        }

        // Medkit.
        StationMedkit medkit = FindObjectsByType<StationMedkit>(FindObjectsSortMode.None).FirstOrDefault();
        if (medkit != null)
        {
            hp.TakeDamage(40f);
            float before = hp.CurrentHealth;
            bool aimed = Aim(medkit.transform, out RaycastHit hit);
            Check(aimed && Prompt().Contains("MEDKIT"), "Medkit is targetable with a USE MEDKIT prompt");
            if (aimed) hit.collider.GetComponentInParent<IInteractable>().Interact();
            yield return null;
            Check(hp.CurrentHealth > before, $"Medkit heals the player ({before} -> {hp.CurrentHealth})");
            hp.Heal(1000f);
        }

        // Physics props.
        CarryableObject prop = FindObjectsByType<CarryableObject>(FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.activeInHierarchy);
        PlayerGrabber grabber = player.GetComponent<PlayerGrabber>();
        if (prop != null)
        {
            bool aimed = Aim(prop.transform, out RaycastHit hit);
            Check(aimed, $"Prop '{prop.name}' is targetable");
            if (aimed)
            {
                grabber.TryGrab(hit.collider.GetComponentInParent<CarryableObject>());
                yield return new WaitForSecondsRealtime(0.2f);
                Check(grabber.IsHolding, "Player picks up the prop (E)");
                grabber.Throw();
                yield return new WaitForFixedUpdate();
                Check(!grabber.IsHolding && prop.Body != null && prop.Body.linearVelocity.magnitude > 1f, $"Prop is thrown (Q) at {prop.Body.linearVelocity.magnitude:0.0} m/s");
            }
        }

        // Doors and relays.
        StationProgressionManager progression = StationProgressionManager.Instance;
        StationSlidingDoor[] doors = FindObjectsByType<StationSlidingDoor>(FindObjectsSortMode.None);
        StationSlidingDoor yellow = doors.FirstOrDefault(d => d.Requirement == StationSlidingDoor.AccessRequirement.RelayA);
        StationSlidingDoor red = doors.FirstOrDefault(d => d.Requirement == StationSlidingDoor.AccessRequirement.BothRelays);
        StationSlidingDoor plain = doors.FirstOrDefault(d => d.Requirement == StationSlidingDoor.AccessRequirement.None);
        Note($"doors: {doors.Length}, RelayA-locked: {doors.Count(d => d.Requirement == StationSlidingDoor.AccessRequirement.RelayA)}, both-relays-locked: {doors.Count(d => d.Requirement == StationSlidingDoor.AccessRequirement.BothRelays)}");
        if (plain != null)
        {
            bool wasOpen = plain.IsOpen;
            if (Aim(plain.transform, out RaycastHit hit)) hit.collider.GetComponentInParent<IInteractable>().Interact(); else plain.Interact();
            Check(plain.IsOpen != wasOpen, "Unlocked door opens/closes with E");
        }
        if (yellow != null)
        {
            yellow.Interact();
            Check(!yellow.IsOpen, "Yellow door stays locked before Relay A");
        }
        if (red != null)
        {
            red.Interact();
            Check(!red.IsOpen, "Red evacuation door stays locked before both relays");
        }

        StationEvacuationTerminal terminal = FindFirstObjectByType<StationEvacuationTerminal>();
        Check(terminal != null && !progression.TryEvacuate() && session.State == GameSessionController.SessionState.Playing,
            "Evacuation refused while relays are offline");

        foreach (StationRelay relay in FindObjectsByType<StationRelay>(FindObjectsSortMode.None).OrderBy(r => r.Id))
        {
            bool aimed = Aim(relay.transform, out RaycastHit hit);
            Check(aimed && hit.collider.GetComponentInParent<StationRelay>() == relay, $"Relay {relay.Id} is targetable");
            Check(Prompt().Contains("HOLD [E]"), $"Relay {relay.Id} shows the hold prompt");
            relay.AdvanceHold(0.5f);
            relay.CancelHold();
            Check(!relay.Activated && relay.HoldProgress == 0f, $"Releasing E early resets Relay {relay.Id}");
            float t = 0f;
            while (t < 2.2f) { relay.AdvanceHold(Time.deltaTime); t += Time.deltaTime; yield return null; }
            Check(relay.Activated, $"Holding E for 2 s activates Relay {relay.Id}");
            relay.AdvanceHold(1f);
            Check(progression.RelaysOnline == (relay.Id == StationRelay.RelayId.A ? 1 : 2), $"Relays online: {progression.RelaysOnline}/2 (duplicate hold ignored)");
            if (relay.Id == StationRelay.RelayId.A && yellow != null)
            {
                yellow.Interact();
                Check(yellow.IsOpen, "Relay A unlocks the yellow door");
                if (red != null) { red.Interact(); Check(!red.IsOpen, "Red door still locked with one relay"); }
            }
        }
        if (red != null) { red.Interact(); Check(red.IsOpen, "Both relays unlock the red evacuation door"); }

        if (terminal != null)
        {
            bool aimed = Aim(terminal.transform, out RaycastHit hit);
            Check(aimed && Prompt().Contains("EVACUATION"), "Evacuation terminal is targetable");
            if (aimed) hit.collider.GetComponentInParent<IInteractable>().Interact(); else terminal.Interact();
            yield return null;
            Check(session.State == GameSessionController.SessionState.Won, "Evacuating alive with both relays wins");
        }

        // Restart resets the whole level.
        session.RestartGame();
        yield return new WaitForSecondsRealtime(1.5f);
        session = Session;
        BindPlayer();
        shooter = player.GetComponent<PlayerShooter>();
        Check(session.State == GameSessionController.SessionState.Start, "Restart returns to the start screen");
        Check(shooter.CurrentDefinition == null && FindObjectsByType<StationWeaponPickup>(FindObjectsSortMode.None).Length == pickupCount, $"Restart restores unarmed player and all {pickupCount} bench weapons");
        Check(StationProgressionManager.Instance.RelaysOnline == 0, "Restart resets both relays");

        // Exit trigger: walking into the evacuation bay with both relays online wins (plan, Stage 8).
        session.StartGame();
        yield return null;
        QuietAgents();
        foreach (StationRelay relay in FindObjectsByType<StationRelay>(FindObjectsSortMode.None))
        {
            float held = 0f;
            while (held < 2.2f) { relay.AdvanceHold(Time.deltaTime); held += Time.deltaTime; yield return null; }
        }
        StationEvacuationZone zone = FindFirstObjectByType<StationEvacuationZone>();
        Check(zone != null, "Evacuation bay has an exit trigger");
        if (zone != null)
        {
            BoxCollider area = zone.GetComponent<BoxCollider>();
            Vector3 inside = area.bounds.center;
            Vector3 outside = new Vector3(area.bounds.min.x - 0.6f, area.bounds.min.y + 0.1f, inside.z);
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = outside + Vector3.up * (cc.height * 0.5f - cc.center.y);
            cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 40 && session.State == GameSessionController.SessionState.Playing; i++)
            {
                cc.Move(Vector3.right * 0.1f + Vector3.down * 0.05f);
                yield return new WaitForFixedUpdate();
            }
            Check(session.State == GameSessionController.SessionState.Won, "Walking into the evacuation bay with both relays wins");
        }
        session.RestartGame();
        yield return new WaitForSecondsRealtime(1.5f);
        session = Session;
        BindPlayer();

        // Pause and defeat.
        session.StartGame();
        yield return null;
        session.PauseGame();
        Check(session.State == GameSessionController.SessionState.Paused && Time.timeScale == 0f, "Escape pauses and freezes time");
        session.ResumeGame();
        yield return null;
        Check(session.State == GameSessionController.SessionState.Playing && Time.timeScale > 0f, "Resume continues play");

        // Let the full station run with every agent active for a few seconds.
        player.GetComponent<PlayerMovement>().enabled = true;
        player.GetComponent<PlayerLook>().enabled = true;
        // Member B demo: a thrown/dropped prop makes noise and the Sound Hunter investigates it.
        var hunter = FindObjectsByType<LastLight.AI.Hunter.HunterAgent>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.gameObject.activeInHierarchy);
        CarryableObject crate = FindObjectsByType<CarryableObject>(FindObjectsSortMode.None)
            .Where(c => c.name.Contains("crate")).OrderBy(c => hunter != null ? Vector3.Distance(c.transform.position, hunter.transform.position) : 0f).FirstOrDefault();
        if (hunter != null && crate != null)
        {
            yield return new WaitForSecondsRealtime(1f);
            var stateBefore = hunter.Brain.State;
            crate.Body.position = crate.transform.position + Vector3.up * 1.4f;
            crate.Body.linearVelocity = Vector3.down * 5f;
            Vector3 cratePos = crate.transform.position;
            bool heard = false;
            float waited = 0f;
            while (waited < 4f && !heard)
            {
                var target = hunter.Brain.TargetNoise;
                heard = hunter.Brain.State == LastLight.AI.Hunter.HunterState.Investigate && target != null &&
                        Vector3.Distance(new Vector3(target.Position.x, 0f, target.Position.z), new Vector3(cratePos.x, 0f, cratePos.z)) < 2.5f;
                waited += Time.unscaledDeltaTime; yield return null;
            }
            Check(stateBefore == LastLight.AI.Hunter.HunterState.Patrol, $"Sound Hunter is patrolling before the noise (no fake start-up noise): {stateBefore}");
            Check(heard, $"Sound Hunter hears the dropped crate and investigates its position ({hunter.Brain.State}; {hunter.Brain.Reason})");
        }
        yield return new WaitForSecondsRealtime(4f);
        Check(session.State == GameSessionController.SessionState.Playing, "Station runs 6 s with every agent active and the player safe in Arrival");

        player.GetComponent<Health>().TakeDamage(1000f);
        yield return null;
        Check(session.State == GameSessionController.SessionState.Lost, "Zero health shows defeat");
        Check(!StationProgressionManager.Instance.TryEvacuate(), "Exit cannot award victory after death");
    }
}
#endif
