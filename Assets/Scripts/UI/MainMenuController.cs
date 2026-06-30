using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject levelSelectionPanel;
    public GameObject creditsPanel;

    private void Start()
    {
        // Asegurarnos de que solo el menú principal esté activo al iniciar
        ShowMainMenu();
    }

    // --- Métodos para mostrar/ocultar paneles ---

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        levelSelectionPanel.SetActive(false);
        creditsPanel.SetActive(false);
    }

    public void ShowLevelSelection()
    {
        mainMenuPanel.SetActive(false);
        levelSelectionPanel.SetActive(true);
        creditsPanel.SetActive(false);
    }

    public void ShowCredits()
    {
        mainMenuPanel.SetActive(false);
        levelSelectionPanel.SetActive(false);
        creditsPanel.SetActive(true);
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
