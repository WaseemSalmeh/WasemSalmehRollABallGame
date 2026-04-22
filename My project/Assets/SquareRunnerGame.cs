using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public sealed class SquareRunnerGame : MonoBehaviour
{
    private const string GameTitle = "LAST LIGHT";
    private const string HighestScoreKey = "LastLight.HighestScore";
    private const string GlowOpacityKey = "LastLight.GlowOpacity";
    private const string SoundVolumeKey = "LastLight.SoundVolume";

    private const float CameraHalfHeight = 6.45f;
    private const float GroundHeight = 0.56f;
    private const float RoofHeight = 0.46f;
    private const float GroundCenterY = -1.6f;
    private const float RoofCenterY = 4.9f;
    private const float PlayerSize = 1.32f;
    private const float PlayerCollisionHalfExtent = 0.47f;
    private const float JumpVelocity = 11.3f;
    private const float GravityStrength = -34.5f;
    private const float BaseSpeed = 9.1f;
    private const float SpeedRamp = 0.26f;
    private const float ScoreDistanceStep = 10f;
    private const float SpawnBuffer = 18f;
    private const float OffscreenCullPadding = 6f;
    private const float ObstacleWidth = 0.36f;
    private const float BottomObstacleHeight = 2.05f;
    private const float TopObstacleHeight = 2.85f;
    private const float FreezeMaxSeconds = 4f;
    private const float FreezeRechargeSeconds = 5f;
    private const float FreezeHoldThreshold = 0.05f;
    private const float FreezeTransitionSeconds = 0.1f;
    private const float StartAnimationSeconds = 0.45f;
    private const float DeathAnimationSeconds = 0.34f;
    private const float SettingsPanelWidth = 760f;
    private const float SettingsPanelHeight = 500f;

    private static Sprite s_whiteSprite;
    private static Sprite s_softSquareSprite;
    private static Sprite s_glowSquareSprite;
    private static Sprite s_retryIconSprite;
    private static Font s_builtinFont;

    private enum GameMode
    {
        Menu,
        Starting,
        Running,
        Failed,
    }

    private readonly List<SquareRunnerHazard> _hazards = new List<SquareRunnerHazard>();

    private Camera _mainCamera;
    private Transform _worldRoot;
    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;
    private SquareRunnerPlayer _player;
    private SpriteRenderer _groundRenderer;
    private SpriteRenderer _roofRenderer;

    private CanvasGroup _menuGroup;
    private CanvasGroup _settingsGroup;
    private CanvasGroup _hudGroup;
    private CanvasGroup _deathGroup;
    private CanvasGroup _confirmGroup;
    private CanvasGroup _abilityGroup;

    private Text _scoreText;
    private Text _highestText;
    private Text _failedText;
    private Text _finalScoreText;
    private Text _finalHighestText;
    private Text _confirmText;

    private Slider _soundSlider;
    private Slider _glowSlider;
    private Image _freezeBarFill;
    private Image _freezeOverlay;
    private SquareRunnerHoldButton _freezeHoldButton;

    private Button _startButton;
    private Button _settingsButton;
    private Button _settingsCloseButton;
    private Button _topRetryButton;
    private Button _deathRetryButton;
    private Button _confirmYesButton;
    private Button _confirmNoButton;

    private float _halfWidth;
    private float _groundTop;
    private float _roofBottom;
    private float _playerStartX;
    private float _spawnCursorX;
    private float _runTime;
    private float _runDistance;
    private float _currentSpeed;
    private float _startBlend;
    private float _deathBlend;
    private float _freezeBlend;
    private float _freezeEnergy;
    private float _freezeCooldownTimer;
    private float _freezeCooldownStartEnergy;
    private float _keyboardFreezeHoldTime;
    private float _soundVolume;
    private float _glowOpacity;

    private int _score;
    private int _highestScore;

    private bool _freezeCoolingDown;
    private bool _settingsOpen;
    private bool _restartConfirmOpen;
    private GameMode _mode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<SquareRunnerGame>() != null)
        {
            return;
        }

        GameObject root = new GameObject("Square Runner");
        root.AddComponent<SquareRunnerGame>();
    }

    private void Awake()
    {
        LoadPreferences();
        BuildGame();
        ShowMenuImmediate();
    }

    private void Update()
    {
        float unscaledDeltaTime = Time.unscaledDeltaTime;

        UpdateKeyboardHold(unscaledDeltaTime);
        UpdateFreezeState(unscaledDeltaTime);

        if (_mode == GameMode.Starting)
        {
            UpdateStarting(unscaledDeltaTime);
        }
        else if (_mode == GameMode.Running)
        {
            UpdateRunning(unscaledDeltaTime);
        }
        else if (_mode == GameMode.Failed)
        {
            UpdateFailed(unscaledDeltaTime);
        }
        else
        {
            _player.TickPresentation(unscaledDeltaTime, 0f, _glowOpacity, false, 0f, 0f);
        }

        UpdateInterface();
    }

    private void BuildGame()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        AudioListener.volume = _soundVolume;

        _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        if (_mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            _mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        _mainCamera.orthographic = true;
        _mainCamera.orthographicSize = CameraHalfHeight;
        _mainCamera.clearFlags = CameraClearFlags.SolidColor;
        _mainCamera.backgroundColor = Color.black;
        _mainCamera.transform.position = new Vector3(0f, 0f, -10f);

        _halfWidth = CameraHalfHeight * _mainCamera.aspect;
        _groundTop = GroundCenterY + (GroundHeight * 0.5f);
        _roofBottom = RoofCenterY - (RoofHeight * 0.5f);
        _playerStartX = -_halfWidth + 2.65f;

        _worldRoot = new GameObject("World").transform;
        _worldRoot.SetParent(transform, false);

        _groundRenderer = CreateWorldStripe("Ground", new Vector2((_halfWidth * 2f) + 8f, GroundHeight), new Vector2(0f, GroundCenterY), 3);
        _roofRenderer = CreateWorldStripe("Roof", new Vector2((_halfWidth * 2f) + 8f, RoofHeight), new Vector2(0f, RoofCenterY), 3);

        BuildPlayer();
        BuildInterface();
        EnsureEventSystem();
    }

    private void BuildPlayer()
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.SetParent(_worldRoot, false);

        _player = playerObject.AddComponent<SquareRunnerPlayer>();
        _player.Initialize(
            GetSoftSquareSprite(),
            GetGlowSquareSprite(),
            PlayerSize,
            PlayerCollisionHalfExtent,
            new Vector3(_playerStartX, _groundTop + PlayerCollisionHalfExtent, 0f));
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("UI");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        Font font = GetBuiltinFont();

        _freezeOverlay = CreateImage("Freeze Overlay", canvas.transform, GetWhiteSprite(), Color.black);
        StretchRect(_freezeOverlay.rectTransform);
        _freezeOverlay.raycastTarget = false;

        _menuGroup = CreateCanvasGroup("Menu", canvas.transform);
        _settingsGroup = CreateCanvasGroup("Settings", canvas.transform);
        _hudGroup = CreateCanvasGroup("HUD", canvas.transform);
        _abilityGroup = CreateCanvasGroup("Ability HUD", canvas.transform);
        _deathGroup = CreateCanvasGroup("Death", canvas.transform);
        _confirmGroup = CreateCanvasGroup("Confirm", canvas.transform);

        BuildMenu(font);
        BuildSettings(font);
        BuildHud(font);
        BuildDeathScreen(font);
        BuildRestartConfirm(font);

        SetCanvasGroup(_settingsGroup, 0f, false);
        SetCanvasGroup(_hudGroup, 0f, false);
        SetCanvasGroup(_abilityGroup, 0f, false);
        SetCanvasGroup(_deathGroup, 0f, false);
        SetCanvasGroup(_confirmGroup, 0f, false);
    }

    private void BuildMenu(Font font)
    {
        Text title = CreateText(
            "Title",
            _menuGroup.transform,
            font,
            116,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 200f),
            new Vector2(1100f, 160f),
            GameTitle,
            new Color(1f, 1f, 1f, 0.96f));
        AddTextShadow(title, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, -3f));

        CreateText(
            "Subtitle",
            _menuGroup.transform,
            font,
            34,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 108f),
            new Vector2(900f, 80f),
            "HOLD ON TO THE LIGHT",
            new Color(1f, 1f, 1f, 0.45f));

        _startButton = CreateTextButton(_menuGroup.transform, font, "START", new Vector2(0f, -10f), new Vector2(360f, 110f));
        _startButton.onClick.AddListener(BeginRun);

        _settingsButton = CreateTextButton(_menuGroup.transform, font, "SETTINGS", new Vector2(0f, -150f), new Vector2(360f, 102f));
        _settingsButton.onClick.AddListener(OpenSettings);
    }

    private void BuildSettings(Font font)
    {
        CreateDimmer(_settingsGroup.transform);

        Image panel = CreateImage("Panel", _settingsGroup.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.08f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(SettingsPanelWidth, SettingsPanelHeight);
        panelRect.anchoredPosition = Vector2.zero;

        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(1f, 1f, 1f, 0.07f);
        panelOutline.effectDistance = new Vector2(1f, -1f);

        Text settingsTitle = CreateText(
            "Settings Title",
            panel.transform,
            font,
            58,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -68f),
            new Vector2(500f, 80f),
            "SETTINGS",
            Color.white);
        AddTextShadow(settingsTitle, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, -2f));

        CreateText(
            "Sound Label",
            panel.transform,
            font,
            32,
            TextAnchor.MiddleLeft,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(-200f, -168f),
            new Vector2(400f, 50f),
            "SOUND",
            new Color(1f, 1f, 1f, 0.8f));

        _soundSlider = CreateSlider(panel.transform, new Vector2(0f, -228f), new Vector2(520f, 42f), _soundVolume);
        _soundSlider.onValueChanged.AddListener(SetSoundVolume);

        CreateText(
            "Glow Label",
            panel.transform,
            font,
            32,
            TextAnchor.MiddleLeft,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(-200f, -308f),
            new Vector2(400f, 50f),
            "GLOW",
            new Color(1f, 1f, 1f, 0.8f));

        _glowSlider = CreateSlider(panel.transform, new Vector2(0f, -368f), new Vector2(520f, 42f), _glowOpacity);
        _glowSlider.onValueChanged.AddListener(SetGlowOpacity);

        _settingsCloseButton = CreateTextButton(panel.transform, font, "BACK", new Vector2(0f, 152f), new Vector2(240f, 90f));
        _settingsCloseButton.onClick.AddListener(CloseSettings);
    }

    private void BuildHud(Font font)
    {
        _scoreText = CreateText(
            "Score",
            _hudGroup.transform,
            font,
            46,
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(320f, 80f),
            "0",
            Color.white);
        AddTextShadow(_scoreText, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, -2f));

        _highestText = CreateText(
            "Highest",
            _hudGroup.transform,
            font,
            28,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(44f, -42f),
            new Vector2(460f, 70f),
            "HIGHEST SCORE 0",
            new Color(1f, 1f, 1f, 0.72f));

        _topRetryButton = CreateIconButton(_hudGroup.transform, GetRetryIconSprite(), new Vector2(-54f, -48f), new Vector2(80f, 80f));
        RectTransform topRetryRect = _topRetryButton.GetComponent<RectTransform>();
        topRetryRect.anchorMin = new Vector2(1f, 1f);
        topRetryRect.anchorMax = new Vector2(1f, 1f);
        topRetryRect.pivot = new Vector2(1f, 1f);
        _topRetryButton.onClick.AddListener(RequestRestart);

        Image freezeBarBack = CreateImage("Freeze Bar Back", _abilityGroup.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.12f));
        RectTransform barBackRect = freezeBarBack.rectTransform;
        barBackRect.anchorMin = new Vector2(1f, 0f);
        barBackRect.anchorMax = new Vector2(1f, 0f);
        barBackRect.pivot = new Vector2(1f, 0f);
        barBackRect.sizeDelta = new Vector2(280f, 20f);
        barBackRect.anchoredPosition = new Vector2(-54f, 166f);
        freezeBarBack.raycastTarget = false;

        _freezeBarFill = CreateImage("Freeze Bar Fill", freezeBarBack.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.92f));
        RectTransform barFillRect = _freezeBarFill.rectTransform;
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.pivot = new Vector2(0f, 0.5f);
        barFillRect.sizeDelta = new Vector2(280f, 0f);
        barFillRect.anchoredPosition = Vector2.zero;
        _freezeBarFill.raycastTarget = false;

        Image freezeButtonImage = CreateImage("Freeze Button", _abilityGroup.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.07f));
        RectTransform freezeButtonRect = freezeButtonImage.rectTransform;
        freezeButtonRect.anchorMin = new Vector2(1f, 0f);
        freezeButtonRect.anchorMax = new Vector2(1f, 0f);
        freezeButtonRect.pivot = new Vector2(1f, 0f);
        freezeButtonRect.sizeDelta = new Vector2(200f, 200f);
        freezeButtonRect.anchoredPosition = new Vector2(-54f, 52f);

        Button freezeButton = freezeButtonImage.gameObject.AddComponent<Button>();
        freezeButton.transition = Selectable.Transition.ColorTint;
        ColorBlock freezeColors = freezeButton.colors;
        freezeColors.normalColor = new Color(1f, 1f, 1f, 1f);
        freezeColors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        freezeColors.pressedColor = new Color(1f, 1f, 1f, 1f);
        freezeColors.selectedColor = new Color(1f, 1f, 1f, 1f);
        freezeColors.fadeDuration = 0.05f;
        freezeButton.colors = freezeColors;

        _freezeHoldButton = freezeButtonImage.gameObject.AddComponent<SquareRunnerHoldButton>();

        Text holdText = CreateText(
            "Freeze Label",
            freezeButtonImage.transform,
            font,
            28,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 26f),
            new Vector2(160f, 40f),
            "HOLD",
            Color.white);
        holdText.raycastTarget = false;

        Text abilityText = CreateText(
            "Freeze Hint",
            freezeButtonImage.transform,
            font,
            20,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -16f),
            new Vector2(150f, 50f),
            "FREEZE",
            new Color(1f, 1f, 1f, 0.65f));
        abilityText.raycastTarget = false;
    }

    private void BuildDeathScreen(Font font)
    {
        Text failedGlow = CreateText(
            "Failed",
            _deathGroup.transform,
            font,
            84,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 122f),
            new Vector2(900f, 110f),
            "FAILED",
            Color.white);
        AddTextShadow(failedGlow, new Color(1f, 1f, 1f, 0.14f), new Vector2(0f, -3f));
        _failedText = failedGlow;

        _finalScoreText = CreateText(
            "Final Score",
            _deathGroup.transform,
            font,
            74,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 18f),
            new Vector2(460f, 90f),
            "0",
            new Color(1f, 1f, 1f, 0.96f));

        _finalHighestText = CreateText(
            "Final Highest",
            _deathGroup.transform,
            font,
            30,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -54f),
            new Vector2(560f, 50f),
            "HIGHEST SCORE 0",
            new Color(1f, 1f, 1f, 0.72f));

        _deathRetryButton = CreateIconButton(_deathGroup.transform, GetRetryIconSprite(), new Vector2(0f, -180f), new Vector2(110f, 110f));
        _deathRetryButton.onClick.AddListener(BeginRun);
    }

    private void BuildRestartConfirm(Font font)
    {
        CreateDimmer(_confirmGroup.transform);

        Image panel = CreateImage("Confirm Panel", _confirmGroup.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.08f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(700f, 300f);
        panelRect.anchoredPosition = Vector2.zero;

        _confirmText = CreateText(
            "Confirm Text",
            panel.transform,
            font,
            40,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 46f),
            new Vector2(520f, 80f),
            "RESTART RUN?",
            Color.white);

        _confirmYesButton = CreateTextButton(panel.transform, font, "YES", new Vector2(-120f, -82f), new Vector2(190f, 82f));
        _confirmNoButton = CreateTextButton(panel.transform, font, "NO", new Vector2(120f, -82f), new Vector2(190f, 82f));

        _confirmYesButton.onClick.AddListener(ConfirmRestart);
        _confirmNoButton.onClick.AddListener(CancelRestart);
    }

    private void EnsureEventSystem()
    {
        _eventSystem = FindAnyObjectByType<EventSystem>();
        if (_eventSystem != null)
        {
            if (_eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                _eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        _eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void LoadPreferences()
    {
        _highestScore = PlayerPrefs.GetInt(HighestScoreKey, 0);
        _glowOpacity = PlayerPrefs.GetFloat(GlowOpacityKey, 0.62f);
        _soundVolume = PlayerPrefs.GetFloat(SoundVolumeKey, 1f);
    }

    private void SaveHighestScore()
    {
        PlayerPrefs.SetInt(HighestScoreKey, _highestScore);
        PlayerPrefs.Save();
    }

    private void BeginRun()
    {
        _settingsOpen = false;
        _restartConfirmOpen = false;
        ResetRun();
        _mode = GameMode.Starting;
        _startBlend = 0f;
        _deathBlend = 0f;
    }

    private void ResetRun()
    {
        ClearHazards();

        _runTime = 0f;
        _runDistance = 0f;
        _currentSpeed = BaseSpeed;
        _score = 0;
        _freezeBlend = 0f;
        _freezeEnergy = FreezeMaxSeconds;
        _freezeCoolingDown = false;
        _freezeCooldownTimer = 0f;
        _freezeCooldownStartEnergy = FreezeMaxSeconds;
        _keyboardFreezeHoldTime = 0f;
        _spawnCursorX = _halfWidth + 8f;

        _player.ResetTo(new Vector3(_playerStartX, _groundTop + PlayerCollisionHalfExtent, 0f));
        EnsureHazardsAhead();
        RefreshTexts();
    }

    private void ShowMenuImmediate()
    {
        _mode = GameMode.Menu;
        _settingsOpen = false;
        _restartConfirmOpen = false;
        ClearHazards();
        _player.ResetTo(new Vector3(_playerStartX, _groundTop + PlayerCollisionHalfExtent, 0f));
        _freezeBlend = 0f;
        _freezeEnergy = FreezeMaxSeconds;
        _freezeCoolingDown = false;
        _score = 0;
        RefreshTexts();
    }

    private void OpenSettings()
    {
        _settingsOpen = true;
    }

    private void CloseSettings()
    {
        _settingsOpen = false;
    }

    private void RequestRestart()
    {
        if (_mode == GameMode.Menu)
        {
            return;
        }

        _restartConfirmOpen = true;
    }

    private void ConfirmRestart()
    {
        _restartConfirmOpen = false;
        BeginRun();
    }

    private void CancelRestart()
    {
        _restartConfirmOpen = false;
    }

    private void UpdateKeyboardHold(float unscaledDeltaTime)
    {
        if (Keyboard.current != null && Keyboard.current.fKey.isPressed)
        {
            _keyboardFreezeHoldTime += unscaledDeltaTime;
        }
        else
        {
            _keyboardFreezeHoldTime = 0f;
        }

        if (_mode == GameMode.Running && !_settingsOpen && !_restartConfirmOpen && Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RequestRestart();
            }
        }
    }

    private void UpdateStarting(float unscaledDeltaTime)
    {
        _startBlend = Mathf.MoveTowards(_startBlend, 1f, unscaledDeltaTime / StartAnimationSeconds);
        _player.TickPresentation(unscaledDeltaTime, 0f, _glowOpacity, false, _startBlend, 0f);

        if (_startBlend >= 1f)
        {
            _mode = GameMode.Running;
        }
    }

    private void UpdateRunning(float unscaledDeltaTime)
    {
        if (_settingsOpen || _restartConfirmOpen)
        {
            _player.TickPresentation(unscaledDeltaTime, _freezeBlend, _glowOpacity, false, 1f, 0f);
            return;
        }

        if (WasJumpPressedThisFrame())
        {
            _player.TryJump(JumpVelocity);
        }

        float simulationScale = 1f - Mathf.SmoothStep(0f, 1f, _freezeBlend);
        float deltaTime = unscaledDeltaTime * simulationScale;

        _runTime += deltaTime;
        _currentSpeed = CalculateSpeed(_runTime);
        _runDistance += _currentSpeed * deltaTime;

        UpdateHazards(deltaTime);
        _player.TickMotion(deltaTime, GravityStrength, _groundTop, _roofBottom);
        _player.TickPresentation(unscaledDeltaTime, _freezeBlend, _glowOpacity, false, 1f, 0f);

        EnsureHazardsAhead();
        UpdateScore();
        CheckHazardCollisions();
    }

    private void UpdateFailed(float unscaledDeltaTime)
    {
        _deathBlend = Mathf.MoveTowards(_deathBlend, 1f, unscaledDeltaTime / DeathAnimationSeconds);
        _player.TickPresentation(unscaledDeltaTime, 0f, _glowOpacity, true, 1f, _deathBlend);
    }

    private void UpdateFreezeState(float unscaledDeltaTime)
    {
        bool gameplayBlocked = _mode != GameMode.Running || _settingsOpen || _restartConfirmOpen;

        bool abilityHeld =
            !gameplayBlocked &&
            !_freezeCoolingDown &&
            _freezeEnergy > 0f &&
            ((_freezeHoldButton != null && _freezeHoldButton.IsHeld && _freezeHoldButton.HeldDuration >= FreezeHoldThreshold) ||
             _keyboardFreezeHoldTime >= FreezeHoldThreshold);

        if (abilityHeld)
        {
            _freezeEnergy = Mathf.Max(0f, _freezeEnergy - unscaledDeltaTime);
        }

        bool shouldStartRecharge =
            !abilityHeld &&
            !_freezeCoolingDown &&
            _freezeEnergy < FreezeMaxSeconds &&
            _mode != GameMode.Menu;

        if (_freezeEnergy <= 0f && !_freezeCoolingDown)
        {
            shouldStartRecharge = true;
        }

        if (shouldStartRecharge)
        {
            _freezeCoolingDown = true;
            _freezeCooldownTimer = FreezeRechargeSeconds;
            _freezeCooldownStartEnergy = _freezeEnergy;
        }

        if (_freezeCoolingDown)
        {
            _freezeCooldownTimer = Mathf.Max(0f, _freezeCooldownTimer - unscaledDeltaTime);
            float rechargeProgress = 1f - (_freezeCooldownTimer / FreezeRechargeSeconds);
            _freezeEnergy = Mathf.Lerp(_freezeCooldownStartEnergy, FreezeMaxSeconds, rechargeProgress);

            if (_freezeCooldownTimer <= 0f)
            {
                _freezeCoolingDown = false;
                _freezeEnergy = FreezeMaxSeconds;
            }
        }

        float targetFreeze = abilityHeld ? 1f : 0f;
        _freezeBlend = Mathf.MoveTowards(_freezeBlend, targetFreeze, unscaledDeltaTime / FreezeTransitionSeconds);
    }

    private void UpdateHazards(float deltaTime)
    {
        float movement = _currentSpeed * deltaTime;
        float leftCullX = -_halfWidth - OffscreenCullPadding;

        for (int i = _hazards.Count - 1; i >= 0; i--)
        {
            SquareRunnerHazard hazard = _hazards[i];
            if (hazard == null)
            {
                _hazards.RemoveAt(i);
                continue;
            }

            hazard.MoveLeft(movement);
            if (hazard.RightEdge < leftCullX)
            {
                Destroy(hazard.gameObject);
                _hazards.RemoveAt(i);
            }
        }
    }

    private void EnsureHazardsAhead()
    {
        float spawnHorizon = _halfWidth + SpawnBuffer;
        while (_spawnCursorX < spawnHorizon)
        {
            SpawnHazard(_spawnCursorX);
            _spawnCursorX += NextSpawnGap();
        }
    }

    private void SpawnHazard(float spawnX)
    {
        bool fromRoof = Random.value < Mathf.Lerp(0.32f, 0.48f, Mathf.Clamp01(_runTime / 35f));
        float height = fromRoof ? TopObstacleHeight : BottomObstacleHeight;
        float yCenter = fromRoof
            ? _roofBottom - (height * 0.5f)
            : _groundTop + (height * 0.5f);

        GameObject obstacleObject = new GameObject(fromRoof ? "Top Obstacle" : "Bottom Obstacle");
        obstacleObject.transform.SetParent(_worldRoot, false);

        SquareRunnerHazard hazard = obstacleObject.AddComponent<SquareRunnerHazard>();
        hazard.Initialize(
            GetSoftSquareSprite(),
            GetGlowSquareSprite(),
            new Vector2(ObstacleWidth, height),
            new Vector3(spawnX, yCenter, 0f));

        _hazards.Add(hazard);
    }

    private float NextSpawnGap()
    {
        float difficulty = Mathf.Clamp01(_runTime / 45f);
        float minGap = Mathf.Lerp(5.7f, 4.2f, difficulty);
        float maxGap = Mathf.Lerp(6.8f, 5.05f, difficulty);
        return Random.Range(minGap, maxGap);
    }

    private void UpdateScore()
    {
        int nextScore = Mathf.Max(0, Mathf.FloorToInt(_runDistance / ScoreDistanceStep));
        if (nextScore == _score)
        {
            return;
        }

        _score = nextScore;
        if (_score > _highestScore)
        {
            _highestScore = _score;
            SaveHighestScore();
        }

        RefreshTexts();
    }

    private void CheckHazardCollisions()
    {
        Rect playerBounds = _player.Bounds;
        for (int i = 0; i < _hazards.Count; i++)
        {
            if (_hazards[i] != null && _hazards[i].Overlaps(playerBounds))
            {
                FailRun();
                return;
            }
        }
    }

    private void FailRun()
    {
        if (_mode != GameMode.Running)
        {
            return;
        }

        _mode = GameMode.Failed;
        _deathBlend = 0f;
        _freezeBlend = 0f;
        _freezeCoolingDown = false;
        _player.TriggerFailure();

        if (_score > _highestScore)
        {
            _highestScore = _score;
            SaveHighestScore();
        }

        RefreshTexts();
    }

    private float CalculateSpeed(float runTime)
    {
        return BaseSpeed + (runTime * SpeedRamp) + (Mathf.Pow(runTime, 1.16f) * 0.08f);
    }

    private void RefreshTexts()
    {
        if (_scoreText != null)
        {
            _scoreText.text = _score.ToString();
        }

        if (_highestText != null)
        {
            _highestText.text = $"HIGHEST SCORE {_highestScore}";
        }

        if (_finalScoreText != null)
        {
            _finalScoreText.text = _score.ToString();
        }

        if (_finalHighestText != null)
        {
            _finalHighestText.text = $"HIGHEST SCORE {_highestScore}";
        }
    }

    private void UpdateInterface()
    {
        float startVisibility = _mode == GameMode.Starting ? EaseOut(_startBlend) : (_mode == GameMode.Running || _mode == GameMode.Failed ? 1f : 0f);
        float freezeWorldFade = 1f - (Mathf.SmoothStep(0f, 1f, _freezeBlend) * 0.96f);
        float overlayDim = Mathf.SmoothStep(0f, 1f, _freezeBlend) * 0.97f;

        float worldAlpha = startVisibility * freezeWorldFade;
        if (_mode == GameMode.Menu)
        {
            worldAlpha = 0f;
        }

        if (_settingsOpen || _restartConfirmOpen)
        {
            worldAlpha *= 0.48f;
        }

        SetRendererAlpha(_groundRenderer, worldAlpha);
        SetRendererAlpha(_roofRenderer, worldAlpha);

        for (int i = 0; i < _hazards.Count; i++)
        {
            if (_hazards[i] != null)
            {
                _hazards[i].SetAlpha(worldAlpha);
            }
        }

        float menuAlpha = _mode == GameMode.Menu ? 1f : 0f;
        float hudAlpha = (_mode == GameMode.Running || _mode == GameMode.Starting) ? 1f - (_freezeBlend * 0.9f) : 0f;
        float abilityAlpha = _mode == GameMode.Running ? 1f : 0f;
        float deathAlpha = _mode == GameMode.Failed ? EaseOut(_deathBlend) : 0f;

        SetCanvasGroup(_menuGroup, menuAlpha, _mode == GameMode.Menu && !_settingsOpen);
        SetCanvasGroup(_hudGroup, hudAlpha, _mode == GameMode.Running && !_settingsOpen && !_restartConfirmOpen);
        SetCanvasGroup(_abilityGroup, abilityAlpha, _mode == GameMode.Running && !_settingsOpen && !_restartConfirmOpen);
        SetCanvasGroup(_deathGroup, deathAlpha, _mode == GameMode.Failed && !_restartConfirmOpen);
        SetCanvasGroup(_settingsGroup, _settingsOpen ? 1f : 0f, _settingsOpen);
        SetCanvasGroup(_confirmGroup, _restartConfirmOpen ? 1f : 0f, _restartConfirmOpen);

        if (_freezeOverlay != null)
        {
            Color overlayColor = Color.black;
            overlayColor.a = overlayDim;
            _freezeOverlay.color = overlayColor;
        }

        if (_freezeBarFill != null)
        {
            RectTransform fillRect = _freezeBarFill.rectTransform;
            fillRect.sizeDelta = new Vector2(280f * Mathf.Clamp01(_freezeEnergy / FreezeMaxSeconds), 0f);
        }

        if (_failedText != null)
        {
            float deathGlow = _mode == GameMode.Failed ? (0.92f + (Mathf.Sin(Time.unscaledTime * 5f) * 0.04f)) : 1f;
            _failedText.color = new Color(1f, 1f, 1f, deathAlpha * deathGlow);
        }
    }

    private void SetSoundVolume(float value)
    {
        _soundVolume = value;
        AudioListener.volume = _soundVolume;
        PlayerPrefs.SetFloat(SoundVolumeKey, _soundVolume);
        PlayerPrefs.Save();
    }

    private void SetGlowOpacity(float value)
    {
        _glowOpacity = value;
        PlayerPrefs.SetFloat(GlowOpacityKey, _glowOpacity);
        PlayerPrefs.Save();
    }

    private bool WasJumpPressedThisFrame()
    {
        if (_mode != GameMode.Running || _settingsOpen || _restartConfirmOpen)
        {
            return false;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return !IsScreenPointOverUi(Mouse.current.position.ReadValue());
        }

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    return !IsScreenPointOverUi(touch.position.ReadValue());
                }
            }
        }

        return false;
    }

    private bool IsScreenPointOverUi(Vector2 screenPoint)
    {
        if (_raycaster == null || _eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(_eventSystem);
        pointerEventData.position = screenPoint;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        _raycaster.Raycast(pointerEventData, raycastResults);
        return raycastResults.Count > 0;
    }

    private void ClearHazards()
    {
        for (int i = _hazards.Count - 1; i >= 0; i--)
        {
            if (_hazards[i] != null)
            {
                Destroy(_hazards[i].gameObject);
            }
        }

        _hazards.Clear();
    }

    private SpriteRenderer CreateWorldStripe(string name, Vector2 size, Vector2 position, int sortingOrder)
    {
        GameObject stripe = new GameObject(name);
        stripe.transform.SetParent(_worldRoot, false);
        stripe.transform.localPosition = new Vector3(position.x, position.y, 0f);
        stripe.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = stripe.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSoftSquareSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;

        return renderer;
    }

    private static CanvasGroup CreateCanvasGroup(string name, Transform parent)
    {
        GameObject groupObject = new GameObject(name);
        groupObject.transform.SetParent(parent, false);
        RectTransform rectTransform = groupObject.AddComponent<RectTransform>();
        StretchRect(rectTransform);
        CanvasGroup canvasGroup = groupObject.AddComponent<CanvasGroup>();
        return canvasGroup;
    }

    private static void SetCanvasGroup(CanvasGroup group, float alpha, bool interactable)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;
    }

    private static void StretchRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void StretchRect(RectTransform rectTransform, Vector2 padding)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(padding.x, padding.y);
        rectTransform.offsetMax = new Vector2(-padding.x, -padding.y);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        imageObject.AddComponent<RectTransform>();
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Simple;
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        string textValue,
        Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = textValue;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void AddTextShadow(Text text, Color color, Vector2 distance)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    private static Button CreateTextButton(Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = GetSoftSquareSprite();
        image.color = new Color(1f, 1f, 1f, 0.1f);

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.05f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 1.06f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.selectedColor = new Color(1f, 1f, 1f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        Text text = CreateText(
            label + " Label",
            buttonObject.transform,
            font,
            34,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            label,
            Color.white);
        AddTextShadow(text, new Color(1f, 1f, 1f, 0.08f), new Vector2(0f, -2f));

        return button;
    }

    private static Button CreateIconButton(Transform parent, Sprite iconSprite, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject("Icon Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image background = buttonObject.AddComponent<Image>();
        background.sprite = GetSoftSquareSprite();
        background.color = new Color(1f, 1f, 1f, 0.08f);

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 1.05f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.8f);
        colors.selectedColor = new Color(1f, 1f, 1f, 1f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        Image icon = CreateImage("Icon", buttonObject.transform, iconSprite, Color.white);
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = size * 0.55f;

        return button;
    }

    private static Slider CreateSlider(Transform parent, Vector2 anchoredPosition, Vector2 size, float value)
    {
        GameObject root = new GameObject("Slider");
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = size;

        Slider slider = root.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        Image background = CreateImage("Background", root.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.12f));
        RectTransform backgroundRect = background.rectTransform;
        StretchRect(backgroundRect);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(root.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        StretchRect(fillAreaRect, new Vector2(8f, 6f));

        Image fill = CreateImage("Fill", fillArea.transform, GetSoftSquareSprite(), new Color(1f, 1f, 1f, 0.92f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleSlideArea = new GameObject("Handle Slide Area");
        handleSlideArea.transform.SetParent(root.transform, false);
        RectTransform handleAreaRect = handleSlideArea.AddComponent<RectTransform>();
        StretchRect(handleAreaRect);

        Image handle = CreateImage("Handle", handleSlideArea.transform, GetGlowSquareSprite(), Color.white);
        RectTransform handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(42f, 42f);

        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = handle;
        slider.handleRect = handleRect;
        return slider;
    }

    private static Image CreateDimmer(Transform parent)
    {
        Image dimmer = CreateImage("Dimmer", parent, GetWhiteSprite(), new Color(0f, 0f, 0f, 0.76f));
        StretchRect(dimmer.rectTransform);
        return dimmer;
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static Font GetBuiltinFont()
    {
        if (s_builtinFont == null)
        {
            s_builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return s_builtinFont;
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite == null)
        {
            s_whiteSprite = CreateGeneratedSprite(2, 2, (_, __) => 1f);
        }

        return s_whiteSprite;
    }

    private static Sprite GetSoftSquareSprite()
    {
        if (s_softSquareSprite == null)
        {
            s_softSquareSprite = CreateGeneratedSprite(128, 128, SoftSquareAlpha);
        }

        return s_softSquareSprite;
    }

    private static Sprite GetGlowSquareSprite()
    {
        if (s_glowSquareSprite == null)
        {
            s_glowSquareSprite = CreateGeneratedSprite(192, 192, GlowSquareAlpha);
        }

        return s_glowSquareSprite;
    }

    private static Sprite GetRetryIconSprite()
    {
        if (s_retryIconSprite == null)
        {
            s_retryIconSprite = CreateGeneratedSprite(128, 128, RetryIconAlpha);
        }

        return s_retryIconSprite;
    }

    private static Sprite CreateGeneratedSprite(int width, int height, System.Func<float, float, float> alphaFunction)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = ((x + 0.5f) / width * 2f) - 1f;
                float py = ((y + 0.5f) / height * 2f) - 1f;
                byte alpha = (byte)(Mathf.Clamp01(alphaFunction(px, py)) * 255f);
                pixels[(y * width) + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static float SoftSquareAlpha(float x, float y)
    {
        float distance = RoundedSquareSignedDistance(new Vector2(x, y), new Vector2(0.56f, 0.56f), 0.28f);
        return 1f - Mathf.SmoothStep(-0.12f, 0.08f, distance);
    }

    private static float GlowSquareAlpha(float x, float y)
    {
        float distance = RoundedSquareSignedDistance(new Vector2(x, y), new Vector2(0.44f, 0.44f), 0.34f);
        return 1f - Mathf.SmoothStep(-0.34f, 0.54f, distance);
    }

    private static float RetryIconAlpha(float x, float y)
    {
        Vector2 point = new Vector2(x, y);
        float radius = point.magnitude;
        float angle = Mathf.Atan2(point.y, point.x);

        while (angle < 0f)
        {
            angle += Mathf.PI * 2f;
        }

        bool arcRange = angle > 0.55f && angle < 5.2f;
        float arcDistance = Mathf.Abs(radius - 0.55f);
        float arcAlpha = arcRange ? 1f - Mathf.SmoothStep(0.05f, 0.16f, arcDistance) : 0f;

        Vector2 arrowPoint = new Vector2(-0.28f, 0.72f);
        Vector2 dir = (point - arrowPoint).normalized;
        float arrowA = 1f - Mathf.SmoothStep(0.02f, 0.13f, Vector2.Distance(point, arrowPoint + (new Vector2(-0.18f, -0.08f) * 0.8f)));
        float arrowB = 1f - Mathf.SmoothStep(0.02f, 0.13f, Vector2.Distance(point, arrowPoint + (new Vector2(0.02f, -0.2f) * 0.8f)));
        float arrowHead = Mathf.Max(arrowA, arrowB) * Mathf.Clamp01(Vector2.Dot(dir, new Vector2(-0.35f, -0.94f)));

        return Mathf.Clamp01(Mathf.Max(arcAlpha, arrowHead));
    }

    private static float RoundedSquareSignedDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - halfSize + new Vector2(radius, radius);
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        return outside + inside - radius;
    }
}

public sealed class SquareRunnerPlayer : MonoBehaviour
{
    private readonly List<SpriteRenderer> _trailRenderers = new List<SpriteRenderer>();

    private SpriteRenderer _coreRenderer;
    private SpriteRenderer _innerGlowRenderer;
    private SpriteRenderer _outerGlowRenderer;
    private float _visualSize;
    private float _collisionHalfExtent;
    private float _velocityY;
    private float _rotationCurrent;
    private float _rotationTarget;
    private float _landJellyTimer;
    private float _landJellyStrength;
    private float _failurePulse;
    private bool _grounded;
    private bool _failed;

    public Rect Bounds
    {
        get
        {
            Vector3 position = transform.position;
            return new Rect(
                position.x - _collisionHalfExtent,
                position.y - _collisionHalfExtent,
                _collisionHalfExtent * 2f,
                _collisionHalfExtent * 2f);
        }
    }

    public void Initialize(Sprite coreSprite, Sprite glowSprite, float visualSize, float collisionHalfExtent, Vector3 startPosition)
    {
        _visualSize = visualSize;
        _collisionHalfExtent = collisionHalfExtent;

        for (int i = 0; i < 5; i++)
        {
            SpriteRenderer trail = CreateLayer("Trail " + i, glowSprite, 8 + i, new Color(1f, 1f, 1f, 0f));
            trail.transform.localScale = Vector3.one * (0.9f - (i * 0.08f));
            _trailRenderers.Add(trail);
        }

        _outerGlowRenderer = CreateLayer("Outer Glow", glowSprite, 16, new Color(1f, 1f, 1f, 0.16f));
        _innerGlowRenderer = CreateLayer("Inner Glow", glowSprite, 17, new Color(1f, 1f, 1f, 0.25f));
        _coreRenderer = CreateLayer("Core", coreSprite, 18, Color.white);

        ResetTo(startPosition);
    }

    public void ResetTo(Vector3 position)
    {
        transform.position = position;
        _velocityY = 0f;
        _rotationCurrent = 0f;
        _rotationTarget = 0f;
        _grounded = true;
        _failed = false;
        _landJellyTimer = 0f;
        _landJellyStrength = 0f;
        _failurePulse = 0f;

        for (int i = 0; i < _trailRenderers.Count; i++)
        {
            _trailRenderers[i].transform.position = position;
            _trailRenderers[i].color = new Color(1f, 1f, 1f, 0f);
        }

        transform.rotation = Quaternion.identity;
    }

    public void TryJump(float jumpVelocity)
    {
        if (!_grounded || _failed)
        {
            return;
        }

        _grounded = false;
        _velocityY = jumpVelocity;
        _rotationTarget -= 90f;
    }

    public void TriggerFailure()
    {
        _failed = true;
        _velocityY = 0f;
        _failurePulse = 0f;
    }

    public void TickMotion(float deltaTime, float gravity, float groundTop, float roofBottom)
    {
        if (_failed || deltaTime <= 0f)
        {
            return;
        }

        Vector3 position = transform.position;
        bool wasGrounded = _grounded;

        _velocityY += gravity * deltaTime;
        position.y += _velocityY * deltaTime;

        if (position.y - _collisionHalfExtent <= groundTop)
        {
            if (!wasGrounded)
            {
                _landJellyTimer = 0.2f;
                _landJellyStrength = Mathf.Clamp01(Mathf.Abs(_velocityY) / 13f);
            }

            position.y = groundTop + _collisionHalfExtent;
            _velocityY = 0f;
            _grounded = true;
        }
        else
        {
            _grounded = false;
        }

        if (position.y + _collisionHalfExtent >= roofBottom)
        {
            position.y = roofBottom - _collisionHalfExtent;
            if (_velocityY > 0f)
            {
                _velocityY = 0f;
            }
        }

        transform.position = position;
    }

    public void TickPresentation(float deltaTime, float freezeBlend, float glowOpacity, bool failed, float visibility, float deathBlend)
    {
        if (_landJellyTimer > 0f)
        {
            _landJellyTimer = Mathf.Max(0f, _landJellyTimer - deltaTime);
        }

        if (failed)
        {
            _failurePulse += deltaTime;
        }

        _rotationCurrent = Mathf.LerpAngle(_rotationCurrent, _rotationTarget, 1f - Mathf.Exp(-12f * deltaTime));
        transform.rotation = Quaternion.Euler(0f, 0f, _rotationCurrent);

        float airStretch = !_grounded ? Mathf.Clamp(_velocityY * 0.018f, -0.14f, 0.1f) : 0f;
        float jelly = _landJellyTimer > 0f ? Mathf.Sin((1f - (_landJellyTimer / 0.2f)) * Mathf.PI) * _landJellyStrength : 0f;
        float failureScale = failed ? 1f + (Mathf.Sin(_failurePulse * 7f) * 0.03f * deathBlend) : 1f;

        float scaleX = (1f + jelly * 0.18f - airStretch) * visibility * failureScale;
        float scaleY = (1f - jelly * 0.16f + airStretch) * visibility * failureScale;
        float finalSize = _visualSize;

        _coreRenderer.transform.localScale = new Vector3(finalSize * scaleX, finalSize * scaleY, 1f);

        float glowBoost = 1f + (Mathf.Sin(Time.unscaledTime * 4.2f) * 0.03f) + (freezeBlend * 0.12f);
        _innerGlowRenderer.transform.localScale = new Vector3(finalSize * 1.45f * scaleX * glowBoost, finalSize * 1.45f * scaleY * glowBoost, 1f);
        _outerGlowRenderer.transform.localScale = new Vector3(finalSize * 1.95f * scaleX * glowBoost, finalSize * 1.95f * scaleY * glowBoost, 1f);

        float coreAlpha = visibility;
        float innerAlpha = glowOpacity * 0.38f * visibility;
        float outerAlpha = glowOpacity * 0.18f * visibility;
        if (failed)
        {
            coreAlpha *= Mathf.Lerp(1f, 0.9f, deathBlend);
            innerAlpha *= Mathf.Lerp(1f, 0.72f, deathBlend);
            outerAlpha *= Mathf.Lerp(1f, 0.65f, deathBlend);
        }

        _coreRenderer.color = new Color(1f, 1f, 1f, coreAlpha);
        _innerGlowRenderer.color = new Color(1f, 1f, 1f, innerAlpha);
        _outerGlowRenderer.color = new Color(1f, 1f, 1f, outerAlpha);

        UpdateTrail(deltaTime, glowOpacity, visibility);
    }

    private void UpdateTrail(float deltaTime, float glowOpacity, float visibility)
    {
        if (_trailRenderers.Count == 0)
        {
            return;
        }

        Vector3 targetPosition = transform.position;
        float targetRotation = _rotationCurrent;

        for (int i = 0; i < _trailRenderers.Count; i++)
        {
            SpriteRenderer trail = _trailRenderers[i];
            trail.transform.position = Vector3.Lerp(trail.transform.position, targetPosition, 1f - Mathf.Exp(-(10f - i) * deltaTime));
            trail.transform.rotation = Quaternion.Lerp(trail.transform.rotation, Quaternion.Euler(0f, 0f, targetRotation), 1f - Mathf.Exp(-(8f - (i * 0.8f)) * deltaTime));

            float alpha = glowOpacity * 0.16f * (1f - (i * 0.18f)) * visibility;
            trail.color = new Color(1f, 1f, 1f, alpha);

            targetPosition = trail.transform.position;
            targetRotation = trail.transform.rotation.eulerAngles.z;
        }
    }

    private SpriteRenderer CreateLayer(string name, Sprite sprite, int sortingOrder, Color color)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(transform, false);
        layer.transform.localPosition = Vector3.zero;

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }
}

public sealed class SquareRunnerHazard : MonoBehaviour
{
    private SpriteRenderer _coreRenderer;
    private SpriteRenderer _glowRenderer;
    private float _width;
    private float _height;

    public float RightEdge => transform.position.x + (_width * 0.5f);

    public void Initialize(Sprite coreSprite, Sprite glowSprite, Vector2 size, Vector3 position)
    {
        _width = size.x;
        _height = size.y;
        transform.position = position;

        _glowRenderer = CreateLayer("Glow", glowSprite, new Color(1f, 1f, 1f, 0.08f), 9, new Vector3(_width * 2.35f, _height * 1.14f, 1f));
        _coreRenderer = CreateLayer("Core", coreSprite, Color.white, 10, new Vector3(_width, _height, 1f));
    }

    public void MoveLeft(float distance)
    {
        transform.position += Vector3.left * distance;
    }

    public bool Overlaps(Rect rect)
    {
        float left = transform.position.x - (_width * 0.5f);
        float right = transform.position.x + (_width * 0.5f);
        float bottom = transform.position.y - (_height * 0.5f);
        float top = transform.position.y + (_height * 0.5f);

        return rect.xMin < right && rect.xMax > left && rect.yMin < top && rect.yMax > bottom;
    }

    public void SetAlpha(float alpha)
    {
        if (_coreRenderer != null)
        {
            _coreRenderer.color = new Color(1f, 1f, 1f, alpha);
        }

        if (_glowRenderer != null)
        {
            _glowRenderer.color = new Color(1f, 1f, 1f, alpha * 0.2f);
        }
    }

    private SpriteRenderer CreateLayer(string name, Sprite sprite, Color color, int sortingOrder, Vector3 scale)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(transform, false);
        layer.transform.localScale = scale;

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }
}

public sealed class SquareRunnerHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsHeld { get; private set; }
    public float HeldDuration { get; private set; }

    private void Update()
    {
        if (IsHeld)
        {
            HeldDuration += Time.unscaledDeltaTime;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
        HeldDuration = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
        HeldDuration = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerDrag == gameObject || eventData.pointerPress == gameObject)
        {
            return;
        }

        IsHeld = false;
        HeldDuration = 0f;
    }
}
