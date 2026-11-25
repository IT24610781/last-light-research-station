using System.Collections.Generic;
using LastLight.AI.Guard;
using LastLight.AI.Hunter;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD map of the floor the player is on. Shows the player's position and facing, relay and exit
/// state, and a robot only while it is firing/attacking (fades 2.5 s after its last shot).
/// It listens to the agents' existing AttackPerformed events, so it does not change AI logic.
/// M toggles a larger view. Hidden on start, pause and end screens.
/// </summary>
public sealed class StationMinimap : MonoBehaviour
{
    [SerializeField] private float firingMarkerSeconds = 2.5f;
    [SerializeField] private Vector2 smallSize = new Vector2(230f, 299f);
    [SerializeField] private Vector2 largeSize = new Vector2(520f, 676f);

    private sealed class Shooter
    {
        public Transform Robot;
        public float LastShot = -999f;
        public Image Marker;
        public Text Floor;
    }

    private readonly List<Shooter> shooters = new List<Shooter>();
    private readonly List<(StationMapLayout.Room room, Image fill, Text label)> groundCells = new List<(StationMapLayout.Room, Image, Text)>();
    private readonly List<(StationMapLayout.Room room, Image fill, Text label)> upperCells = new List<(StationMapLayout.Room, Image, Text)>();
    private RectTransform frame, mapArea, groundLayer, upperLayer;
    private Text floorTitle, relayA, relayB, exitMark, legend;
    private RectTransform playerArrow;
    private GameSessionController session;
    private Transform player;
    private bool large;
    private Font font;
    private static Sprite triangle, disc;

    public int VisibleFiringMarkers { get; private set; }

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        session = FindFirstObjectByType<GameSessionController>();
        PlayerShooter shooter = FindFirstObjectByType<PlayerShooter>();
        if (shooter != null) player = shooter.transform;
        Build();
        foreach (TacticalGuardAgent guard in FindObjectsByType<TacticalGuardAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Shooter entry = AddShooter(guard.transform);
            guard.AttackPerformed += (agent, target) => entry.LastShot = Time.time;
        }
        foreach (HunterAgent hunter in FindObjectsByType<HunterAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Shooter entry = AddShooter(hunter.transform);
            hunter.AttackPerformed += (agent, target) => entry.LastShot = Time.time;
        }
    }

    private void Update()
    {
        bool playing = session == null || session.State == GameSessionController.SessionState.Playing;
        frame.gameObject.SetActive(playing && player != null);
        if (!playing || player == null) return;

        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            large = !large;
            ApplyLayout();
        }

        bool upper = StationMapLayout.IsUpper(player.position);
        groundLayer.gameObject.SetActive(!upper);
        upperLayer.gameObject.SetActive(upper);
        floorTitle.text = upper ? "FLOOR 2 — UPPER" : "FLOOR 1 — GROUND";

        string current = StationMapLayout.RoomAt(player.position);
        foreach (var (room, fill, label) in upper ? upperCells : groundCells)
        {
            bool here = room.Label == current || (room.Corridor && current == string.Empty && room.Area.Contains(new Vector2(player.position.x, player.position.z)));
            fill.color = here ? new Color(0.16f, 0.42f, 0.46f, 0.95f) : room.Corridor ? new Color(0.1f, 0.13f, 0.16f, 0.9f) : new Color(0.07f, 0.09f, 0.11f, 0.92f);
            if (label != null) label.color = here ? Color.white : new Color(0.62f, 0.78f, 0.82f, 1f);
        }

        playerArrow.anchoredPosition = ToMap(player.position);
        playerArrow.localEulerAngles = new Vector3(0f, 0f, -player.eulerAngles.y);

        StationProgressionManager progression = StationProgressionManager.Instance;
        bool a = progression != null && progression.RelayAOnline;
        bool b = progression != null && progression.RelayBOnline;
        relayA.gameObject.SetActive(!upper);
        relayB.gameObject.SetActive(upper);
        exitMark.gameObject.SetActive(!upper);
        relayA.color = a ? new Color(0.3f, 1f, 0.55f) : new Color(1f, 0.35f, 0.3f);
        relayB.color = b ? new Color(0.3f, 1f, 0.55f) : new Color(1f, 0.35f, 0.3f);
        exitMark.color = a && b ? new Color(0.3f, 1f, 0.55f) : new Color(1f, 0.35f, 0.3f);

        int visible = 0;
        foreach (Shooter s in shooters)
        {
            float since = Time.time - s.LastShot;
            bool firing = s.Robot != null && s.Robot.gameObject.activeInHierarchy && since <= firingMarkerSeconds;
            s.Marker.gameObject.SetActive(firing);
            if (!firing) continue;
            visible++;
            bool sameFloor = StationMapLayout.IsUpper(s.Robot.position) == upper;
            s.Marker.rectTransform.anchoredPosition = ToMap(s.Robot.position);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 12f);
            float fade = 1f - since / firingMarkerSeconds * 0.6f;
            s.Marker.color = new Color(1f, 0.18f, 0.12f, (sameFloor ? 1f : 0.45f) * fade);
            s.Marker.rectTransform.localScale = Vector3.one * (sameFloor ? pulse : 0.7f);
            s.Floor.text = sameFloor ? string.Empty : (StationMapLayout.IsUpper(s.Robot.position) ? "F2" : "F1");
        }
        VisibleFiringMarkers = visible;
        legend.text = visible > 0 ? "<color=#FF4A3A>●</color> FIRING" : "M  MAP";
    }

    // ---------------- Building ----------------

    private Vector2 ToMap(Vector3 world)
    {
        Rect e = StationMapLayout.Extent;
        Vector2 size = mapArea.rect.size;
        return new Vector2((world.x - e.xMin) / e.width * size.x, (world.z - e.yMin) / e.height * size.y);
    }

    private void Build()
    {
        var canvasObject = new GameObject("Station Map Canvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // below the HUD/menu canvas (100)
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        frame = Panel("Map frame", canvasObject.transform, new Color(0.01f, 0.02f, 0.03f, 0.78f));
        floorTitle = Label("Floor", frame, "FLOOR 1 — GROUND", 15, TextAnchor.UpperLeft, new Color(0.3f, 0.95f, 0.88f));
        Stretch(floorTitle.rectTransform, new Vector2(10f, -26f), new Vector2(-10f, -6f), top: true);
        legend = Label("Legend", frame, "M  MAP", 13, TextAnchor.UpperRight, new Color(0.62f, 0.78f, 0.82f));
        legend.supportRichText = true;
        Stretch(legend.rectTransform, new Vector2(10f, -26f), new Vector2(-10f, -6f), top: true);

        mapArea = new GameObject("Map area", typeof(RectTransform)).GetComponent<RectTransform>();
        mapArea.SetParent(frame, false);
        mapArea.anchorMin = Vector2.zero; mapArea.anchorMax = Vector2.one;
        mapArea.offsetMin = new Vector2(10f, 10f); mapArea.offsetMax = new Vector2(-10f, -32f);

        groundLayer = Layer("Floor 1");
        upperLayer = Layer("Floor 2");
        foreach (var room in StationMapLayout.Ground) groundCells.Add(Cell(groundLayer, room));
        foreach (var room in StationMapLayout.Upper) upperCells.Add(Cell(upperLayer, room));

        relayA = Marker("Relay A", "A", new Vector3(-13.35f, 0f, -1.85f));
        relayB = Marker("Relay B", "B", new Vector3(12f, 4f, 6f));
        exitMark = Marker("Exit", "EXIT", new Vector3(9f, 0f, 4.5f));

        playerArrow = new GameObject("Player", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        playerArrow.SetParent(mapArea, false);
        playerArrow.anchorMin = playerArrow.anchorMax = Vector2.zero;
        playerArrow.sizeDelta = new Vector2(16f, 18f);
        var arrowImage = playerArrow.GetComponent<Image>();
        arrowImage.sprite = Triangle();
        arrowImage.color = new Color(0.35f, 1f, 0.9f, 1f);
        ApplyLayout();
    }

    private Shooter AddShooter(Transform robot)
    {
        var marker = new GameObject("Firing " + robot.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        marker.rectTransform.SetParent(mapArea, false);
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
        marker.rectTransform.sizeDelta = new Vector2(14f, 14f);
        marker.sprite = Disc();
        marker.raycastTarget = false;
        Text floor = Label("Floor", marker.rectTransform, "", 11, TextAnchor.MiddleCenter, Color.white);
        floor.rectTransform.sizeDelta = new Vector2(30f, 14f);
        floor.rectTransform.anchoredPosition = new Vector2(0f, 13f);
        marker.gameObject.SetActive(false);
        var entry = new Shooter { Robot = robot, Marker = marker, Floor = floor };
        shooters.Add(entry);
        return entry;
    }

    private void ApplyLayout()
    {
        frame.anchorMin = frame.anchorMax = large ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 0f);
        frame.pivot = large ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 0f);
        frame.sizeDelta = large ? largeSize : smallSize;
        frame.anchoredPosition = large ? Vector2.zero : new Vector2(-28f, 136f);
        Canvas.ForceUpdateCanvases();
        foreach (var (room, fill, label) in groundCells) Place(room, fill, label);
        foreach (var (room, fill, label) in upperCells) Place(room, fill, label);
        PlaceMarker(relayA, new Vector3(-13.35f, 0f, -1.85f));
        PlaceMarker(relayB, new Vector3(12f, 4f, 6f));
        PlaceMarker(exitMark, new Vector3(9f, 0f, 4.5f));
        int labelSize = large ? 14 : 8;
        playerArrow.sizeDelta = large ? new Vector2(24f, 27f) : new Vector2(16f, 18f);
        foreach (Shooter sh in shooters) sh.Marker.rectTransform.sizeDelta = large ? new Vector2(22f, 22f) : new Vector2(14f, 14f);
        foreach (var (_, _, label) in groundCells) if (label) label.fontSize = labelSize;
        foreach (var (_, _, label) in upperCells) if (label) label.fontSize = labelSize;
    }

    private void Place(StationMapLayout.Room room, Image fill, Text label)
    {
        RectTransform rt = fillToBorder[fill].rectTransform;
        Vector2 min = ToMap(new Vector3(room.Area.xMin, 0f, room.Area.yMin));
        Vector2 max = ToMap(new Vector3(room.Area.xMax, 0f, room.Area.yMax));
        rt.anchoredPosition = min;
        rt.sizeDelta = max - min;
    }

    private void PlaceMarker(Text marker, Vector3 world) => marker.rectTransform.anchoredPosition = ToMap(world);

    private RectTransform Layer(string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(mapArea, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private (StationMapLayout.Room, Image, Text) Cell(RectTransform layer, StationMapLayout.Room room)
    {
        var border = new GameObject(room.Label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        border.rectTransform.SetParent(layer, false);
        border.rectTransform.anchorMin = border.rectTransform.anchorMax = Vector2.zero;
        border.rectTransform.pivot = Vector2.zero;
        border.color = new Color(0.25f, 0.55f, 0.6f, 0.9f);
        border.raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.rectTransform.SetParent(border.rectTransform, false);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(1f, 1f); fill.rectTransform.offsetMax = new Vector2(-1f, -1f);
        fill.raycastTarget = false;
        Text label = null;
        if (!room.Corridor)
        {
            label = Label("Label", fill.rectTransform, room.Label.Replace("  ", " ").Replace(" / ", "\n"), 8, TextAnchor.UpperCenter, Color.white);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = new Vector2(0f, -3f);
        }
        return (room, FillOf(border, fill), label);
    }

    // Keep the border for placement and return the fill for colouring.
    private readonly Dictionary<Image, Image> fillToBorder = new Dictionary<Image, Image>();
    private Image FillOf(Image border, Image fill) { fillToBorder[fill] = border; return fill; }

    private Text Marker(string name, string text, Vector3 world)
    {
        Text t = Label(name, mapArea, text, 12, TextAnchor.MiddleCenter, Color.red);
        t.fontStyle = FontStyle.Bold;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
        t.rectTransform.sizeDelta = new Vector2(40f, 16f);
        return t;
    }

    private RectTransform Panel(string name, Transform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private Text Label(string name, Transform parent, string text, int size, TextAnchor anchor, Color color)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax, bool top)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    private static Sprite Triangle()
    {
        if (triangle != null) return triangle;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float half = (y / (float)(n - 1)) * 0.5f; // pointing up: narrow at top
                float cx = x / (float)(n - 1) - 0.5f;
                bool inside = Mathf.Abs(cx) <= 0.5f - half;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        tex.Apply();
        triangle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return triangle;
    }

    private static Sprite Disc()
    {
        if (disc != null) return disc;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2((n - 1) / 2f, (n - 1) / 2f)) / (n / 2f);
                float a = d < 0.62f ? 1f : d < 0.8f ? 0.15f : d < 0.98f ? 1f : 0f; // dot with ring
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return disc;
    }
}
