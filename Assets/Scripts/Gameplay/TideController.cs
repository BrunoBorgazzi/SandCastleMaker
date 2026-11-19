using UnityEngine;

/// <summary>
/// Controla el avance de la marea viva y las olas pixel art que funcionan como el timer diegético
/// de cada nivel. El mar arranca desde el borde superior de la pantalla (debajo de la UI) y avanza
/// hacia abajo hasta la base del castillo.
/// </summary>
public class TideController : MonoBehaviour
{
    public static TideController Instance { get; private set; }

    [Header("Referencias Visuales")]
    public Material tideWaterMaterial;

    [Header("Estado en Tiempo de Ejecución (Lectura)")]
    public float tideProgress01;
    public float currentBaseTideY;
    public float currentWaveFrontY;
    public bool isWaveSurging;
    public bool isGustActive;
    public float currentWindGust01 { get; private set; }
    public float currentWindIntensity => _windIntensity;
    public bool IsInitialized => _isInitialized;

    private SpriteRenderer _waterRenderer;
    private Material _runtimeMaterial;
    private Camera _mainCamera;

    // Geometría del overlay de agua
    private Bounds _waterQuadBounds;
    private float _startShoreY;
    private float _defeatShoreY;

    // Parámetros del nivel activo
    private float _maxDurationSeconds = 65f;
    private float _waveSwingAmplitude = 0.85f;
    private float _waveFrequency = 0.24f;
    private float _windIntensity = 0f;
    private float _waveCurveAmp = 0.18f;

    private float _elapsedTime;
    private float _wavePhase;
    private float _randomSeed;
    private float _windVfxTimer;
    private bool _isInitialized;
    private ResponsiveViewportCamera _subscribedViewportCamera;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBeachAmbience();
        }
        if (_subscribedViewportCamera != null)
        {
            _subscribedViewportCamera.OnViewportAdapted -= RefreshWaterQuadFromCamera;
            _subscribedViewportCamera = null;
        }
    }

    /// <summary>
    /// Inicializa el plano de marea viva cubriendo desde arriba de todo de la pantalla hacia abajo.
    /// </summary>
    public void InitializeTide(Bounds castleBounds, LevelData levelData)
    {
        _mainCamera = Camera.main;

        if (levelData != null)
        {
            _maxDurationSeconds = Mathf.Max(15f, levelData.tideMaxDurationSeconds);
            _waveSwingAmplitude = Mathf.Max(0.2f, levelData.waveSwingAmplitude);
            _waveFrequency = Mathf.Max(0.08f, levelData.waveFrequency);
            _windIntensity = Mathf.Clamp01(levelData.windHazardIntensity);
        }

        if (_mainCamera != null)
        {
            ResponsiveViewportCamera viewportCam = _mainCamera.GetComponent<ResponsiveViewportCamera>();
            if (viewportCam != null && viewportCam != _subscribedViewportCamera)
            {
                if (_subscribedViewportCamera != null)
                {
                    _subscribedViewportCamera.OnViewportAdapted -= RefreshWaterQuadFromCamera;
                }
                _subscribedViewportCamera = viewportCam;
                _subscribedViewportCamera.OnViewportAdapted += RefreshWaterQuadFromCamera;
            }
        }

        float camOrtho = _mainCamera != null ? _mainCamera.orthographicSize : 7f;
        Vector3 camPos = _mainCamera != null ? _mainCamera.transform.position : castleBounds.center;

        // La orilla inicial arranca bien arriba en la pantalla (~82% de altura) y el mar llena todo hasta el borde superior
        _startShoreY = Mathf.Max(castleBounds.max.y + 1.8f, camPos.y + camOrtho * 0.64f);
        // La derrota ocurre cuando la marea base llega a cubrir la base del castillo
        _defeatShoreY = castleBounds.min.y + 0.25f;

        _waterRenderer = GetComponent<SpriteRenderer>();
        if (_waterRenderer == null)
        {
            _waterRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        RefreshWaterQuadFromCamera();

        _waterRenderer.sprite = SandCanvasController.CreateUnitWhiteSprite();
        // sortingOrder = 3: por encima de la arena (2), pero muy por debajo del Canvas UI (15)
        _waterRenderer.sortingOrder = 3;

        Shader waterShader = Shader.Find("SandCastle/TideWater");
        if (tideWaterMaterial != null)
        {
            _runtimeMaterial = new Material(tideWaterMaterial);
        }
        else if (waterShader != null)
        {
            _runtimeMaterial = new Material(waterShader);
        }
        else
        {
            _runtimeMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        ApplyAtmosphericWaterPalette(levelData);

        _elapsedTime = 0f;
        _windVfxTimer = 0.25f;
        _randomSeed = Random.Range(0f, 100f);
        currentBaseTideY = _startShoreY;
        currentWaveFrontY = _startShoreY;
        currentWindGust01 = 0f;
        isGustActive = false;

        _runtimeMaterial.SetFloat("_CurrentShoreY", currentWaveFrontY);
        _runtimeMaterial.SetFloat("_WaveCurveAmp", _waveCurveAmp);
        _runtimeMaterial.SetFloat("_WindIntensity", _windIntensity);
        _runtimeMaterial.SetFloat("_RandomSeed", _randomSeed);
        _runtimeMaterial.SetFloat("_UseWaveBarrier", 0f);

        _waterRenderer.material = _runtimeMaterial;
        _isInitialized = true;

        Debug.Log($"[TideController] Marea inicializada desde el tope de pantalla: Costa inicial Y={_startShoreY:F2} -> Base Castillo Y={_defeatShoreY:F2} en {_maxDurationSeconds}s.");
    }

    public void RefreshWaterQuadFromCamera()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        float camOrtho = _mainCamera != null ? _mainCamera.orthographicSize : 7f;
        float camHeight = camOrtho * 2f;
        float camWidth = _mainCamera != null ? camHeight * _mainCamera.aspect : 8.5f;
        Vector3 camPos = _mainCamera != null ? _mainCamera.transform.position : transform.position;

        float quadTopY = Mathf.Max(camPos.y + camOrtho + 2.0f, _startShoreY + 2.5f);
        float quadBottomY = Mathf.Min(camPos.y - camOrtho - 1.5f, _defeatShoreY - 2.5f);
        float quadHeight = quadTopY - quadBottomY;
        float quadWidth = Mathf.Max(camWidth * 1.65f, 12f);

        float zPos = SandCanvasController.Instance != null
            ? SandCanvasController.Instance.CastleWorldBounds.center.z - 0.1f
            : -8.835f;

        Vector3 quadCenter = new Vector3(camPos.x, (quadTopY + quadBottomY) * 0.5f, zPos);
        _waterQuadBounds = new Bounds(quadCenter, new Vector3(quadWidth, quadHeight, 0.1f));

        transform.position = quadCenter;
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
    }

    private void ApplyAtmosphericWaterPalette(LevelData levelData)
    {
        if (_runtimeMaterial == null) return;

        int levelId = levelData != null ? levelData.levelID : 0;
        if (levelId >= 101 && levelId <= 104)
        {
            switch (levelId)
            {
                case 101: // Brasil 1 - Sol Tropical (turquesa vibrante Caribe/Río)
                    _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.04f, 0.44f, 0.62f, 0.94f));
                    _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.14f, 0.80f, 0.82f, 0.80f));
                    _runtimeMaterial.SetColor("_FoamColor", new Color(1.00f, 0.98f, 0.92f, 0.97f));
                    return;
                case 102: // Brasil 2 - Ocaso Tropical (coral/turquesa dorado)
                    _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.12f, 0.34f, 0.50f, 0.94f));
                    _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.28f, 0.68f, 0.72f, 0.82f));
                    _runtimeMaterial.SetColor("_FoamColor", new Color(1.00f, 0.93f, 0.84f, 0.97f));
                    return;
                case 103: // Brasil 3 - Noche de Ráfagas (azul nocturno esmeralda eléctrico)
                    _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.06f, 0.20f, 0.34f, 0.95f));
                    _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.14f, 0.46f, 0.56f, 0.84f));
                    _runtimeMaterial.SetColor("_FoamColor", new Color(0.86f, 0.97f, 0.98f, 0.97f));
                    return;
                case 104: // Brasil 4 - Tormenta Tropical / Huracán
                    _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.09f, 0.24f, 0.30f, 0.96f));
                    _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.22f, 0.48f, 0.52f, 0.86f));
                    _runtimeMaterial.SetColor("_FoamColor", new Color(0.94f, 0.99f, 0.98f, 0.98f));
                    return;
            }
        }

        string bgName = (levelData != null && levelData.backgroundImage != null)
            ? levelData.backgroundImage.name.ToLowerInvariant()
            : "";

        if (bgName.Contains("nivel 2"))
        {
            _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.16f, 0.34f, 0.56f, 0.94f));
            _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.32f, 0.62f, 0.72f, 0.82f));
            _runtimeMaterial.SetColor("_FoamColor", new Color(1.00f, 0.94f, 0.86f, 0.97f));
        }
        else if (bgName.Contains("nivel 3"))
        {
            _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.08f, 0.16f, 0.36f, 0.95f));
            _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.20f, 0.38f, 0.62f, 0.84f));
            _runtimeMaterial.SetColor("_FoamColor", new Color(0.86f, 0.92f, 1.00f, 0.96f));
        }
        else if (bgName.Contains("nivel 4") || _windIntensity >= 0.6f)
        {
            _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.18f, 0.30f, 0.38f, 0.95f));
            _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.35f, 0.52f, 0.58f, 0.85f));
            _runtimeMaterial.SetColor("_FoamColor", new Color(0.95f, 0.97f, 0.98f, 0.98f));
        }
        else
        {
            _runtimeMaterial.SetColor("_DeepWaterColor", new Color(0.06f, 0.46f, 0.70f, 0.94f));
            _runtimeMaterial.SetColor("_ShallowWaterColor", new Color(0.16f, 0.74f, 0.84f, 0.80f));
            _runtimeMaterial.SetColor("_FoamColor", new Color(0.98f, 0.99f, 0.96f, 0.97f));
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;
        if (LevelManager.Instance != null && (LevelManager.Instance.isGameOver || LevelManager.Instance.isPausedByUI)) return;
        if (Time.timeScale <= 0.001f) return;

        float dt = Time.deltaTime;
        _elapsedTime += dt;

        // 1. Avance neto constante de la Marea Base (avanza más de lo que retrocede)
        tideProgress01 = Mathf.Clamp01(_elapsedTime / _maxDurationSeconds);
        currentBaseTideY = Mathf.Lerp(_startShoreY, _defeatShoreY, tideProgress01);

        // 2. Oscilación asimétrica de la rompiente (Swash rápido hacia abajo, Backwash marcado hacia arriba)
        // En niveles ventosos sumamos modulación de ciclo orgánico para que las olas no sean todas iguales
        float jitterRate = _elapsedTime + _randomSeed;
        float cycleMod = 1f + (_windIntensity * 0.25f * Mathf.Sin(jitterRate * 0.67f));
        float cyclePos = (_elapsedTime * _waveFrequency * cycleMod) % 1.0f;
        float swash01;
        if (cyclePos < 0.36f)
        {
            float tSurge = cyclePos / 0.36f;
            swash01 = Mathf.Sin(tSurge * Mathf.PI * 0.5f);
            isWaveSurging = true;
        }
        else
        {
            float tRetreat = (cyclePos - 0.36f) / 0.64f;
            swash01 = 0.5f * (1f + Mathf.Cos(tRetreat * Mathf.PI));
            isWaveSurging = false;
        }

        // 3. Ráfagas impredecibles por viento (combinación multi-frecuencia + ruido por partida)
        float windGustNoise = Mathf.PerlinNoise(_elapsedTime * 0.55f + _randomSeed, 7.3f);
        float suddenGust = Mathf.Max(0f, Mathf.Sin(_elapsedTime * 1.15f + _randomSeed * 2.1f));
        currentWindGust01 = Mathf.Clamp01(0.60f * Mathf.SmoothStep(0.30f, 0.85f, windGustNoise) + 0.35f * suddenGust);
        isGustActive = _windIntensity >= 0.30f && currentWindGust01 >= 0.42f;
        float windMultiplier = 1f + (_windIntensity * currentWindGust01);

        // Desplazamiento real de ola: avanza con fuerza hacia el castillo y retrocede bien arriba dejando playa mojada
        float surgeReach = _waveSwingAmplitude * 1.12f * windMultiplier;
        float retreatLift = _waveSwingAmplitude * 0.24f;
        currentWaveFrontY = currentBaseTideY - (swash01 * (surgeReach + retreatLift)) + retreatLift;

        if (_windIntensity >= 0.20f)
        {
            _windVfxTimer -= dt;
            if (_windVfxTimer <= 0f)
            {
                float gustFactor = _windIntensity * (0.4f + 0.6f * currentWindGust01);
                _windVfxTimer = Mathf.Lerp(0.48f, 0.14f, Mathf.Clamp01(gustFactor));

                if (_mainCamera != null && SandVFXManager.Instance != null)
                {
                    float camHeight = _mainCamera.orthographicSize * 2f;
                    float camWidth = camHeight * _mainCamera.aspect;
                    Bounds viewportBounds = new Bounds(_mainCamera.transform.position, new Vector3(camWidth, camHeight, 1f));
                    SandVFXManager.Instance.PlayWindGustVFX(viewportBounds, _windIntensity * (0.55f + 0.65f * currentWindGust01));
                }
            }
        }
        else
        {
            isGustActive = false;
        }

        // 4. Actualizar el shader de agua pixel art y el audio dinámico de olas + viento
        _wavePhase = _elapsedTime * (1.8f + _windIntensity * 1.6f);
        if (_runtimeMaterial != null)
        {
            _runtimeMaterial.SetFloat("_CurrentShoreY", currentWaveFrontY);
            _runtimeMaterial.SetFloat("_WavePhase", _wavePhase);
            _runtimeMaterial.SetFloat("_WindIntensity", _windIntensity);
        }

        if (AudioManager.Instance != null)
        {
            float waveEnergy01 = Mathf.InverseLerp(0.55f, 1.45f, _waveSwingAmplitude);
            AudioManager.Instance.UpdateBeachAmbience(
                tideProgress01,
                swash01,
                isWaveSurging,
                waveEnergy01,
                _windIntensity,
                currentWindGust01
            );
        }

        // 5. Aplicar erosión sobre la arena dibujada en el lienzo
        if (SandCanvasController.Instance != null && SandCanvasController.Instance.IsInitialized)
        {
            SandCanvasController.Instance.ApplyWaveErosion(this, dt);
        }

        // 6. Condición de Derrota Diegética: la marea base cubrió por completo la base del castillo
        if (tideProgress01 >= 1.0f && LevelManager.Instance != null && !LevelManager.Instance.isGameOver)
        {
            LevelManager.Instance.TriggerTideDefeat();
        }
    }

    public float GetShoreYAtWorldX(float worldX)
    {
        float u = Mathf.Clamp01((worldX - _waterQuadBounds.min.x) / Mathf.Max(_waterQuadBounds.size.x, 0.001f));
        float uWrapped = u * 6.283185f;
        float primary = Mathf.Sin(uWrapped + _wavePhase) * _waveCurveAmp;
        float secondary = Mathf.Cos(uWrapped * 2.0f - _wavePhase * 1.35f + _randomSeed) * (_waveCurveAmp * 0.45f);
        float windPacketA = Mathf.Sin(uWrapped * 3.0f + _wavePhase * 2.1f + _randomSeed * 1.7f) * (0.20f * _windIntensity);
        float windPacketB = Mathf.Cos(uWrapped * 5.0f - _wavePhase * 2.9f + _randomSeed * 0.6f) * (0.12f * _windIntensity);
        return currentWaveFrontY + primary + secondary + windPacketA + windPacketB;
    }

    /// <summary>
    /// Límite orgánico en movimiento detrás de las olas donde rompe la parte más profunda
    /// de la marea, evitando cortes rectos horizontales sobre la arena.
    /// </summary>
    public float GetMaxBuildableWorldY(float worldX)
    {
        float deepSurfOffset = Mathf.Max(0.45f, _waveSwingAmplitude * 0.56f);
        return GetShoreYAtWorldX(worldX) + deepSurfOffset;
    }

    public void UpdateWaveBarrier(Texture2D barrierTex, Bounds canvasBounds, float minY, float spanY)
    {
        if (_runtimeMaterial == null) return;
        if (barrierTex != null && spanY > 0.01f)
        {
            _runtimeMaterial.SetTexture("_WaveBarrierTex", barrierTex);
            _runtimeMaterial.SetFloat("_UseWaveBarrier", 1f);
            _runtimeMaterial.SetVector("_CanvasWorldX", new Vector4(canvasBounds.min.x, 1f / Mathf.Max(canvasBounds.size.x, 0.001f), 0f, 0f));
            _runtimeMaterial.SetVector("_BarrierRangeY", new Vector4(minY, spanY, 0f, 0f));
        }
        else
        {
            _runtimeMaterial.SetFloat("_UseWaveBarrier", 0f);
        }
    }
}
