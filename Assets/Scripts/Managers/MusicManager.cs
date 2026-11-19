using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public enum MusicTheme
    {
        Base = 0,
        Brasil = 1,
        Japon = 2,
        Islandia = 3
    }

    public static MusicManager Instance { get; private set; }

    [Header("Temas Musicales por Región (Opcional: si se dejan vacíos cargan de Resources/Audio/Music)")]
    public AudioClip brasilThemeClip;
    public AudioClip japonThemeClip;
    public AudioClip islandiaThemeClip;

    private AudioSource _deckA;
    private AudioSource _deckB;
    private AudioClip _baseThemeClip;
    private float _masterMusicVolume = 1f;
    private bool _isMuted;

    private bool _deckAIsActive = true;
    private MusicTheme _currentTheme = MusicTheme.Base;
    private bool _isCrossfading;
    private float _crossfadeElapsed;
    private float _crossfadeDuration = 0.7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyMobilePerformanceDefaults()
    {
        // En Android Unity corre capado a 30 FPS por defecto; subimos a 60 FPS para trazo y olas fluidas
        Application.targetFrameRate = 60;
        // Evitar que se apague la pantalla mientras el jugador dibuja castillos
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            _deckA = GetComponent<AudioSource>();
            _deckA.loop = true;
            _deckA.playOnAwake = true;
            _baseThemeClip = _deckA.clip;
            _masterMusicVolume = _deckA.volume > 0.01f ? _deckA.volume : 0.75f;

            _deckB = gameObject.AddComponent<AudioSource>();
            _deckB.loop = true;
            _deckB.playOnAwake = false;
            _deckB.volume = 0f;

            // Cargar tema de Brasil desde Resources si no fue asignado manualmente en Inspector
            if (brasilThemeClip == null)
            {
                brasilThemeClip = Resources.Load<AudioClip>("Audio/Music/Brasil_Beach_Theme");
            }

            _isMuted = PlayerPrefs.GetInt("MusicMuted", 0) == 1;
            ApplyMuteState();

            if (_deckA.clip != null && !_deckA.isPlaying)
            {
                _deckA.volume = _masterMusicVolume;
                _deckA.Play();
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string name = scene.name ?? string.Empty;
        if (name.StartsWith("NivelDLC") || name.Contains("Brasil"))
        {
            SetTheme(MusicTheme.Brasil, 0.75f);
        }
        else if (name.StartsWith("NivelJapon"))
        {
            SetTheme(MusicTheme.Japon, 0.75f);
        }
        else if (name.StartsWith("NivelIslandia"))
        {
            SetTheme(MusicTheme.Islandia, 0.75f);
        }
        else
        {
            SetTheme(MusicTheme.Base, 0.65f);
        }
    }

    private void Update()
    {
        if (!_isCrossfading) return;

        _crossfadeElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_crossfadeElapsed / Mathf.Max(0.05f, _crossfadeDuration));
        // Curva de potencia igual (smooth crossfade sin caída de volumen en el medio)
        float fadeIn = Mathf.Sin(t * Mathf.PI * 0.5f) * _masterMusicVolume;
        float fadeOut = Mathf.Cos(t * Mathf.PI * 0.5f) * _masterMusicVolume;

        AudioSource active = _deckAIsActive ? _deckA : _deckB;
        AudioSource outgoing = _deckAIsActive ? _deckB : _deckA;

        if (active != null) active.volume = fadeIn;
        if (outgoing != null) outgoing.volume = fadeOut;

        if (t >= 1f)
        {
            _isCrossfading = false;
            if (active != null) active.volume = _masterMusicVolume;
            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
            }
        }
    }

    public bool IsMuted => _isMuted;

    public void ToggleMusic()
    {
        _isMuted = !_isMuted;
        ApplyMuteState();
        PlayerPrefs.SetInt("MusicMuted", _isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyMuteState()
    {
        if (_deckA != null) _deckA.mute = _isMuted;
        if (_deckB != null) _deckB.mute = _isMuted;
    }

    public void SetThemeByPackId(string packId, float fadeSeconds = 0.70f)
    {
        if (string.IsNullOrEmpty(packId))
        {
            SetTheme(MusicTheme.Base, fadeSeconds);
            return;
        }

        string lower = packId.ToLowerInvariant();
        if (lower.Contains("brasil"))
        {
            SetTheme(MusicTheme.Brasil, fadeSeconds);
        }
        else if (lower.Contains("japon"))
        {
            SetTheme(MusicTheme.Japon, fadeSeconds);
        }
        else if (lower.Contains("islandia"))
        {
            SetTheme(MusicTheme.Islandia, fadeSeconds);
        }
        else
        {
            SetTheme(MusicTheme.Base, fadeSeconds);
        }
    }

    public void SetTheme(MusicTheme theme, float fadeSeconds = 0.70f)
    {
        AudioClip targetClip = ResolveClipForTheme(theme);
        if (targetClip == null) targetClip = _baseThemeClip;
        if (targetClip == null) return;

        AudioSource currentActive = _deckAIsActive ? _deckA : _deckB;
        if (currentActive != null && currentActive.clip == targetClip && currentActive.isPlaying && !_isCrossfading)
        {
            _currentTheme = theme;
            return;
        }

        // Si ya estábamos haciendo crossfade hacia este mismo clip, mantenerlo
        if (_isCrossfading && currentActive != null && currentActive.clip == targetClip)
        {
            _currentTheme = theme;
            return;
        }

        _currentTheme = theme;

        // Alternar deck entrante / saliente
        _deckAIsActive = !_deckAIsActive;
        AudioSource incoming = _deckAIsActive ? _deckA : _deckB;
        AudioSource outgoing = _deckAIsActive ? _deckB : _deckA;

        incoming.clip = targetClip;
        incoming.loop = true;
        incoming.mute = _isMuted;
        incoming.volume = 0f;
        incoming.Play();

        if (outgoing != null && outgoing.isPlaying && fadeSeconds > 0.05f)
        {
            _crossfadeDuration = fadeSeconds;
            _crossfadeElapsed = 0f;
            _isCrossfading = true;
        }
        else
        {
            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
            }
            incoming.volume = _masterMusicVolume;
            _isCrossfading = false;
        }
    }

    private AudioClip ResolveClipForTheme(MusicTheme theme)
    {
        switch (theme)
        {
            case MusicTheme.Brasil:
                return brasilThemeClip != null ? brasilThemeClip : _baseThemeClip;
            case MusicTheme.Japon:
                return japonThemeClip != null ? japonThemeClip : _baseThemeClip;
            case MusicTheme.Islandia:
                return islandiaThemeClip != null ? islandiaThemeClip : _baseThemeClip;
            default:
                return _baseThemeClip;
        }
    }
}
