using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [Tooltip("El componente AudioSource para reproducir SFX (Efectos de sonido)")]
    public AudioSource sfxSource;

    [Header("Clips de Sonido (Asignar en Inspector)")]
    public AudioClip winSound;
    public AudioClip loseSound;
    public AudioClip spawnWetSandSound;
    public AudioClip errorNoSandSound;

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
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
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

    public void PlaySpawnWetSandSound()
    {
        PlayClip(spawnWetSandSound);
    }

    public void PlayErrorNoSandSound()
    {
        PlayClip(errorNoSandSound);
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
