using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("El componente AudioSource para reproducir SFX (Efectos de sonido)")]
    public AudioSource sfxSource;
    [Tooltip("El componente AudioSource exclusivo para reproducir sonidos de la interfaz")]
    public AudioSource uiSource;

    [Header("Clips Principales (Asignar en Inspector o autocargados desde Resources/Audio/SFX)")]
    public AudioClip winSound;
    public AudioClip loseSound;
    [UnityEngine.Serialization.FormerlySerializedAs("spawnWetSandSound")]
    public AudioClip spawnSandSound;
    public AudioClip errorNoSandSound;
    public AudioClip buttonClickSound;

    // Biblioteca de SFX temáticos pixel-art cargada automáticamente desde Resources/Audio/
    private AudioClip _palmCocoClip;
    private AudioClip _waterSplashClip;
    private AudioClip _sunSparkleClip;
    private AudioClip _gullChirpClip;
    private AudioClip _castlePatClip;
    private AudioClip _natureRustleClip;
    private AudioClip _whooshPageClip;
    private AudioClip _levelSelectClip;
    private AudioClip _lockedThudClip;
    private AudioClip _packUnlockClip;
    private AudioClip _paperScrollClip;
    private AudioClip _bucketToWetClip;
    private AudioClip _bucketToDryClip;
    private AudioClip _wetSandSculptClip;
    private AudioClip _waveBlockClip;
    private AudioClip _sandCrumbleClip;
    private AudioClip _starPopClip;

    // Fuentes dedicadas para ambiente vivo (olas y viento en gameplay)
    private AudioSource _oceanAmbSource;
    private AudioSource _windAmbSource;
    private float _targetOceanVol;
    private float _targetOceanPitch = 1f;
    private float _targetWindVol;
    private float _targetWindPitch = 1f;
    private float _lastAmbienceFeedTime = -10f;

    // Anti-spam cooldowns para evitar saturación en toques/erosión rápida
    private float _lastSplashTime = -10f;
    private float _lastRustleTime = -10f;
    private float _lastWaveBlockTime = -10f;
    private float _lastCrumbleTime = -10f;

    private void Awake()
    {
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
                uiSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            uiSource.playOnAwake = false;
            uiSource.loop = false;

            LoadGeneratedAudioLibrary();
            SetupAmbienceSources();

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
        if (scene.name == "MainMenu")
        {
            StopBeachAmbience(immediate: false);
        }
    }

    private void LoadGeneratedAudioLibrary()
    {
        if (winSound == null) winSound = Resources.Load<AudioClip>("Audio/SFX/sfx_victory_fanfare");
        if (loseSound == null) loseSound = Resources.Load<AudioClip>("Audio/SFX/sfx_defeat_wash");
        if (errorNoSandSound == null) errorNoSandSound = Resources.Load<AudioClip>("Audio/SFX/sfx_locked_thud");

        _palmCocoClip = Resources.Load<AudioClip>("Audio/SFX/sfx_palm_coco");
        _waterSplashClip = Resources.Load<AudioClip>("Audio/SFX/sfx_water_splash");
        _sunSparkleClip = Resources.Load<AudioClip>("Audio/SFX/sfx_sun_sparkle");
        _gullChirpClip = Resources.Load<AudioClip>("Audio/SFX/sfx_gull_chirp");
        _castlePatClip = Resources.Load<AudioClip>("Audio/SFX/sfx_castle_pat");
        _natureRustleClip = Resources.Load<AudioClip>("Audio/SFX/sfx_nature_rustle");
        _whooshPageClip = Resources.Load<AudioClip>("Audio/SFX/sfx_whoosh_page");
        _levelSelectClip = Resources.Load<AudioClip>("Audio/SFX/sfx_level_select");
        _lockedThudClip = Resources.Load<AudioClip>("Audio/SFX/sfx_locked_thud");
        _packUnlockClip = Resources.Load<AudioClip>("Audio/SFX/sfx_pack_unlock");
        _paperScrollClip = Resources.Load<AudioClip>("Audio/SFX/sfx_paper_scroll");
        _bucketToWetClip = Resources.Load<AudioClip>("Audio/SFX/sfx_bucket_to_wet");
        _bucketToDryClip = Resources.Load<AudioClip>("Audio/SFX/sfx_bucket_to_dry");
        _wetSandSculptClip = Resources.Load<AudioClip>("Audio/SFX/sfx_wet_sand_sculpt");
        _waveBlockClip = Resources.Load<AudioClip>("Audio/SFX/sfx_wave_block");
        _sandCrumbleClip = Resources.Load<AudioClip>("Audio/SFX/sfx_sand_crumble");
        _starPopClip = Resources.Load<AudioClip>("Audio/SFX/sfx_star_pop");

        // Respaldo solo si se ejecuta una escena de nivel directo sin pasar por MainMenu
        if (spawnSandSound == null) spawnSandSound = _castlePatClip;
        if (buttonClickSound == null) buttonClickSound = _whooshPageClip;
    }

    private void SetupAmbienceSources()
    {
        AudioClip oceanClip = Resources.Load<AudioClip>("Audio/Ambience/amb_ocean_waves");
        _oceanAmbSource = gameObject.AddComponent<AudioSource>();
        _oceanAmbSource.clip = oceanClip;
        _oceanAmbSource.loop = true;
        _oceanAmbSource.playOnAwake = false;
        _oceanAmbSource.volume = 0f;

        AudioClip windClip = Resources.Load<AudioClip>("Audio/Ambience/amb_wind_gust");
        _windAmbSource = gameObject.AddComponent<AudioSource>();
        _windAmbSource.clip = windClip;
        _windAmbSource.loop = true;
        _windAmbSource.playOnAwake = false;
        _windAmbSource.volume = 0f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        bool pausedOrEnded = Time.timeScale <= 0.01f
            || (LevelManager.Instance != null && (LevelManager.Instance.isGameOver || LevelManager.Instance.isPausedByUI));
        bool ambienceActive = !pausedOrEnded && (now - _lastAmbienceFeedTime <= 0.35f);

        float desiredOcean = ambienceActive ? _targetOceanVol : 0f;
        float desiredWind = ambienceActive ? _targetWindVol : 0f;

        UpdateLoopingSource(_oceanAmbSource, desiredOcean, _targetOceanPitch, dt, 4.2f);
        UpdateLoopingSource(_windAmbSource, desiredWind, _targetWindPitch, dt, 3.5f);
    }

    private static void UpdateLoopingSource(AudioSource src, float targetVol, float targetPitch, float dt, float smoothSpeed)
    {
        if (src == null || src.clip == null) return;

        if (targetVol > 0.005f && !src.isPlaying)
        {
            src.volume = 0f;
            src.Play();
        }

        if (src.isPlaying)
        {
            src.volume = Mathf.MoveTowards(src.volume, targetVol, dt * smoothSpeed * 0.35f);
            src.pitch = Mathf.Lerp(src.pitch, targetPitch, Mathf.Clamp01(dt * 4f));
            if (targetVol <= 0.004f && src.volume <= 0.004f)
            {
                src.volume = 0f;
                src.Stop();
            }
        }
    }

    /// <summary>
    /// Modula en tiempo real el sonido de las olas y el viento según la intensidad real de la marea y las ráfagas.
    /// </summary>
    public void UpdateBeachAmbience(
        float tideProgress01,
        float waveSwash01,
        bool isWaveSurging,
        float waveEnergy01,
        float windIntensity01,
        float windGust01)
    {
        _lastAmbienceFeedTime = Time.unscaledTime;

        // Olas: base suave + crecimiento con progreso de marea + pico cuando rompe la ola hacia el castillo
        float surgeBoost = isWaveSurging ? 0.09f : 0.02f;
        float oceanBase = Mathf.Lerp(0.055f, 0.14f, Mathf.Clamp01(waveEnergy01));
        float tideTension = Mathf.Clamp01(tideProgress01) * 0.09f;
        float swashDynamic = Mathf.Clamp01(waveSwash01) * (0.075f + 0.06f * Mathf.Clamp01(waveEnergy01)) + surgeBoost * Mathf.Clamp01(waveSwash01);
        _targetOceanVol = Mathf.Clamp(oceanBase + tideTension + swashDynamic, 0.05f, 0.36f);
        _targetOceanPitch = Mathf.Lerp(0.92f, 1.10f, Mathf.Clamp01(waveSwash01 * 0.6f + waveEnergy01 * 0.4f));

        // Viento: mudo en mapas calmos (Soleado), y ruge en niveles ventosos/tormenta con cada ráfaga
        float wind = Mathf.Clamp01(windIntensity01);
        if (wind < 0.06f)
        {
            _targetWindVol = 0f;
            _targetWindPitch = 0.95f;
        }
        else
        {
            float gustFactor = 0.35f + 0.65f * Mathf.Clamp01(windGust01);
            _targetWindVol = Mathf.Clamp(wind * gustFactor * 0.40f, 0f, 0.38f);
            _targetWindPitch = Mathf.Lerp(0.86f, 1.22f, Mathf.Clamp01(wind * 0.5f + windGust01 * 0.5f));
        }
    }

    public void StopBeachAmbience(bool immediate = false)
    {
        _lastAmbienceFeedTime = -10f;
        _targetOceanVol = 0f;
        _targetWindVol = 0f;

        if (immediate)
        {
            if (_oceanAmbSource != null) { _oceanAmbSource.Stop(); _oceanAmbSource.volume = 0f; }
            if (_windAmbSource != null) { _windAmbSource.Stop(); _windAmbSource.volume = 0f; }
        }
    }

    // --- SFX de Gameplay y Resultado ---

    public void PlayWinSound()
    {
        PlayUiClip(winSound, 1.0f, 0.90f);
    }

    public void PlayLoseSound()
    {
        PlayUiClip(loseSound, 1.0f, 0.85f);
    }

    public void PlayStarPopSound(int starIndexZeroBased)
    {
        if (_starPopClip == null) return;
        // Acorde ascendente Do6 -> Mi6 -> Sol6 según la estrella que aparece
        float[] starPitches = { 1.00f, 1.2599f, 1.4983f };
        int idx = Mathf.Clamp(starIndexZeroBased, 0, starPitches.Length - 1);
        PlayUiClip(_starPopClip, starPitches[idx], 0.88f);
    }

    public void PlaySpawnSandSound()
    {
        PlaySpawnSandSound(false);
    }

    public void PlaySpawnSandSound(bool isWetSand)
    {
        if (isWetSand && _wetSandSculptClip != null)
        {
            PlaySfxClip(_wetSandSculptClip, Random.Range(0.92f, 1.08f), 0.72f);
            return;
        }
        PlaySfxClip(spawnSandSound, Random.Range(0.94f, 1.08f), 0.78f);
    }

    public void PlayBucketSwitchSound(bool switchedToWetSand)
    {
        AudioClip clip = switchedToWetSand ? _bucketToWetClip : _bucketToDryClip;
        PlayUiClip(clip, Random.Range(0.96f, 1.05f), 0.82f);
    }

    public void PlayWaveBlockSound()
    {
        float now = Time.unscaledTime;
        if (now - _lastWaveBlockTime < 0.28f) return;
        _lastWaveBlockTime = now;
        PlaySfxClip(_waveBlockClip, Random.Range(0.90f, 1.12f), 0.56f);
    }

    public void PlaySandCrumbleSound()
    {
        float now = Time.unscaledTime;
        if (now - _lastCrumbleTime < 0.26f) return;
        _lastCrumbleTime = now;
        PlaySfxClip(_sandCrumbleClip, Random.Range(0.88f, 1.10f), 0.54f);
    }

    public void PlayErrorNoSandSound()
    {
        PlayUiClip(errorNoSandSound != null ? errorNoSandSound : _lockedThudClip, 1.0f, 0.75f);
    }

    // --- SFX de UI, Navegación y Tienda ---

    public void PlayButtonSound()
    {
        if (buttonClickSound != null && uiSource != null)
        {
            uiSource.pitch = Random.Range(1.0f, 1.20f);
            uiSource.PlayOneShot(buttonClickSound, 0.88f);
        }
    }

    public void PlayPageWhooshSound()
    {
        PlayUiClip(_whooshPageClip, Random.Range(0.95f, 1.08f), 0.72f);
    }

    public void PlayLevelSelectSound()
    {
        PlayUiClip(_levelSelectClip, 1.0f, 0.86f);
    }

    public void PlayLockedSound()
    {
        PlayUiClip(_lockedThudClip, Random.Range(0.95f, 1.04f), 0.80f);
    }

    public void PlayPackUnlockSound()
    {
        PlayUiClip(_packUnlockClip, 1.0f, 0.92f);
    }

    public void PlayPaperScrollSound(bool opening)
    {
        PlayUiClip(_paperScrollClip, opening ? 1.06f : 0.92f, 0.78f);
    }

    // --- SFX de Diorama Interactivo (Menú, Créditos, Fondos de Mundos y Postales de Tienda) ---

    public void PlayPalmCocoSound()
    {
        PlaySfxClip(_palmCocoClip, Random.Range(0.95f, 1.07f), 0.84f);
    }

    public void PlayWaterSplashSound(bool softDrag = false)
    {
        float now = Time.unscaledTime;
        float minGap = softDrag ? 0.16f : 0.08f;
        if (now - _lastSplashTime < minGap) return;
        _lastSplashTime = now;
        PlaySfxClip(_waterSplashClip, Random.Range(0.92f, 1.16f), softDrag ? 0.45f : 0.72f);
    }

    public void PlaySunSparkleSound()
    {
        PlayUiClip(_sunSparkleClip, Random.Range(0.97f, 1.06f), 0.74f);
    }

    public void PlayGullChirpSound()
    {
        PlaySfxClip(_gullChirpClip, Random.Range(0.93f, 1.12f), 0.68f);
    }

    public void PlayCastlePatSound()
    {
        PlaySfxClip(_castlePatClip, Random.Range(0.94f, 1.08f), 0.76f);
    }

    public void PlayNatureRustleSound(bool softDrag = false)
    {
        float now = Time.unscaledTime;
        float minGap = softDrag ? 0.18f : 0.09f;
        if (now - _lastRustleTime < minGap) return;
        _lastRustleTime = now;
        PlaySfxClip(_natureRustleClip, Random.Range(0.92f, 1.14f), softDrag ? 0.42f : 0.66f);
    }

    private void PlaySfxClip(AudioClip clip, float pitch, float volume)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    private void PlayUiClip(AudioClip clip, float pitch, float volume)
    {
        if (clip != null && uiSource != null)
        {
            uiSource.pitch = pitch;
            uiSource.PlayOneShot(clip, volume);
        }
    }
}
