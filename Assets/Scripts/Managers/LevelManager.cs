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
            // Iniciar chequeo periódico de victoria cada medio segundo para no saturar el Update
            InvokeRepeating(nameof(CheckWinCondition), 1f, 0.5f);
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

        // Si tenemos un prefab de objetivo en el nivel, lo instanciamos.
        if (currentLevelData.targetShapePrefab != null)
        {
            // Instanciamos respetando la posición que el usuario guardó en el Prefab
            Instantiate(currentLevelData.targetShapePrefab);
        }
    }

    private void CheckWinCondition()
    {
        // Asumimos que el GoalArea se instanció con el prefab del nivel
        GoalArea goalArea = FindObjectOfType<GoalArea>();
        if (goalArea == null) return;

        int sandInside = goalArea.GetSandInsideCount();
        
        // Obtenemos la arena total para la regla de "mayoría"
        int totalSandInScene = FindObjectsOfType<SandBlock>().Length; 
        if (totalSandInScene == 0) return;

        bool isMajorityInside = sandInside > (totalSandInScene * 0.5f);
        float fillPercentage = (float)sandInside / goalArea.targetSandCount;

        if (isMajorityInside && fillPercentage >= 0.9f)
        {
            CancelInvoke(nameof(CheckWinCondition)); // Detenemos el chequeo

            int scorePercentage = Mathf.FloorToInt(fillPercentage * 100f);
            int extraPoints = (scorePercentage - 90) * 100;
            if (extraPoints < 0) extraPoints = 0;

            Debug.Log($"¡VICTORIA AUTOMÁTICA! Nivel completado con exactitud del {scorePercentage}%. Puntos extra: {extraPoints}");
            // Aquí llamaremos al script de UI de victoria en el futuro
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
