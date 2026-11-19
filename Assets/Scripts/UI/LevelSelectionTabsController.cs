using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controlador de la Pantalla S-02 (Selección de Niveles) con sistema de Pestañas (Tabs)
/// para alternar entre la Campaña Principal y las Expansiones adquiridas.
/// También gestiona el Pop-up S-02-1 de contenido bloqueado.
/// </summary>
public class LevelSelectionTabsController : MonoBehaviour
{
    [Header("Referencias Generales")]
    public MainMenuController mainMenuController;
    public Button backToMenuButton;

    [Header("Contenedores de Grillas")]
    [Tooltip("Panel o grilla con los niveles de la Campaña Base")]
    public GameObject baseCampaignGrid;

    [Tooltip("Panel o grilla donde se muestran los niveles de la expansión seleccionada")]
    public GameObject dlcLevelsGrid;

    [Tooltip("Contenedor donde se generan o alojan los botones de niveles de DLC")]
    public Transform dlcLevelsContainer;

    [Tooltip("Prefab de botón de nivel para instanciar en las expansiones")]
    public GameObject levelButtonPrefab;

    [Header("Pestañas (Tabs)")]
    public Button baseCampaignTabButton;
    public Image baseCampaignTabImage;

    [Tooltip("Contenedor horizontal donde van las pestañas de expansiones")]
    public Transform tabsContainer;

    [Tooltip("Prefab del botón de pestaña de expansión (debe contener Button, Text y LockIcon)")]
    public GameObject expansionTabPrefab;

    [Header("Colores de Pestañas")]
    public Color activeTabColor = new Color(1f, 0.9f, 0.6f, 1f);
    public Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Pantalla S-02-1: Pop-up Contenido Bloqueado")]
    public GameObject blockedPopup;
    public TextMeshProUGUI blockedPopupTitleText;
    public TextMeshProUGUI blockedPopupDescText;
    public Button goToStoreButton;
    public Button closePopupButton;

    private DLCData _pendingLockedDLC;
    private readonly List<GameObject> _spawnedTabButtons = new List<GameObject>();
    private readonly List<GameObject> _spawnedLevelButtons = new List<GameObject>();

    private void Awake()
    {
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }

        if (baseCampaignTabButton != null)
        {
            baseCampaignTabButton.onClick.AddListener(SelectBaseCampaignTab);
        }

        if (goToStoreButton != null)
        {
            goToStoreButton.onClick.AddListener(OnGoToStoreClicked);
        }

        if (closePopupButton != null)
        {
            closePopupButton.onClick.AddListener(CloseBlockedPopup);
        }
    }

    private void OnEnable()
    {
        DLCManager.OnDLCStateChanged += OnDLCStateChanged;

        if (blockedPopup != null)
        {
            blockedPopup.SetActive(false);
        }

        RefreshTabs();
        SelectBaseCampaignTab();
    }

    private void OnDisable()
    {
        DLCManager.OnDLCStateChanged -= OnDLCStateChanged;
    }

    /// <summary>
    /// Construye o actualiza las pestañas de expansiones según lo registrado en DLCManager.
    /// </summary>
    public void RefreshTabs()
    {
        if (tabsContainer == null || expansionTabPrefab == null) return;

        // Limpiar pestañas previas de DLC
        foreach (var tab in _spawnedTabButtons)
        {
            if (tab != null) Destroy(tab);
        }
        _spawnedTabButtons.Clear();

        if (DLCManager.Instance == null) return;

        foreach (var dlc in DLCManager.Instance.registeredDLCs)
        {
            if (dlc == null) continue;

            GameObject tabObj = Instantiate(expansionTabPrefab, tabsContainer);
            _spawnedTabButtons.Add(tabObj);

            // Configurar textos e íconos de la pestaña
            TextMeshProUGUI label = tabObj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = dlc.packTitle;

            Button btn = tabObj.GetComponent<Button>();
            bool isOwned = DLCManager.Instance.IsDLCOwned(dlc);

            // Buscar indicador de candado (objeto llamado 'LockIcon' o tag)
            Transform lockTransform = tabObj.transform.Find("LockIcon");
            if (lockTransform != null)
            {
                lockTransform.gameObject.SetActive(!isOwned);
            }

            if (btn != null)
            {
                DLCData capturedDLC = dlc;
                btn.onClick.AddListener(() => OnExpansionTabClicked(capturedDLC));
            }
        }
    }

    /// <summary>
    /// Activa la pestaña de la campaña principal.
    /// </summary>
    public void SelectBaseCampaignTab()
    {
        if (baseCampaignGrid != null) baseCampaignGrid.SetActive(true);
        if (dlcLevelsGrid != null) dlcLevelsGrid.SetActive(false);

        if (baseCampaignTabImage != null) baseCampaignTabImage.color = activeTabColor;
        SetExpansionTabsInactiveColor();
    }

    private void OnExpansionTabClicked(DLCData dlc)
    {
        if (dlc == null) return;

        bool isOwned = DLCManager.Instance != null && DLCManager.Instance.IsDLCOwned(dlc);

        if (isOwned)
        {
            // Mostrar grilla de la expansión
            ShowDLCLevels(dlc);
        }
        else
        {
            // Mostrar Pop-up S-02-1 de contenido bloqueado
            ShowBlockedPopup(dlc);
        }
    }

    private void ShowDLCLevels(DLCData dlc)
    {
        if (baseCampaignGrid != null) baseCampaignGrid.SetActive(false);
        if (dlcLevelsGrid != null) dlcLevelsGrid.SetActive(true);

        if (baseCampaignTabImage != null) baseCampaignTabImage.color = inactiveTabColor;

        // Limpiar botones de niveles previos
        foreach (var btnObj in _spawnedLevelButtons)
        {
            if (btnObj != null) Destroy(btnObj);
        }
        _spawnedLevelButtons.Clear();

        if (dlcLevelsContainer == null || levelButtonPrefab == null) return;

        // Instanciar botones por cada escena o nivel del DLC
        for (int i = 0; i < dlc.sceneNames.Count; i++)
        {
            string sceneName = dlc.sceneNames[i];
            GameObject lvlBtnObj = Instantiate(levelButtonPrefab, dlcLevelsContainer);
            _spawnedLevelButtons.Add(lvlBtnObj);

            TextMeshProUGUI lvlText = lvlBtnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (lvlText != null) lvlText.text = $"Nivel {i + 1}";

            Button btn = lvlBtnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => SceneManager.LoadScene(sceneName));
            }
        }
    }

    private void ShowBlockedPopup(DLCData dlc)
    {
        _pendingLockedDLC = dlc;

        if (blockedPopup != null)
        {
            if (blockedPopupTitleText != null)
            {
                blockedPopupTitleText.text = $"🔒 {dlc.packTitle}";
            }

            if (blockedPopupDescText != null)
            {
                blockedPopupDescText.text = $"Esta expansión incluye {dlc.levels.Count} niveles nuevos y desafíos exclusivos.\n\n¿Querés ir a la Tienda a desbloquearla?";
            }

            blockedPopup.SetActive(true);
        }
    }

    public void CloseBlockedPopup()
    {
        if (blockedPopup != null)
        {
            blockedPopup.SetActive(false);
        }
        _pendingLockedDLC = null;
    }

    public void OnGoToStoreClicked()
    {
        CloseBlockedPopup();

        if (mainMenuController != null)
        {
            // Redirigir directamente a la pantalla de Tienda S-03
            mainMenuController.ShowStore();
        }
    }

    private void OnBackToMenuClicked()
    {
        if (mainMenuController != null)
        {
            mainMenuController.ShowMainMenu();
        }
    }

    private void OnDLCStateChanged()
    {
        RefreshTabs();
    }

    private void SetExpansionTabsInactiveColor()
    {
        foreach (var tabObj in _spawnedTabButtons)
        {
            if (tabObj != null)
            {
                Image img = tabObj.GetComponent<Image>();
                if (img != null) img.color = inactiveTabColor;
            }
        }
    }
}
