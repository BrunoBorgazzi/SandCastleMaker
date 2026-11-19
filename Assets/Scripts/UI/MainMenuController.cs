using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject levelSelectionPanel;
    public GameObject creditsPanel;
    public GameObject storePanel; // Pantalla S-03: Tienda de DLCs

    private void Awake()
    {
        EnsureRootCanvasScaler();
        EnsureMainMenuPanelResponsive();
        EnsureCreditsPanelResponsive();
    }

    private void Start()
    {
        // Asegurarnos de que solo el menú principal esté activo al iniciar
        ShowMainMenu();
    }

    private void Update()
    {
        // Soporte nativo para el botón/gesto "Atrás" de Android (KeyCode.Escape)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleAndroidBackButton();
        }
    }

    private void HandleAndroidBackButton()
    {
        if (storePanel != null && storePanel.activeSelf)
        {
            WorldStoreController storeCtrl = storePanel.GetComponent<WorldStoreController>();
            if (storeCtrl != null && storeCtrl.TryHandleBackKey())
            {
                return;
            }
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonSound();
            ShowMainMenu();
            return;
        }

        if ((levelSelectionPanel != null && levelSelectionPanel.activeSelf) ||
            (creditsPanel != null && creditsPanel.activeSelf))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonSound();
            ShowMainMenu();
            return;
        }

        QuitGame();
    }

    private void EnsureRootCanvasScaler()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();
        if (rootCanvas == null) return;

        CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(450f, 800f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }

    private void EnsureMainMenuPanelResponsive()
    {
        if (mainMenuPanel == null) return;

        RectTransform panelRt = mainMenuPanel.GetComponent<RectTransform>();
        if (panelRt == null) return;

        // Evitar que AspectRatioFitter en el panel padre expanda y desalinee botones en tablets/pantallas anchas
        AspectRatioFitter rootFitter = mainMenuPanel.GetComponent<AspectRatioFitter>();
        if (rootFitter != null)
        {
            Destroy(rootFitter);
        }

        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        panelRt.pivot = new Vector2(0.5f, 0.5f);

        RawImage rootRaw = mainMenuPanel.GetComponent<RawImage>();
        Transform bgVideoT = mainMenuPanel.transform.Find("Background_Video");
        if (bgVideoT == null && rootRaw != null)
        {
            GameObject bgObj = new GameObject("Background_Video", typeof(RectTransform), typeof(RawImage), typeof(AspectCoverImage));
            bgObj.transform.SetParent(mainMenuPanel.transform, false);
            bgObj.transform.SetAsFirstSibling();

            RawImage childRaw = bgObj.GetComponent<RawImage>();
            childRaw.texture = rootRaw.texture;
            childRaw.color = rootRaw.color;
            childRaw.uvRect = rootRaw.uvRect;
            childRaw.raycastTarget = false;

            float texAspect = (rootRaw.texture != null && rootRaw.texture.height > 1)
                ? (float)rootRaw.texture.width / rootRaw.texture.height
                : (16f / 9f);
            AspectCoverImage.Attach(bgObj, texAspect);
            Destroy(rootRaw);
        }
        else if (bgVideoT != null)
        {
            bgVideoT.SetAsFirstSibling();
            AspectCoverImage.Attach(bgVideoT.gameObject, 16f / 9f);
        }

        // Crisp vertical 9:16 interactive 60 FPS coastal diorama replaces static background
        Transform bgMenuT = mainMenuPanel.transform.Find("Background_Menu");
        if (bgMenuT != null)
        {
            if (bgVideoT != null) Destroy(bgVideoT.gameObject);
            foreach (var vp in mainMenuPanel.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true))
                vp.gameObject.SetActive(false);

            bgMenuT.SetAsFirstSibling();
            AspectCoverImage.Attach(bgMenuT.gameObject, 9f / 16f);
            InteractiveMenuDiorama.Attach(bgMenuT.gameObject, isCreditsMode: false);
        }

        RectTransform safeRt = SafeAreaFitter.EnsureSafeAreaContainer(
            mainMenuPanel.transform,
            "SafeArea_Content",
            bgMenuT != null ? "Background_Menu" : "Background_Video"
        );
        Transform scope = safeRt != null ? (Transform)safeRt : mainMenuPanel.transform;

        Transform logoT = scope.Find("Logo_SandCastleMaker");
        if (logoT != null)
        {
            RectTransform lrt = logoT.GetComponent<RectTransform>();
            if (lrt != null)
            {
                lrt.anchorMin = new Vector2(0.5f, 1f);
                lrt.anchorMax = new Vector2(0.5f, 1f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -22f);
                lrt.sizeDelta = new Vector2(336f, 136f);
                lrt.localScale = Vector3.one;
            }
            Image logoImg = logoT.GetComponent<Image>();
            if (logoImg != null)
            {
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;
            }
        }

        var menuButtons = new (Transform btn, float bottomY)[]
        {
            (scope.Find("Boton_Jugar"), 172f),
            (scope.Find("Boton_Tienda"), 102f),
            (scope.Find("Boton_Creditos"), 32f)
        };
        for (int i = 0; i < menuButtons.Length; i++)
        {
            Transform btnT = menuButtons[i].btn;
            if (btnT == null) continue;
            RectTransform brt = btnT.GetComponent<RectTransform>();
            if (brt != null)
            {
                brt.anchorMin = new Vector2(0.5f, 0f);
                brt.anchorMax = new Vector2(0.5f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.anchoredPosition = new Vector2(0f, menuButtons[i].bottomY);
                brt.sizeDelta = new Vector2(228f, 58f);
                brt.localScale = Vector3.one;
            }
            PreserveImageAspect(btnT);
        }

        Transform toggleMusicT = scope.Find("ToggleMusic");
        if (toggleMusicT != null)
        {
            RectTransform mrt = toggleMusicT.GetComponent<RectTransform>();
            if (mrt != null)
            {
                mrt.anchorMin = new Vector2(1f, 0f);
                mrt.anchorMax = new Vector2(1f, 0f);
                mrt.pivot = new Vector2(1f, 0f);
                mrt.anchoredPosition = new Vector2(-18f, 22f);
                mrt.sizeDelta = new Vector2(44f, 44f);
                mrt.localScale = Vector3.one;
            }
            PreserveImageAspect(toggleMusicT);
        }
    }

    private void EnsureCreditsPanelResponsive()
    {
        if (creditsPanel == null) return;

        RectTransform panelRt = creditsPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
        }

        Image rootImg = creditsPanel.GetComponent<Image>();
        Transform bgT = creditsPanel.transform.Find("Background_Creditos");
        if (rootImg != null && bgT == null)
        {
            GameObject bgObj = new GameObject("Background_Creditos", typeof(RectTransform), typeof(Image), typeof(AspectCoverImage));
            bgObj.transform.SetParent(creditsPanel.transform, false);
            bgObj.transform.SetAsFirstSibling();

            Image childImg = bgObj.GetComponent<Image>();
            childImg.sprite = rootImg.sprite;
            childImg.color = rootImg.color;
            childImg.type = rootImg.type;
            childImg.raycastTarget = true;

            AspectCoverImage.Attach(bgObj, 9f / 16f);
            InteractiveMenuDiorama.Attach(bgObj, isCreditsMode: true);
            Destroy(rootImg);
            bgT = bgObj.transform;
        }
        else if (bgT != null)
        {
            bgT.SetAsFirstSibling();
            AspectCoverImage.Attach(bgT.gameObject, 9f / 16f);
            InteractiveMenuDiorama.Attach(bgT.gameObject, isCreditsMode: true);
        }

        RectTransform safeRt = SafeAreaFitter.EnsureSafeAreaContainer(
            creditsPanel.transform,
            "SafeArea_Content",
            "Background_Creditos"
        );
        Transform scope = safeRt != null ? (Transform)safeRt : creditsPanel.transform;
        Transform btnVolver = scope.Find("Boton_Volver");
        if (btnVolver != null)
        {
            RectTransform vrt = btnVolver.GetComponent<RectTransform>();
            if (vrt != null)
            {
                vrt.anchorMin = new Vector2(0.5f, 0f);
                vrt.anchorMax = new Vector2(0.5f, 0f);
                vrt.pivot = new Vector2(0.5f, 0f);
                vrt.anchoredPosition = new Vector2(0f, 32f);
                vrt.sizeDelta = new Vector2(228f, 58f);
                vrt.localScale = Vector3.one;
            }
            for (int i = 0; i < btnVolver.childCount; i++)
                btnVolver.GetChild(i).gameObject.SetActive(false);
        }
        PreserveImageAspect(btnVolver);
    }

    private static void PreserveImageAspect(Transform t)
    {
        if (t == null) return;
        Image img = t.GetComponent<Image>();
        if (img != null) img.preserveAspect = true;
    }

    // --- Métodos para mostrar/ocultar paneles ---

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (storePanel != null) storePanel.SetActive(false);
        if (MusicManager.Instance != null) MusicManager.Instance.SetTheme(MusicManager.MusicTheme.Base);
    }

    public void ShowLevelSelection()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (storePanel != null) storePanel.SetActive(false);
    }

    public void ShowCredits()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(true);
        if (storePanel != null) storePanel.SetActive(false);
        if (MusicManager.Instance != null) MusicManager.Instance.SetTheme(MusicManager.MusicTheme.Base);
    }

    public void ShowStore()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (storePanel != null) storePanel.SetActive(true);
        if (MusicManager.Instance != null) MusicManager.Instance.SetTheme(MusicManager.MusicTheme.Base);
    }

    // --- Métodos para cargar escenas (Niveles) ---

    // Puedes llamar a este método desde el botón de un nivel específico
    // pasando el nombre de la escena como parámetro en el inspector de Unity.
    public void LoadLevel(string levelName)
    {
        SceneManager.LoadScene(levelName);
    }

    // Alternativa: cargar por índice de la Build (si prefieres usar números)
    public void LoadLevelByIndex(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void QuitGame()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }
}
