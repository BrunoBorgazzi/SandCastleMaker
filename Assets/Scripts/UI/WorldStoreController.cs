using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador principal de la pantalla "Tienda de Playas" (Panel_Tienda).
/// Maneja la exhibición de cards de DLC con botón y candado integrados,
/// animaciones táctiles de desbloqueo (game feel), conteo rodante de monedas
/// y navegación directa al mundo adquirido.
/// </summary>
public class WorldStoreController : MonoBehaviour
{
    [Header("Controladores de Navegación")]
    public MainMenuController mainMenuController;
    public WorldSelectionController worldSelectionController;

    [Header("Packs Disponibles (Data-Driven)")]
    public List<WorldPackData> worlds = new List<WorldPackData>();
    public int currentPackIndex = 0;

    [Header("Visuales de la Card Central")]
    public RectTransform cardContainerTransform;
    public Image cardDisplayImage;
    public Button prevCardButton;
    public Button nextCardButton;

    [Header("Elementos Modulares en el Zócalo de la Card")]
    public GameObject padlockOverlay;
    public Image padlockImage;
    public Button integratedActionButton;
    public Image integratedActionButtonImage;
    public TextMeshProUGUI integratedActionButtonText;

    [Header("Sprites Modulares de Botones y Candados")]
    public Sprite playButtonSprite;
    public Sprite buyButtonSprite;
    public Sprite comingSoonButtonSprite;
    public Sprite closedPadlockSprite;
    public Sprite openPadlockSprite;

    [Header("Barra Inferior de Navegación")]
    public Button backButton;
    public TextMeshProUGUI coinsText;
    public Transform coinIconTransform;

    [Header("Popup de Feedback Informativo")]
    public GameObject purchasePopup;
    public TextMeshProUGUI purchasePopupText;
    public Button closePopupButton;

    [Header("Paleta de Botones")]
    public Color buyColor = new Color(0.12f, 0.55f, 0.95f, 1f);      // Azul eléctrico
    public Color ownedColor = new Color(0.18f, 0.8f, 0.38f, 1f);      // Verde esmeralda
    public Color disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);     // Gris

    private bool _isAnimating = false;
    private WorldBackgroundAnimator _bgAnimator;
    private StoreCardAnimator _cardAnimator;
    private GameObject _comingSoonOverlay;
    private GameObject _purchasePopupDimmer;
    private Button _testerCodeTriggerButton;
    private GameObject _testerCodeModal;
    private TMP_InputField _testerCodeInput;
    private TextMeshProUGUI _testerCodeFeedbackText;

    private static readonly HashSet<string> VALID_TESTER_CODES = new HashSet<string>
    {
        "TEST67"
    };

    private void Awake()
    {
        EnsureCanonicalWorldOrder();
        EnsureResponsiveLayout();

        if (prevCardButton != null)
            prevCardButton.onClick.AddListener(PrevCard);

        if (nextCardButton != null)
            nextCardButton.onClick.AddListener(NextCard);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (closePopupButton != null)
            closePopupButton.onClick.AddListener(ClosePopup);

        if (integratedActionButton != null)
            integratedActionButton.onClick.AddListener(OnActionButtonClicked);
    }

    public void EnsureResponsiveLayout()
    {
        RectTransform panelRT = GetComponent<RectTransform>();
        if (panelRT != null)
        {
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
        }

        // Eliminar Canvas/CanvasScaler/GraphicRaycaster heredados si hay un Canvas padre
        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        Canvas ownCanvas = GetComponent<Canvas>();
        if (ownCanvas != null && parentCanvas != null && ownCanvas != parentCanvas)
        {
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) DestroyImmediate(raycaster);
            CanvasScaler legacyScaler = GetComponent<CanvasScaler>();
            if (legacyScaler != null) DestroyImmediate(legacyScaler);
            DestroyImmediate(ownCanvas);
        }

        CanvasScaler rootScaler = parentCanvas != null
            ? parentCanvas.GetComponent<CanvasScaler>()
            : GetComponentInParent<CanvasScaler>();
        if (rootScaler != null)
        {
            rootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            rootScaler.referenceResolution = new Vector2(450f, 800f);
            rootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        Transform bg = transform.Find("Background_Tienda");
        if (bg != null)
        {
            bg.SetAsFirstSibling();
            AspectCoverImage.Attach(bg.gameObject, 0.5625f);
            _bgAnimator = WorldBackgroundAnimator.Attach(bg.gameObject);
            if (_bgAnimator != null)
                _bgAnimator.SetBiome(WorldBackgroundAnimator.BiomeType.Tienda);
        }

        string popupName = purchasePopup != null ? purchasePopup.name : "Purchase_Feedback_Popup";
        SafeAreaFitter.EnsureSafeAreaContainer(transform, "SafeArea_Content", "Background_Tienda");
        if (bg != null) bg.SetAsFirstSibling();
        Transform popupTransform = purchasePopup != null
            ? purchasePopup.transform
            : SafeAreaFitter.FindInScope(transform, popupName);
        if (popupTransform != null)
        {
            popupTransform.SetAsLastSibling();
        }

        Transform headerTienda = SafeAreaFitter.FindInScope(transform, "Header_Tienda");
        if (headerTienda != null)
        {
            RectTransform hrt = headerTienda.GetComponent<RectTransform>();
            if (hrt != null) hrt.sizeDelta = new Vector2(288f, 100f);
            SetPreserveAspect(headerTienda.GetComponent<Image>());
        }

        if (cardDisplayImage != null)
        {
            cardDisplayImage.rectTransform.sizeDelta = new Vector2(288f, 460f);
            cardDisplayImage.preserveAspect = true;
            _cardAnimator = StoreCardAnimator.Attach(cardDisplayImage.gameObject);
        }

        RectTransform cardContainer = cardContainerTransform;
        if (cardContainer == null)
        {
            Transform foundCard = SafeAreaFitter.FindInScope(transform, "Card_Carousel_Container");
            if (foundCard != null) cardContainer = foundCard.GetComponent<RectTransform>();
        }
        if (cardContainer != null)
        {
            cardContainer.sizeDelta = new Vector2(410f, 520f);
        }

        Transform bottomBar = SafeAreaFitter.FindInScope(transform, "Bottom_Bar");
        if (bottomBar != null)
        {
            RectTransform bottomRT = bottomBar.GetComponent<RectTransform>();
            if (bottomRT != null)
            {
                bottomRT.anchorMin = new Vector2(0f, 0f);
                bottomRT.anchorMax = new Vector2(1f, 0f);
                bottomRT.pivot = new Vector2(0.5f, 0f);
                bottomRT.anchoredPosition = new Vector2(0f, 20f);
                bottomRT.sizeDelta = new Vector2(-24f, 64f);
            }
        }

        SetPreserveAspect(cardDisplayImage);
        if (prevCardButton != null) SetPreserveAspect(prevCardButton.image);
        if (nextCardButton != null) SetPreserveAspect(nextCardButton.image);
        SetPreserveAspect(padlockImage);
        SetPreserveAspect(integratedActionButtonImage);
        if (integratedActionButton != null) SetPreserveAspect(integratedActionButton.image);
        if (backButton != null)
        {
            SetPreserveAspect(backButton.image);
            RectTransform brt = backButton.GetComponent<RectTransform>();
            if (brt != null) brt.sizeDelta = new Vector2(165f, 42f);
        }
        if (closePopupButton != null)
        {
            SetPreserveAspect(closePopupButton.image);
            RectTransform cRt = closePopupButton.GetComponent<RectTransform>();
            if (cRt != null) cRt.sizeDelta = new Vector2(180f, 46f);
        }
        if (coinIconTransform != null) SetPreserveAspect(coinIconTransform.GetComponent<Image>());

        EnsurePurchasePopupDimmer(popupTransform);
        EnsureComingSoonOverlay();
        EnsureTesterCodeUI();

        TMP_FontAsset pixelFont = ResolveFontAsset();
        if (pixelFont != null)
        {
            if (coinsText != null) coinsText.font = pixelFont;
            if (purchasePopupText != null) purchasePopupText.font = pixelFont;
            if (integratedActionButtonText != null) integratedActionButtonText.font = pixelFont;
        }
    }

    private void EnsurePurchasePopupDimmer(Transform popupTransform)
    {
        if (popupTransform == null) return;
        Transform parent = popupTransform.parent != null ? popupTransform.parent : transform;

        Transform existing = parent.Find("PurchasePopup_Dimmer");
        if (existing != null)
        {
            _purchasePopupDimmer = existing.gameObject;
        }
        else
        {
            GameObject dimmerObj = new GameObject("PurchasePopup_Dimmer", typeof(RectTransform), typeof(Image));
            dimmerObj.transform.SetParent(parent, false);
            RectTransform drt = dimmerObj.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(-120f, -200f);
            drt.offsetMax = new Vector2(120f, 200f);

            Image dImg = dimmerObj.GetComponent<Image>();
            dImg.color = new Color(0.03f, 0.06f, 0.12f, 0.78f);
            dImg.raycastTarget = true;
            _purchasePopupDimmer = dimmerObj;
        }

        if (_purchasePopupDimmer != null)
        {
            _purchasePopupDimmer.SetActive(purchasePopup != null && purchasePopup.activeSelf);
        }
    }

    private static void SetPreserveAspect(Image img)
    {
        if (img != null) img.preserveAspect = true;
    }

    private void OnEnable()
    {
        EnsureCanonicalWorldOrder();
        if (_bgAnimator != null)
            _bgAnimator.SetBiome(WorldBackgroundAnimator.BiomeType.Tienda);
        if (_cardAnimator != null)
            _cardAnimator.SetExternalAnimationPaused(false);
        _isAnimating = false;
        RefreshCoinsInstant();
        if (purchasePopup != null) purchasePopup.SetActive(false);
        if (_purchasePopupDimmer != null) _purchasePopupDimmer.SetActive(false);
        if (_testerCodeModal != null) _testerCodeModal.SetActive(false);
        currentPackIndex = 0;
        DisplayCard(0);
    }

    /// <summary>
    /// Garantiza que las cartas de la tienda respeten siempre el orden de progresión:
    /// [0] Playa Local (pack_base), [1] Brasil (dlc_brasil), [2] Japón (dlc_japon), [3] Islandia (dlc_islandia).
    /// </summary>
    public void EnsureCanonicalWorldOrder()
    {
        if (worlds == null || worlds.Count <= 1) return;
        worlds.Sort((a, b) => GetWorldSortOrder(a).CompareTo(GetWorldSortOrder(b)));
    }

    private static int GetWorldSortOrder(WorldPackData w)
    {
        if (w == null) return 999;
        switch (w.packId)
        {
            case "pack_base": return 0;
            case "dlc_brasil": return 1;
            case "dlc_japon": return 2;
            case "dlc_islandia": return 3;
            default:
                return w.isUnlockedByDefault ? 0 : 50;
        }
    }

    public void RefreshCoinsInstant()
    {
        int coins = PlayerPrefs.GetInt("PlayerCoins", 150);
        if (coinsText != null)
        {
            coinsText.text = coins.ToString();
        }
    }

    public void NextCard()
    {
        if (_isAnimating || worlds == null || worlds.Count == 0) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPageWhooshSound();
        currentPackIndex = (currentPackIndex + 1) % worlds.Count;
        StartCoroutine(AnimateCardSwap(1));
    }

    public void PrevCard()
    {
        if (_isAnimating || worlds == null || worlds.Count == 0) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPageWhooshSound();
        currentPackIndex--;
        if (currentPackIndex < 0) currentPackIndex = worlds.Count - 1;
        StartCoroutine(AnimateCardSwap(-1));
    }

    private IEnumerator AnimateCardSwap(int direction)
    {
        _isAnimating = true;
        if (_cardAnimator != null) _cardAnimator.SetExternalAnimationPaused(true);
        if (cardDisplayImage != null)
        {
            // Micro animación de transición lateral
            Vector3 origPos = cardDisplayImage.rectTransform.localPosition;
            float duration = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                cardDisplayImage.rectTransform.localPosition = origPos + new Vector3(-direction * 35f * t, 0, 0);
                yield return null;
            }

            DisplayCard(currentPackIndex);

            elapsed = 0f;
            cardDisplayImage.rectTransform.localPosition = origPos + new Vector3(direction * 35f, 0, 0);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                cardDisplayImage.rectTransform.localPosition = Vector3.Lerp(origPos + new Vector3(direction * 35f, 0, 0), origPos, t);
                yield return null;
            }
            cardDisplayImage.rectTransform.localPosition = origPos;
        }
        else
        {
            DisplayCard(currentPackIndex);
        }
        if (_cardAnimator != null) _cardAnimator.SetExternalAnimationPaused(false);
        _isAnimating = false;
    }

    public void DisplayCard(int index)
    {
        if (worlds == null || worlds.Count == 0) return;
        if (index < 0 || index >= worlds.Count) index = 0;
        currentPackIndex = index;

        WorldPackData pack = worlds[currentPackIndex];
        if (pack == null) return;

        // 1. Asignar imagen de la card y activar su postal viva + partículas recortadas
        if (cardDisplayImage != null && pack.storeCard != null)
        {
            cardDisplayImage.sprite = pack.storeCard;
            if (_cardAnimator != null) _cardAnimator.SetPackById(pack.packId);
        }

        // 2. Determinar estado de posesión y aplicar velo escarchado pixel-exacto en el shader (sin cuadrado fantasma)
        bool isComingSoon = IsComingSoonPack(pack);
        bool isOwned = !isComingSoon && IsPackOwned(pack);

        EnsureComingSoonOverlay();
        if (_comingSoonOverlay != null)
        {
            _comingSoonOverlay.SetActive(false);
        }
        if (_cardAnimator != null)
        {
            _cardAnimator.SetComingSoonFrost(isComingSoon ? 0.14f : 0f);
        }

        // 3. Ocultar candado suelto en la Tienda (la distinción es 100% clara con JUGAR / COMPRAR / PRÓXIMAMENTE)
        if (padlockOverlay != null)
        {
            padlockOverlay.SetActive(false);
        }

        // 4. Botón modular independiente encastrado automáticamente dentro del hueco exacto de cada tarjeta
        if (integratedActionButton != null)
        {
            integratedActionButton.onClick.RemoveAllListeners();
            integratedActionButton.interactable = true;

            if (integratedActionButtonImage != null)
            {
                integratedActionButtonImage.color = Color.white;
                integratedActionButtonImage.preserveAspect = true;
            }
            if (integratedActionButtonText != null)
            {
                integratedActionButtonText.text = string.Empty;
            }

            if (isComingSoon)
            {
                if (integratedActionButtonImage != null && comingSoonButtonSprite != null)
                {
                    integratedActionButtonImage.sprite = comingSoonButtonSprite;
                }

                integratedActionButton.onClick.AddListener(() =>
                {
                    ShowPopup("¡PRÓXIMAMENTE!\n\nEste destino costero se abrirá en la próxima actualización de la Gira Mundial.");
                });
            }
            else if (isOwned)
            {
                if (integratedActionButtonImage != null && playButtonSprite != null)
                {
                    integratedActionButtonImage.sprite = playButtonSprite;
                }

                integratedActionButton.onClick.AddListener(() => GoToWorld(currentPackIndex));
            }
            else
            {
                if (integratedActionButtonImage != null && buyButtonSprite != null)
                {
                    integratedActionButtonImage.sprite = buyButtonSprite;
                }

                integratedActionButton.onClick.AddListener(() => TryBuyPack(pack));
            }

            RectTransform btnRt = integratedActionButton.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                if (_cardAnimator != null)
                {
                    _cardAnimator.BindActionButton(btnRt, integratedActionButtonImage);
                }
                else
                {
                    btnRt.anchoredPosition = new Vector2(0f, -168f);
                    btnRt.sizeDelta = new Vector2(168f, 43f);
                }
            }
        }
    }

    private static bool IsComingSoonPack(WorldPackData pack)
    {
        if (pack == null) return false;
        return pack.isComingSoon || pack.packId == "dlc_japon" || pack.packId == "dlc_islandia";
    }

    public string GetIapProductId(WorldPackData pack)
    {
        if (pack == null) return string.Empty;
        if (!string.IsNullOrEmpty(pack.iapProductId)) return pack.iapProductId;

        switch (pack.packId)
        {
            case "dlc_brasil": return "sandcastle_dlc_brasil";
            case "dlc_japon": return "sandcastle_dlc_japon";
            case "dlc_islandia": return "sandcastle_dlc_islandia";
            default: return string.Empty;
        }
    }

    public bool IsPackOwned(WorldPackData pack)
    {
        if (pack == null) return false;
        if (pack.isUnlockedByDefault) return true;

        return PlayerPrefs.GetInt("SANDCASTLE_WORLD_UNLOCKED_" + pack.packId, 0) == 1
            || PlayerPrefs.GetInt("SANDCASTLE_DLC_OWNED_" + pack.packId, 0) == 1;
    }

    private void OnActionButtonClicked()
    {
        if (_isAnimating || worlds == null || currentPackIndex >= worlds.Count) return;
        WorldPackData pack = worlds[currentPackIndex];
        if (pack == null) return;

        if (IsComingSoonPack(pack))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayLockedSound();
            ShowPopup("¡PRÓXIMAMENTE!\n\nEste destino costero se abrirá en la próxima actualización de la Gira Mundial.");
            return;
        }

        if (IsPackOwned(pack))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayLevelSelectSound();
            GoToWorld(currentPackIndex);
        }
        else
        {
            TryBuyPack(pack);
        }
    }

    public void TryBuyPack(WorldPackData pack)
    {
        if (_isAnimating || pack == null || IsComingSoonPack(pack)) return;

        string iapId = GetIapProductId(pack);
        Debug.Log($"[WorldStoreController] Iniciando compra del pack '{pack.packTitle}' (IAP: '{iapId}').");

        // Hook listo para Unity IAP / Billing nativo: actualmente desbloquea el pack al instante para QA/Demo
        StartCoroutine(AnimateUnlockSequence(pack));
    }

    private IEnumerator AnimateUnlockSequence(WorldPackData pack)
    {
        _isAnimating = true;
        if (_cardAnimator != null) _cardAnimator.SetExternalAnimationPaused(true);

        // 1. Temblor del Candado (vibración de destrabe)
        if (padlockOverlay != null && padlockOverlay.activeSelf)
        {
            if (padlockImage == null) padlockImage = padlockOverlay.GetComponent<Image>();

            Vector3 originalPos = padlockOverlay.transform.localPosition;
            float elapsed = 0f;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float angle = Mathf.Sin(elapsed * 50f) * 14f;
                padlockOverlay.transform.localEulerAngles = new Vector3(0, 0, angle);
                yield return null;
            }
            padlockOverlay.transform.localEulerAngles = Vector3.zero;

            // ¡Cambiar sprite a Candado Abierto!
            if (padlockImage != null && openPadlockSprite != null)
            {
                padlockImage.sprite = openPadlockSprite;
            }
            yield return new WaitForSeconds(0.18f);

            // 2. Salto y Fade Out del candado abierto
            elapsed = 0f;
            duration = 0.35f;
            Vector3 jumpTarget = originalPos + new Vector3(0, 45f, 0);
            Vector3 startScale = padlockOverlay.transform.localScale;

            CanvasGroup lockCg = padlockOverlay.GetComponent<CanvasGroup>();
            if (lockCg == null) lockCg = padlockOverlay.gameObject.AddComponent<CanvasGroup>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                padlockOverlay.transform.localPosition = Vector3.Lerp(originalPos, jumpTarget, Mathf.Sin(t * Mathf.PI * 0.5f));
                padlockOverlay.transform.localScale = Vector3.Lerp(startScale, startScale * 1.35f, t);
                lockCg.alpha = 1f - t;
                yield return null;
            }

            padlockOverlay.SetActive(false);
            padlockOverlay.transform.localPosition = originalPos;
            padlockOverlay.transform.localScale = startScale;
            lockCg.alpha = 1f;
        }

        // 3. Guardar compra directa del pack en PlayerPrefs y sincronizar DLCManager
        SetPackUnlockedState(pack.packId, true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPackUnlockSound();

        // 4. Actualizar botón modular de COMPRAR -> JUGAR y animar Scale Punch del botón y la Card
        DisplayCard(currentPackIndex);

        if (integratedActionButton != null)
        {
            float elapsed = 0f;
            float duration = 0.32f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float punch = Mathf.Sin((elapsed / duration) * Mathf.PI) * 0.18f;
                integratedActionButton.transform.localScale = Vector3.one * (1f + punch);
                if (cardDisplayImage != null)
                {
                    cardDisplayImage.transform.localScale = Vector3.one * (1f + punch * 0.35f);
                }
                yield return null;
            }
            integratedActionButton.transform.localScale = Vector3.one;
            if (cardDisplayImage != null) cardDisplayImage.transform.localScale = Vector3.one;
        }

        if (_cardAnimator != null) _cardAnimator.SetExternalAnimationPaused(false);
        _isAnimating = false;
        Debug.Log($"[WorldStoreController] ¡Pack {pack.packTitle} desbloqueado exitosamente!");
    }

    private static void SetPackUnlockedState(string packId, bool unlocked)
    {
        if (string.IsNullOrEmpty(packId)) return;

        int val = unlocked ? 1 : 0;
        PlayerPrefs.SetInt("SANDCASTLE_WORLD_UNLOCKED_" + packId, val);
        PlayerPrefs.SetInt("SANDCASTLE_DLC_OWNED_" + packId, val);
        PlayerPrefs.Save();

        if (DLCManager.Instance != null)
        {
            DLCManager.Instance.LoadDLCStates();
        }
    }

    private void EnsureComingSoonOverlay()
    {
        if (cardDisplayImage == null) return;

        // El velo escarchado de Próximamente ahora se renderiza por shader sobre la silueta exacta de la card (cero rectángulo fantasma).
        // Si existía el overlay rectangular heredado en la escena, lo desactivamos limpiamente.
        Transform existing = cardDisplayImage.transform.Find("ComingSoon_Frosted_Overlay");
        if (existing != null)
        {
            _comingSoonOverlay = existing.gameObject;
            _comingSoonOverlay.SetActive(false);
        }
    }

    private void EnsureTesterCodeUI()
    {
        Transform safeArea = SafeAreaFitter.FindInScope(transform, "SafeArea_Content");
        Transform triggerParent = safeArea != null ? safeArea : transform;

        if (_testerCodeTriggerButton == null)
        {
            Transform existingBtn = triggerParent.Find("Tester_Code_Button");
            if (existingBtn != null)
            {
                _testerCodeTriggerButton = existingBtn.GetComponent<Button>();
            }
            else
            {
                _testerCodeTriggerButton = CreateProceduralButton(
                    "Tester_Code_Button",
                    triggerParent,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-8f, -10f), new Vector2(76f, 28f),
                    new Color(0.10f, 0.18f, 0.28f, 0.90f),
                    "CÓDIGO",
                    11.5f,
                    new Color(1f, 0.88f, 0.50f, 1f)
                );
            }

            if (_testerCodeTriggerButton != null)
            {
                RectTransform tRt = _testerCodeTriggerButton.GetComponent<RectTransform>();
                if (tRt != null)
                {
                    tRt.anchorMin = new Vector2(1f, 1f);
                    tRt.anchorMax = new Vector2(1f, 1f);
                    tRt.pivot = new Vector2(1f, 1f);
                    tRt.anchoredPosition = new Vector2(-8f, -10f);
                    tRt.sizeDelta = new Vector2(76f, 28f);
                }

                Sprite miniCodeSpr = Resources.Load<Sprite>("UI_Themes/common/Btn_Codigo_Mini");
                Image tImg = _testerCodeTriggerButton.GetComponent<Image>();
                TextMeshProUGUI tLabel = _testerCodeTriggerButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (miniCodeSpr != null && tImg != null)
                {
                    tImg.sprite = miniCodeSpr;
                    tImg.color = Color.white;
                    tImg.preserveAspect = true;
                    if (tLabel != null)
                    {
                        tLabel.text = string.Empty;
                        tLabel.gameObject.SetActive(false);
                    }
                }
                else if (tLabel != null)
                {
                    tLabel.text = "CÓDIGO";
                    tLabel.fontSize = 11.5f;
                    tLabel.enableWordWrapping = false;
                }

                _testerCodeTriggerButton.onClick.RemoveListener(OpenTesterCodeModal);
                _testerCodeTriggerButton.onClick.AddListener(OpenTesterCodeModal);
            }
        }

        if (_testerCodeModal == null)
        {
            Transform existingModal = transform.Find("TesterCode_Modal_Overlay");
            if (existingModal != null)
            {
                _testerCodeModal = existingModal.gameObject;
                return;
            }

            TMP_FontAsset font = ResolveFontAsset();

            GameObject modalGO = new GameObject("TesterCode_Modal_Overlay", typeof(RectTransform), typeof(Image));
            modalGO.transform.SetParent(transform, false);
            RectTransform modalRT = modalGO.GetComponent<RectTransform>();
            modalRT.anchorMin = Vector2.zero;
            modalRT.anchorMax = Vector2.one;
            modalRT.offsetMin = Vector2.zero;
            modalRT.offsetMax = Vector2.zero;

            Image dimmer = modalGO.GetComponent<Image>();
            dimmer.color = new Color(0.03f, 0.06f, 0.12f, 0.78f);
            dimmer.raycastTarget = true;

            GameObject cardGO = new GameObject("TesterCode_Card", typeof(RectTransform), typeof(Image), typeof(Outline));
            cardGO.transform.SetParent(modalGO.transform, false);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.sizeDelta = new Vector2(396f, 360f);

            Image cardImg = cardGO.GetComponent<Image>();
            cardImg.color = new Color(0.12f, 0.20f, 0.32f, 0.98f);
            cardImg.raycastTarget = true;

            Outline cardBorder = cardGO.GetComponent<Outline>();
            cardBorder.effectColor = new Color(0.36f, 0.56f, 0.78f, 0.85f);
            cardBorder.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI titleTMP = CreateModalLabel("Title", cardGO.transform, font, "ACCESO PARA TESTERS",
                new Vector2(0f, 134f), new Vector2(364f, 36f), 16.5f, FontStyles.Bold, new Color(1f, 0.88f, 0.45f, 1f));
            titleTMP.enableWordWrapping = false;

            TextMeshProUGUI subTMP = CreateModalLabel("Subtitle", cardGO.transform, font,
                "Ingresá un código para desbloquear packs (o RESET para resetear tus packs):",
                new Vector2(0f, 72f), new Vector2(348f, 68f), 12.5f, FontStyles.Normal, new Color(0.92f, 0.96f, 1f, 1f));
            subTMP.lineSpacing = 8f;

            _testerCodeInput = CreateModalInputField(cardGO.transform, font);

            _testerCodeFeedbackText = CreateModalLabel("Feedback", cardGO.transform, font, string.Empty,
                new Vector2(0f, -60f), new Vector2(344f, 42f), 12f, FontStyles.Bold, new Color(1f, 0.58f, 0.52f, 1f));

            Button redeemBtn = CreateProceduralButton(
                "Redeem_Button", cardGO.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-84f, -124f), new Vector2(152f, 46f),
                new Color(0.14f, 0.68f, 0.38f, 1f),
                "CANJEAR", 15f, Color.white
            );
            redeemBtn.onClick.AddListener(OnSubmitTesterCode);

            Button closeBtn = CreateProceduralButton(
                "Close_Modal_Button", cardGO.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(84f, -124f), new Vector2(152f, 46f),
                new Color(0.48f, 0.24f, 0.26f, 1f),
                "CERRAR", 15f, Color.white
            );
            closeBtn.onClick.AddListener(CloseTesterCodeModal);

            _testerCodeModal = modalGO;
            _testerCodeModal.SetActive(false);
        }
    }

    private TMP_FontAsset ResolveFontAsset()
    {
        TMP_FontAsset pixel = Resources.Load<TMP_FontAsset>("Fonts/PixelSand");
        if (pixel != null) return pixel;
        if (purchasePopupText != null && purchasePopupText.font != null) return purchasePopupText.font;
        if (coinsText != null && coinsText.font != null) return coinsText.font;
        if (integratedActionButtonText != null && integratedActionButtonText.font != null) return integratedActionButtonText.font;
        return TMP_Settings.defaultFontAsset;
    }

    private Button CreateProceduralButton(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor,
        string caption, float fontSize, Color textColor)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = btnGO.GetComponent<Image>();
        img.color = bgColor;

        GameObject txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(6f, 2f);
        txtRT.offsetMax = new Vector2(-6f, -2f);

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.font = ResolveFontAsset();
        tmp.text = caption;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        return btnGO.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateModalLabel(
        string name, Transform parent, TMP_FontAsset font, string text,
        Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private TMP_InputField CreateModalInputField(Transform parent, TMP_FontAsset font)
    {
        GameObject inputGO = new GameObject("TesterCode_Input", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(TMP_InputField));
        inputGO.transform.SetParent(parent, false);

        RectTransform inputRT = inputGO.GetComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0.5f, 0.5f);
        inputRT.anchorMax = new Vector2(0.5f, 0.5f);
        inputRT.pivot = new Vector2(0.5f, 0.5f);
        inputRT.anchoredPosition = new Vector2(0f, -4f);
        inputRT.sizeDelta = new Vector2(336f, 48f);

        Image inputBg = inputGO.GetComponent<Image>();
        inputBg.color = new Color(0.06f, 0.10f, 0.17f, 1f);

        Outline inputBorder = inputGO.GetComponent<Outline>();
        inputBorder.effectColor = new Color(0.30f, 0.48f, 0.68f, 0.80f);
        inputBorder.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject textAreaGO = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaGO.transform.SetParent(inputGO.transform, false);
        RectTransform areaRT = textAreaGO.GetComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(14f, 6f);
        areaRT.offsetMax = new Vector2(-14f, -6f);

        TextMeshProUGUI placeholderTMP = CreateModalLabel("Placeholder", textAreaGO.transform, font, "Ingresá tu código...",
            Vector2.zero, Vector2.zero, 13.5f, FontStyles.Italic, new Color(0.55f, 0.68f, 0.78f, 0.75f));
        placeholderTMP.rectTransform.anchorMin = Vector2.zero;
        placeholderTMP.rectTransform.anchorMax = Vector2.one;
        placeholderTMP.alignment = TextAlignmentOptions.Left;
        placeholderTMP.enableWordWrapping = false;

        TextMeshProUGUI textTMP = CreateModalLabel("Text", textAreaGO.transform, font, string.Empty,
            Vector2.zero, Vector2.zero, 15.5f, FontStyles.Bold, Color.white);
        textTMP.rectTransform.anchorMin = Vector2.zero;
        textTMP.rectTransform.anchorMax = Vector2.one;
        textTMP.alignment = TextAlignmentOptions.Left;
        textTMP.enableWordWrapping = false;

        TMP_InputField inputField = inputGO.GetComponent<TMP_InputField>();
        inputField.targetGraphic = inputBg;
        inputField.textViewport = areaRT;
        inputField.textComponent = textTMP;
        inputField.placeholder = placeholderTMP;
        inputField.characterLimit = 20;
        inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        inputField.onSubmit.AddListener(_ => OnSubmitTesterCode());

        return inputField;
    }

    public void OpenTesterCodeModal()
    {
        EnsureTesterCodeUI();
        if (_testerCodeModal == null) return;

        if (_testerCodeInput != null)
        {
            _testerCodeInput.text = string.Empty;
        }
        if (_testerCodeFeedbackText != null)
        {
            _testerCodeFeedbackText.text = string.Empty;
        }

        _testerCodeModal.SetActive(true);
        _testerCodeModal.transform.SetAsLastSibling();
    }

    public void CloseTesterCodeModal()
    {
        if (_testerCodeModal != null)
        {
            _testerCodeModal.SetActive(false);
        }
    }

    public void OnSubmitTesterCode()
    {
        string raw = _testerCodeInput != null ? _testerCodeInput.text : string.Empty;
        string code = raw.Trim().ToUpperInvariant();

        if (code == "RESET" || code == "RESETTEST" || code == "RESETPACKS")
        {
            // Reiniciar todos los packs comprables (no gratuitos por defecto)
            if (worlds != null)
            {
                for (int i = 0; i < worlds.Count; i++)
                {
                    WorldPackData w = worlds[i];
                    if (w != null && !w.isUnlockedByDefault && !string.IsNullOrEmpty(w.packId))
                    {
                        SetPackUnlockedState(w.packId, false);
                    }
                }
            }
            SetPackUnlockedState("dlc_brasil", false);
            SetPackUnlockedState("dlc_japon", false);
            SetPackUnlockedState("dlc_islandia", false);

            DisplayCard(currentPackIndex);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPageWhooshSound();
            if (_testerCodeFeedbackText != null)
            {
                _testerCodeFeedbackText.color = new Color(1f, 0.85f, 0.45f, 1f);
                _testerCodeFeedbackText.text = "Todos los packs comprados fueron reiniciados.";
            }
            return;
        }

        if (VALID_TESTER_CODES.Contains(code))
        {
            int focusIndex = -1;
            int unlockedCount = 0;
            if (worlds != null)
            {
                for (int i = 0; i < worlds.Count; i++)
                {
                    WorldPackData w = worlds[i];
                    if (w != null && !w.isUnlockedByDefault && !IsComingSoonPack(w) && !string.IsNullOrEmpty(w.packId))
                    {
                        SetPackUnlockedState(w.packId, true);
                        unlockedCount++;
                        if (focusIndex < 0) focusIndex = i;
                    }
                }
            }
            if (unlockedCount == 0)
            {
                SetPackUnlockedState("dlc_brasil", true);
            }

            if (focusIndex >= 0)
            {
                currentPackIndex = focusIndex;
            }
            DisplayCard(currentPackIndex);
            CloseTesterCodeModal();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPackUnlockSound();
            ShowPopup("¡MODO TESTER ACTIVADO!\n\nTodos los packs disponibles fueron desbloqueados.");
            return;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayLockedSound();
        if (_testerCodeFeedbackText != null)
        {
            _testerCodeFeedbackText.color = new Color(1f, 0.52f, 0.48f, 1f);
            _testerCodeFeedbackText.text = "Código inválido. Verificá e intentá de nuevo.";
        }
    }

    private IEnumerator AnimateCoinDeduction(int fromCoins, int toCoins)
    {
        float elapsed = 0f;
        float duration = 0.55f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            int current = Mathf.RoundToInt(Mathf.Lerp(fromCoins, toCoins, t));

            if (coinsText != null)
            {
                coinsText.text = current.ToString();
            }

            // Micro-punch scale en el icono de la moneda
            if (coinIconTransform != null)
            {
                float coinPunch = Mathf.Sin(t * Mathf.PI * 4f) * 0.22f;
                coinIconTransform.localScale = Vector3.one * (1f + Mathf.Max(0, coinPunch));
            }

            yield return null;
        }

        if (coinsText != null) coinsText.text = toCoins.ToString();
        if (coinIconTransform != null) coinIconTransform.localScale = Vector3.one;
    }

    public void GoToWorld(int worldIndex)
    {
        if (mainMenuController != null)
        {
            mainMenuController.ShowLevelSelection();
        }

        if (worldSelectionController != null)
        {
            worldSelectionController.DisplayWorld(worldIndex);
        }
    }

    private void ShowPopup(string message)
    {
        if (purchasePopup != null)
        {
            if (purchasePopupText != null)
            {
                purchasePopupText.text = message;
            }
            if (_purchasePopupDimmer != null)
            {
                _purchasePopupDimmer.SetActive(true);
                _purchasePopupDimmer.transform.SetAsLastSibling();
            }
            purchasePopup.SetActive(true);
            purchasePopup.transform.SetAsLastSibling();
        }
    }

    public void ClosePopup()
    {
        if (_purchasePopupDimmer != null)
        {
            _purchasePopupDimmer.SetActive(false);
        }
        if (purchasePopup != null)
        {
            purchasePopup.SetActive(false);
        }
    }

    public bool TryHandleBackKey()
    {
        if (_testerCodeModal != null && _testerCodeModal.activeSelf)
        {
            CloseTesterCodeModal();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonSound();
            return true;
        }
        if (purchasePopup != null && purchasePopup.activeSelf)
        {
            ClosePopup();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonSound();
            return true;
        }
        return false;
    }

    public void OnBackClicked()
    {
        if (mainMenuController != null)
        {
            mainMenuController.ShowMainMenu();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
