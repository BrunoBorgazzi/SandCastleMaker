using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    public bool isPausedByUI = false;
    public float elapsedTime = 0f;
    public int lastStarsEarned = 0;

    public bool IsUnlimitedWetSand => currentLevelData == null || currentLevelData.unlimitedWetSand;

    private GameObject _spawnedTargetShape;

    // Requisitos de estrellas acumuladas por mundo para desbloquear cada nivel (1 al 4)
    public static readonly int[] REQUIRED_STARS_PER_LEVEL = new int[] { 0, 1, 3, 5 };
    private static readonly string[] BASE_WORLD_SCENES = new string[] { "Nivel1", "Nivel2", "Nivel3", "Nivel4" };
    private static readonly string[] DLC1_WORLD_SCENES = new string[] { "NivelDLC1", "NivelDLC2", "NivelDLC3", "NivelDLC4" };

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
            // Chequeo frecuente para detectar el 90% apenas el jugador termina de esculpir
            InvokeRepeating(nameof(CheckWinCondition), 0.5f, 0.2f);
        }
        else
        {
            Debug.LogWarning("¡Falta asignar un LevelData al LevelManager!");
        }
    }

    private void Update()
    {
        if (isGameOver || isPausedByUI) return;
        elapsedTime += Time.deltaTime;
    }

    private void SetupLevel()
    {
        currentWetSand = currentLevelData.wetSandAmount;
        elapsedTime = 0f;
        lastStarsEarned = 0;
        isGameOver = false;
        isPausedByUI = false;

        Debug.Log($"Nivel {currentLevelData.levelID} cargado. Arena húmeda disponible: {currentWetSand}");

        // Garantizar AudioManager activo incluso al probar una escena de nivel directo desde el Editor
        if (AudioManager.Instance == null)
        {
            GameObject audioGO = new GameObject("AudioManager_Runtime");
            audioGO.AddComponent<AudioManager>();
        }
        
        // 1. Configurar Fondo y asegurar orden de capas UI vs Sprites 2D
        if (backgroundImageUI != null)
        {
            if (currentLevelData.backgroundImage != null)
            {
                backgroundImageUI.sprite = currentLevelData.backgroundImage;
            }
            ApplyAtmosphericBackgroundTint(backgroundImageUI, currentLevelData);
            EnsureUISortingLayers(backgroundImageUI);
        }

        // 2. Desactivar objetos legacy del prototipo de caída de bloques si quedaron en la escena
        DisableLegacyPhysicsObjects();

        // 3. Instanciar el prefab de silueta objetivo y levantar el sistema de Esculpido + Marea
        if (currentLevelData.targetShapePrefab != null)
        {
            _spawnedTargetShape = Instantiate(currentLevelData.targetShapePrefab);

            SandCanvasController sandCanvas = FindObjectOfType<SandCanvasController>();
            if (sandCanvas == null)
            {
                GameObject canvasGO = new GameObject("SandSculptCanvas");
                sandCanvas = canvasGO.AddComponent<SandCanvasController>();
            }
            sandCanvas.InitializeFromTargetShape(_spawnedTargetShape, currentLevelData);

            TideController tideController = FindObjectOfType<TideController>();
            if (tideController == null)
            {
                GameObject tideGO = new GameObject("TideWaterOverlay");
                tideController = tideGO.AddComponent<TideController>();
            }
            tideController.InitializeTide(sandCanvas.CastleWorldBounds, currentLevelData);
        }
    }

    /// <summary>
    /// Mantiene blanco neutro cuando el nivel ya trae arte de fondo regional propio (ej. BG_Brasil_1..4),
    /// o aplica un matiz cromático sutil únicamente si usa fondos genéricos.
    /// </summary>
    private static void ApplyAtmosphericBackgroundTint(Image bgImage, LevelData levelData)
    {
        if (bgImage == null || levelData == null) return;

        string bgName = levelData.backgroundImage != null
            ? levelData.backgroundImage.name.ToLowerInvariant()
            : string.Empty;

        if (bgName.Contains("brasil"))
        {
            bgImage.color = Color.white;
            return;
        }

        switch (levelData.levelID)
        {
            case 101:
                bgImage.color = new Color(1.00f, 0.97f, 0.90f, 1f);
                break;
            case 102:
                bgImage.color = new Color(1.00f, 0.92f, 0.85f, 1f);
                break;
            case 103:
                bgImage.color = new Color(0.88f, 0.95f, 0.98f, 1f);
                break;
            case 104:
                bgImage.color = new Color(0.84f, 0.92f, 0.90f, 1f);
                break;
            default:
                bgImage.color = Color.white;
                break;
        }
    }

    /// <summary>
    /// Asegura que la imagen de fondo quede detrás del lienzo de arena y olas (sortingOrder = 0),
    /// cubra toda la pantalla sin estirarse (AspectCoverImage),
    /// y mantenga los botones y paneles del Canvas por encima (sortingOrder = 15).
    /// </summary>
    private void EnsureUISortingLayers(Image bgImage)
    {
        AspectCoverImage.Attach(bgImage.gameObject);

        Canvas rootCanvas = bgImage.canvas;
        if (rootCanvas != null)
        {
            rootCanvas.sortingOrder = 15;
        }

        Canvas bgSubCanvas = bgImage.GetComponent<Canvas>();
        if (bgSubCanvas == null)
        {
            bgSubCanvas = bgImage.gameObject.AddComponent<Canvas>();
        }
        bgSubCanvas.overrideSorting = true;
        bgSubCanvas.sortingOrder = 0;
    }

    private void DisableLegacyPhysicsObjects()
    {
        SandSpawner legacySpawner = FindObjectOfType<SandSpawner>();
        if (legacySpawner != null) legacySpawner.gameObject.SetActive(false);

        SandPoolManager legacyPool = FindObjectOfType<SandPoolManager>();
        if (legacyPool != null) legacyPool.gameObject.SetActive(false);

        GyroGravity legacyGyro = FindObjectOfType<GyroGravity>();
        if (legacyGyro != null) legacyGyro.gameObject.SetActive(false);

        KillZone legacyKill = FindObjectOfType<KillZone>();
        if (legacyKill != null) legacyKill.gameObject.SetActive(false);

        GameObject legacyFloor = GameObject.Find("Suelo");
        if (legacyFloor != null) legacyFloor.SetActive(false);
    }

    public float GetCurrentFillPercentage()
    {
        if (SandCanvasController.Instance != null && SandCanvasController.Instance.IsInitialized)
        {
            return SandCanvasController.Instance.CurrentFillPercentage;
        }

        GoalArea goalArea = FindObjectOfType<GoalArea>();
        if (goalArea != null && goalArea.targetSandCount > 0)
        {
            return (float)goalArea.GetSandInsideCount() / goalArea.targetSandCount;
        }
        return 0f;
    }

    private void CheckWinCondition()
    {
        if (isGameOver || isPausedByUI) return;

        float fillPercentage = GetCurrentFillPercentage();

        if (fillPercentage >= 0.9f)
        {
            isGameOver = true;
            CancelInvoke(nameof(CheckWinCondition));

            int scorePercentage = Mathf.Clamp(Mathf.FloorToInt(fillPercentage * 100f), 90, 100);

            // --- CÁLCULO DE ESTRELLAS (CASO A: TIEMPO + PROLIJIDAD O AHORRO DE ARENA HÚMEDA) ---
            int starsEarned = 1; // 1 Estrella base por completar el castillo (>= 90%)

            float targetTime = (currentLevelData != null && currentLevelData.targetTimeSeconds > 0f)
                ? currentLevelData.targetTimeSeconds
                : 40f;

            bool achievedTime = elapsedTime <= targetTime;
            bool achievedEfficiency;
            string masteryLog;

            if (IsUnlimitedWetSand)
            {
                float maxOverdraw = (currentLevelData != null && currentLevelData.maxOverdrawRatioForStar > 0f)
                    ? currentLevelData.maxOverdrawRatioForStar
                    : 0.14f;
                float overdraw = SandCanvasController.Instance != null
                    ? SandCanvasController.Instance.CurrentOverdrawRatio
                    : 0f;
                achievedEfficiency = overdraw <= maxOverdraw;
                masteryLog = $"Exceso fuera del molde: {overdraw * 100f:F1}% (Meta <= {maxOverdraw * 100f:F0}%)";
            }
            else
            {
                float saveRatio = (currentLevelData != null)
                    ? currentLevelData.wetSandSaveRatio
                    : 0.25f;
                int minWetSandToSave = (currentLevelData != null)
                    ? Mathf.CeilToInt(currentLevelData.wetSandAmount * saveRatio)
                    : 0;
                achievedEfficiency = currentWetSand >= minWetSandToSave;
                masteryLog = $"Arena húmeda restante: {currentWetSand}/{minWetSandToSave}";
            }

            if (achievedTime) starsEarned++;
            if (achievedEfficiency) starsEarned++;

            lastStarsEarned = starsEarned;

            // Guardar récord de estrellas para esta escena en PlayerPrefs
            string sceneName = SceneManager.GetActiveScene().name;
            string starKey = "STARS_" + sceneName;
            int previousBest = PlayerPrefs.GetInt(starKey, 0);
            if (starsEarned > previousBest)
            {
                PlayerPrefs.SetInt(starKey, starsEarned);
                PlayerPrefs.Save();
            }

            Debug.Log($"¡VICTORIA! Nivel '{sceneName}' esculpido al {scorePercentage}% en {elapsedTime:F1}s (Meta: {targetTime}s). " +
                      $"{masteryLog}. Estrellas obtenidas: {starsEarned}");
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayWinSound();
            }

            LevelUIController uiController = FindObjectOfType<LevelUIController>();
            if (uiController != null)
            {
                uiController.ShowEndGamePanel(scorePercentage, true, starsEarned, elapsedTime);
            }
        }
    }

    /// <summary>
    /// Dispara la derrota cuando la marea base cubre la totalidad de la playa hasta la base del castillo.
    /// </summary>
    public void TriggerTideDefeat()
    {
        if (isGameOver) return;
        isGameOver = true;
        CancelInvoke(nameof(CheckWinCondition));

        int scorePercentage = Mathf.Clamp(Mathf.FloorToInt(GetCurrentFillPercentage() * 100f), 0, 89);

        Debug.Log($"¡DERROTA! La marea subió por completo y el castillo quedó en {scorePercentage}%.");

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLoseSound();
        }

        LevelUIController uiController = FindObjectOfType<LevelUIController>();
        if (uiController != null)
        {
            uiController.ShowEndGamePanel(scorePercentage, false, 0, elapsedTime);
        }
    }

    public void ToggleSandType()
    {
        isUsingWetSand = !isUsingWetSand;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBucketSwitchSound(isUsingWetSand);
        }
        Debug.Log("Pincel de arena cambiado a: " + (isUsingWetSand ? "Arena Húmeda (Escudo contra olas)" : "Arena Seca (Rápida)"));
    }

    public bool TryUseWetSand()
    {
        if (IsUnlimitedWetSand)
        {
            return true;
        }

        if (currentWetSand > 0)
        {
            currentWetSand--;
            return true;
        }
        return false;
    }

    // --- Métodos de Navegación ---
    
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBeachAmbience();
        }
        SceneManager.LoadScene("MainMenu");
    }

    public void ReloadLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToNextLevel()
    {
        Time.timeScale = 1f;
        string currentScene = SceneManager.GetActiveScene().name;
        string[] currentPackScenes = GetPackScenesForScene(currentScene, out int indexInPack);

        if (currentPackScenes != null && indexInPack >= 0)
        {
            int nextIndexInPack = indexInPack + 1;

            if (nextIndexInPack >= currentPackScenes.Length)
            {
                Debug.Log("[LevelManager] ¡Mundo completado! Volviendo al Menú Principal.");
                GoToMainMenu();
                return;
            }

            int totalStarsInWorld = GetTotalStarsForPack(currentPackScenes);
            int requiredStars = (nextIndexInPack < REQUIRED_STARS_PER_LEVEL.Length)
                ? REQUIRED_STARS_PER_LEVEL[nextIndexInPack]
                : 0;

            if (totalStarsInWorld >= requiredStars)
            {
                string nextSceneName = currentPackScenes[nextIndexInPack];
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                Debug.Log($"[LevelManager] El siguiente nivel requiere {requiredStars} estrellas en este mundo (tenés {totalStarsInWorld}). Volviendo al Selector de Niveles.");
                GoToMainMenu();
            }
            return;
        }

        GoToMainMenu();
    }

    public static string[] GetPackScenesForScene(string sceneName, out int indexInPack)
    {
        for (int i = 0; i < BASE_WORLD_SCENES.Length; i++)
        {
            if (BASE_WORLD_SCENES[i] == sceneName)
            {
                indexInPack = i;
                return BASE_WORLD_SCENES;
            }
        }

        for (int i = 0; i < DLC1_WORLD_SCENES.Length; i++)
        {
            if (DLC1_WORLD_SCENES[i] == sceneName)
            {
                indexInPack = i;
                return DLC1_WORLD_SCENES;
            }
        }

        indexInPack = -1;
        return null;
    }

    public static int GetTotalStarsForPack(string[] packScenes)
    {
        if (packScenes == null) return 0;
        int total = 0;
        for (int i = 0; i < packScenes.Length; i++)
        {
            total += Mathf.Clamp(PlayerPrefs.GetInt("STARS_" + packScenes[i], 0), 0, 3);
        }
        return total;
    }
}
