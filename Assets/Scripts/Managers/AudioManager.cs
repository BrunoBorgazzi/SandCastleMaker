using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [Tooltip("El componente AudioSource para reproducir SFX (Efectos de sonido)")]
    public AudioSource sfxSource;
    [Tooltip("El componente AudioSource exclusivo para reproducir sonidos de la interfaz")]
    public AudioSource uiSource;

    [Header("Clips de Sonido (Asignar en Inspector)")]
    public AudioClip winSound;
    public AudioClip loseSound;
    [UnityEngine.Serialization.FormerlySerializedAs("spawnWetSandSound")]
    public AudioClip spawnSandSound;
    public AudioClip errorNoSandSound;
    public AudioClip buttonClickSound;

    private void Awake()
    {
        // Patrón Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
            }
            if (uiSource == null)
            {
                // Si no se asignó en el inspector, creamos uno nuevo en runtime
                uiSource = gameObject.AddComponent<AudioSource>();
            }
            
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            uiSource.playOnAwake = false;
            uiSource.loop = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayWinSound()
    {
        PlayClip(winSound);
    }

    public void PlayLoseSound()
    {
        PlayClip(loseSound);
    }

    public void PlaySpawnSandSound()
    {
        PlayClip(spawnSandSound);
    }

    public void PlayErrorNoSandSound()
    {
        PlayClip(errorNoSandSound);
    }

    public void PlayButtonSound()
    {
        if (buttonClickSound != null && uiSource != null)
        {
            // Variamos el pitch aleatoriamente entre 1.0 (normal) y 1.20 (más agudo)
            uiSource.pitch = Random.Range(1.0f, 1.20f);
            uiSource.PlayOneShot(buttonClickSound);
        }
        else
        {
            Debug.LogWarning("Falta asignar el AudioClip buttonClickSound en el AudioManager.");
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning("Falta asignar un AudioClip o el AudioSource en el AudioManager.");
        }
    }
}
