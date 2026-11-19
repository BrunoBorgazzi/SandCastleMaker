using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelUIController : MonoBehaviour
{
    [Header("End Game Panel")]
    public GameObject endGamePanel;
    public TextMeshProUGUI scoreText; 
    public Button returnToMenuButton;
    public Button retryButton;
    public Button nextLevelButton;

    [Header("Mobile Controls")]
    public Button toggleSandButton;
    public Image sandButtonImage;
    public Sprite drySandSprite;
    public Sprite wetSandSprite;

    [Header("Plano Objetivo (Hoja Arrugada Deslizable)")]
    public Sprite blueprintButtonSprite;
    public Sprite blueprintSheetSprite;
    public Button toggleBlueprintButton;
    public RectTransform blueprintSheetPanel;
    public Image blueprintTargetCastleImage;

    [Header("Live Gameplay HUD")]
    [Tooltip("Texto en vivo con la exactitud del castillo y la arena húmeda restante")]
    public TextMeshProUGUI liveHudText;

    private bool _isBlueprintOpen = false;
    private float _blueprintTargetY = -2600f;
    private Image _blueprintSheetVisualImage;
    private RectTransform _hudContainer;
    private const float BLUEPRINT_OPEN_Y = 0f;
    private const float DEFAULT_BLUEPRINT_CLOSED_Y = -2600f;
    private const float ENDGAME_PANEL_ASPECT = 720f / 1080f;

    [Header("Theme & EndGame Visuals")]
    public Sprite starGoldSprite;
    public Sprite starEmptySprite;

    private string _appliedThemeId;
    private GameObject _blueprintDimmerGO;
    private Button _blueprintCloseXButton;
    private TextMeshProUGUI _blueprintCaptionText;
    private Image _tutorialPreviewMiniImage;
    private GameObject _tutorialStepOverlayGO;
    private Coroutine _initialFlowCoroutine;
    private Coroutine _arrowBobCoroutine;
    private GameObject _pauseModalCardGO;
    private GameObject _endGameDimmerGO;
    private Image _endGameBannerImage;
    private TextMeshProUGUI _statPrecisionValueText;
    private TextMeshProUGUI _statPrecisionSubText;
    private TextMeshProUGUI _statSecondTitleText;
    private TextMeshProUGUI _statSecondValueText;
    private TextMeshProUGUI _statSecondSubText;
    private TextMeshProUGUI _hudCastlePctText;
    private Image _hudCastleFillBar;
    private Image _hudTideFillBar;
    private TextMeshProUGUI _hudTideStatusText;
    private TextMeshProUGUI _hudTimerText;
    private Sprite _retryOrangeSprite;
    private Sprite _retryRedSprite;
    private Sprite _blueprintTargetMapPropSprite;
    private Sprite _tutorialStep1Sprite;
    private Sprite _tutorialStep2Sprite;
    private int _tutorialStepMode = 0; // 0 = normal target map, 1 = tutorial draw, 2 = tutorial wet sand
    private bool _waitingModalClose = false;
    private bool _waitingBucketTap = false;
    private readonly Image[] _starBgImages = new Image[3];
    private readonly Image[] _starFillImages = new Image[3];
    private Sprite _victoryBannerSprite;
    private Sprite _defeatBannerSprite;
    private Sprite _unifiedVictoryPanelSprite;
    private Sprite _unifiedDefeatPanelSprite;
    private Sprite _starGoldSprite;
    private Sprite _starEmptySprite;
    private Coroutine _starsAnimCoroutine;
    private Coroutine _bucketPunchCoroutine;
    private bool _hasRenderedSandState = false;
    private bool _lastRenderedWetSand = false;

    private void Start()
    {
        EnsureResponsiveGameplayUI();

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }
        if (retryButton != null)
        {
            if (retryButton.image != null) _retryOrangeSprite = retryButton.image.sprite;
            retryButton.onClick.AddListener(OnRetryClicked);
        }
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

        if (toggleSandButton != null)
        {
            toggleSandButton.onClick.AddListener(OnToggleSandButtonClicked);
        }

        EnsureLiveHudBadge();
        EnsureBlueprintSystemUI();
        EnsurePausePanelLayout();
        ApplyRegionTheme();
        EnsureEndGamePanelLayout();
        if (_endGameDimmerGO != null) _endGameDimmerGO.SetActive(false);
        if (endGamePanel != null) endGamePanel.SetActive(false);
        UpdateSandButtonVisual();

        // Nivel 1: tutorial rejugable paso a paso. Niveles 2+: plano objetivo pausado hasta tocar la X.
        _initialFlowCoroutine = StartCoroutine(StartInitialLevelFlowRoutine());
    }

    private void EnsureResponsiveGameplayUI()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        Transform bgT = rootCanvas.transform.Find("Fondo");
        if (bgT != null)
        {
            AspectCoverImage.Attach(bgT.gameObject);
        }

        _hudContainer = SafeAreaFitter.EnsureSafeAreaContainer(
            rootCanvas.transform,
            "SafeArea_HUD",
            "Fondo",
            "PauseMenu_Manager"
        );

        Transform hudParent = _hudContainer != null ? _hudContainer : rootCanvas.transform;

        // Mantener LevelUI estirado en todo el área segura
        RectTransform ownRt = transform as RectTransform;
        if (ownRt != null)
        {
            ownRt.anchorMin = Vector2.zero;
            ownRt.anchorMax = Vector2.one;
            ownRt.offsetMin = Vector2.zero;
            ownRt.offsetMax = Vector2.zero;
            ownRt.pivot = new Vector2(0.5f, 0.5f);
        }

        // Botón de Pausa arriba a la derecha dentro del SafeArea
        Transform pauseBtnT = SafeAreaFitter.FindInScope(rootCanvas.transform, "Btn_AbrirPausa", "SafeArea_HUD");
        if (pauseBtnT != null)
        {
            if (pauseBtnT.parent != hudParent) pauseBtnT.SetParent(hudParent, false);
            RectTransform prt = pauseBtnT.GetComponent<RectTransform>();
            if (prt != null)
            {
                prt.anchorMin = new Vector2(1f, 1f);
                prt.anchorMax = new Vector2(1f, 1f);
                prt.pivot = new Vector2(1f, 1f);
                prt.anchoredPosition = new Vector2(-24f, -24f);
                prt.sizeDelta = new Vector2(145f, 145f);
            }
            Image pImg = pauseBtnT.GetComponent<Image>();
            if (pImg != null) pImg.preserveAspect = true;
        }

        // Setup inferior derecho: Balde interactivo debajo de la mesa de esculpido
        if (toggleSandButton != null)
        {
            if (toggleSandButton.transform.parent != hudParent)
            {
                toggleSandButton.transform.SetParent(hudParent, false);
            }
            RectTransform srt = toggleSandButton.GetComponent<RectTransform>();
            if (srt != null)
            {
                srt.anchorMin = new Vector2(1f, 0f);
                srt.anchorMax = new Vector2(1f, 0f);
                srt.pivot = new Vector2(1f, 0f);
                srt.anchoredPosition = new Vector2(-28f, 20f);
                srt.sizeDelta = new Vector2(180f, 200f);
                srt.localScale = Vector3.one;
            }
        }
        if (sandButtonImage != null)
        {
            sandButtonImage.preserveAspect = true;
        }

        // Panel vertical de fin de juego adaptable a SafeArea sin escala 2.1x heredada
        EnsureEndGamePanelLayout();
        EnsurePausePanelLayout();
    }

    private static void PreserveButtonAspect(Button btn)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.preserveAspect = true;
    }

    private float GetBlueprintClosedY()
    {
        float refHeight = _hudContainer != null && _hudContainer.rect.height > 100f
            ? _hudContainer.rect.height
            : 2200f;
        return -Mathf.Max(DEFAULT_BLUEPRINT_CLOSED_Y * -1f, refHeight + 300f);
    }

    private TMP_FontAsset ResolveFontAsset()
    {
        TMP_FontAsset pixelFont = Resources.Load<TMP_FontAsset>("Fonts/PixelSand");
        if (pixelFont != null) return pixelFont;
        if (liveHudText != null && liveHudText.font != null) return liveHudText.font;
        if (scoreText != null && scoreText.font != null) return scoreText.font;
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        return (loaded != null && loaded.Length > 0) ? loaded[0] : null;
    }

    private IEnumerator StartInitialLevelFlowRoutine()
    {
        // Esperar un frame para asegurar que LevelManager y SandCanvasController finalizaron su arranque
        yield return null;

        int levelId = (LevelManager.Instance != null && LevelManager.Instance.currentLevelData != null)
            ? LevelManager.Instance.currentLevelData.levelID
            : 0;
        string sceneName = SceneManager.GetActiveScene().name;
        bool isFirstTutorialLevel = (levelId == 1) || string.Equals(sceneName, "Nivel1", System.StringComparison.OrdinalIgnoreCase);

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.isPausedByUI = true;
        }

        if (isFirstTutorialLevel)
        {
            // Cartel 1 (siempre en Nivel 1): Cómo dibujar el castillo + mini silueta objetivo
            _tutorialStepMode = 1;
            _waitingModalClose = true;
            RefreshBlueprintCastleSprite();
            SetBlueprintOpen(true);
            while (_waitingModalClose)
            {
                yield return null;
            }

            // Cartel 2: Cómo resistir la marea con Arena Mojada
            yield return new WaitForSecondsRealtime(0.18f);
            _tutorialStepMode = 2;
            _waitingModalClose = true;
            RefreshBlueprintCastleSprite();
            SetBlueprintOpen(true);
            while (_waitingModalClose)
            {
                yield return null;
            }

            // Paso 3: Overlay + Flecha + Globo de diálogo sobre el Balde interactivo
            yield return RunBucketSpotlightStepRoutine();
        }
        else
        {
            // Resto de niveles: mostrar el castillo objetivo y esperar que el jugador toque la X
            _tutorialStepMode = 0;
            _waitingModalClose = true;
            RefreshBlueprintCastleSprite();
            SetBlueprintOpen(true);
            while (_waitingModalClose)
            {
                yield return null;
            }
        }

        _tutorialStepMode = 0;
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.isPausedByUI = false;
        }
        _initialFlowCoroutine = null;
    }

    private IEnumerator RunBucketSpotlightStepRoutine()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform hudParent = _hudContainer != null
            ? (Transform)_hudContainer
            : (rootCanvas != null ? rootCanvas.transform : null);
        if (hudParent == null || toggleSandButton == null) yield break;

        _waitingBucketTap = true;
        TMP_FontAsset font = ResolveFontAsset();

        if (_tutorialStepOverlayGO != null) Destroy(_tutorialStepOverlayGO);

        GameObject overlayGO = new GameObject("Tutorial_Bucket_Spotlight", typeof(RectTransform), typeof(Image), typeof(Button));
        overlayGO.transform.SetParent(hudParent, false);
        RectTransform ort = overlayGO.GetComponent<RectTransform>();
        ort.anchorMin = new Vector2(-0.5f, -0.5f);
        ort.anchorMax = new Vector2(1.5f, 1.5f);
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;

        Image dimImg = overlayGO.GetComponent<Image>();
        dimImg.color = new Color(0.03f, 0.06f, 0.12f, 0.78f);
        dimImg.raycastTarget = true;

        Button overlayBtn = overlayGO.GetComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(() => { _waitingBucketTap = false; });

        // Traer el balde interactivo por encima del overlay para enfocar la atención del jugador
        Transform originalParent = toggleSandButton.transform.parent;
        int originalSibling = toggleSandButton.transform.GetSiblingIndex();

        // Globo de diálogo pixel art corto encima/izquierda del balde
        GameObject bubbleGO = new GameObject("Tutorial_SpeechBubble", typeof(RectTransform), typeof(Image));
        bubbleGO.transform.SetParent(hudParent, false);
        RectTransform brt = bubbleGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(1f, 0f);
        brt.anchoredPosition = new Vector2(-44f, 415f);
        brt.sizeDelta = new Vector2(640f, 260f);

        Image bubbleImg = bubbleGO.GetComponent<Image>();
        Sprite bubbleSpr = Resources.Load<Sprite>("UI_Themes/common/UI_SpeechBubble");
        if (bubbleSpr != null)
        {
            bubbleImg.sprite = bubbleSpr;
            bubbleImg.preserveAspect = true;
        }
        else
        {
            bubbleImg.color = new Color(1f, 0.97f, 0.88f, 0.98f);
        }
        bubbleImg.raycastTarget = false;

        GameObject bTextGO = new GameObject("Bubble_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        bTextGO.transform.SetParent(bubbleGO.transform, false);
        RectTransform btrt = bTextGO.GetComponent<RectTransform>();
        btrt.anchorMin = new Vector2(0.08f, 0.24f);
        btrt.anchorMax = new Vector2(0.92f, 0.88f);
        btrt.offsetMin = Vector2.zero;
        btrt.offsetMax = Vector2.zero;
        TextMeshProUGUI bTmp = bTextGO.GetComponent<TextMeshProUGUI>();
        if (font != null) bTmp.font = font;
        bTmp.text = "<b>¡CAMBIÁ A ARENA MOJADA!</b>\nTocá el balde antes de que llegue la ola para proteger tu castillo.";
        bTmp.alignment = TextAlignmentOptions.Center;
        bTmp.enableAutoSizing = true;
        bTmp.fontSizeMin = 20f;
        bTmp.fontSizeMax = 34f;
        bTmp.color = new Color(0.20f, 0.11f, 0.04f, 1f);
        bTmp.raycastTarget = false;

        // Flecha animada apuntando directo al balde
        GameObject arrowGO = new GameObject("Tutorial_Bucket_Arrow", typeof(RectTransform), typeof(Image));
        arrowGO.transform.SetParent(hudParent, false);
        RectTransform art = arrowGO.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(1f, 0f);
        art.anchorMax = new Vector2(1f, 0f);
        art.pivot = new Vector2(0.5f, 0f);
        art.anchoredPosition = new Vector2(-118f, 245f);
        art.sizeDelta = new Vector2(128f, 160f);

        Image arrowImg = arrowGO.GetComponent<Image>();
        Sprite arrowSpr = Resources.Load<Sprite>("UI_Themes/common/UI_Tutorial_Arrow");
        if (arrowSpr != null)
        {
            arrowImg.sprite = arrowSpr;
            arrowImg.preserveAspect = true;
        }
        else
        {
            arrowImg.color = new Color(1f, 0.84f, 0.2f, 1f);
        }
        arrowImg.raycastTarget = false;

        _tutorialStepOverlayGO = overlayGO;
        overlayGO.transform.SetAsLastSibling();
        bubbleGO.transform.SetAsLastSibling();
        arrowGO.transform.SetAsLastSibling();
        toggleSandButton.transform.SetParent(hudParent, false);
        toggleSandButton.transform.SetAsLastSibling();

        float bobTimer = 0f;
        while (_waitingBucketTap)
        {
            bobTimer += Time.unscaledDeltaTime * 5.2f;
            float offset = Mathf.Sin(bobTimer) * 14f;
            art.anchoredPosition = new Vector2(-118f, 245f + offset);
            yield return null;
        }

        if (originalParent != null && toggleSandButton.transform.parent != originalParent)
        {
            toggleSandButton.transform.SetParent(originalParent, false);
        }
        toggleSandButton.transform.SetSiblingIndex(originalSibling);

        if (arrowGO != null) Destroy(arrowGO);
        if (bubbleGO != null) Destroy(bubbleGO);
        if (overlayGO != null) Destroy(overlayGO);
        _tutorialStepOverlayGO = null;
        UpdateBlueprintSiblingOrder();
    }

    private void EnsureLiveHudBadge()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform hudParent = _hudContainer != null
            ? (Transform)_hudContainer
            : (rootCanvas != null ? rootCanvas.transform : null);

        if (hudParent == null) return;

        Transform existingHud = rootCanvas != null
            ? SafeAreaFitter.FindInScope(rootCanvas.transform, "LiveSculptHUD", "SafeArea_HUD")
            : null;

        GameObject hudPanelGO;
        if (existingHud != null)
        {
            hudPanelGO = existingHud.gameObject;
            if (hudPanelGO.transform.parent != hudParent)
            {
                hudPanelGO.transform.SetParent(hudParent, false);
            }
            if (liveHudText == null)
            {
                liveHudText = existingHud.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        else
        {
            hudPanelGO = new GameObject("LiveSculptHUD", typeof(RectTransform), typeof(Image));
            hudPanelGO.transform.SetParent(hudParent, false);
        }

        RectTransform panelRT = hudPanelGO.GetComponent<RectTransform>();
        if (panelRT != null)
        {
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(1f, 1f);
            panelRT.pivot = new Vector2(0.5f, 1f);
            panelRT.offsetMin = new Vector2(24f, -148f);
            panelRT.offsetMax = new Vector2(-186f, -24f);
        }

        Image rootHudImg = hudPanelGO.GetComponent<Image>();
        if (rootHudImg != null)
        {
            rootHudImg.color = new Color(0f, 0f, 0f, 0f);
            rootHudImg.raycastTarget = false;
        }

        if (liveHudText != null)
        {
            liveHudText.gameObject.SetActive(false);
        }

        if (_hudCastlePctText != null) return;

        TMP_FontAsset font = ResolveFontAsset();
        Sprite pillBg = Resources.Load<Sprite>("UI_Themes/common/UI_HudPill_Bg");

        // Pill 1 (Izquierda): Progreso del Castillo (0..100% + marca 90%)
        GameObject pill1 = BuildHudPill(hudPanelGO.transform, "HUD_Pill_Castillo", pillBg,
            new Vector2(0.00f, 0.04f), new Vector2(0.49f, 0.96f),
            font, "CASTILLO", new Color(0.95f, 0.72f, 0.22f, 1f),
            out _hudCastlePctText, out _hudCastleFillBar, true);

        // Pill 2 (Derecha): Progreso de Marea / Tiempo transcurrido / Alertas de viento
        GameObject pill2 = BuildHudPill(hudPanelGO.transform, "HUD_Pill_Marea", pillBg,
            new Vector2(0.51f, 0.04f), new Vector2(1.00f, 0.96f),
            font, "MAREA", new Color(0.24f, 0.76f, 0.94f, 1f),
            out _hudTimerText, out _hudTideFillBar, false);

        Transform statusT = pill2.transform.Find("Title");
        if (statusT != null) _hudTideStatusText = statusT.GetComponent<TextMeshProUGUI>();
    }

    private GameObject BuildHudPill(
        Transform parent, string name, Sprite bgSprite,
        Vector2 aMin, Vector2 aMax, TMP_FontAsset font,
        string labelText, Color barColor,
        out TextMeshProUGUI valueTMP, out Image fillImg, bool show90Mark)
    {
        GameObject pill = new GameObject(name, typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(parent, false);
        RectTransform rt = pill.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = pill.GetComponent<Image>();
        if (bgSprite != null)
        {
            img.sprite = bgSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.16f, 0.11f, 0.06f, 0.85f);
        }
        img.raycastTarget = false;

        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(pill.transform, false);
        RectTransform trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.08f, 0.50f);
        trt.anchorMax = new Vector2(0.62f, 0.90f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        if (font != null) tTmp.font = font;
        tTmp.text = labelText;
        tTmp.alignment = TextAlignmentOptions.Left;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.enableAutoSizing = true;
        tTmp.fontSizeMin = 17f;
        tTmp.fontSizeMax = 28f;
        tTmp.color = new Color(1f, 0.94f, 0.78f, 1f);
        tTmp.raycastTarget = false;

        GameObject valGO = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        valGO.transform.SetParent(pill.transform, false);
        RectTransform vrt = valGO.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.58f, 0.48f);
        vrt.anchorMax = new Vector2(0.92f, 0.92f);
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;
        valueTMP = valGO.GetComponent<TextMeshProUGUI>();
        if (font != null) valueTMP.font = font;
        valueTMP.text = show90Mark ? "0%" : "0s";
        valueTMP.alignment = TextAlignmentOptions.Right;
        valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.enableAutoSizing = true;
        valueTMP.fontSizeMin = 21f;
        valueTMP.fontSizeMax = 32f;
        valueTMP.color = new Color(1f, 0.96f, 0.84f, 1f);
        valueTMP.raycastTarget = false;

        GameObject trackGO = new GameObject("Bar_Track", typeof(RectTransform), typeof(Image));
        trackGO.transform.SetParent(pill.transform, false);
        RectTransform krt = trackGO.GetComponent<RectTransform>();
        krt.anchorMin = new Vector2(0.08f, 0.16f);
        krt.anchorMax = new Vector2(0.92f, 0.38f);
        krt.offsetMin = Vector2.zero;
        krt.offsetMax = Vector2.zero;
        Image kImg = trackGO.GetComponent<Image>();
        kImg.color = new Color(0.11f, 0.07f, 0.03f, 0.92f);
        kImg.raycastTarget = false;

        GameObject fillGO = new GameObject("Bar_Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(trackGO.transform, false);
        RectTransform frt = fillGO.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = new Vector2(2f, 2f);
        frt.offsetMax = new Vector2(-2f, -2f);
        fillImg = fillGO.GetComponent<Image>();
        fillImg.color = barColor;
        fillImg.raycastTarget = false;

        if (show90Mark)
        {
            GameObject markGO = new GameObject("Goal_90_Mark", typeof(RectTransform), typeof(Image));
            markGO.transform.SetParent(trackGO.transform, false);
            RectTransform mrt = markGO.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.895f, -0.15f);
            mrt.anchorMax = new Vector2(0.915f, 1.15f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            Image mImg = markGO.GetComponent<Image>();
            mImg.color = new Color(1f, 0.96f, 0.72f, 0.95f);
            mImg.raycastTarget = false;
        }

        return pill;
    }

    private void EnsurePausePanelLayout()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        Transform pauseMgr = rootCanvas.transform.Find("PauseMenu_Manager");
        if (pauseMgr == null) return;
        pauseMgr.SetAsLastSibling();

        Transform pausePanelT = pauseMgr.Find("PausePanel");
        if (pausePanelT == null) return;

        // Fondo del PausePanel = Overlay oscuro uniforme cubriendo toda la pantalla
        Image panelImg = pausePanelT.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.sprite = null;
            panelImg.type = Image.Type.Simple;
            panelImg.color = new Color(0.03f, 0.06f, 0.12f, 0.78f);
            panelImg.raycastTarget = true;
        }

        RectTransform prt = pausePanelT.GetComponent<RectTransform>();
        if (prt != null)
        {
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            prt.localScale = Vector3.one;
        }

        Transform modalT = pausePanelT.Find("PauseModal_Card");
        if (modalT == null)
        {
            GameObject cardGO = new GameObject("PauseModal_Card", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(pausePanelT, false);
            RectTransform crt = cardGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(740f, 884f);
            crt.localScale = Vector3.one;

            Image cardImg = cardGO.GetComponent<Image>();
            Sprite modalSpr = Resources.Load<Sprite>("UI_Themes/common/UI_Pause_Panel");
            if (modalSpr != null)
            {
                cardImg.sprite = modalSpr;
                cardImg.preserveAspect = true;
            }
            else
            {
                cardImg.color = new Color(0.95f, 0.88f, 0.72f, 0.98f);
            }
            cardImg.raycastTarget = true;

            TMP_FontAsset font = ResolveFontAsset();
            GameObject headerGO = new GameObject("Pause_Header_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGO.transform.SetParent(cardGO.transform, false);
            RectTransform hrt = headerGO.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.16f, 0.80f);
            hrt.anchorMax = new Vector2(0.84f, 0.92f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            TextMeshProUGUI hTmp = headerGO.GetComponent<TextMeshProUGUI>();
            if (font != null) hTmp.font = font;
            hTmp.text = "PAUSA";
            hTmp.alignment = TextAlignmentOptions.Center;
            hTmp.fontStyle = FontStyles.Bold;
            hTmp.enableAutoSizing = true;
            hTmp.fontSizeMin = 32f;
            hTmp.fontSizeMax = 56f;
            hTmp.color = Color.white;
            hTmp.outlineWidth = 0.28f;
            hTmp.outlineColor = new Color(0.14f, 0.07f, 0.03f, 1f);
            hTmp.raycastTarget = false;

            modalT = cardGO.transform;
        }
        _pauseModalCardGO = modalT.gameObject;

        // Reubicar botones de Pausa centrados y del mismo tamaño sin escala 2.93x
        ArrangePauseButton(pausePanelT, modalT, "Btn_Reanudar", new Vector2(0.14f, 0.56f), new Vector2(0.86f, 0.72f));
        ArrangePauseButton(pausePanelT, modalT, "Btn_Reiniciar", new Vector2(0.14f, 0.36f), new Vector2(0.86f, 0.52f));
        ArrangePauseButton(pausePanelT, modalT, "Btn_MenuPrincipal", new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.32f));
    }

    private static void ArrangePauseButton(Transform searchScope, Transform modalCard, string btnName, Vector2 aMin, Vector2 aMax)
    {
        Transform btnT = modalCard.Find(btnName) ?? searchScope.Find(btnName);
        if (btnT == null) return;
        if (btnT.parent != modalCard) btnT.SetParent(modalCard, false);

        RectTransform rt = btnT.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
        Image img = btnT.GetComponent<Image>();
        if (img != null) img.preserveAspect = true;
    }

    /// <summary>
    /// Crea o vincula el botón izquierdo del plano y la hoja arrugada que sube desde abajo.
    /// El botón queda siempre en una capa superior para poder cerrarlo en cualquier momento.
    /// </summary>
    private void EnsureBlueprintSystemUI()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;
        Transform hudParent = _hudContainer != null ? (Transform)_hudContainer : rootCanvas.transform;

        if (_blueprintTargetMapPropSprite == null)
        {
            _blueprintTargetMapPropSprite = Resources.Load<Sprite>("UI_Plano/Setup_Mapa_Plano");
        }
        if (blueprintButtonSprite == null)
        {
            blueprintButtonSprite = _blueprintTargetMapPropSprite != null
                ? _blueprintTargetMapPropSprite
                : Resources.Load<Sprite>("UI_Plano/Boton_Ver_Plano");
        }
        if (blueprintSheetSprite == null)
        {
            blueprintSheetSprite = Resources.Load<Sprite>("UI_Plano/Hoja_Plano_Objetivo");
        }
        if (_tutorialStep1Sprite == null)
        {
            _tutorialStep1Sprite = Resources.Load<Sprite>("UI_Plano/Tutorial_Paso1_Dibujar");
        }
        if (_tutorialStep2Sprite == null)
        {
            _tutorialStep2Sprite = Resources.Load<Sprite>("UI_Plano/Tutorial_Paso2_ArenaMojada");
        }

        // 0. Overlay oscuro detrás del modal de plano / tutorial
        if (_blueprintDimmerGO == null)
        {
            Transform existingDim = SafeAreaFitter.FindInScope(rootCanvas.transform, "Blueprint_Dimmer", "SafeArea_HUD");
            if (existingDim != null)
            {
                _blueprintDimmerGO = existingDim.gameObject;
                if (_blueprintDimmerGO.transform.parent != hudParent) _blueprintDimmerGO.transform.SetParent(hudParent, false);
            }
            else
            {
                _blueprintDimmerGO = new GameObject("Blueprint_Dimmer", typeof(RectTransform), typeof(Image), typeof(Button));
                _blueprintDimmerGO.transform.SetParent(hudParent, false);
                RectTransform drt = _blueprintDimmerGO.GetComponent<RectTransform>();
                drt.anchorMin = new Vector2(-0.5f, -0.5f);
                drt.anchorMax = new Vector2(1.5f, 1.5f);
                drt.offsetMin = Vector2.zero;
                drt.offsetMax = Vector2.zero;

                Image dimImg = _blueprintDimmerGO.GetComponent<Image>();
                dimImg.color = new Color(0.03f, 0.06f, 0.12f, 0.78f);
                dimImg.raycastTarget = true;

                Button dimBtn = _blueprintDimmerGO.GetComponent<Button>();
                dimBtn.transition = Selectable.Transition.None;
                dimBtn.onClick.AddListener(() =>
                {
                    if (_isBlueprintOpen && _tutorialStepMode == 0 && !_waitingModalClose)
                    {
                        CloseBlueprintOrTutorialModal();
                    }
                });
            }
            _blueprintDimmerGO.SetActive(false);
        }

        // 1. Panel de la Hoja Arrugada (Centrado con overlay cuando está abierto)
        if (blueprintSheetPanel == null)
        {
            Transform existingSheet = SafeAreaFitter.FindInScope(rootCanvas.transform, "Panel_Hoja_Plano", "SafeArea_HUD");
            if (existingSheet != null)
            {
                if (existingSheet.parent != hudParent) existingSheet.SetParent(hudParent, false);
                blueprintSheetPanel = existingSheet.GetComponent<RectTransform>();
                Transform visualT = existingSheet.Find("Hoja_Papel_Visual");
                _blueprintSheetVisualImage = visualT != null
                    ? visualT.GetComponent<Image>()
                    : existingSheet.GetComponent<Image>();

                Transform clipT = visualT != null
                    ? visualT.Find("Area_Dibujo_Plano")
                    : existingSheet.Find("Area_Dibujo_Plano");
                Transform previewT = clipT != null
                    ? clipT.Find("Castillo_Objetivo_Dibujo")
                    : existingSheet.Find("Castillo_Objetivo_Dibujo");
                if (previewT != null) blueprintTargetCastleImage = previewT.GetComponent<Image>();
            }
            else
            {
                GameObject sheetGO = new GameObject("Panel_Hoja_Plano", typeof(RectTransform));
                sheetGO.transform.SetParent(hudParent, false);

                blueprintSheetPanel = sheetGO.GetComponent<RectTransform>();
                blueprintSheetPanel.anchorMin = new Vector2(0.06f, 0.11f);
                blueprintSheetPanel.anchorMax = new Vector2(0.94f, 0.89f);
                blueprintSheetPanel.offsetMin = Vector2.zero;
                blueprintSheetPanel.offsetMax = Vector2.zero;
                blueprintSheetPanel.anchoredPosition = new Vector2(0f, GetBlueprintClosedY());

                // Contenedor visual ajustado exactamente al aspect ratio real de Hoja_Plano_Objetivo.png
                GameObject sheetVisualGO = new GameObject("Hoja_Papel_Visual", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter), typeof(Button));
                sheetVisualGO.transform.SetParent(sheetGO.transform, false);

                RectTransform visualRT = sheetVisualGO.GetComponent<RectTransform>();
                visualRT.anchorMin = Vector2.zero;
                visualRT.anchorMax = Vector2.one;
                visualRT.offsetMin = Vector2.zero;
                visualRT.offsetMax = Vector2.zero;

                _blueprintSheetVisualImage = sheetVisualGO.GetComponent<Image>();
                if (blueprintSheetSprite != null)
                {
                    _blueprintSheetVisualImage.sprite = blueprintSheetSprite;
                    _blueprintSheetVisualImage.preserveAspect = false;
                }
                else
                {
                    _blueprintSheetVisualImage.color = new Color(0.98f, 0.95f, 0.86f, 0.98f);
                }

                float sheetAspect = (blueprintSheetSprite != null && blueprintSheetSprite.rect.height > 1f)
                    ? (blueprintSheetSprite.rect.width / blueprintSheetSprite.rect.height)
                    : (796f / 1080f);
                AspectRatioFitter fitter = sheetVisualGO.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = sheetAspect;

                // Tocar la hoja fuera de los carteles tutoriales también cierra rápido
                Button sheetCloseBtn = sheetVisualGO.GetComponent<Button>();
                sheetCloseBtn.transition = Selectable.Transition.None;
                sheetCloseBtn.onClick.AddListener(() =>
                {
                    if (_isBlueprintOpen && _tutorialStepMode == 0 && !_waitingModalClose)
                    {
                        CloseBlueprintOrTutorialModal();
                    }
                });

                // Botón X explícito en la esquina superior derecha de la hoja
                GameObject closeXGO = new GameObject("Btn_Close_X", typeof(RectTransform), typeof(Image), typeof(Button));
                closeXGO.transform.SetParent(sheetVisualGO.transform, false);
                RectTransform xrt = closeXGO.GetComponent<RectTransform>();
                xrt.anchorMin = new Vector2(1f, 1f);
                xrt.anchorMax = new Vector2(1f, 1f);
                xrt.pivot = new Vector2(1f, 1f);
                xrt.anchoredPosition = new Vector2(18f, 18f);
                xrt.sizeDelta = new Vector2(96f, 96f);

                Image xImg = closeXGO.GetComponent<Image>();
                Sprite xSpr = Resources.Load<Sprite>("UI_Themes/common/Btn_Close_X");
                if (xSpr != null)
                {
                    xImg.sprite = xSpr;
                    xImg.preserveAspect = true;
                }
                else
                {
                    xImg.color = new Color(0.85f, 0.22f, 0.16f, 1f);
                }
                _blueprintCloseXButton = closeXGO.GetComponent<Button>();
                _blueprintCloseXButton.onClick.AddListener(CloseBlueprintOrTutorialModal);

                // Área segura dentro del papel real (debajo de "COMO TIENE QUE QUEDAR" y entre las flechas)
                GameObject clipGO = new GameObject("Area_Dibujo_Plano", typeof(RectTransform));
                clipGO.transform.SetParent(sheetVisualGO.transform, false);

                RectTransform clipRT = clipGO.GetComponent<RectTransform>();
                clipRT.anchorMin = new Vector2(0.085f, 0.105f);
                clipRT.anchorMax = new Vector2(0.915f, 0.765f);
                clipRT.offsetMin = Vector2.zero;
                clipRT.offsetMax = Vector2.zero;

                // Imagen principal del castillo o cartel de tutorial
                GameObject castlePreviewGO = new GameObject("Castillo_Objetivo_Dibujo", typeof(RectTransform), typeof(Image));
                castlePreviewGO.transform.SetParent(clipGO.transform, false);

                RectTransform previewRT = castlePreviewGO.GetComponent<RectTransform>();
                previewRT.anchorMin = new Vector2(0.02f, 0.24f);
                previewRT.anchorMax = new Vector2(0.98f, 0.98f);
                previewRT.pivot = new Vector2(0.5f, 0.5f);
                previewRT.offsetMin = Vector2.zero;
                previewRT.offsetMax = Vector2.zero;

                blueprintTargetCastleImage = castlePreviewGO.GetComponent<Image>();
                blueprintTargetCastleImage.preserveAspect = true;
                blueprintTargetCastleImage.color = Color.white;
                blueprintTargetCastleImage.raycastTarget = false;

                // Mini silueta objetivo en la esquina superior izquierda durante Cartel 1 del Tutorial
                GameObject miniBgGO = new GameObject("Tutorial_MiniTarget_Box", typeof(RectTransform), typeof(Image));
                miniBgGO.transform.SetParent(clipGO.transform, false);
                RectTransform mbrt = miniBgGO.GetComponent<RectTransform>();
                mbrt.anchorMin = new Vector2(0.02f, 0.68f);
                mbrt.anchorMax = new Vector2(0.32f, 0.98f);
                mbrt.offsetMin = Vector2.zero;
                mbrt.offsetMax = Vector2.zero;
                Image mbImg = miniBgGO.GetComponent<Image>();
                mbImg.color = new Color(0.96f, 0.90f, 0.76f, 0.95f);
                mbImg.raycastTarget = false;

                GameObject miniCastleGO = new GameObject("Tutorial_MiniTarget_Img", typeof(RectTransform), typeof(Image));
                miniCastleGO.transform.SetParent(miniBgGO.transform, false);
                RectTransform mcrt = miniCastleGO.GetComponent<RectTransform>();
                mcrt.anchorMin = new Vector2(0.08f, 0.08f);
                mcrt.anchorMax = new Vector2(0.92f, 0.92f);
                mcrt.offsetMin = Vector2.zero;
                mcrt.offsetMax = Vector2.zero;
                _tutorialPreviewMiniImage = miniCastleGO.GetComponent<Image>();
                _tutorialPreviewMiniImage.preserveAspect = true;
                _tutorialPreviewMiniImage.raycastTarget = false;
                miniBgGO.SetActive(false);

                // Texto corto y claro debajo del dibujo
                GameObject capGO = new GameObject("Caption_Plano", typeof(RectTransform), typeof(TextMeshProUGUI));
                capGO.transform.SetParent(clipGO.transform, false);
                RectTransform crt = capGO.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.02f, 0.00f);
                crt.anchorMax = new Vector2(0.98f, 0.22f);
                crt.offsetMin = Vector2.zero;
                crt.offsetMax = Vector2.zero;
                _blueprintCaptionText = capGO.GetComponent<TextMeshProUGUI>();
                TMP_FontAsset font = ResolveFontAsset();
                if (font != null) _blueprintCaptionText.font = font;
                _blueprintCaptionText.alignment = TextAlignmentOptions.Center;
                _blueprintCaptionText.enableAutoSizing = true;
                _blueprintCaptionText.fontSizeMin = 20f;
                _blueprintCaptionText.fontSizeMax = 34f;
                _blueprintCaptionText.color = new Color(0.24f, 0.13f, 0.05f, 1f);
                _blueprintCaptionText.raycastTarget = false;
            }
        }

        // 2. Setup inferior izquierdo: Mapa físico enrollado/desplegado debajo de la mesa de esculpido
        if (toggleBlueprintButton == null)
        {
            Transform existingBtn = SafeAreaFitter.FindInScope(rootCanvas.transform, "Btn_Ver_Plano", "SafeArea_HUD");
            if (existingBtn != null)
            {
                if (existingBtn.parent != hudParent) existingBtn.SetParent(hudParent, false);
                toggleBlueprintButton = existingBtn.GetComponent<Button>();
            }
            else
            {
                GameObject btnGO = new GameObject("Btn_Ver_Plano", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(hudParent, false);

                Image btnImg = btnGO.GetComponent<Image>();
                Sprite propSprite = _blueprintTargetMapPropSprite != null ? _blueprintTargetMapPropSprite : blueprintButtonSprite;
                if (propSprite != null)
                {
                    btnImg.sprite = propSprite;
                    btnImg.preserveAspect = true;
                }
                else
                {
                    btnImg.color = new Color(0.95f, 0.78f, 0.42f, 1f);
                }

                toggleBlueprintButton = btnGO.GetComponent<Button>();
            }

            if (toggleBlueprintButton != null)
            {
                RectTransform btnRT = toggleBlueprintButton.GetComponent<RectTransform>();
                if (btnRT != null)
                {
                    btnRT.anchorMin = new Vector2(0f, 0f);
                    btnRT.anchorMax = new Vector2(0f, 0f);
                    btnRT.pivot = new Vector2(0f, 0f);
                    btnRT.anchoredPosition = new Vector2(28f, 22f);
                    btnRT.sizeDelta = new Vector2(210f, 165f);
                    btnRT.localScale = Vector3.one;
                }
                Image img = toggleBlueprintButton.GetComponent<Image>();
                if (img != null)
                {
                    if (_blueprintTargetMapPropSprite != null) img.sprite = _blueprintTargetMapPropSprite;
                    img.preserveAspect = true;
                }

                toggleBlueprintButton.onClick.RemoveAllListeners();
                toggleBlueprintButton.onClick.AddListener(ToggleBlueprintSheet);
            }
        }

        UpdateBlueprintSiblingOrder();
        RefreshBlueprintCastleSprite();
        SetBlueprintOpen(false, true);
    }

    private void UpdateBlueprintSiblingOrder()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (_blueprintDimmerGO != null)
        {
            _blueprintDimmerGO.transform.SetAsLastSibling();
        }
        if (blueprintSheetPanel != null)
        {
            blueprintSheetPanel.SetAsLastSibling();
        }
        if (!_isBlueprintOpen)
        {
            if (toggleBlueprintButton != null) toggleBlueprintButton.transform.SetAsLastSibling();
            if (toggleSandButton != null && !_waitingBucketTap) toggleSandButton.transform.SetAsLastSibling();
        }
        if (_tutorialStepOverlayGO != null)
        {
            _tutorialStepOverlayGO.transform.SetAsLastSibling();
            if (_hudContainer != null)
            {
                Transform bT = _hudContainer.Find("Tutorial_SpeechBubble");
                Transform aT = _hudContainer.Find("Tutorial_Bucket_Arrow");
                if (bT != null) bT.SetAsLastSibling();
                if (aT != null) aT.SetAsLastSibling();
            }
            if (toggleSandButton != null && _waitingBucketTap) toggleSandButton.transform.SetAsLastSibling();
        }
        if (rootCanvas != null)
        {
            Transform pauseBtn = SafeAreaFitter.FindInScope(rootCanvas.transform, "Btn_AbrirPausa", "SafeArea_HUD");
            if (pauseBtn != null && !_isBlueprintOpen && !_waitingBucketTap)
            {
                pauseBtn.SetAsLastSibling();
            }
            if (_hudContainer != null) _hudContainer.SetAsLastSibling();
            Transform pausePanel = rootCanvas.transform.Find("PauseMenu_Manager");
            if (pausePanel != null) pausePanel.SetAsLastSibling();
        }
        if (endGamePanel != null && endGamePanel.transform.parent != null)
        {
            endGamePanel.transform.parent.SetAsLastSibling();
            if (rootCanvas != null)
            {
                Transform pausePanel = rootCanvas.transform.Find("PauseMenu_Manager");
                if (pausePanel != null) pausePanel.SetAsLastSibling();
            }
        }
    }

    private void CloseBlueprintOrTutorialModal()
    {
        if (_waitingModalClose)
        {
            _waitingModalClose = false;
        }
        SetBlueprintOpen(false);
    }

    private void RefreshBlueprintCastleSprite()
    {
        if (blueprintTargetCastleImage == null) return;

        Sprite sourceTargetSprite = null;
        if (SandCanvasController.Instance != null && SandCanvasController.Instance.TargetCastleSprite != null)
        {
            sourceTargetSprite = SandCanvasController.Instance.TargetCastleSprite;
        }
        else if (LevelManager.Instance != null && LevelManager.Instance.currentLevelData != null &&
                 LevelManager.Instance.currentLevelData.targetShapePrefab != null)
        {
            SpriteRenderer sr = LevelManager.Instance.currentLevelData.targetShapePrefab.GetComponent<SpriteRenderer>();
            if (sr != null) sourceTargetSprite = sr.sprite;
        }

        int levelId = (LevelManager.Instance != null && LevelManager.Instance.currentLevelData != null)
            ? LevelManager.Instance.currentLevelData.levelID
            : 0;

        Sprite blueprintSprite = ResolveBlueprintHintSprite(levelId, sourceTargetSprite);
        Sprite targetShapeSprite = blueprintSprite != null ? blueprintSprite : sourceTargetSprite;

        Sprite displaySprite = targetShapeSprite;
        if (_tutorialStepMode == 1 && _tutorialStep1Sprite != null)
        {
            displaySprite = _tutorialStep1Sprite;
        }
        else if (_tutorialStepMode == 2 && _tutorialStep2Sprite != null)
        {
            displaySprite = _tutorialStep2Sprite;
        }

        if (displaySprite != null)
        {
            blueprintTargetCastleImage.sprite = displaySprite;
            blueprintTargetCastleImage.preserveAspect = true;
            blueprintTargetCastleImage.color = Color.white;
        }

        if (_tutorialPreviewMiniImage != null && _tutorialPreviewMiniImage.transform.parent != null)
        {
            bool showMini = (_tutorialStepMode == 1) && targetShapeSprite != null;
            _tutorialPreviewMiniImage.transform.parent.gameObject.SetActive(showMini);
            if (showMini)
            {
                _tutorialPreviewMiniImage.sprite = targetShapeSprite;
                _tutorialPreviewMiniImage.preserveAspect = true;
                _tutorialPreviewMiniImage.color = Color.white;
            }
        }

        if (_blueprintCaptionText != null)
        {
            if (_tutorialStepMode == 1)
            {
                _blueprintCaptionText.text = "<b>¡DIBUJÁ TU CASTILLO!</b>\nDeslizá el dedo sobre la arena y rellená la silueta al <b>90%</b>.";
            }
            else if (_tutorialStepMode == 2)
            {
                _blueprintCaptionText.text = "<b>¡FRENÁ LAS OLAS!</b>\nUsá <b>Arena Mojada</b> arriba del castillo para resistir la marea.";
            }
            else
            {
                _blueprintCaptionText.text = "<b>OBJETIVO DEL NIVEL</b>\nReplicá esta forma antes de que suba la marea.";
            }
        }
    }

    private static Sprite ResolveBlueprintHintSprite(int levelId, Sprite sourceTargetSprite)
    {
        if (levelId > 0)
        {
            Sprite byLevelId = Resources.Load<Sprite>($"UI_Plano/Castillos/Plano_ID_{levelId}");
            if (byLevelId != null) return byLevelId;
        }

        if (sourceTargetSprite != null && !string.IsNullOrEmpty(sourceTargetSprite.name))
        {
            string safeKey = System.Text.RegularExpressions.Regex.Replace(sourceTargetSprite.name.Trim(), @"[^a-zA-Z0-9]+", "_").Trim('_');
            if (!string.IsNullOrEmpty(safeKey))
            {
                Sprite byTargetName = Resources.Load<Sprite>($"UI_Plano/Castillos/Plano_{safeKey}");
                if (byTargetName != null) return byTargetName;
            }
        }

        return null;
    }

    public void ToggleBlueprintSheet()
    {
        if (_waitingModalClose || _waitingBucketTap) return;
        _tutorialStepMode = 0;
        SetBlueprintOpen(!_isBlueprintOpen);
    }

    public void SetBlueprintOpen(bool open, bool instant = false)
    {
        bool stateChanged = (_isBlueprintOpen != open);
        _isBlueprintOpen = open;
        _blueprintTargetY = open ? BLUEPRINT_OPEN_Y : GetBlueprintClosedY();

        if (!instant && stateChanged && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPaperScrollSound(open);
        }

        if (_blueprintDimmerGO != null)
        {
            _blueprintDimmerGO.SetActive(open);
        }

        if (open)
        {
            RefreshBlueprintCastleSprite();
        }

        if (LevelManager.Instance != null && _initialFlowCoroutine == null && !_waitingBucketTap)
        {
            LevelManager.Instance.isPausedByUI = open;
        }

        if (blueprintSheetPanel != null)
        {
            if (_blueprintSheetVisualImage != null)
            {
                _blueprintSheetVisualImage.raycastTarget = open;
            }
            else
            {
                Image sheetImg = blueprintSheetPanel.GetComponent<Image>();
                if (sheetImg != null) sheetImg.raycastTarget = open;
            }

            if (instant)
            {
                blueprintSheetPanel.anchoredPosition = new Vector2(0f, _blueprintTargetY);
            }
        }

        // Mantener el botón de abrir/cerrar, pausa y fin de juego siempre encima de la hoja
        UpdateBlueprintSiblingOrder();
    }

    public void ShowEndGamePanel(int scorePercentage, bool isWin, int starsEarned = 0, float elapsedTime = 0f)
    {
        SetBlueprintOpen(false, true);
        ApplyRegionTheme();
        EnsureEndGamePanelLayout();

        if (_endGameDimmerGO != null)
        {
            _endGameDimmerGO.SetActive(true);
            _endGameDimmerGO.transform.SetAsLastSibling();
        }

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
            Image panelImg = endGamePanel.GetComponent<Image>();
            Sprite unifiedPanel = isWin ? _unifiedVictoryPanelSprite : _unifiedDefeatPanelSprite;
            if (panelImg != null && unifiedPanel != null)
            {
                panelImg.sprite = unifiedPanel;
                panelImg.preserveAspect = true;
                panelImg.color = Color.white;
            }
            endGamePanel.transform.parent.SetAsLastSibling();
            endGamePanel.transform.SetAsLastSibling();
        }

        if (_endGameBannerImage != null)
        {
            Sprite unifiedPanel = isWin ? _unifiedVictoryPanelSprite : _unifiedDefeatPanelSprite;
            if (unifiedPanel != null)
            {
                _endGameBannerImage.enabled = false;
                _endGameBannerImage.gameObject.SetActive(false);
            }
            else
            {
                Sprite targetBanner = isWin ? _victoryBannerSprite : _defeatBannerSprite;
                if (targetBanner != null)
                {
                    _endGameBannerImage.sprite = targetBanner;
                    _endGameBannerImage.preserveAspect = true;
                    _endGameBannerImage.enabled = true;
                    _endGameBannerImage.gameObject.SetActive(true);
                }
            }
        }

        if (isWin)
        {
            if (_statPrecisionValueText != null)
            {
                _statPrecisionValueText.text = $"{scorePercentage}%";
                _statPrecisionValueText.color = new Color(0.10f, 0.44f, 0.20f, 1f);
            }
            if (_statPrecisionSubText != null)
            {
                _statPrecisionSubText.text = "¡Castillo perfecto!";
            }
            if (_statSecondTitleText != null)
            {
                _statSecondTitleText.text = "TIEMPO";
            }
            if (_statSecondValueText != null)
            {
                _statSecondValueText.text = $"{elapsedTime:F1}s";
                _statSecondValueText.color = new Color(0.18f, 0.09f, 0.03f, 1f);
            }
            if (_statSecondSubText != null)
            {
                _statSecondSubText.text = $"Estrellas: {starsEarned}/3";
            }
        }
        else
        {
            if (_statPrecisionValueText != null)
            {
                _statPrecisionValueText.text = $"{scorePercentage}%";
                _statPrecisionValueText.color = new Color(0.68f, 0.16f, 0.10f, 1f);
            }
            if (_statPrecisionSubText != null)
            {
                _statPrecisionSubText.text = "Meta: 90%";
            }
            if (_statSecondTitleText != null)
            {
                _statSecondTitleText.text = "CONSEJO";
            }
            if (_statSecondValueText != null)
            {
                _statSecondValueText.text = "BALDE";
                _statSecondValueText.color = new Color(0.75f, 0.40f, 0.05f, 1f);
            }
            if (_statSecondSubText != null)
            {
                _statSecondSubText.text = "Usá Arena Mojada";
            }
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }

        UpdateEndGameButtonsForOutcome(isWin);

        if (_starsAnimCoroutine != null) StopCoroutine(_starsAnimCoroutine);
        _starsAnimCoroutine = StartCoroutine(AnimateEndGameEntryAndStarsRoutine(isWin, starsEarned));
    }

    private string ResolveActiveRegionThemeId()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.currentLevelData != null &&
            !string.IsNullOrEmpty(LevelManager.Instance.currentLevelData.regionThemeId))
        {
            return LevelManager.Instance.currentLevelData.regionThemeId.Trim().ToLowerInvariant();
        }
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("NivelDLC", System.StringComparison.OrdinalIgnoreCase))
        {
            return "brasil";
        }
        return "base";
    }

    private void ApplyRegionTheme()
    {
        string themeId = ResolveActiveRegionThemeId();
        if (_appliedThemeId == themeId) return;
        _appliedThemeId = themeId;

        bool isBrasil = themeId == "brasil";
        Canvas rootCanvas = GetComponentInParent<Canvas>();

        if (isBrasil)
        {
            Sprite brDry = Resources.Load<Sprite>("UI_Themes/brasil/Btn_Balde_ArenaSeca");
            Sprite brWet = Resources.Load<Sprite>("UI_Themes/brasil/Btn_Balde_ArenaHumeda");
            if (brDry != null) drySandSprite = brDry;
            if (brWet != null) wetSandSprite = brWet;

            if (rootCanvas != null)
            {
                Transform pauseBtnT = SafeAreaFitter.FindInScope(rootCanvas.transform, "Btn_AbrirPausa", "SafeArea_HUD");
                Sprite brPause = Resources.Load<Sprite>("UI_Themes/brasil/Btn_Pausa");
                if (pauseBtnT != null && brPause != null)
                {
                    Image pImg = pauseBtnT.GetComponent<Image>();
                    if (pImg != null)
                    {
                        pImg.sprite = brPause;
                        pImg.preserveAspect = true;
                    }
                }
            }
        }

        // El mapa de abajo a la izquierda mantiene su estilo de mapa enrollado en todos los packs
        if (_blueprintTargetMapPropSprite != null && toggleBlueprintButton != null)
        {
            Image bpImg = toggleBlueprintButton.GetComponent<Image>();
            if (bpImg != null)
            {
                bpImg.sprite = _blueprintTargetMapPropSprite;
                bpImg.preserveAspect = true;
            }
        }

        if (_retryRedSprite == null)
        {
            _retryRedSprite = Resources.Load<Sprite>("UI_Themes/common/Btn_Reintentar_Rojo");
        }

        _unifiedVictoryPanelSprite = Resources.Load<Sprite>($"UI_Themes/{themeId}/EndGame_Victoria")
            ?? Resources.Load<Sprite>("UI_Themes/base/EndGame_Victoria");
        _unifiedDefeatPanelSprite = Resources.Load<Sprite>($"UI_Themes/{themeId}/EndGame_Derrota")
            ?? Resources.Load<Sprite>("UI_Themes/base/EndGame_Derrota");

        Sprite themedPanel = _unifiedVictoryPanelSprite
            ?? Resources.Load<Sprite>($"UI_Themes/{themeId}/EndGame_Panel")
            ?? Resources.Load<Sprite>("UI_Themes/base/EndGame_Panel");
        if (endGamePanel != null && themedPanel != null)
        {
            Image panelImg = endGamePanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.sprite = themedPanel;
                panelImg.preserveAspect = true;
                panelImg.color = Color.white;
            }
        }

        _victoryBannerSprite = Resources.Load<Sprite>($"UI_Themes/{themeId}/Banner_Victoria")
            ?? Resources.Load<Sprite>("UI_Themes/base/Banner_Victoria");
        _defeatBannerSprite = Resources.Load<Sprite>($"UI_Themes/{themeId}/Banner_Derrota")
            ?? Resources.Load<Sprite>("UI_Themes/base/Banner_Derrota");
        _starGoldSprite = starGoldSprite != null
            ? starGoldSprite
            : Resources.Load<Sprite>("UI_Themes/common/Estrella_Dorada");
        _starEmptySprite = starEmptySprite != null
            ? starEmptySprite
            : Resources.Load<Sprite>("UI_Themes/common/Estrella_Vacia");

        UpdateSandButtonVisual();
    }

    private void EnsureEndGamePanelLayout()
    {
        if (endGamePanel == null) return;

        if (_starGoldSprite == null)
        {
            _starGoldSprite = starGoldSprite != null
                ? starGoldSprite
                : Resources.Load<Sprite>("UI_Themes/common/Estrella_Dorada");
        }
        if (_starEmptySprite == null)
        {
            _starEmptySprite = starEmptySprite != null
                ? starEmptySprite
                : Resources.Load<Sprite>("UI_Themes/common/Estrella_Vacia");
        }

        Transform parentT = endGamePanel.transform.parent;
        if (parentT != null && _endGameDimmerGO == null)
        {
            Transform existingDim = parentT.Find("EndGame_Dimmer");
            if (existingDim != null)
            {
                _endGameDimmerGO = existingDim.gameObject;
            }
            else
            {
                _endGameDimmerGO = new GameObject("EndGame_Dimmer", typeof(RectTransform), typeof(Image));
                _endGameDimmerGO.transform.SetParent(parentT, false);
                RectTransform dimRT = _endGameDimmerGO.GetComponent<RectTransform>();
                dimRT.anchorMin = new Vector2(-0.5f, -0.5f);
                dimRT.anchorMax = new Vector2(1.5f, 1.5f);
                dimRT.offsetMin = Vector2.zero;
                dimRT.offsetMax = Vector2.zero;
                dimRT.localScale = Vector3.one;
                Image dimImg = _endGameDimmerGO.GetComponent<Image>();
                dimImg.color = new Color(0.05f, 0.08f, 0.12f, 0.72f);
                dimImg.raycastTarget = true;
                _endGameDimmerGO.SetActive(false);
            }
        }

        RectTransform parentRT = parentT as RectTransform;
        float parentW = (parentRT != null && parentRT.rect.width > 100f) ? parentRT.rect.width : 1080f;
        float parentH = (parentRT != null && parentRT.rect.height > 100f) ? parentRT.rect.height : 1920f;
        float maxW = parentW * 0.88f;
        float maxH = parentH * 0.78f;
        float panelW = Mathf.Min(maxW, maxH * ENDGAME_PANEL_ASPECT);
        float panelH = panelW / ENDGAME_PANEL_ASPECT;

        RectTransform egrt = endGamePanel.GetComponent<RectTransform>();
        if (egrt != null)
        {
            egrt.anchorMin = new Vector2(0.5f, 0.5f);
            egrt.anchorMax = new Vector2(0.5f, 0.5f);
            egrt.pivot = new Vector2(0.5f, 0.5f);
            egrt.anchoredPosition = new Vector2(0f, -parentH * 0.02f);
            egrt.sizeDelta = new Vector2(panelW, panelH);
            egrt.localScale = Vector3.one;
        }

        Image egImg = endGamePanel.GetComponent<Image>();
        if (egImg != null)
        {
            egImg.preserveAspect = true;
            egImg.color = Color.white;
            egImg.raycastTarget = true;
        }

        // Banner superior (¡VICTORIA! / ¡DERROTA!)
        if (_endGameBannerImage == null)
        {
            Transform existingBanner = endGamePanel.transform.Find("EndGame_Banner");
            if (existingBanner != null)
            {
                _endGameBannerImage = existingBanner.GetComponent<Image>();
            }
            else
            {
                GameObject bannerGO = new GameObject("EndGame_Banner", typeof(RectTransform), typeof(Image));
                bannerGO.transform.SetParent(endGamePanel.transform, false);
                RectTransform brt = bannerGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.04f, 0.74f);
                brt.anchorMax = new Vector2(0.96f, 1.055f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                brt.localScale = Vector3.one;
                _endGameBannerImage = bannerGO.GetComponent<Image>();
                _endGameBannerImage.preserveAspect = true;
                _endGameBannerImage.raycastTarget = false;
            }
        }

        // Fila de 3 Estrellas Pixel Art
        Transform starsRowT = endGamePanel.transform.Find("EndGame_StarsRow");
        if (starsRowT == null)
        {
            GameObject rowGO = new GameObject("EndGame_StarsRow", typeof(RectTransform));
            rowGO.transform.SetParent(endGamePanel.transform, false);
            RectTransform rrt = rowGO.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.14f, 0.578f);
            rrt.anchorMax = new Vector2(0.86f, 0.695f);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            rrt.localScale = Vector3.one;
            starsRowT = rowGO.transform;

            float[] cxList = new float[] { 0.21f, 0.50f, 0.79f };
            float[] cyList = new float[] { 0.45f, 0.56f, 0.45f };
            for (int i = 0; i < 3; i++)
            {
                GameObject slotGO = new GameObject($"StarSlot_{i}", typeof(RectTransform), typeof(Image));
                slotGO.transform.SetParent(starsRowT, false);
                RectTransform slotRT = slotGO.GetComponent<RectTransform>();
                slotRT.anchorMin = new Vector2(cxList[i] - 0.135f, cyList[i] - 0.44f);
                slotRT.anchorMax = new Vector2(cxList[i] + 0.135f, cyList[i] + 0.44f);
                slotRT.offsetMin = Vector2.zero;
                slotRT.offsetMax = Vector2.zero;
                slotRT.localScale = Vector3.one;

                Image bgStar = slotGO.GetComponent<Image>();
                bgStar.preserveAspect = true;
                bgStar.raycastTarget = false;
                if (_starEmptySprite != null) bgStar.sprite = _starEmptySprite;
                _starBgImages[i] = bgStar;

                GameObject fillGO = new GameObject($"StarFill_{i}", typeof(RectTransform), typeof(Image));
                fillGO.transform.SetParent(slotGO.transform, false);
                RectTransform fillRT = fillGO.GetComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = Vector2.one;
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
                fillRT.localScale = Vector3.one;

                Image fillStar = fillGO.GetComponent<Image>();
                fillStar.preserveAspect = true;
                fillStar.raycastTarget = false;
                if (_starGoldSprite != null) fillStar.sprite = _starGoldSprite;
                _starFillImages[i] = fillStar;
                fillGO.SetActive(false);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                Transform slotT = starsRowT.Find($"StarSlot_{i}");
                if (slotT != null)
                {
                    _starBgImages[i] = slotT.GetComponent<Image>();
                    if (_starBgImages[i] != null && _starEmptySprite != null) _starBgImages[i].sprite = _starEmptySprite;
                    Transform fillT = slotT.Find($"StarFill_{i}");
                    if (fillT != null)
                    {
                        _starFillImages[i] = fillT.GetComponent<Image>();
                        if (_starFillImages[i] != null && _starGoldSprite != null) _starFillImages[i].sprite = _starGoldSprite;
                    }
                }
            }
        }

        // Tarjetas visuales pixel art para métricas (reemplaza texto plano ScoreText)
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(false);
        }

        if (_statPrecisionValueText == null)
        {
            Transform cardsRow = endGamePanel.transform.Find("EndGame_StatCards");
            if (cardsRow == null)
            {
                GameObject rowGO = new GameObject("EndGame_StatCards", typeof(RectTransform));
                rowGO.transform.SetParent(endGamePanel.transform, false);
                RectTransform rrt = rowGO.GetComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0.09f, 0.415f);
                rrt.anchorMax = new Vector2(0.91f, 0.568f);
                rrt.offsetMin = Vector2.zero;
                rrt.offsetMax = Vector2.zero;
                rrt.localScale = Vector3.one;

                TMP_FontAsset font = ResolveFontAsset();
                Sprite chipSpr = Resources.Load<Sprite>("UI_Themes/common/UI_StatChip_Card");

                BuildStatCard(rowGO.transform, "StatCard_Precision", chipSpr,
                    new Vector2(0.00f, 0f), new Vector2(0.485f, 1f),
                    font, "PRECISIÓN", "0%", "Meta: 90%",
                    out _, out _statPrecisionValueText, out _statPrecisionSubText);

                BuildStatCard(rowGO.transform, "StatCard_Tiempo", chipSpr,
                    new Vector2(0.515f, 0f), new Vector2(1.00f, 1f),
                    font, "TIEMPO", "0.0s", "Estrellas: 0/3",
                    out _statSecondTitleText, out _statSecondValueText, out _statSecondSubText);
            }
        }

        UpdateEndGameButtonsForOutcome(true);
    }

    private static void BuildStatCard(
        Transform parent, string name, Sprite cardSprite,
        Vector2 aMin, Vector2 aMax, TMP_FontAsset font,
        string titleStr, string valStr, string subStr,
        out TextMeshProUGUI titleTMP, out TextMeshProUGUI valueTMP, out TextMeshProUGUI subTMP)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform crt = card.GetComponent<RectTransform>();
        crt.anchorMin = aMin;
        crt.anchorMax = aMax;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        Image cImg = card.GetComponent<Image>();
        if (cardSprite != null)
        {
            cImg.sprite = cardSprite;
            cImg.type = Image.Type.Sliced;
            cImg.color = Color.white;
        }
        else
        {
            cImg.color = new Color(0.95f, 0.87f, 0.72f, 0.96f);
        }
        cImg.raycastTarget = false;

        titleTMP = CreateChipTMP("Title", card.transform, font,
            new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.94f),
            titleStr, 16f, 24f, new Color(0.45f, 0.25f, 0.10f, 1f), FontStyles.Bold);

        valueTMP = CreateChipTMP("Value", card.transform, font,
            new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.70f),
            valStr, 26f, 44f, new Color(0.18f, 0.09f, 0.03f, 1f), FontStyles.Bold);

        subTMP = CreateChipTMP("Sub", card.transform, font,
            new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.30f),
            subStr, 14f, 21f, new Color(0.36f, 0.20f, 0.08f, 1f), FontStyles.Normal);
    }

    private static TextMeshProUGUI CreateChipTMP(
        string name, Transform parent, TMP_FontAsset font,
        Vector2 aMin, Vector2 aMax, string text,
        float minSize, float maxSize, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = style;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = minSize;
        tmp.fontSizeMax = maxSize;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void UpdateEndGameButtonsForOutcome(bool isWin)
    {
        if (endGamePanel == null) return;

        // Todos los botones mantienen EXACTAMENTE el mismo ancho (0.20..0.80) y alto (0.100)
        // Siguiendo jerarquía: Rojo (primario), Naranja (secundario), Gris (Menú)
        if (isWin)
        {
            if (retryButton != null && retryButton.image != null && _retryOrangeSprite != null)
            {
                retryButton.image.sprite = _retryOrangeSprite;
            }
            LayoutEndGameButton(nextLevelButton, true, new Vector2(0.20f, 0.302f), new Vector2(0.80f, 0.394f));
            LayoutEndGameButton(retryButton, true, new Vector2(0.20f, 0.196f), new Vector2(0.80f, 0.288f));
            LayoutEndGameButton(returnToMenuButton, true, new Vector2(0.20f, 0.090f), new Vector2(0.80f, 0.182f));
        }
        else
        {
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(false);
            if (retryButton != null && retryButton.image != null && _retryRedSprite != null)
            {
                retryButton.image.sprite = _retryRedSprite;
            }
            LayoutEndGameButton(retryButton, true, new Vector2(0.20f, 0.248f), new Vector2(0.80f, 0.344f));
            LayoutEndGameButton(returnToMenuButton, true, new Vector2(0.20f, 0.124f), new Vector2(0.80f, 0.220f));
        }
    }

    private void LayoutEndGameButton(Button btn, bool active, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(active);
        if (!active) return;

        if (btn.transform.parent != endGamePanel.transform)
        {
            btn.transform.SetParent(endGamePanel.transform, false);
        }
        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
        btn.transform.SetAsLastSibling();
        PreserveButtonAspect(btn);
    }

    private IEnumerator AnimateEndGameEntryAndStarsRoutine(bool isWin, int starsEarned)
    {
        if (endGamePanel != null)
        {
            Transform panelT = endGamePanel.transform;
            float t = 0f;
            const float entryDur = 0.22f;
            while (t < entryDur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / entryDur);
                float scale = u < 0.7f
                    ? Mathf.Lerp(0.84f, 1.04f, u / 0.7f)
                    : Mathf.Lerp(1.04f, 1f, (u - 0.7f) / 0.3f);
                panelT.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            panelT.localScale = Vector3.one;
        }

        int targetStars = isWin ? Mathf.Clamp(starsEarned, 0, 3) : 0;
        for (int i = 0; i < 3; i++)
        {
            if (_starBgImages[i] != null)
            {
                _starBgImages[i].color = isWin ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
            if (_starFillImages[i] != null)
            {
                _starFillImages[i].gameObject.SetActive(false);
                _starFillImages[i].transform.localScale = Vector3.one;
            }
        }

        if (!isWin || targetStars <= 0)
        {
            _starsAnimCoroutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.18f);

        for (int i = 0; i < targetStars; i++)
        {
            Image fill = _starFillImages[i];
            if (fill == null) continue;

            fill.gameObject.SetActive(true);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayStarPopSound(i);
            }
            float t = 0f;
            const float popDur = 0.22f;
            while (t < popDur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / popDur);
                float s = u < 0.65f
                    ? Mathf.Lerp(0.25f, 1.28f, u / 0.65f)
                    : Mathf.Lerp(1.28f, 1f, (u - 0.65f) / 0.35f);
                fill.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            fill.transform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.12f);
        }

        _starsAnimCoroutine = null;
    }

    public void OnToggleSandButtonClicked()
    {
        if (_waitingBucketTap)
        {
            _waitingBucketTap = false;
        }
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ToggleSandType();
            UpdateSandButtonVisual();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) ||
            (_hasRenderedSandState && LevelManager.Instance != null && LevelManager.Instance.isUsingWetSand != _lastRenderedWetSand))
        {
            UpdateSandButtonVisual();
        }

        // Atajo en PC (Tab) para abrir/cerrar rápido el plano objetivo (bloqueado durante tutorial o pausa)
        if (Input.GetKeyDown(KeyCode.Tab) && Time.timeScale > 0.01f && !_waitingModalClose && !_waitingBucketTap &&
            (endGamePanel == null || !endGamePanel.activeSelf))
        {
            ToggleBlueprintSheet();
        }

        // Soporte nativo para botón/gesto "Atrás" de Android (KeyCode.Escape)
        if (Input.GetKeyDown(KeyCode.Escape) && !_waitingModalClose && !_waitingBucketTap &&
            (LevelManager.Instance == null || !LevelManager.Instance.isGameOver) &&
            (endGamePanel == null || !endGamePanel.activeSelf))
        {
            if (_isBlueprintOpen)
            {
                SetBlueprintOpen(false);
            }
            else
            {
                PauseMenu pauseMenu = FindObjectOfType<PauseMenu>();
                if (pauseMenu != null)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonSound();
                    pauseMenu.TogglePause();
                }
            }
        }

        // Animación suave de la hoja arrugada subiendo o bajando desde la parte inferior
        if (blueprintSheetPanel != null)
        {
            Vector2 pos = blueprintSheetPanel.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, _blueprintTargetY, Time.unscaledDeltaTime * 12f);
            blueprintSheetPanel.anchoredPosition = pos;
        }

        UpdateLiveHudText();
    }

    private void UpdateLiveHudText()
    {
        if (LevelManager.Instance == null) return;

        float fill01 = Mathf.Clamp01(LevelManager.Instance.GetCurrentFillPercentage());
        int fillPct = Mathf.Clamp(Mathf.FloorToInt(fill01 * 100f), 0, 100);
        if (_hudCastlePctText != null)
        {
            _hudCastlePctText.text = $"{fillPct}%";
            _hudCastlePctText.color = fillPct >= 90
                ? new Color(0.55f, 0.96f, 0.56f, 1f)
                : Color.white;
        }
        if (_hudCastleFillBar != null)
        {
            RectTransform frt = _hudCastleFillBar.rectTransform;
            frt.anchorMax = new Vector2(fill01, 1f);
            _hudCastleFillBar.color = fillPct >= 90
                ? new Color(0.32f, 0.86f, 0.42f, 1f)
                : new Color(0.95f, 0.72f, 0.22f, 1f);
        }

        float tide01 = 0f;
        string tideTitle = "MAREA";
        Color tideBarColor = new Color(0.24f, 0.76f, 0.94f, 1f);
        if (TideController.Instance != null)
        {
            tide01 = Mathf.Clamp01(TideController.Instance.tideProgress01);
            float wind = TideController.Instance.currentWindIntensity;
            if (wind >= 0.20f && TideController.Instance.isGustActive)
            {
                tideTitle = wind >= 0.70f ? "¡HURACÁN!" : "¡RÁFAGA!";
                tideBarColor = wind >= 0.70f
                    ? new Color(0.96f, 0.34f, 0.28f, 1f)
                    : new Color(0.98f, 0.68f, 0.20f, 1f);
            }
        }
        if (_hudTideStatusText != null)
        {
            _hudTideStatusText.text = tideTitle;
            _hudTideStatusText.color = tideTitle == "MAREA"
                ? new Color(1f, 0.94f, 0.80f, 1f)
                : tideBarColor;
        }
        if (_hudTideFillBar != null)
        {
            _hudTideFillBar.rectTransform.anchorMax = new Vector2(tide01, 1f);
            _hudTideFillBar.color = tideBarColor;
        }
        if (_hudTimerText != null)
        {
            int secs = Mathf.Max(0, Mathf.FloorToInt(LevelManager.Instance.elapsedTime));
            _hudTimerText.text = $"{secs}s";
        }
    }

    public void UpdateSandButtonVisual()
    {
        if (LevelManager.Instance != null && sandButtonImage != null)
        {
            bool isWet = LevelManager.Instance.isUsingWetSand;
            if (isWet)
            {
                if (wetSandSprite != null)
                {
                    sandButtonImage.sprite = wetSandSprite;
                }
            }
            else
            {
                if (drySandSprite != null)
                {
                    sandButtonImage.sprite = drySandSprite;
                }
            }

            if (_hasRenderedSandState && _lastRenderedWetSand != isWet)
            {
                TriggerBucketPunchAnimation();
            }
            _hasRenderedSandState = true;
            _lastRenderedWetSand = isWet;
        }
    }

    private void TriggerBucketPunchAnimation()
    {
        if (toggleSandButton == null || !gameObject.activeInHierarchy) return;
        if (_bucketPunchCoroutine != null) StopCoroutine(_bucketPunchCoroutine);
        _bucketPunchCoroutine = StartCoroutine(AnimateBucketPunchRoutine());
    }

    private IEnumerator AnimateBucketPunchRoutine()
    {
        Transform bt = toggleSandButton != null ? toggleSandButton.transform : null;
        if (bt == null)
        {
            _bucketPunchCoroutine = null;
            yield break;
        }

        float t = 0f;
        const float duration = 0.24f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float sx;
            float sy;
            if (u < 0.35f)
            {
                float k = u / 0.35f;
                sx = Mathf.Lerp(1f, 1.18f, k);
                sy = Mathf.Lerp(1f, 0.84f, k);
            }
            else if (u < 0.72f)
            {
                float k = (u - 0.35f) / 0.37f;
                sx = Mathf.Lerp(1.18f, 0.93f, k);
                sy = Mathf.Lerp(0.84f, 1.12f, k);
            }
            else
            {
                float k = (u - 0.72f) / 0.28f;
                sx = Mathf.Lerp(0.93f, 1f, k);
                sy = Mathf.Lerp(1.12f, 1f, k);
            }
            bt.localScale = new Vector3(sx, sy, 1f);
            yield return null;
        }

        bt.localScale = Vector3.one;
        _bucketPunchCoroutine = null;
    }

    private void OnReturnToMenuClicked()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.GoToMainMenu();
        }
    }

    private void OnRetryClicked()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ReloadLevel();
        }
    }

    private void OnNextLevelClicked()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.GoToNextLevel();
        }
    }
}
