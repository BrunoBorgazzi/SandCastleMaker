using UnityEngine;
using UnityEngine.UI; // Necesario para interactuar con el Canvas y las imágenes UI
using UnityEngine.SceneManagement; // Necesario para cambiar de escenas

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
    public bool isGameOver = false;

    private float defeatTimer = 0f;
    private bool isDefeatTimerRunning = false;
    private const float DEFEAT_TIME_LIMIT = 20f;

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

    private void Update()
    {
        if (isGameOver) return;

        // Si nos quedamos sin arena húmeda, empezamos el conteo de derrota
        if (currentWetSand <= 0 && !isDefeatTimerRunning)
        {
            isDefeatTimerRunning = true;
            Debug.Log("¡Sin arena húmeda! Inicia la cuenta regresiva para la derrota...");
        }

        if (isDefeatTimerRunning)
        {
            defeatTimer += Time.deltaTime;
            if (defeatTimer >= DEFEAT_TIME_LIMIT)
            {
                TriggerDefeat();
            }
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
        if (isGameOver) return;

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
            isGameOver = true;
            CancelInvoke(nameof(CheckWinCondition)); // Detenemos el chequeo
            isDefeatTimerRunning = false; // Detenemos el timer si ganamos justo a tiempo

            int scorePercentage = Mathf.FloorToInt(fillPercentage * 100f);
            int extraPoints = (scorePercentage - 90) * 100;
            if (extraPoints < 0) extraPoints = 0;

            Debug.Log($"¡VICTORIA AUTOMÁTICA! Nivel completado con exactitud del {scorePercentage}%. Puntos extra: {extraPoints}");
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayWinSound();
            }

            // Mostrar UI de victoria a través del controlador
            LevelUIController uiController = FindObjectOfType<LevelUIController>();
            if (uiController != null)
            {
                uiController.ShowEndGamePanel(scorePercentage, true);
            }
        }
    }

    private void TriggerDefeat()
    {
        isGameOver = true;
        CancelInvoke(nameof(CheckWinCondition));
        
        // Calcular puntaje final a pesar de perder
        GoalArea goalArea = FindObjectOfType<GoalArea>();
        int scorePercentage = 0;
        
        if (goalArea != null)
        {
            int sandInside = goalArea.GetSandInsideCount();
            float fillPercentage = (float)sandInside / goalArea.targetSandCount;
            scorePercentage = Mathf.FloorToInt(fillPercentage * 100f);
        }

        Debug.Log($"¡DERROTA! Se acabó el tiempo y no lograste el 90%. Puntaje obtenido: {scorePercentage}%");

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLoseSound();
        }

        // Mostrar UI de fin de juego a través del controlador (marcado como derrota)
        LevelUIController uiController = FindObjectOfType<LevelUIController>();
        if (uiController != null)
        {
            uiController.ShowEndGamePanel(scorePercentage, false);
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

    // --- Métodos de Navegación ---
    
    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void ReloadLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToNextLevel()
    {
        // Asume que los niveles están en orden en el Build Settings
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("No hay más niveles, volviendo al menú principal");
            GoToMainMenu();
        }
    }
}
