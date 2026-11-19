using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestor centralizado de efectos visuales (VFX) y partículas para SandCastleMaker.
/// Implementa un pool de ParticleSystems de alto rendimiento para garantizar 60 FPS estables
/// en dispositivos móviles sin recolección de basura (GC Alloc = 0 durante el gameplay).
/// Cuenta con inicialización diferida (Lazy Singleton) a prueba de fallos.
/// </summary>
public class SandVFXManager : MonoBehaviour
{
    private static SandVFXManager _instance;

    public static SandVFXManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SandVFXManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SandVFXManager");
                    _instance = go.AddComponent<SandVFXManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Configuración del Pool")]
    [Tooltip("Tamaño de la reserva de emisores de partículas")]
    public int poolSize = 15;

    [Header("Paleta de Color VFX")]
    public Color drySandColor = new Color(0.95f, 0.78f, 0.45f, 0.90f);
    public Color wetSandColor = new Color(0.62f, 0.44f, 0.24f, 0.95f);
    public Color foamSplashColor = new Color(0.95f, 0.99f, 1.00f, 0.88f);
    public Color windStreakColor = new Color(0.88f, 0.97f, 1.00f, 0.48f);
    public Color hurricaneSandSprayColor = new Color(0.96f, 0.84f, 0.56f, 0.52f);

    [Header("Prefabs Opcionales")]
    [Tooltip("Prefab con ParticleSystem personalizado. Si es nulo, se genera uno proceduralmente")]
    public GameObject impactVFXPrefab;

    private readonly Queue<ParticleSystem> _vfxPool = new Queue<ParticleSystem>();
    private ParticleSystem _windParticleSystem;
    private static Material _cachedPixelParticleMaterial;
    private static Texture2D _cachedPixelSquareTex;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            InitializePool();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        // Limpiar si ya había elementos
        _vfxPool.Clear();

        for (int i = 0; i < poolSize; i++)
        {
            ParticleSystem ps;
            if (impactVFXPrefab != null)
            {
                GameObject obj = Instantiate(impactVFXPrefab, transform);
                ps = obj.GetComponent<ParticleSystem>();
            }
            else
            {
                ps = CreateProceduralSandParticleSystem();
            }

            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                _vfxPool.Enqueue(ps);
            }
        }
    }

    /// <summary>
    /// Genera un ParticleSystem optimizado para móviles por código si no hay prefab asignado.
    /// </summary>
    private ParticleSystem CreateProceduralSandParticleSystem()
    {
        GameObject vfxObj = new GameObject($"SandVFX_Pooled_{_vfxPool.Count}");
        vfxObj.transform.SetParent(transform);

        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        main.startSpeed = 0f; // Controlado por EmitParams para 0 allocaciones y dirección precisa
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
        main.gravityModifier = 0.65f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0; // Se emite por ráfagas (Bursts)

        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0.25f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ParticleSystemRenderer renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = GetOrCreatePixelParticleMaterial();
            renderer.sortingOrder = 5;
        }

        return ps;
    }

    private static Material GetOrCreatePixelParticleMaterial()
    {
        if (_cachedPixelParticleMaterial != null) return _cachedPixelParticleMaterial;

        _cachedPixelSquareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        _cachedPixelSquareTex.filterMode = FilterMode.Point;
        _cachedPixelSquareTex.wrapMode = TextureWrapMode.Clamp;
        Color32[] cols = new Color32[16];
        for (int i = 0; i < 16; i++) cols[i] = new Color32(255, 255, 255, 255);
        _cachedPixelSquareTex.SetPixels32(cols);
        _cachedPixelSquareTex.Apply();

        _cachedPixelParticleMaterial = new Material(Shader.Find("Sprites/Default"));
        _cachedPixelParticleMaterial.mainTexture = _cachedPixelSquareTex;
        return _cachedPixelParticleMaterial;
    }

    /// <summary>
    /// Emite una ráfaga de polvo al esculpir arena en el lienzo.
    /// </summary>
    public void PlaySpawnVFX(Vector2 position, bool isWet)
    {
        EmitRadialBurst(position, isWet ? wetSandColor : drySandColor, 5, 0.075f, 0.9f, 0.28f);
    }

    /// <summary>
    /// Emite una ráfaga de partículas genérica por compatibilidad.
    /// </summary>
    public void PlayImpactVFX(Vector2 position, bool isWet, float intensity = 1.0f)
    {
        int particleCount = Mathf.Clamp(Mathf.RoundToInt(8 * intensity), 3, 20);
        float size = isWet ? 0.10f : 0.07f;
        EmitRadialBurst(position, isWet ? wetSandColor : drySandColor, particleCount, size, 1.3f, 0.35f);
    }

    /// <summary>
    /// Emite partículas Pixel Art elegantes cuando las olas disuelven la arena del castillo:
    /// arena seca se desmorona rápido en granos dorados + espuma; húmeda suelta terrones pesados al resistir.
    /// </summary>
    public void PlayWaterErosionVFX(Vector2 position, bool isWetSand)
    {
        if (_vfxPool.Count == 0) return;

        ParticleSystem ps = _vfxPool.Dequeue();
        if (ps == null) return;

        ps.gameObject.SetActive(true);

        if (isWetSand)
        {
            // Terrones oscuros más pesados cayendo despacio mientras el escudo aguanta 2-3s
            for (int i = 0; i < 4; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = position + new Vector2(Random.Range(-0.10f, 0.10f), Random.Range(-0.04f, 0.06f)),
                    velocity = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.85f, 0.25f), 0f),
                    startColor = Color.Lerp(wetSandColor, wetSandColor * 0.80f, Random.value),
                    startSize = Random.Range(0.085f, 0.125f),
                    startLifetime = Random.Range(0.32f, 0.50f)
                };
                ps.Emit(ep, 1);
            }
        }
        else
        {
            // Arena seca desmoronándose al instante + chispitas blancas de la ola
            for (int i = 0; i < 6; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = position + new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(-0.05f, 0.07f)),
                    velocity = new Vector3(Random.Range(-1.05f, 1.05f), Random.Range(-0.45f, 0.65f), 0f),
                    startColor = Color.Lerp(drySandColor, new Color(1f, 0.88f, 0.58f, 0.92f), Random.value),
                    startSize = Random.Range(0.055f, 0.095f),
                    startLifetime = Random.Range(0.28f, 0.46f)
                };
                ps.Emit(ep, 1);
            }

            for (int i = 0; i < 3; i++)
            {
                var foamEp = new ParticleSystem.EmitParams
                {
                    position = position + new Vector2(Random.Range(-0.10f, 0.10f), Random.Range(-0.02f, 0.05f)),
                    velocity = new Vector3(Random.Range(-0.75f, 0.75f), Random.Range(0.35f, 0.95f), 0f),
                    startColor = foamSplashColor,
                    startSize = Random.Range(0.05f, 0.085f),
                    startLifetime = Random.Range(0.22f, 0.36f)
                };
                ps.Emit(foamEp, 1);
            }
        }

        _vfxPool.Enqueue(ps);
    }

    /// <summary>
    /// Emite salpicaduras blancas hacia arriba y hacia los lados cuando la ola rebota contra un dique de arena húmeda.
    /// </summary>
    public void PlayWaterBounceVFX(Vector2 position)
    {
        if (_vfxPool.Count == 0) return;

        ParticleSystem ps = _vfxPool.Dequeue();
        if (ps == null) return;

        ps.gameObject.SetActive(true);

        for (int i = 0; i < 5; i++)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = position + new Vector2(Random.Range(-0.14f, 0.14f), Random.Range(0.0f, 0.08f)),
                velocity = new Vector3(Random.Range(-1.15f, 1.15f), Random.Range(0.85f, 1.75f), 0f),
                startColor = Color.Lerp(foamSplashColor, new Color(0.55f, 0.92f, 1.00f, 0.95f), Random.value * 0.45f),
                startSize = Random.Range(0.055f, 0.095f),
                startLifetime = Random.Range(0.24f, 0.40f)
            };
            ps.Emit(ep, 1);
        }

        _vfxPool.Enqueue(ps);
    }

    /// <summary>
    /// Emite terrones pesados y bruma al colapsar finalmente un sector saturado de arena húmeda tras resistir varias olas.
    /// </summary>
    public void PlayWetCrumbleVFX(Vector2 position)
    {
        if (_vfxPool.Count == 0) return;

        ParticleSystem ps = _vfxPool.Dequeue();
        if (ps == null) return;

        ps.gameObject.SetActive(true);

        for (int i = 0; i < 7; i++)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = position + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.06f, 0.08f)),
                velocity = new Vector3(Random.Range(-0.95f, 0.95f), Random.Range(-1.25f, 0.45f), 0f),
                startColor = Color.Lerp(wetSandColor, new Color(0.38f, 0.24f, 0.12f, 0.98f), Random.value),
                startSize = Random.Range(0.095f, 0.155f),
                startLifetime = Random.Range(0.36f, 0.58f)
            };
            ps.Emit(ep, 1);
        }

        for (int i = 0; i < 3; i++)
        {
            var epFoam = new ParticleSystem.EmitParams
            {
                position = position + new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0f, 0.06f)),
                velocity = new Vector3(Random.Range(-0.85f, 0.85f), Random.Range(0.45f, 1.05f), 0f),
                startColor = foamSplashColor,
                startSize = Random.Range(0.06f, 0.10f),
                startLifetime = Random.Range(0.25f, 0.38f)
            };
            ps.Emit(epFoam, 1);
        }

        _vfxPool.Enqueue(ps);
    }

    private void EmitRadialBurst(Vector2 position, Color baseColor, int count, float particleSize, float speed, float lifetime)
    {
        if (_vfxPool.Count == 0) return;

        ParticleSystem ps = _vfxPool.Dequeue();
        if (ps == null) return;

        ps.gameObject.SetActive(true);

        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float spd = Random.Range(speed * 0.45f, speed * 1.15f);
            var ep = new ParticleSystem.EmitParams
            {
                position = position + new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(-0.08f, 0.08f)),
                velocity = new Vector3(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd * 0.75f + 0.25f, 0f),
                startColor = baseColor,
                startSize = Random.Range(particleSize * 0.85f, particleSize * 1.15f),
                startLifetime = Random.Range(lifetime * 0.8f, lifetime * 1.15f)
            };
            ps.Emit(ep, 1);
        }

        _vfxPool.Enqueue(ps);
    }

    /// <summary>
    /// Emite ráfagas diagonales de viento costero y bruma marina sin asignación de memoria (0 GC Alloc).
    /// Se usa en niveles con viento fuerte o huracanes (ej. Brasil / Tormentas).
    /// </summary>
    public void PlayWindGustVFX(Bounds viewportBounds, float windIntensity)
    {
        if (windIntensity <= 0.05f || viewportBounds.size.x <= 0.1f || viewportBounds.size.y <= 0.1f)
        {
            return;
        }

        ParticleSystem windPs = EnsureWindParticleSystem();
        if (windPs == null) return;

        int streakCount = Mathf.Clamp(Mathf.RoundToInt(3 + windIntensity * 7f), 3, 12);
        float spawnRightEdge = viewportBounds.max.x + 0.3f;
        float spanWidth = viewportBounds.size.x * 0.45f;

        for (int i = 0; i < streakCount; i++)
        {
            bool isSandSpray = i % 3 == 0 && windIntensity >= 0.45f;
            float spawnX = spawnRightEdge - Random.Range(0f, spanWidth);
            float spawnY = isSandSpray
                ? Random.Range(viewportBounds.min.y + viewportBounds.size.y * 0.08f, viewportBounds.center.y)
                : Random.Range(viewportBounds.min.y + viewportBounds.size.y * 0.12f, viewportBounds.max.y - viewportBounds.size.y * 0.08f);

            float speedX = -(5.2f + windIntensity * 5.8f) * Random.Range(0.85f, 1.25f);
            float speedY = -0.65f + Random.Range(-0.45f, 0.28f);

            var ep = new ParticleSystem.EmitParams
            {
                position = new Vector3(spawnX, spawnY, 0f),
                velocity = new Vector3(speedX, speedY, 0f),
                startColor = isSandSpray ? hurricaneSandSprayColor : windStreakColor,
                startSize = isSandSpray ? Random.Range(0.055f, 0.095f) : Random.Range(0.045f, 0.075f),
                startLifetime = Random.Range(0.42f, 0.78f)
            };
            windPs.Emit(ep, 1);
        }
    }

    private ParticleSystem EnsureWindParticleSystem()
    {
        if (_windParticleSystem != null) return _windParticleSystem;

        GameObject windObj = new GameObject("WindGustVFX_Dedicated");
        windObj.transform.SetParent(transform);

        ParticleSystem ps = windObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1.0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.80f);
        main.startSpeed = 0f;
        main.startSize = 0.06f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        ParticleSystemRenderer renderer = windObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 3.6f;
            renderer.material = GetOrCreatePixelParticleMaterial();
            renderer.sortingOrder = 6;
        }

        _windParticleSystem = ps;
        return _windParticleSystem;
    }
}
