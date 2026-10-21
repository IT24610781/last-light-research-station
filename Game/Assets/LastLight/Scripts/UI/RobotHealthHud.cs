using System.Collections.Generic;
using UnityEngine;

/// <summary>Small screen-space health markers for robots the player can actually see.</summary>
public sealed class RobotHealthHud : MonoBehaviour
{
    private sealed class Marker
    {
        public Health Health;
        public RectTransform Root;
        public UnityEngine.UI.Image Fill;
        public UnityEngine.UI.Text Label;
        public float Height;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private Camera viewCamera;
    private RectTransform canvasRect;

    public void Initialize(Camera camera)
    {
        viewCamera = camera;
        canvasRect = transform as RectTransform;
    }

    private void Start()
    {
        foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (health.CompareTag("Player")) continue;
            if (health.GetComponent<LastLight.AI.Movement.AgentMotor>() == null) continue;
            CreateMarker(health);
        }
    }

    private void LateUpdate()
    {
        if (viewCamera == null || canvasRect == null) return;
        foreach (Marker marker in markers)
        {
            if (marker.Health == null || marker.Health.IsDead ||
                !marker.Health.gameObject.activeInHierarchy)
            {
                marker.Root.gameObject.SetActive(false);
                continue;
            }

            Vector3 target = marker.Health.transform.position + Vector3.up * marker.Height;
            Vector3 screen = viewCamera.WorldToScreenPoint(target);
            Vector3 fromCamera = target - viewCamera.transform.position;
            bool visible = screen.z > 0f &&
                screen.x >= 40f && screen.x <= Screen.width - 40f &&
                screen.y >= 40f && screen.y <= Screen.height - 40f &&
                fromCamera.sqrMagnitude < 20f * 20f;
            if (visible && Physics.Raycast(viewCamera.transform.position,
                    fromCamera.normalized, out RaycastHit hit, fromCamera.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                visible = hit.transform == marker.Health.transform ||
                          hit.transform.IsChildOf(marker.Health.transform);

            marker.Root.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, null, out Vector2 localPoint);
            marker.Root.anchoredPosition = localPoint;
            float fraction = marker.Health.MaximumHealth > 0f
                ? Mathf.Clamp01(marker.Health.CurrentHealth / marker.Health.MaximumHealth) : 0f;
            marker.Fill.rectTransform.sizeDelta = new Vector2(148f * fraction, 8f);
            marker.Fill.color = fraction > 0.35f
                ? new Color(0.2f, 0.92f, 0.68f) : new Color(1f, 0.34f, 0.24f);
            string displayName = marker.Health.name.Contains("Repair") ? "REPAIR ROBOT" :
                marker.Health.name.Contains("Hunter") ? "SOUND HUNTER" :
                marker.Health.name.Contains("Guard") ? "TACTICAL GUARD" : "SERVICE ROBOT";
            marker.Label.text = displayName + "  " +
                Mathf.CeilToInt(marker.Health.CurrentHealth) + "/" +
                Mathf.CeilToInt(marker.Health.MaximumHealth);
        }
    }

    private void CreateMarker(Health health)
    {
        GameObject root = new GameObject(health.name + " Health", typeof(RectTransform),
            typeof(UnityEngine.UI.Image));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(164f, 39f);
        UnityEngine.UI.Image background = root.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.015f, 0.03f, 0.045f, 0.88f);
        background.raycastTarget = false;

        GameObject labelObject = new GameObject("Name and health", typeof(RectTransform),
            typeof(UnityEngine.UI.Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = labelRect.anchorMax = Vector2.zero;
        labelRect.pivot = Vector2.zero;
        labelRect.anchoredPosition = new Vector2(8f, 15f);
        labelRect.sizeDelta = new Vector2(150f, 20f);
        UnityEngine.UI.Text label = labelObject.GetComponent<UnityEngine.UI.Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 12;
        label.color = new Color(0.9f, 1f, 0.98f);
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;

        GameObject fillObject = new GameObject("Health fill", typeof(RectTransform),
            typeof(UnityEngine.UI.Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(rect, false);
        fillRect.anchorMin = fillRect.anchorMax = Vector2.zero;
        fillRect.pivot = Vector2.zero;
        fillRect.anchoredPosition = new Vector2(8f, 5f);
        fillRect.sizeDelta = new Vector2(148f, 8f);
        UnityEngine.UI.Image fill = fillObject.GetComponent<UnityEngine.UI.Image>();
        fill.raycastTarget = false;

        float height = 0.4f;
        foreach (Renderer renderer in health.GetComponentsInChildren<Renderer>(true))
            if (renderer.enabled && renderer.bounds.size.y < 6f)
                height = Mathf.Max(height, renderer.bounds.max.y - health.transform.position.y + 0.15f);
        markers.Add(new Marker { Health = health, Root = rect, Fill = fill,
            Label = label, Height = Mathf.Clamp(height, 0.4f, 2.5f) });
    }
}
