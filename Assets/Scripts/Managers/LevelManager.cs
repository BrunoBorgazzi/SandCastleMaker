using UnityEngine;
using UnityEngine.UI; // Necesario para interactuar con el Canvas y las imágenes UI

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Configuration")]
    public LevelData currentLevelData;

    [Header("UI References")]
    [Tooltip("El componente Image del fondo dentro del Canvas")]
    public Image backgroundImageUI; 

    [Header("Runtime Variables (No Tocar)")]
    public int currentWetSand;
    public bool isUsingWetSand = false; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (currentLevelData != null)
        {
            SetupLevel();
        }
        else
        {
            Debug.LogWarning("¡Falta asignar un LevelData al LevelManager!");
        }
    }

    private void SetupLevel()
    {
        currentWetSand = currentLevelData.wetSandAmount;
        Debug.Log($"Nivel {currentLevelData.levelID} cargado. Arena húmeda disponible: {currentWetSand}");
        
        // --- AQUÍ CAMBIAMOS EL FONDO ---
        if (backgroundImageUI != null && currentLevelData.backgroundImage != null)
        {
            backgroundImageUI.sprite = currentLevelData.backgroundImage;
        }
    }

    public void ToggleSandType()
    {
        isUsingWetSand = !isUsingWetSand;
        Debug.Log("Modo de arena cambiado a: " + (isUsingWetSand ? "Húmeda" : "Seca"));
    }

    public bool TryUseWetSand()
    {
        if (currentWetSand > 0)
        {
            currentWetSand--;
            return true;
        }
        return false; // No queda arena húmeda
    }
}
