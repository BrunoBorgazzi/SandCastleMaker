using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador principal de la Pantalla S-03: Tienda de Expansiones (DLCs).
/// Genera dinámicamente las tarjetas de cada expansión registrada y gestiona la navegación de regreso.
/// </summary>
public class StoreUIController : MonoBehaviour
{
    [Header("Navegación")]
    public MainMenuController mainMenuController;
    public Button backButton;

    [Header("Contenedor de Expansiones")]
    [Tooltip("Transform del contenedor con LayoutGroup (Horizontal o Grid) donde se ubican las tarjetas")]
    public Transform cardsContainer;

    [Tooltip("Prefab de la tarjeta (DLCCardUI) para instanciar dinámicamente")]
    public GameObject cardPrefab;

    [Header("Feedback de Notificación")]
    public GameObject purchaseSuccessPopup;
    public TextMeshProUGUI purchaseSuccessText;
    public Button closePopupButton;

    private readonly List<DLCCardUI> _spawnedCards = new List<DLCCardUI>();

    private void Awake()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (closePopupButton != null)
        {
            closePopupButton.onClick.AddListener(ClosePurchasePopup);
        }
    }

    private void OnEnable()
    {
        // Suscribirse a los eventos del DLCManager
        DLCManager.OnDLCStateChanged += OnDLCStateChanged;
        DLCManager.OnDLCPurchased += OnDLCPurchasedFeedback;

        if (purchaseSuccessPopup != null)
        {
            purchaseSuccessPopup.SetActive(false);
        }

        PopulateStore();
    }

    private void OnDisable()
    {
        DLCManager.OnDLCStateChanged -= OnDLCStateChanged;
        DLCManager.OnDLCPurchased -= OnDLCPurchasedFeedback;
    }

    /// <summary>
    /// Pobla la tienda con las expansiones registradas en el DLCManager.
    /// Soporta tanto instanciación por Prefab como tarjetas preexistentes en escena.
    /// </summary>
    public void PopulateStore()
    {
        if (DLCManager.Instance == null)
        {
            Debug.LogWarning("[StoreUIController] DLCManager no encontrado en escena.");
            return;
        }

        List<DLCData> dlcs = DLCManager.Instance.registeredDLCs;
        if (dlcs == null || dlcs.Count == 0)
        {
            Debug.Log("[StoreUIController] No hay expansiones registradas en el DLCManager.");
            return;
        }

        // Si ya hay tarjetas Hijas estáticas en cardsContainer, las reutilizamos
        if (cardsContainer != null)
        {
            DLCCardUI[] existingChildren = cardsContainer.GetComponentsInChildren<DLCCardUI>(true);
            if (existingChildren.Length > 0 && cardPrefab == null)
            {
                _spawnedCards.Clear();
                for (int i = 0; i < existingChildren.Length; i++)
                {
                    if (i < dlcs.Count && dlcs[i] != null)
                    {
                        existingChildren[i].Setup(dlcs[i]);
                        _spawnedCards.Add(existingChildren[i]);
                    }
                    else
                    {
                        existingChildren[i].gameObject.SetActive(false);
                    }
                }
                return;
            }
        }

        // Modo Dinámico: Limpiar e instanciar
        ClearCards();

        if (cardPrefab != null && cardsContainer != null)
        {
            foreach (var dlc in dlcs)
            {
                if (dlc == null) continue;

                GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
                DLCCardUI cardUI = cardObj.GetComponent<DLCCardUI>();
                if (cardUI != null)
                {
                    cardUI.Setup(dlc);
                    _spawnedCards.Add(cardUI);
                }
            }
        }
    }

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        _spawnedCards.Clear();
    }

    private void OnDLCStateChanged()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
            {
                card.RefreshState();
            }
        }
    }

    private void OnDLCPurchasedFeedback(DLCData dlc)
    {
        if (purchaseSuccessPopup != null && dlc != null)
        {
            if (purchaseSuccessText != null)
            {
                purchaseSuccessText.text = $"¡{dlc.packTitle} adquirida con éxito!\nYa tenés disponibles sus nuevos niveles en la Selección de Niveles.";
            }
            purchaseSuccessPopup.SetActive(true);
        }
    }

    public void ClosePurchasePopup()
    {
        if (purchaseSuccessPopup != null)
        {
            purchaseSuccessPopup.SetActive(false);
        }
    }

    public void OnBackButtonClicked()
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
