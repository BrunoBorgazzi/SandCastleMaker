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
        }
        else
        {
            // Si ya hay un MusicManager existiendo y volvemos al menú, destruimos el clon
            Destroy(gameObject);
        }
    }
}
