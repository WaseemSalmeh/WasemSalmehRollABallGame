using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class MainMenuController : MonoBehaviour
{
    private const string ThemeName = "Sunlit Garden Arcade theme";
    private const string CountTextName = "Count Text";
    private const string WinTextName = "Win Text";
    private const string GroundObjectName = "Ground";

    private readonly List<FloatingDecoration> floatingDecorations = new List<FloatingDecoration>();

    private Camera mainCamera;
    private CameraController cameraController;
    private PlayerController playerController;
    private PlayerFallReset playerFallReset;
    private PlayerInput playerInput;
    private Rigidbody playerRigidbody;
    private EnemyMovement[] enemies;
    private GameObject countTextObject;
    private GameObject winTextObject;
    private RectTransform menuRoot;
    private CanvasGroup menuCanvasGroup;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private TMP_FontAsset fontAsset;
    private Material titleMaterial;
    private Button startButton;
    private Button homeButton;
    private CanvasGroup gameplayHudCanvasGroup;
    private Image transitionOverlay;
    private Vector3 gameplayCameraPosition;
    private Quaternion gameplayCameraRotation;
    private float gameplayFieldOfView;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;
    private Vector3 orbitTarget;
    private float orbitAngle;
    private float orbitRadius;
    private bool menuVisible;
    private bool isTransitioning;

    private sealed class FloatingDecoration
    {
        public FloatingDecoration(RectTransform rectTransform, Vector2 basePosition, float driftAmount, float speed)
        {
            RectTransform = rectTransform;
            BasePosition = basePosition;
            DriftAmount = driftAmount;
            Speed = speed;
        }

        public RectTransform RectTransform { get; }
        public Vector2 BasePosition { get; }
        public float DriftAmount { get; }
        public float Speed { get; }
    }

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasScaler = GetComponent<CanvasScaler>();

        CacheReferences();
        CaptureGameplayState();
        ConfigureCanvasScaler();
        BuildMenu();
        SetMenuVisible(true);
        SetGameplayActive(false);
        ApplyMenuCameraPose(true);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!menuVisible || isTransitioning)
        {
            return;
        }

        orbitAngle += Time.unscaledDeltaTime * 10f;
        ApplyMenuCameraPose(false);
        AnimateDecorations();

        if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            StartGame();
        }
    }

    private void CacheReferences()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraController = mainCamera.GetComponent<CameraController>();
        }

        playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerInput = playerController.GetComponent<PlayerInput>();
            playerFallReset = playerController.GetComponent<PlayerFallReset>();
            playerRigidbody = playerController.GetComponent<Rigidbody>();
        }

        enemies = FindObjectsByType<EnemyMovement>();
        countTextObject = GameObject.Find(CountTextName);
        winTextObject = GameObject.Find(WinTextName);
        fontAsset = TMP_Settings.defaultFontAsset;

        if (fontAsset == null)
        {
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        titleMaterial = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");
    }

    private void CaptureGameplayState()
    {
        if (mainCamera != null)
        {
            gameplayCameraPosition = mainCamera.transform.position;
            gameplayCameraRotation = mainCamera.transform.rotation;
            gameplayFieldOfView = mainCamera.fieldOfView;
        }

        if (playerController != null)
        {
            playerStartPosition = playerController.transform.position;
            playerStartRotation = playerController.transform.rotation;
        }

        orbitTarget = ResolveOrbitTarget();
        orbitRadius = ResolveOrbitRadius();
        orbitAngle = 32f;
    }

    private void ConfigureCanvasScaler()
    {
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildMenu()
    {
        if (menuRoot != null)
        {
            return;
        }

        menuRoot = CreateRect("Main Menu Root", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
        menuRoot.SetAsLastSibling();
        menuCanvasGroup = menuRoot.gameObject.AddComponent<CanvasGroup>();

        Image tint = CreateImage("World Tint", menuRoot, new Color(0.05f, 0.10f, 0.06f, 0.54f));
        Stretch(tint.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform panelShadow = CreateRect("Menu Panel Shadow", menuRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(88f, -12f), new Vector2(660f, 900f), new Vector2(0f, 0.5f));
        Image panelShadowImage = panelShadow.gameObject.AddComponent<Image>();
        panelShadowImage.color = new Color(0f, 0f, 0f, 0.24f);

        RectTransform mainPanel = CreateRect("Menu Panel", menuRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(640f, 900f), new Vector2(0f, 0.5f));
        Image panelImage = mainPanel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.09f, 0.19f, 0.12f, 0.92f);
        Outline panelOutline = mainPanel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.89f, 0.78f, 0.47f, 0.60f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform accentBar = CreateRect("Accent Bar", mainPanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(14f, 0f), new Vector2(0f, 0.5f));
        Image accentImage = accentBar.gameObject.AddComponent<Image>();
        accentImage.color = new Color(0.93f, 0.78f, 0.28f, 1f);

        RectTransform themePill = CreateRect("Theme Pill", mainPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -34f), new Vector2(330f, 44f), new Vector2(0f, 1f));
        Image themePillImage = themePill.gameObject.AddComponent<Image>();
        themePillImage.color = new Color(0.89f, 0.77f, 0.40f, 0.95f);
        CreateText("Theme Text", themePill, ThemeName, 22f, FontStyles.Bold, new Color(0.16f, 0.18f, 0.09f, 1f), TextAlignmentOptions.Center, new Vector2(8f, -4f));

        RectTransform title = CreateRect("Title", mainPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f, 96f), new Vector2(560f, 330f), new Vector2(0.5f, 0.5f));
        TMP_Text titleText = CreateText("Title Text", title, "ROLL\nA\nBALL", 100f, FontStyles.Bold, new Color(0.98f, 0.95f, 0.86f, 1f), TextAlignmentOptions.Center, Vector2.zero, 10f);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 56f;
        titleText.fontSizeMax = 100f;
        titleText.lineSpacing = -22f;
        if (titleMaterial != null)
        {
            titleText.fontSharedMaterial = titleMaterial;
        }
        Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        titleShadow.effectDistance = new Vector2(4f, -4f);

        startButton = CreateButton(mainPanel, "Start Game", new Vector2(40f, 88f), new Vector2(250f, 74f), new Color(0.94f, 0.78f, 0.29f, 1f), new Color(0.18f, 0.19f, 0.08f, 1f));
        startButton.onClick.AddListener(StartGame);

        Button quitButton = CreateButton(mainPanel, "Quit", new Vector2(308f, 88f), new Vector2(180f, 74f), new Color(0.20f, 0.35f, 0.24f, 0.95f), new Color(0.96f, 0.95f, 0.90f, 1f));
        quitButton.onClick.AddListener(QuitGame);

        RectTransform controls = CreateRect("Controls Card", menuRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-72f, 72f), new Vector2(420f, 228f), new Vector2(1f, 0f));
        Image controlsImage = controls.gameObject.AddComponent<Image>();
        controlsImage.color = new Color(0.10f, 0.18f, 0.11f, 0.80f);
        Outline controlsOutline = controls.gameObject.AddComponent<Outline>();
        controlsOutline.effectColor = new Color(0.93f, 0.78f, 0.28f, 0.32f);
        controlsOutline.effectDistance = new Vector2(1f, -1f);
        CreateText("Controls Header", controls, "HOW TO PLAY", 22f, FontStyles.Bold, new Color(0.96f, 0.83f, 0.42f, 1f), TextAlignmentOptions.TopLeft, new Vector2(22f, -20f), 80f);
        CreateText(
            "Controls Body",
            controls,
            "Move: WASD\nJump: Space\nGoal: collect 11 pickups and stay above the ground.",
            24f,
            FontStyles.Normal,
            new Color(0.90f, 0.95f, 0.89f, 0.96f),
            TextAlignmentOptions.TopLeft,
            new Vector2(22f, -62f));

        RectTransform gameplayHud = CreateRect("Gameplay Hud", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
        gameplayHudCanvasGroup = gameplayHud.gameObject.AddComponent<CanvasGroup>();
        gameplayHudCanvasGroup.alpha = 0f;
        gameplayHudCanvasGroup.interactable = false;
        gameplayHudCanvasGroup.blocksRaycasts = false;

        homeButton = CreateHomeButton(gameplayHud);
        homeButton.onClick.AddListener(ReturnToMenu);

        transitionOverlay = CreateImage("Transition Overlay", transform as RectTransform, new Color(0f, 0f, 0f, 0f));
        transitionOverlay.raycastTarget = false;
        Stretch(transitionOverlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        transitionOverlay.transform.SetAsLastSibling();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
    }

    private void SetMenuVisible(bool visible)
    {
        menuVisible = visible;
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = visible ? 1f : 0f;
            menuCanvasGroup.interactable = visible;
            menuCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void SetGameplayActive(bool active)
    {
        Time.timeScale = active ? 1f : 0f;

        if (playerInput != null)
        {
            playerInput.enabled = active;
        }

        if (playerController != null)
        {
            playerController.enabled = active;
        }

        if (playerFallReset != null)
        {
            playerFallReset.enabled = active;
        }

        if (cameraController != null)
        {
            cameraController.enabled = active;
        }

        foreach (EnemyMovement enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.enabled = active;
            }
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.isKinematic = !active;
        }

        if (countTextObject != null)
        {
            countTextObject.SetActive(active);
        }

        if (winTextObject != null)
        {
            winTextObject.SetActive(false);
        }

        SetGameplayHudVisible(active);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StartGame()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(StartGameTransition());
    }

    private void ReturnToMenu()
    {
        if (isTransitioning || menuVisible)
        {
            return;
        }

        StartCoroutine(ReturnToMenuTransition());
    }

    private IEnumerator StartGameTransition()
    {
        isTransitioning = true;

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        if (startButton != null)
        {
            startButton.interactable = false;
        }

        if (transitionOverlay != null)
        {
            transitionOverlay.raycastTarget = true;
        }

        yield return FadeOverlay(0f, 1f, 0.28f, fadeMenu: true);

        ResetGameplayState();

        if (mainCamera != null)
        {
            mainCamera.transform.SetPositionAndRotation(gameplayCameraPosition, gameplayCameraRotation);
            mainCamera.fieldOfView = gameplayFieldOfView;
        }

        SetMenuVisible(false);
        SetGameplayActive(true);

        yield return FadeOverlay(1f, 0f, 0.38f, fadeMenu: false);

        if (transitionOverlay != null)
        {
            transitionOverlay.color = new Color(0f, 0f, 0f, 0f);
            transitionOverlay.raycastTarget = false;
        }

        if (startButton != null)
        {
            startButton.interactable = true;
        }

        isTransitioning = false;
    }

    private IEnumerator ReturnToMenuTransition()
    {
        isTransitioning = true;

        if (homeButton != null)
        {
            homeButton.interactable = false;
        }

        if (transitionOverlay != null)
        {
            transitionOverlay.raycastTarget = true;
        }

        yield return FadeOverlay(0f, 1f, 0.24f, fadeMenu: false);

        ResetGameplayState();
        SetGameplayActive(false);
        SetMenuVisible(true);
        ApplyMenuCameraPose(true);

        if (startButton != null)
        {
            startButton.interactable = true;
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
        }

        if (EventSystem.current != null && startButton != null)
        {
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        yield return FadeOverlay(1f, 0f, 0.32f, fadeMenu: false);

        if (transitionOverlay != null)
        {
            transitionOverlay.color = new Color(0f, 0f, 0f, 0f);
            transitionOverlay.raycastTarget = false;
        }

        isTransitioning = false;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ApplyMenuCameraPose(bool snap)
    {
        if (mainCamera == null)
        {
            return;
        }

        float currentRadius = Mathf.Max(14f, orbitRadius);
        Vector3 orbitOffset = Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(currentRadius, 0f, -currentRadius * 0.38f);
        Vector3 position = orbitTarget + orbitOffset + Vector3.up * (currentRadius * 0.34f);

        if (snap)
        {
            mainCamera.transform.position = position;
            mainCamera.transform.rotation = Quaternion.LookRotation((orbitTarget + Vector3.up * 2.8f) - position);
        }
        else
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, position, Time.unscaledDeltaTime * 2.6f);
            Quaternion targetRotation = Quaternion.LookRotation((orbitTarget + Vector3.up * 2.8f) - mainCamera.transform.position);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetRotation, Time.unscaledDeltaTime * 2.6f);
        }

        mainCamera.fieldOfView = 48f;
    }

    private void AnimateDecorations()
    {
        float time = Time.unscaledTime;
        foreach (FloatingDecoration decoration in floatingDecorations)
        {
            if (decoration.RectTransform == null)
            {
                continue;
            }

            float offsetY = Mathf.Sin(time * decoration.Speed) * decoration.DriftAmount;
            decoration.RectTransform.anchoredPosition = decoration.BasePosition + new Vector2(0f, offsetY);
        }
    }

    private Vector3 ResolveOrbitTarget()
    {
        GameObject groundObject = GameObject.Find(GroundObjectName);
        if (groundObject != null && groundObject.TryGetComponent(out Collider groundCollider))
        {
            return groundCollider.bounds.center + new Vector3(0f, 0f, -2f);
        }

        return playerController != null ? playerController.transform.position : Vector3.zero;
    }

    private float ResolveOrbitRadius()
    {
        GameObject groundObject = GameObject.Find(GroundObjectName);
        if (groundObject != null && groundObject.TryGetComponent(out Collider groundCollider))
        {
            float extent = Mathf.Max(groundCollider.bounds.extents.x, groundCollider.bounds.extents.z);
            return Mathf.Clamp(extent * 1.05f, 16f, 34f);
        }

        return 18f;
    }

    private void ResetGameplayState()
    {
        if (playerFallReset != null)
        {
            playerFallReset.ResetLevelState();
            return;
        }

        if (playerController != null)
        {
            playerController.transform.SetPositionAndRotation(playerStartPosition, playerStartRotation);
            playerController.ResetRun();
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.position = playerStartPosition;
            playerRigidbody.rotation = playerStartRotation;
            Physics.SyncTransforms();
        }
    }

    private void SetGameplayHudVisible(bool visible)
    {
        if (gameplayHudCanvasGroup == null)
        {
            return;
        }

        gameplayHudCanvasGroup.alpha = visible ? 1f : 0f;
        gameplayHudCanvasGroup.interactable = visible;
        gameplayHudCanvasGroup.blocksRaycasts = visible;

        if (homeButton != null)
        {
            homeButton.interactable = visible;
        }
    }

    private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration, bool fadeMenu)
    {
        if (transitionOverlay == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float overlayAlpha = Mathf.Lerp(fromAlpha, toAlpha, eased);

            transitionOverlay.color = new Color(0f, 0f, 0f, overlayAlpha);

            if (fadeMenu && menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 1f - eased;
            }

            yield return null;
        }

        transitionOverlay.color = new Color(0f, 0f, 0f, toAlpha);

        if (fadeMenu && menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
        }
    }

    private void CreateDecoration(RectTransform parent, Vector2 anchor, Vector2 size, Color color, float rotationZ, float speed, float driftAmount)
    {
        RectTransform decoration = CreateRect("Decoration", parent, anchor, anchor, Vector2.zero, size, anchor);
        Image image = decoration.gameObject.AddComponent<Image>();
        image.color = color;
        decoration.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        floatingDecorations.Add(new FloatingDecoration(decoration, decoration.anchoredPosition, driftAmount, speed));
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Color fillColor, Color textColor)
    {
        RectTransform buttonRect = CreateRect(label + " Button", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), anchoredPosition, size, new Vector2(0f, 0f));
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = fillColor;

        Outline outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.24f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonRect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = fillColor;
        colors.highlightedColor = Color.Lerp(fillColor, Color.white, 0.10f);
        colors.pressedColor = Color.Lerp(fillColor, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(fillColor.r, fillColor.g, fillColor.b, 0.45f);
        button.colors = colors;

        TMP_Text text = CreateText(label + " Label", buttonRect, label, 28f, FontStyles.Bold, textColor, TextAlignmentOptions.Center, Vector2.zero, 8f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 28f;

        return button;
    }

    private Button CreateHomeButton(Transform parent)
    {
        RectTransform buttonRect = CreateRect("Home Button", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(72f, 72f), new Vector2(1f, 1f));
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.10f, 0.18f, 0.11f, 0.90f);

        Outline outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.93f, 0.78f, 0.28f, 0.40f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonRect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonImage.color;
        colors.highlightedColor = Color.Lerp(buttonImage.color, Color.white, 0.10f);
        colors.pressedColor = Color.Lerp(buttonImage.color, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(buttonImage.color.r, buttonImage.color.g, buttonImage.color.b, 0.35f);
        button.colors = colors;

        CreateIconShape(buttonRect, "Home Roof", new Vector2(0f, 10f), new Vector2(24f, 24f), new Color(0.98f, 0.95f, 0.86f, 1f), 45f);
        CreateIconShape(buttonRect, "Home Body", new Vector2(0f, -4f), new Vector2(28f, 20f), new Color(0.98f, 0.95f, 0.86f, 1f));
        CreateIconShape(buttonRect, "Home Door", new Vector2(0f, -9f), new Vector2(8f, 12f), new Color(0.10f, 0.18f, 0.11f, 1f));
        CreateIconShape(buttonRect, "Home Chimney", new Vector2(12f, 8f), new Vector2(5f, 10f), new Color(0.98f, 0.95f, 0.86f, 1f));

        return button;
    }

    private static void CreateIconShape(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color, float rotationZ = 0f)
    {
        RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size, new Vector2(0.5f, 0.5f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
    }

    private TMP_Text CreateText(string name, Transform parent, string content, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment, Vector2 anchoredPosition, float characterSpacing = 0f)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.characterSpacing = characterSpacing;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        rect.offsetMin = new Vector2(Mathf.Max(anchoredPosition.x, 0f), 0f);
        rect.offsetMax = new Vector2(0f, Mathf.Min(anchoredPosition.y, 0f));
        return text;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.pivot = pivot;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
