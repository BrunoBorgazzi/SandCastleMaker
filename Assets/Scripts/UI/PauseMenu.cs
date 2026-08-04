using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("El contenedor (Panel) de las opciones de pausa")]
    public GameObject pausePanel;

    [Header("Objetos a Ocultar")]
    [Tooltip("Arrastra aquí los objetos (como el Suelo) que quieras desaparecer al pausar.")]
    public GameObject[] objectsToHideOnPause;

    private bool isPaused = false;

    private void Start()
    {
        // Asegurarnos de que el panel de pausa empiece desactivado
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Debug.Log("TogglePause presionado. Estado isPaused: " + isPaused);

        if (isPaused)
        {
            PauseGame();
        }
        else
        {
            ResumeGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        if (pausePanel != null)
        {
            Debug.Log("Activando el panel de pausa visualmente.");
            pausePanel.SetActive(true);
        }
        else
        {
            Debug.LogError("¡ERROR! El pausePanel no está asignado en el Inspector del PauseMenu_Controller.");
        }

        // Apagar los objetos molestos
        foreach (GameObject obj in objectsToHideOnPause)
        {
            if (obj != null) obj.SetActive(false);
        }

        Time.timeScale = 0f; // Pausa el tiempo
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        // Volver a encender los objetos
        foreach (GameObject obj in objectsToHideOnPause)
        {
            if (obj != null) obj.SetActive(true);
        }

        Time.timeScale = 1f; // Reanuda el tiempo
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f; // Asegurar que el tiempo vuelve a la normalidad al reiniciar
        
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ReloadLevel();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // Asegurar que el tiempo vuelve a la normalidad al salir
        
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.GoToMainMenu();
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
