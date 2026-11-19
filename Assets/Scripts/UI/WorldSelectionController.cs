using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Representa la configuración visual e interactiva de cada botón de nivel en el Canvas.
/// </summary>
[System.Serializable]
public class LevelSlotUI
{
    [Tooltip("Botón interactivo de Unity UI")]
    public Button button;

    [Tooltip("Imagen de fondo del botón (Placa de madera con hojas)")]
    public Image buttonImage;

    [Tooltip("Texto con el número o nombre del nivel")]
    public TextMeshProUGUI levelTitleText;

    [Tooltip("Contenedor que aloja las 3 estrellas de puntuación")]
    public GameObject starsContainer;

    [Tooltip("Las 3 imágenes base de estrella vacía (silueta de madera oscura)")]
    public Image[] starImages = new Image[3];

    [Tooltip("Los 3 objetos hijos con el sprite de estrella amarilla dorada (apagados por defecto, se prenden al ganar estrellas)")]
    public GameObject[] starGoldObjects = new GameObject[3];

    [Tooltip("Overlay visual con candado si el nivel o mundo está bloqueado")]
    public GameObject lockOverlay;
}

/// <summary>
/// Controlador principal de la pantalla de Selección de Niveles estilo Carrusel ("Gira Mundial de Playas").
/// Navega entre mundos con fondo dinámico, cabecera de madera y botones verticales para mobile 9:16.
/// </summary>
public class WorldSelectionController : MonoBehaviour
{
    [Header("Referencias de Menú")]
    public MainMenuController mainMenuController;

    [Header("Mundos Disponibles (Data-Driven)")]
    [Tooltip("Lista de ScriptableObjects de mundos (Playa Local, Brasil, Japón, Islandia)")]
    public List<WorldPackData> worlds = new List<WorldPackData>();
    public int currentWorldIndex = 0;

    [Header("Visuales del Carrusel")]
    public Image backgroundImage;
    public Image headerPlaqueImage;
    public Button prevWorldButton;
    public Button nextWorldButton;

    [Header("Barra Inferior de Navegación")]
    public Button backButton;
    public Button storeButton;
    public TextMeshProUGUI coinsText;

    [Header("Slots de los 4 Niveles")]
    public LevelSlotUI[] levelSlots = new LevelSlotUI[4];

    [Header("Progresión por Estrellas Acumuladas")]
    [Tooltip("Estrellas acumuladas en el mundo necesarias para jugar cada nivel (Nivel 1: 0, Nivel 2: 1, Nivel 3: 3, Nivel 4: 5)")]
    public int[] requiredStarsPerLevel = new int[4] { 0, 1, 3, 5 };

    [Header("Sprites de UI")]
    public Sprite normalLevelButtonSprite;
    public Sprite bossLevelButtonSprite;
    public Sprite goldStarSprite;
    public Sprite emptyStarSprite;
    public Sprite lockIconSprite;

    [Header("Bloqueo de Mundo")]
    public GameObject worldLockedBanner;
    public TextMeshProUGUI lockedBannerPromptText;
    public GameObject comingSoonBadgeObject;
    public Button unlockInStoreButton;
    public TextMeshProUGUI unlockPriceText;

    private AspectCoverImage _backgroundCover;
    private WorldBackgroundAnimator _bgAnimator;
    private GameObject _lockedBannerDimmer;
    private bool _hasComprarSprite;

    private void Awake()
    {
        EnsureResponsiveLayout();
        EnsureCanonicalWorldOrder();

        if (prevWorldButton != null)
            prevWorldButton.onClick.AddListener(PrevWorld);

        if (nextWorldButton != null)
            nextWorldButton.onClick.AddListener(NextWorld);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (storeButton != null)
            storeButton.onClick.AddListener(OnStoreClicked);

        if (unlockInStoreButton != null)
            unlockInStoreButton.onClick.AddListener(OnStoreClicked);
    }

    private void EnsureResponsiveLayout()
    {
        RectTransform panelRt = GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
        }

        string bgName = "Background_Dinamico";
        if (backgroundImage != null)
        {
            bgName = backgroundImage.gameObject.name;
            backgroundImage.transform.SetAsFirstSibling();
            _backgroundCover = AspectCoverImage.Attach(backgroundImage.gameObject, 0.5625f);
            _bgAnimator = WorldBackgroundAnimator.Attach(backgroundImage.gameObject);
        }

        RectTransform safeRt = SafeAreaFitter.EnsureSafeAreaContainer(transform, "SafeArea_Content", bgName);
        Transform scope = safeRt != null ? (Transform)safeRt : transform;

        Transform headerT = scope.Find("Header_Carrusel");
        if (headerT != null)
        {
            RectTransform hrt = headerT.GetComponent<RectTransform>();
            if (hrt != null)
            {
                hrt.anchorMin = new Vector2(0.5f, 1f);
                hrt.anchorMax = new Vector2(0.5f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.anchoredPosition = new Vector2(0f, -14f);
                hrt.sizeDelta = new Vector2(416f, 126f);
            }
        }

        Transform bottomT = scope.Find("Bottom_Bar");
        if (bottomT != null)
        {
            RectTransform brt = bottomT.GetComponent<RectTransform>();
            if (brt != null)
            {
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.anchoredPosition = new Vector2(0f, 18f);
                brt.sizeDelta = new Vector2(-24f, 64f);
            }
            Transform sb = bottomT.Find("Btn_Tienda");
            if (sb != null)
            {
                RectTransform sbrt = sb.GetComponent<RectTransform>();
                if (sbrt != null) sbrt.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        PreserveButtonAspect(prevWorldButton);
        if (prevWorldButton != null)
        {
            RectTransform prt = prevWorldButton.GetComponent<RectTransform>();
            if (prt != null) prt.sizeDelta = new Vector2(54f, 54f);
        }
        PreserveButtonAspect(nextWorldButton);
        if (nextWorldButton != null)
        {
            RectTransform nrt = nextWorldButton.GetComponent<RectTransform>();
            if (nrt != null) nrt.sizeDelta = new Vector2(54f, 54f);
        }
        PreserveButtonAspect(backButton);
        if (backButton != null)
        {
            RectTransform backRt = backButton.GetComponent<RectTransform>();
            if (backRt != null) backRt.sizeDelta = new Vector2(165f, 42f);
        }
        PreserveButtonAspect(storeButton);
        PreserveButtonAspect(unlockInStoreButton);
        if (unlockInStoreButton != null)
        {
            RectTransform uRt = unlockInStoreButton.GetComponent<RectTransform>();
            if (uRt != null)
            {
                uRt.anchoredPosition = new Vector2(0f, -44f);
                uRt.sizeDelta = new Vector2(188f, 48f);
            }
            Sprite orangeComprar = Resources.Load<Sprite>("UI_Themes/common/Btn_Comprar_Naranja");
            if (orangeComprar != null && unlockInStoreButton.image != null)
            {
                unlockInStoreButton.image.sprite = orangeComprar;
                unlockInStoreButton.image.preserveAspect = true;
                _hasComprarSprite = true;
            }
            if (_hasComprarSprite && unlockPriceText != null)
            {
                unlockPriceText.text = string.Empty;
                unlockPriceText.gameObject.SetActive(false);
            }
        }
        if (comingSoonBadgeObject != null)
        {
            RectTransform csRt = comingSoonBadgeObject.GetComponent<RectTransform>();
            if (csRt != null)
            {
                csRt.anchoredPosition = new Vector2(0f, -44f);
                csRt.sizeDelta = new Vector2(188f, 48f);
            }
            Image csImg = comingSoonBadgeObject.GetComponent<Image>();
            if (csImg != null) csImg.preserveAspect = true;
        }
        EnsureLockedBannerDimmer(scope);
        if (headerPlaqueImage != null)
        {
            headerPlaqueImage.preserveAspect = true;
            headerPlaqueImage.rectTransform.anchoredPosition = Vector2.zero;
            headerPlaqueImage.rectTransform.sizeDelta = new Vector2(284f, 120f);
        }

        TMP_FontAsset pixelFont = ResolvePixelFontAsset();
        if (pixelFont != null)
        {
            if (coinsText != null)
            {
                coinsText.font = pixelFont;
                coinsText.color = new Color(1f, 0.96f, 0.85f, 1f);
                coinsText.enableWordWrapping = false;
                coinsText.overflowMode = TextOverflowModes.Overflow;
                coinsText.enableAutoSizing = false;
                coinsText.fontSize = 13.5f;
                coinsText.alignment = TextAlignmentOptions.Center;
                coinsText.rectTransform.anchoredPosition = new Vector2(-12f, -1f);
                coinsText.rectTransform.sizeDelta = new Vector2(88f, 28f);
            }
            if (lockedBannerPromptText != null)
            {
                lockedBannerPromptText.font = pixelFont;
                lockedBannerPromptText.color = new Color(0.20f, 0.10f, 0.04f, 1f);
            }
            if (unlockPriceText != null) unlockPriceText.font = pixelFont;
        }

        if (levelSlots != null)
        {
            // Proporción más compacta y armónica con el cartel superior (~252px de ancho) respetando 220:60
            float[] compactSlotYs = new float[] { 136f, 44f, -48f, -140f };
            for (int i = 0; i < levelSlots.Length; i++)
            {
                if (levelSlots[i] == null) continue;
                if (levelSlots[i].buttonImage != null)
                {
                    levelSlots[i].buttonImage.preserveAspect = true;
                    RectTransform btnRt = levelSlots[i].buttonImage.rectTransform;
                    btnRt.sizeDelta = new Vector2(252f, 69f);
                    if (i < compactSlotYs.Length)
                    {
                        btnRt.anchoredPosition = new Vector2(0f, compactSlotYs[i]);
                    }
                }
                if (levelSlots[i].starsContainer != null)
                {
                    RectTransform srt = levelSlots[i].starsContainer.GetComponent<RectTransform>();
                    if (srt != null)
                    {
                        // Centrado exactamente dentro del bloque hundido derecho (220x60 -> x=60..200, y=31..54)
                        srt.anchorMin = new Vector2(0.591f, 0.292f);
                        srt.anchorMax = new Vector2(0.591f, 0.292f);
                        srt.pivot = new Vector2(0.5f, 0.5f);
                        srt.anchoredPosition = Vector2.zero;
                        srt.sizeDelta = new Vector2(104f, 22f);
                    }
                    HorizontalLayoutGroup hlg = levelSlots[i].starsContainer.GetComponent<HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.childAlignment = TextAnchor.MiddleCenter;
                        hlg.spacing = 10f;
                        hlg.childControlWidth = false;
                        hlg.childControlHeight = false;
                        hlg.childForceExpandWidth = false;
                        hlg.childForceExpandHeight = false;
                    }
                    if (levelSlots[i].starImages != null)
                    {
                        for (int s = 0; s < levelSlots[i].starImages.Length; s++)
                        {
                            Image stImg = levelSlots[i].starImages[s];
                            if (stImg == null) continue;
                            stImg.preserveAspect = true;
                            stImg.rectTransform.sizeDelta = new Vector2(20f, 20f);
                            stImg.rectTransform.localScale = Vector3.one;
                        }
                    }
                    if (levelSlots[i].starGoldObjects != null)
                    {
                        for (int s = 0; s < levelSlots[i].starGoldObjects.Length; s++)
                        {
                            GameObject gObj = levelSlots[i].starGoldObjects[s];
                            if (gObj == null) continue;
                            RectTransform grt = gObj.GetComponent<RectTransform>();
                            if (grt != null)
                            {
                                grt.anchorMin = Vector2.zero;
                                grt.anchorMax = Vector2.one;
                                grt.offsetMin = Vector2.zero;
                                grt.offsetMax = Vector2.zero;
                                grt.localScale = Vector3.one;
                            }
                            Image gImg = gObj.GetComponent<Image>();
                            if (gImg != null) gImg.preserveAspect = true;
                        }
                    }
                }
                if (pixelFont != null && levelSlots[i].levelTitleText != null)
                {
                    levelSlots[i].levelTitleText.font = pixelFont;
                }
                PositionLockOnTopCenter(levelSlots[i]);
            }
        }
    }

    private TMP_FontAsset ResolvePixelFontAsset()
    {
        TMP_FontAsset pixel = Resources.Load<TMP_FontAsset>("Fonts/PixelSand");
        if (pixel != null) return pixel;
        if (coinsText != null && coinsText.font != null) return coinsText.font;
        return TMP_Settings.defaultFontAsset;
    }

    private void EnsureLockedBannerDimmer(Transform scope)
    {
        if (worldLockedBanner == null) return;
        Transform parent = worldLockedBanner.transform.parent != null ? worldLockedBanner.transform.parent : scope;
        if (parent == null) return;

        Transform existing = parent.Find("WorldLocked_Dimmer");
        if (existing != null)
        {
            _lockedBannerDimmer = existing.gameObject;
        }
        else
        {
            GameObject dimmerObj = new GameObject("WorldLocked_Dimmer", typeof(RectTransform), typeof(Image));
            dimmerObj.transform.SetParent(parent, false);
            _lockedBannerDimmer = dimmerObj;
        }

        RectTransform drt = _lockedBannerDimmer.GetComponent<RectTransform>();
        if (drt != null)
        {
            // Oscurece toda la pantalla (incluidos márgenes seguros) manteniendo flechas, cartel cabecera y volver/tienda brillantes encima
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(-600f, -600f);
            drt.offsetMax = new Vector2(600f, 600f);
        }

        Image dImg = _lockedBannerDimmer.GetComponent<Image>();
        if (dImg != null)
        {
            dImg.color = new Color(0.01f, 0.025f, 0.055f, 0.50f);
            dImg.raycastTarget = true;
        }

        ApplyWorldLockedSiblingOrder(parent, worldLockedBanner.activeSelf);
    }

    private void ApplyWorldLockedSiblingOrder(Transform scope, bool showLocked)
    {
        if (scope == null) return;
        if (_lockedBannerDimmer != null)
        {
            _lockedBannerDimmer.SetActive(showLocked);
            if (showLocked)
            {
                _lockedBannerDimmer.transform.SetAsLastSibling();
            }
        }

        Transform headerT = scope.Find("Header_Carrusel");
        if (headerT != null) headerT.SetAsLastSibling();
        Transform bottomT = scope.Find("Bottom_Bar");
        if (bottomT != null) bottomT.SetAsLastSibling();

        if (worldLockedBanner != null && showLocked)
        {
            worldLockedBanner.transform.SetAsLastSibling();
        }
    }

    private static void PositionLockOnTopCenter(LevelSlotUI slot)
    {
        if (slot == null || slot.lockOverlay == null) return;

        RectTransform lockRt = slot.lockOverlay.GetComponent<RectTransform>();
        if (lockRt == null) return;

        lockRt.anchorMin = new Vector2(0.5f, 1f);
        lockRt.anchorMax = new Vector2(0.5f, 1f);
        lockRt.pivot = new Vector2(0.5f, 0.5f);
        lockRt.anchoredPosition = new Vector2(0f, 1f);
        lockRt.sizeDelta = new Vector2(22f, 22f);
        lockRt.localScale = Vector3.one;

        Image lockImg = slot.lockOverlay.GetComponent<Image>();
        if (lockImg != null) lockImg.preserveAspect = true;
        lockRt.SetAsLastSibling();
    }

    private static void PreserveButtonAspect(Button btn)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.preserveAspect = true;
    }

    private void OnEnable()
    {
        EnsureCanonicalWorldOrder();
        currentWorldIndex = 0;
        DisplayWorld(0);
    }

    /// <summary>
    /// Garantiza que la lista de mundos respete siempre el orden de progresión:
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

    public void RefreshCoins()
    {
        if (worlds != null && currentWorldIndex >= 0 && currentWorldIndex < worlds.Count)
        {
            int totalStars = GetTotalWorldStars(worlds[currentWorldIndex]);
            if (coinsText != null)
            {
                coinsText.text = $"{totalStars} / 12";
            }
        }
    }

    public int GetStarsForScene(string sceneName, string packId = "", int levelIndex = 0)
    {
        int stars = PlayerPrefs.GetInt("STARS_" + sceneName, 0);
        if (stars == 0 && !string.IsNullOrEmpty(packId))
        {
            stars = PlayerPrefs.GetInt($"{packId}_L{levelIndex + 1}_Stars", 0);
        }
        return Mathf.Clamp(stars, 0, 3);
    }

    public int GetTotalWorldStars(WorldPackData world)
    {
        if (world == null || world.sceneNames == null) return 0;
        int total = 0;
        for (int i = 0; i < world.sceneNames.Length; i++)
        {
            total += GetStarsForScene(world.sceneNames[i], world.packId, i);
        }
        return total;
    }

    public void NextWorld()
    {
        if (worlds == null || worlds.Count == 0) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPageWhooshSound();
        currentWorldIndex = (currentWorldIndex + 1) % worlds.Count;
        DisplayWorld(currentWorldIndex);
    }

    public void PrevWorld()
    {
        if (worlds == null || worlds.Count == 0) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPageWhooshSound();
        currentWorldIndex--;
        if (currentWorldIndex < 0) currentWorldIndex = worlds.Count - 1;
        DisplayWorld(currentWorldIndex);
    }

    public void DisplayWorld(int index)
    {
        if (worlds == null || worlds.Count == 0) return;
        if (index < 0 || index >= worlds.Count) index = 0;
        currentWorldIndex = index;

        WorldPackData world = worlds[currentWorldIndex];
        if (world == null) return;

        // Cambiar el tema musical con crossfade suave según el mundo visible (ej. Brasil -> Samba/Bossa playero)
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetThemeByPackId(world.packId);
        }

        // 1. Actualizar Fondo Dinámico y su animación GPU + partículas por bioma
        if (backgroundImage != null && world.worldBackground != null)
        {
            backgroundImage.sprite = world.worldBackground;
            if (_backgroundCover != null) _backgroundCover.RefreshAspect();
            if (_bgAnimator != null) _bgAnimator.SetBiomeByPackId(world.packId);
        }

        // 2. Actualizar Placa de Cabecera
        if (headerPlaqueImage != null && world.headerPlaque != null)
        {
            headerPlaqueImage.sprite = world.headerPlaque;
        }

        // 3. Verificar si el mundo está desbloqueado y cuántas estrellas acumuló en este mundo
        bool isComingSoon = world.isComingSoon || world.packId == "dlc_japon" || world.packId == "dlc_islandia";
        bool isWorldUnlocked = !isComingSoon && IsWorldUnlocked(world);
        int totalWorldStars = isComingSoon ? 0 : GetTotalWorldStars(world);

        if (coinsText != null)
        {
            coinsText.text = $"{totalWorldStars} / 12";
        }

        if (worldLockedBanner != null)
        {
            worldLockedBanner.SetActive(!isWorldUnlocked);
            ApplyWorldLockedSiblingOrder(worldLockedBanner.transform.parent, !isWorldUnlocked);

            if (comingSoonBadgeObject != null)
            {
                comingSoonBadgeObject.SetActive(isComingSoon);
            }

            if (unlockInStoreButton != null)
            {
                unlockInStoreButton.gameObject.SetActive(!isComingSoon);
            }

            if (lockedBannerPromptText != null)
            {
                lockedBannerPromptText.text = isComingSoon
                    ? $"<b>{world.packTitle}</b>\n{world.difficultyLabel}"
                    : "¡PLAYA BLOQUEADA!\nAdquirí este pack en la tienda";
            }

            if (unlockPriceText != null)
            {
                if (_hasComprarSprite)
                {
                    unlockPriceText.text = string.Empty;
                    unlockPriceText.gameObject.SetActive(false);
                }
                else
                {
                    unlockPriceText.text = "COMPRAR";
                }
            }
        }

        // 4. Configurar los 4 Slots de Niveles
        for (int i = 0; i < levelSlots.Length; i++)
        {
            LevelSlotUI slot = levelSlots[i];
            if (slot == null || slot.button == null) continue;

            int levelIndex = i;
            string sceneName = (world.sceneNames != null && levelIndex < world.sceneNames.Length) 
                ? world.sceneNames[levelIndex] 
                : $"Nivel{levelIndex + 1}";

            string displayName = (world.levelDisplayNames != null && levelIndex < world.levelDisplayNames.Length)
                ? world.levelDisplayNames[levelIndex]
                : $"NIVEL {levelIndex + 1}";

            int reqStars = (requiredStarsPerLevel != null && levelIndex < requiredStarsPerLevel.Length)
                ? requiredStarsPerLevel[levelIndex]
                : 0;

            bool hasEnoughStars = totalWorldStars >= reqStars;
            bool canPlayLevel = isWorldUnlocked && hasEnoughStars;

            bool hasEmbeddedButtonSprite = world.levelButtonSprites != null
                && levelIndex < world.levelButtonSprites.Length
                && world.levelButtonSprites[levelIndex] != null;

            if (slot.levelTitleText != null)
            {
                // Si el sprite del botón ya trae su título e ícono climático embebido en Pixel Art, ocultar el texto UI
                slot.levelTitleText.gameObject.SetActive(!hasEmbeddedButtonSprite);
                if (!hasEmbeddedButtonSprite)
                {
                    if (isWorldUnlocked && !hasEnoughStars)
                    {
                        slot.levelTitleText.text = $"{displayName} ({totalWorldStars}/{reqStars} EST.)";
                    }
                    else
                    {
                        slot.levelTitleText.text = displayName;
                    }
                }
            }

            // Sprite: Botón Pixel Art embebido por nivel, o fallback genérico Normal / Boss
            if (slot.buttonImage != null)
            {
                slot.buttonImage.preserveAspect = false;
                slot.buttonImage.sprite = hasEmbeddedButtonSprite
                    ? world.levelButtonSprites[levelIndex]
                    : ((levelIndex == 3 && bossLevelButtonSprite != null) ? bossLevelButtonSprite : normalLevelButtonSprite);
            }

            // Interactabilidad y Candado
            slot.button.interactable = canPlayLevel;

            if (slot.lockOverlay != null)
            {
                slot.lockOverlay.SetActive(!canPlayLevel);
                PositionLockOnTopCenter(slot);
            }

            // Estrellas del nivel (0 por defecto; se prenden las amarillas según el récord logrado)
            int starsEarned = isComingSoon ? 0 : GetStarsForScene(sceneName, world.packId, levelIndex);

            bool hasGoldOverlays = false;
            if (slot.starGoldObjects != null && slot.starGoldObjects.Length > 0)
            {
                for (int s = 0; s < slot.starGoldObjects.Length; s++)
                {
                    if (slot.starGoldObjects[s] != null)
                    {
                        hasGoldOverlays = true;
                        slot.starGoldObjects[s].SetActive(s < starsEarned);
                    }
                }
            }

            if (slot.starImages != null)
            {
                for (int s = 0; s < slot.starImages.Length; s++)
                {
                    if (slot.starImages[s] != null)
                    {
                        if (hasGoldOverlays)
                        {
                            // Si tenemos los objetos de estrella amarilla encima, la base siempre es la estrella vacía
                            if (emptyStarSprite != null)
                            {
                                slot.starImages[s].sprite = emptyStarSprite;
                            }
                        }
                        else
                        {
                            // Fallback por si aún no se regeneró la escena con los hijos Gold_Star
                            slot.starImages[s].sprite = (s < starsEarned && goldStarSprite != null)
                                ? goldStarSprite
                                : emptyStarSprite;
                        }
                    }
                }
            }

            // Listeners de carga de escena
            slot.button.onClick.RemoveAllListeners();
            if (canPlayLevel)
            {
                slot.button.onClick.AddListener(() => LoadLevelScene(sceneName));
            }
        }
    }

    public bool IsWorldUnlocked(WorldPackData world)
    {
        if (world == null) return false;
        if (world.isUnlockedByDefault) return true;

        // Comprobación de persistencia en PlayerPrefs
        return PlayerPrefs.GetInt("SANDCASTLE_WORLD_UNLOCKED_" + world.packId, 0) == 1
            || PlayerPrefs.GetInt("SANDCASTLE_DLC_OWNED_" + world.packId, 0) == 1;
    }

    private void LoadLevelScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[WorldSelectionController] Nombre de escena vacío.");
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLevelSelectSound();
        }
        Debug.Log($"[WorldSelectionController] Cargando escena de nivel: {sceneName}");
        SceneManager.LoadScene(sceneName);
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

    public void OnStoreClicked()
    {
        if (mainMenuController != null)
        {
            mainMenuController.ShowStore();
        }
    }
}

