using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;

    private void Awake()
    {
        // Implementación del patrón Singleton para que persista
        if (Instance == null)
        {
            Instance = this;
            // Esto es lo que hace que la música NO se corte al cambiar de escena
            DontDestroyOnLoad(gameObject);
            
            audioSource = GetComponent<AudioSource>();
            
            // Asegurarnos de que la música esté en bucle (Loop) y suene automáticamente
            audioSource.loop = true;
            audioSource.playOnAwake = true;

            // Cargar estado de mute guardado (0 = no muteado, 1 = muteado)
            bool isMuted = PlayerPrefs.GetInt("MusicMuted", 0) == 1;
            audioSource.mute = isMuted;
        }
        else
        {
            // Si ya hay un MusicManager existiendo y volvemos al menú, destruimos el clon
            Destroy(gameObject);
        }
    }

    public bool IsMuted 
    { 
        get { return audioSource != null && audioSource.mute; } 
    }

    public void ToggleMusic()
    {
        if (audioSource != null)
        {
            audioSource.mute = !audioSource.mute;
            // Guardamos el estado para que persista al cerrar y abrir el juego
            PlayerPrefs.SetInt("MusicMuted", audioSource.mute ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
