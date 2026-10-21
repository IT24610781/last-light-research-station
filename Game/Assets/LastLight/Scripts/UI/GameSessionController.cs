using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameSessionController : MonoBehaviour
{
    public enum SessionState { Start, Playing, Paused, Won, Lost }

    [Header("Scene references (auto-found when empty)")]
    [SerializeField] private GameObject player;
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    [SerializeField, Range(0.02f, 0.5f)] private float defaultMouseSensitivity = 0.1f;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;
    [SerializeField] private float interactionRange = 3f;

    public SessionState State { get; private set; } = SessionState.Start;

    private Health health;
    private PlayerShooter shooter;
    private PlayerGrabber grabber;
    private PlayerInput playerInput;
    private PlayerLook playerLook;

    private Canvas canvas;
    private GameObject hudRoot;
    private GameObject overlayRoot;
    private Text healthText;
    private Text ammoText;
    private Text objectiveText;
    private Text roomText;
    private GameObject roomPanel;
    private Text promptText;
    private Text overlayTitle;
    private Text overlayMessage;
    private Text primaryButtonText;
    private Slider healthSlider;
    private Slider sensitivitySlider;
    private Slider volumeSlider;
    private Button primaryButton;
    private Button restartButton;
    private Button quitButton;

    private float mouseSensitivity;
    private float volume;
    private string currentRoom;
    private float roomTitleUntil;

    private void Awake()
    {
        Time.timeScale = 0f;
        FindSceneReferences();
        BuildInterface();
        gameObject.AddComponent<StationAudioDirector>();
        LoadSettings();
        BindPlayer();
        SetState(SessionState.Start);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.volume = volume;

        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandlePlayerDied;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (State == SessionState.Playing)
            {
                PauseGame();
            }
            else if (State == SessionState.Paused)
            {
                ResumeGame();
            }
        }

        UpdateHud();
    }

    public void StartGame()
    {
        SetState(SessionState.Playing);
    }

    public void PauseGame()
    {
        if (State == SessionState.Playing)
        {
            SetState(SessionState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (State == SessionState.Paused || State == SessionState.Start)
        {
            SetState(SessionState.Playing);
        }
    }

    public void CompleteGame()
    {
        if (State == SessionState.Playing)
        {
            SetState(SessionState.Won);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandlePlayerDied(GameObject source)
    {
        SetState(SessionState.Lost);
    }

    private void SetState(SessionState next)
    {
        State = next;
        if (next == SessionState.Won || next == SessionState.Lost)
            StationAudioDirector.Instance?.PlayOutcome();
        bool playing = next == SessionState.Playing;
        bool showOverlay = !playing;

        Time.timeScale = playing ? 1f : 0f;

        if (playerInput != null)
        {
            if (playing && health != null && !health.IsDead)
            {
                playerInput.ActivateInput();
            }
            else
            {
                playerInput.DeactivateInput();
            }
        }

        Cursor.lockState = playing
            ? CursorLockMode.Locked
            : CursorLockMode.None;
        Cursor.visible = !playing;

        if (hudRoot != null)
        {
            hudRoot.SetActive(playing);
        }

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(showOverlay);
        }

        if (!showOverlay)
        {
            return;
        }

        switch (next)
        {
            case SessionState.Start:
                ConfigureOverlay(
                    "LAST LIGHT",
                    "The station went dark after an experiment breached containment.\nSecurity machines now hunt anything that moves.\n\nRestore power in the POWER ROOM, activate the MAIN LAB relay,\nthen reach the EVACUATION terminal before the station locks down.\n\nWASD MOVE   •   SHIFT SPRINT   •   C CROUCH   •   E USE\nSprinting, thrown objects and gunfire draw attention.",
                    "ENTER STATION",
                    true);
                break;
            case SessionState.Paused:
                ConfigureOverlay(
                    "PAUSED",
                    "Gameplay is frozen.",
                    "RESUME",
                    true);
                break;
            case SessionState.Won:
                ConfigureOverlay(
                    "EVACUATION COMPLETE",
                    "Both relays are online. The escape route is open.\n\nYou make it beyond the research station as the emergency doors seal behind you.\nThe machines remain inside, but their signal is still active.\n\nMISSION COMPLETE",
                    "PLAY AGAIN",
                    false);
                break;
            case SessionState.Lost:
                ConfigureOverlay(
                    "CONNECTION LOST",
                    "The player has been eliminated.",
                    "RESTART",
                    false);
                break;
        }
    }

    private void ConfigureOverlay(
        string title,
        string message,
        string primaryLabel,
        bool primaryResumes)
    {
        overlayTitle.text = title;
        overlayMessage.text = message;
        primaryButtonText.text = primaryLabel;
        primaryButton.onClick.RemoveAllListeners();

        if (primaryResumes)
        {
            primaryButton.onClick.AddListener(ResumeGame);
        }
        else
        {
            primaryButton.onClick.AddListener(RestartGame);
        }

        restartButton.gameObject.SetActive(
            State == SessionState.Paused || State == SessionState.Start);
    }

    private void FindSceneReferences()
    {
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        if (playerCamera == null && player != null)
        {
            playerCamera = player.GetComponentInChildren<Camera>(true);
        }
    }

    private void BindPlayer()
    {
        if (player == null)
        {
            Debug.LogError(
                "GameSessionController: no GameObject named Player was found.",
                this);
            return;
        }

        health = player.GetComponent<Health>();
        shooter = player.GetComponent<PlayerShooter>();
        grabber = player.GetComponent<PlayerGrabber>();
        playerInput = player.GetComponent<PlayerInput>();
        playerLook = player.GetComponent<PlayerLook>();

        if (health != null)
        {
            health.HealthChanged += HandleHealthChanged;
            health.Died += HandlePlayerDied;
            HandleHealthChanged(health.CurrentHealth, health.MaximumHealth);
        }

        ApplyMouseSensitivity(mouseSensitivity);
    }

    private void HandleHealthChanged(float current, float maximum)
    {
        if (healthSlider != null)
        {
            healthSlider.value = maximum > 0f ? current / maximum : 0f;
        }

        if (healthText != null)
        {
            healthText.text = $"HEALTH  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
        }
    }

    private void UpdateHud()
    {
        if (State != SessionState.Playing)
        {
            return;
        }

        if (health != null)
        {
            HandleHealthChanged(health.CurrentHealth, health.MaximumHealth);
        }

        if (shooter != null && shooter.CurrentDefinition != null)
        {
            string reload = shooter.IsReloading ? "  RELOADING" : string.Empty;
            ammoText.text =
                $"{shooter.CurrentDefinition.name.ToUpperInvariant()}  " +
                $"{shooter.CurrentAmmo} / {shooter.CurrentDefinition.magazineCapacity}" +
                reload;
        }
        else
        {
            ammoText.text = "NO WEAPON  •  SEARCH THE ARMORY";
        }

        StationProgressionManager progression =
            StationProgressionManager.Instance;
        if (objectiveText != null && progression != null)
        {
            if (!string.IsNullOrEmpty(progression.StatusMessage) &&
                Time.unscaledTime < progression.StatusMessageUntil)
            {
                objectiveText.text = progression.StatusMessage;
            }
            else if (!progression.RelayAOnline)
            {
                objectiveText.text = "OBJECTIVE  ACTIVATE RELAY A — POWER";
            }
            else if (!progression.RelayBOnline)
            {
                objectiveText.text = "OBJECTIVE  ACTIVATE RELAY B — MAIN LAB";
            }
            else
            {
                objectiveText.text = "OBJECTIVE  REACH EVACUATION";
            }
        }

        UpdateRoomTitle();
        promptText.text = GetInteractionPrompt();
    }

    private void UpdateRoomTitle()
    {
        if (roomPanel == null || player == null) return;
        Vector3 position = player.transform.position;
        string room = string.Empty;
        if (Mathf.Abs(position.x) > 3.2f)
        {
            bool west = position.x < 0f;
            bool upper = position.y > 3f;
            int row = position.z < -4f ? 0 : position.z > 4f ? 2 : 1;
            if (upper)
                room = west ? new[] { "03  MEDICAL", "08  SECURITY", "07  ROBOT WORKSHOP" }[row]
                            : new[] { "09  CREW QUARTERS", "06  MAIN LAB", "06  MAIN LAB" }[row];
            else
                room = west ? new[] { "02  ARMORY", "04  POWER ROOM", "05  STORAGE" }[row]
                            : new[] { "01  ARRIVAL", "01  ARRIVAL", "10  EVACUATION" }[row];
        }
        if (room != currentRoom)
        {
            currentRoom = room;
            roomTitleUntil = Time.unscaledTime + 4f;
            roomText.text = room;
        }
        roomPanel.SetActive(!string.IsNullOrEmpty(room) && Time.unscaledTime < roomTitleUntil);
    }

    private string GetInteractionPrompt()
    {
        if (grabber != null && grabber.IsHolding)
        {
            return "[E]  DROP OBJECT       [Q]  THROW";
        }

        if (playerCamera == null)
        {
            return string.Empty;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            return string.Empty;
        }

        if (hit.collider.GetComponentInParent<CarryableObject>() != null)
        {
            return "[E]  PICK UP OBJECT";
        }

        StationRelay relay = hit.collider.GetComponentInParent<StationRelay>();
        if (relay != null)
            return relay.Activated ? "RELAY " + relay.Id + " ONLINE" : "[E]  ACTIVATE RELAY " + relay.Id;

        StationSlidingDoor door = hit.collider.GetComponentInParent<StationSlidingDoor>();
        if (door != null)
        {
            StationProgressionManager progression = StationProgressionManager.Instance;
            if (progression != null && !progression.CanOpen(door.Requirement))
                return door.Requirement == StationSlidingDoor.AccessRequirement.RelayA
                    ? "LOCKED  •  RELAY A REQUIRED" : "LOCKED  •  BOTH RELAYS REQUIRED";
            return door.IsOpen ? "[E]  CLOSE DOOR" : "[E]  OPEN DOOR";
        }

        if (hit.collider.GetComponentInParent<StationEvacuationTerminal>() != null)
            return "[E]  INITIATE EVACUATION";
        StationWeaponPickup weapon = hit.collider.GetComponentInParent<StationWeaponPickup>();
        if (weapon != null)
            return "[E]  TAKE " + weapon.Type.ToString().ToUpperInvariant();
        if (hit.collider.GetComponentInParent<StationMedkit>() != null)
            return "[E]  USE MEDKIT";
        if (hit.collider.GetComponentInParent<IInteractable>() != null)
            return "[E]  USE";

        return string.Empty;
    }

    private void LoadSettings()
    {
        mouseSensitivity = PlayerPrefs.GetFloat(
            "MouseSensitivity", defaultMouseSensitivity);
        volume = PlayerPrefs.GetFloat("MasterVolume", defaultVolume);

        sensitivitySlider.SetValueWithoutNotify(mouseSensitivity);
        volumeSlider.SetValueWithoutNotify(volume);

        AudioListener.volume = volume;
    }

    private void ApplyMouseSensitivity(float value)
    {
        mouseSensitivity = value;
        PlayerPrefs.SetFloat("MouseSensitivity", value);

        if (playerLook != null)
        {
            playerLook.SetSensitivity(value);
        }
    }

    private void ApplyVolume(float value)
    {
        volume = value;
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(
            "Game UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildHud(canvasObject.transform);
        BuildOverlay(canvasObject.transform);
    }

    private void BuildHud(Transform parent)
    {
        hudRoot = CreateRect(
            "HUD", parent, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero).gameObject;

        RectTransform healthPanel = CreatePanel(
            "Health Panel", hudRoot.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(32f, 28f), new Vector2(410f, 118f),
            new Color(0.02f, 0.05f, 0.07f, 0.86f));

        healthText = CreateText(
            "Health Text", healthPanel, "HEALTH",
            25f, TextAnchor.MiddleLeft,
            new Vector2(18f, 58f), new Vector2(370f, 40f));

        healthSlider = CreateSlider(
            "Health Bar", healthPanel,
            new Vector2(18f, 18f), new Vector2(370f, 28f),
            new Color(0.12f, 0.85f, 0.78f, 1f));
        healthSlider.interactable = false;

        RectTransform ammoPanel = CreatePanel(
            "Ammo Panel", hudRoot.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-28f, 28f), new Vector2(450f, 92f),
            new Color(0.02f, 0.05f, 0.07f, 0.86f));

        ammoText = CreateText(
            "Ammo Text", ammoPanel, "WEAPON",
            28f, TextAnchor.MiddleCenter,
            new Vector2(12f, 12f), new Vector2(426f, 68f));

        objectiveText = CreateText(
            "Objective Text", hudRoot.transform,
            "OBJECTIVE  ACTIVATE RELAY A — POWER",
            24f, TextAnchor.MiddleLeft,
            new Vector2(32f, -74f), new Vector2(720f, 46f),
            new Vector2(0f, 1f), new Vector2(0f, 1f));
        objectiveText.color = new Color(0.95f, 0.72f, 0.2f, 1f);

        roomPanel = CreatePanel(
            "Room Banner", hudRoot.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(32f, -150f), new Vector2(520f, 58f),
            new Color(0.02f, 0.07f, 0.10f, 0.82f)).gameObject;
        roomText = CreateText(
            "Room Name", roomPanel.transform, string.Empty,
            28f, TextAnchor.MiddleLeft,
            new Vector2(18f, 4f), new Vector2(490f, 50f));
        roomText.color = new Color(0.25f, 0.96f, 0.87f, 1f);
        roomPanel.SetActive(false);

        promptText = CreateText(
            "Interaction Prompt", hudRoot.transform, string.Empty,
            26f, TextAnchor.MiddleCenter,
            new Vector2(-300f, 118f), new Vector2(600f, 54f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        Text crosshair = CreateText(
            "Crosshair", hudRoot.transform, "+",
            30f, TextAnchor.MiddleCenter,
            new Vector2(-20f, -20f), new Vector2(40f, 40f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        crosshair.color = new Color(0.85f, 1f, 0.98f, 0.9f);

        hudRoot.AddComponent<RobotHealthHud>().Initialize(playerCamera);
    }

    private void BuildOverlay(Transform parent)
    {
        overlayRoot = CreatePanel(
            "Overlay", parent,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            new Color(0.008f, 0.015f, 0.025f, 0.94f)).gameObject;

        overlayTitle = CreateText(
            "Title", overlayRoot.transform, "LAST LIGHT",
            72f, TextAnchor.MiddleCenter,
            new Vector2(0f, 300f), new Vector2(1000f, 100f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        overlayTitle.color = new Color(0.3f, 0.95f, 0.88f, 1f);

        overlayMessage = CreateText(
            "Message", overlayRoot.transform, string.Empty,
            20f, TextAnchor.MiddleCenter,
            new Vector2(0f, 135f), new Vector2(1000f, 150f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        primaryButton = CreateButton(
            "Primary Button", overlayRoot.transform, "START",
            new Vector2(0f, -25f), new Vector2(340f, 66f));
        primaryButtonText = primaryButton.GetComponentInChildren<Text>();

        restartButton = CreateButton(
            "Restart Button", overlayRoot.transform, "RESTART",
            new Vector2(0f, -105f), new Vector2(340f, 58f));
        restartButton.onClick.AddListener(RestartGame);

        quitButton = CreateButton(
            "Quit Button", overlayRoot.transform, "QUIT",
            new Vector2(0f, -177f), new Vector2(340f, 58f));
        quitButton.onClick.AddListener(QuitGame);

        Text sensitivityLabel = CreateText(
            "Sensitivity Label", overlayRoot.transform,
            "MOUSE SENSITIVITY",
            20f, TextAnchor.MiddleLeft,
            new Vector2(-320f, -238f), new Vector2(260f, 42f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        sensitivitySlider = CreateSlider(
            "Sensitivity", overlayRoot.transform,
            new Vector2(-30f, -230f), new Vector2(350f, 28f),
            new Color(0.25f, 0.9f, 0.82f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        sensitivitySlider.minValue = 0.02f;
        sensitivitySlider.maxValue = 0.5f;
        sensitivitySlider.onValueChanged.AddListener(ApplyMouseSensitivity);

        Text volumeLabel = CreateText(
            "Volume Label", overlayRoot.transform,
            "MASTER VOLUME",
            20f, TextAnchor.MiddleLeft,
            new Vector2(-320f, -290f), new Vector2(260f, 42f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        volumeSlider = CreateSlider(
            "Volume", overlayRoot.transform,
            new Vector2(-30f, -282f), new Vector2(350f, 28f),
            new Color(0.25f, 0.9f, 0.82f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.onValueChanged.AddListener(ApplyVolume);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventObject);
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax
            ? anchorMin
            : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        RectTransform rect = CreateRect(
            name, parent, anchorMin, anchorMax, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAnchor alignment,
        Vector2 position,
        Vector2 size,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null)
    {
        Vector2 min = anchorMin ?? Vector2.zero;
        Vector2 max = anchorMax ?? Vector2.zero;
        RectTransform rect = CreateRect(
            name, parent, min, max, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = Mathf.RoundToInt(fontSize);
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = CreatePanel(
            name, parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            position, size,
            new Color(0.08f, 0.32f, 0.34f, 0.95f));

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.15f, 0.55f, 0.52f, 1f);
        colors.pressedColor = new Color(0.08f, 0.7f, 0.62f, 1f);
        button.colors = colors;

        CreateText(
            "Label", rect, label, 25f,
            TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.one);

        return button;
    }

    private static Slider CreateSlider(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size,
        Color fillColor,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null)
    {
        Vector2 min = anchorMin ?? Vector2.zero;
        Vector2 max = anchorMax ?? Vector2.zero;
        RectTransform root = CreatePanel(
            name, parent, min, max, position, size,
            new Color(0.08f, 0.12f, 0.14f, 1f));

        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform fillArea = CreateRect(
            "Fill Area", root, Vector2.zero, Vector2.one,
            new Vector2(4f, 0f), new Vector2(-8f, -8f));

        RectTransform fill = CreateRect(
            "Fill", fillArea, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = fillColor;

        RectTransform handleArea = CreateRect(
            "Handle Slide Area", root, Vector2.zero, Vector2.one,
            Vector2.zero, new Vector2(-12f, 0f));

        RectTransform handle = CreateRect(
            "Handle", handleArea,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(20f, 34f));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        return slider;
    }
}
