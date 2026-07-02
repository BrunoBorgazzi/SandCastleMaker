using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Asegúrate de tener importado TextMeshPro

public class LevelUIController : MonoBehaviour
{
    [Header("End Game Panel")]
    public GameObject endGamePanel;
    public TextMeshProUGUI scoreText; 
    public Button returnToMenuButton;
    public Button retryButton;
    public Button nextLevelButton;

    [Header("Mobile Controls")]
    public Button toggleSandButton;
    public Image sandButtonImage;
    public Sprite drySandSprite;
    public Sprite wetSandSprite;

    private void Start()
    {
        // Asegurarse de que el panel de fin de juego esté oculto al iniciar el nivel
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        // Configurar los listeners de los botones
        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryClicked);
        }
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        }

        if (toggleSandButton != null)
        {
            toggleSandButton.onClick.AddListener(OnToggleSandButtonClicked);
        }
        else
        {
            Debug.LogError(">>> ERROR FATAL: No asignaste el 'Toggle Sand Button' en el Inspector de LevelUI. <<<");
        }
    }

    public void ShowEndGamePanel(int scorePercentage, bool isWin)
    {
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        if (scoreText != null)
        {
            scoreText.text = $"Puntaje: {scorePercentage}%";
        }

        // El botón de Siguiente Nivel solo aparece si ganaste
        if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(isWin);
        
        // El botón de Reintentar (y el de Menú) siempre están disponibles
        if (retryButton != null) retryButton.gameObject.SetActive(true);
    }

    public void OnToggleSandButtonClicked()
    {
        Debug.Log(">>> ¡CLICK DETECTADO EN EL SCRIPT! <<<");
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ToggleSandType();
            UpdateSandButtonVisual();
        }
        else
        {
            Debug.LogError(">>> ERROR: LevelManager.Instance es NULL. El LevelManager no está en la escena. <<<");
        }
    }

    private void Update()
    {
        // Esto mantendrá la UI sincronizada incluso si el jugador usa la barra espaciadora en PC
        if (Input.GetKeyDown(KeyCode.Space))
        {
            UpdateSandButtonVisual();
        }
    }

    public void UpdateSandButtonVisual()
    {
        if (LevelManager.Instance != null && sandButtonImage != null)
        {
            if (LevelManager.Instance.isUsingWetSand)
            {
                if (wetSandSprite != null) 
                {
                    sandButtonImage.sprite = wetSandSprite;
                    Debug.Log("UI Actualizada: Mostrando sprite de arena húmeda.");
                }
                else
                {
                    Debug.LogWarning("Falta asignar el Wet Sand Sprite en el Inspector de LevelUIController");
                }
            }
            else
            {
                if (drySandSprite != null) 
                {
                    sandButtonImage.sprite = drySandSprite;
                    Debug.Log("UI Actualizada: Mostrando sprite de arena seca.");
                }
                else
                {
                    Debug.LogWarning("Falta asignar el Dry Sand Sprite en el Inspector de LevelUIController");
                }
            }
        }
        else
        {
            Debug.LogWarning("No se puede actualizar el botón visual: LevelManager o sandButtonImage es null.");
        }
    }

    private void OnReturnToMenuClicked()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.GoToMainMenu();
        }
    }

    private void OnRetryClicked()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ReloadLevel();
        }
    }

    private void OnNextLevelClicked()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.GoToNextLevel();
        }
    }
}
