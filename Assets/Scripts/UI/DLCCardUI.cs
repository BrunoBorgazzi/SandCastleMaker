using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente de UI para representar una tarjeta individual de Expansión en la tienda (S-03).
/// Maneja la presentación visual, estados de compra (disponible/adquirida) y feedback del botón.
/// </summary>
public class DLCCardUI : MonoBehaviour
{
    [Header("Referencias Visuales")]
    public Image bannerImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI levelCountText;

    [Header("Botón de Acción")]
    public Button actionButton;
    public TextMeshProUGUI buttonText;
    public Image buttonImage;
    public Color buyColor = new Color(0.95f, 0.6f, 0.15f, 1f); // Naranja/Dorado
    public Color ownedColor = new Color(0.2f, 0.75f, 0.35f, 1f); // Verde

    [Header("Indicador Adquirido")]
    public GameObject ownedBadge;

    private DLCData _currentDLC;

    /// <summary>
    /// Inicializa la tarjeta con los datos de una Expansión.
    /// </summary>
    public void Setup(DLCData dlcData)
    {
        _currentDLC = dlcData;

        if (_currentDLC == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (titleText != null) 
            titleText.text = _currentDLC.packTitle;

        if (descriptionText != null) 
            descriptionText.text = _currentDLC.description;

        if (bannerImage != null)
        {
            if (_currentDLC.packBanner != null)
            {
                bannerImage.sprite = _currentDLC.packBanner;
                bannerImage.gameObject.SetActive(true);
            }
            else
            {
                bannerImage.gameObject.SetActive(false);
            }
        }

        if (levelCountText != null)
        {
            int count = _currentDLC.levels != null ? _currentDLC.levels.Count : 0;
            levelCountText.text = count > 0 ? $"{count} Niveles Nuevos" : "Niveles Exclusivos";
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnActionButtonClicked);
        }

        RefreshState();
    }

    /// <summary>
    /// Actualiza el estado visual del botón según si la expansión ya fue adquirida o no.
    /// </summary>
    public void RefreshState()
    {
        if (_currentDLC == null) return;

        bool isOwned = DLCManager.Instance != null && DLCManager.Instance.IsDLCOwned(_currentDLC);

        if (ownedBadge != null)
        {
            ownedBadge.SetActive(isOwned);
        }

        if (actionButton != null)
        {
            if (isOwned)
            {
                actionButton.interactable = false;
                if (buttonImage != null) buttonImage.color = ownedColor;
                if (buttonText != null) buttonText.text = "✓ EXPANSION ADQUIRIDA";
            }
            else
            {
                actionButton.interactable = true;
                if (buttonImage != null) buttonImage.color = buyColor;
                string price = string.IsNullOrEmpty(_currentDLC.priceText) ? "DESBLOQUEAR" : $"{_currentDLC.priceText} - OBTENER";
                if (buttonText != null) buttonText.text = price;
            }
        }
    }

    private void OnActionButtonClicked()
    {
        if (_currentDLC == null) return;

        if (DLCManager.Instance != null)
        {
            bool success = DLCManager.Instance.PurchaseDLC(_currentDLC);
            if (success)
            {
                RefreshState();
            }
        }
    }
}
