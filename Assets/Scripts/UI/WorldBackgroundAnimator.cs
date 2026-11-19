using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Anima los fondos del Selector de Niveles (y opcionalmente Tienda) a 60 FPS en GPU
/// mediante WorldBgLiveUI.shader + partículas pixel art temáticas por bioma
/// (gaviotas, hojas tropicales, pétalos de sakura, copos de nieve) e interacción táctil.
/// </summary>
public class WorldBackgroundAnimator : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public enum BiomeType
    {
        PlayaLocal = 0,
        Brasil = 1,
        Japon = 2,
        Islandia = 3,
        Tienda = 4
    }

    private class AmbientParticle
    {
        public RectTransform Rt;
        public Image Img;
        public Vector2 AnchorPos;
        public Vector2 Vel;
        public float Phase;
        public float SwayAmp;
        public float SwayFreq;
        public float RotSpeed;
        public float BaseSize;
        public bool IsGull;
    }

    private class TapParticle
    {
        public RectTransform Rt;
        public Image Img;
        public Vector2 Vel;
        public float Gravity;
        public float Life;
        public float MaxLife;
        public Color BaseColor;
    }

    private Image _bgImage;
    private RectTransform _rootRt;
    private Camera _uiCam;
    private Material _liveMat;
    private BiomeType _biome = BiomeType.PlayaLocal;
    private bool _initialized;

    private readonly Vector4[] _ripples = new Vector4[4];
    private int _nextRippleIdx;
    private float _lastDragTime = -10f;
    private float _lastSandAudioTime = -10f;
    private float _tiltX;

    private Sprite _gullUpSpr;
    private Sprite _gullDownSpr;
    private Sprite _leafSpr;
    private Sprite _sakuraSpr;
    private Sprite _snowSpr;
    private Sprite _grainSpr;

    private readonly List<AmbientParticle> _ambient = new List<AmbientParticle>(16);
    private readonly List<TapParticle> _tapPool = new List<TapParticle>(14);

    public static WorldBackgroundAnimator Attach(GameObject bgGO)
    {
        if (bgGO == null) return null;
        WorldBackgroundAnimator anim = bgGO.GetComponent<WorldBackgroundAnimator>();
        if (anim == null) anim = bgGO.AddComponent<WorldBackgroundAnimator>();
        anim.EnsureInitialized();
        return anim;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (_liveMat != null)
        {
            Destroy(_liveMat);
            _liveMat = null;
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < _tapPool.Count; i++)
        {
            _tapPool[i].Life = 0f;
            if (_tapPool[i].Rt != null) _tapPool[i].Rt.gameObject.SetActive(false);
        }
    }

    public void EnsureInitialized()
    {
        if (_initialized) return;
        _rootRt = GetComponent<RectTransform>();
        _bgImage = GetComponent<Image>();

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        _uiCam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (_bgImage != null)
        {
            _bgImage.raycastTarget = true;
            Shader sh = Shader.Find("UI/WorldBgLive");
            if (sh != null)
            {
                _liveMat = new Material(sh);
                _bgImage.material = _liveMat;
            }
        }

        for (int i = 0; i < 4; i++)
            _ripples[i] = new Vector4(0.5f, 0.5f, -50f, 0f);

        _gullUpSpr = Resources.Load<Sprite>("MenuLayers/Menu_Gull_0");
        _gullDownSpr = Resources.Load<Sprite>("MenuLayers/Menu_Gull_1");
        _leafSpr = Resources.Load<Sprite>("MenuLayers/Menu_Palm_Leaf");
        _sakuraSpr = Resources.Load<Sprite>("MenuLayers/World_Sakura_Petal");
        _snowSpr = Resources.Load<Sprite>("MenuLayers/World_Snowflake");
        _grainSpr = Resources.Load<Sprite>("MenuLayers/Menu_Sand_Grain");

        BuildParticlePools();
        _initialized = true;
        ApplyBiome(_biome, true);
    }

    public void SetBiomeByPackId(string packId)
    {
        EnsureInitialized();
        BiomeType target = BiomeType.PlayaLocal;
        if (!string.IsNullOrEmpty(packId))
        {
            string lower = packId.ToLowerInvariant();
            if (lower.Contains("brasil")) target = BiomeType.Brasil;
            else if (lower.Contains("japon")) target = BiomeType.Japon;
            else if (lower.Contains("islandia")) target = BiomeType.Islandia;
            else if (lower.Contains("tienda") || lower.Contains("store")) target = BiomeType.Tienda;
        }
        ApplyBiome(target, false);
    }

    public void SetBiome(BiomeType biome)
    {
        EnsureInitialized();
        ApplyBiome(biome, false);
    }

    private void BuildParticlePools()
    {
        Transform old = transform.Find("WorldFX_Atmosphere");
        if (old != null) Destroy(old.gameObject);

        GameObject root = new GameObject("WorldFX_Atmosphere", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _ambient.Clear();
        for (int i = 0; i < 16; i++)
        {
            GameObject go = new GameObject("Amb_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.SetActive(false);

            _ambient.Add(new AmbientParticle
            {
                Rt = go.GetComponent<RectTransform>(),
                Img = img
            });
        }

        _tapPool.Clear();
        for (int i = 0; i < 14; i++)
        {
            GameObject go = new GameObject("Tap_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.SetActive(false);

            _tapPool.Add(new TapParticle
            {
                Rt = go.GetComponent<RectTransform>(),
                Img = img
            });
        }
    }

    private void ApplyBiome(BiomeType biome, bool force)
    {
        if (!force && _biome == biome && _liveMat != null)
        {
            _liveMat.SetFloat("_BiomeMode", (float)(int)_biome);
            return;
        }

        _biome = biome;
        if (_liveMat != null)
        {
            _liveMat.SetFloat("_BiomeMode", (float)(int)_biome);
        }

        // Limpiar ripples anteriores al cambiar de mundo
        for (int i = 0; i < 4; i++)
            _ripples[i] = new Vector4(0.5f, 0.5f, -50f, 0f);

        ConfigureAmbientParticlesForBiome(_biome);
    }

    private void ConfigureAmbientParticlesForBiome(BiomeType biome)
    {
        for (int i = 0; i < _ambient.Count; i++)
        {
            AmbientParticle p = _ambient[i];
            p.IsGull = false;
            p.Rt.localRotation = Quaternion.identity;
            p.Rt.localScale = Vector3.one;
            p.Rt.gameObject.SetActive(false);
        }

        if (biome == BiomeType.PlayaLocal)
        {
            // 3 gaviotas pixel art volando serenas por el cielo costero (sin puntitos flotando sobre el mar)
            for (int i = 0; i < 3; i++)
            {
                AmbientParticle p = _ambient[i];
                p.IsGull = true;
                p.Img.sprite = (i % 2 == 0) ? _gullUpSpr : _gullDownSpr;
                p.Img.color = new Color(1f, 0.98f, 0.92f, 0.95f);
                p.AnchorPos = new Vector2(0.15f + i * 0.34f, 0.68f + (i % 2) * 0.12f);
                float dir = (i % 2 == 0) ? 1f : -1f;
                p.Vel = new Vector2(dir * (0.026f + i * 0.006f), 0f);
                p.Phase = i * 2.1f;
                p.SwayAmp = 0.010f;
                p.SwayFreq = 1.8f;
                p.RotSpeed = 0f;
                p.BaseSize = 26f - i * 3f;
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.42f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == BiomeType.Tienda)
        {
            // Tienda de Playas: 2 gaviotas costeras en el cielo celeste + 4 hojitas de palmera cayendo suave con la brisa
            for (int i = 0; i < 2; i++)
            {
                AmbientParticle p = _ambient[i];
                p.IsGull = true;
                p.Img.sprite = (i % 2 == 0) ? _gullUpSpr : _gullDownSpr;
                p.Img.color = new Color(1f, 0.99f, 0.94f, 0.95f);
                p.AnchorPos = new Vector2(0.22f + i * 0.48f, 0.70f + i * 0.09f);
                float dir = (i % 2 == 0) ? 1f : -1f;
                p.Vel = new Vector2(dir * (0.024f + i * 0.005f), 0f);
                p.Phase = i * 2.7f;
                p.SwayAmp = 0.009f;
                p.SwayFreq = 1.7f;
                p.RotSpeed = 0f;
                p.BaseSize = 24f - i * 3f;
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.42f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
            for (int i = 2; i < 6; i++)
            {
                AmbientParticle p = _ambient[i];
                p.Img.sprite = _leafSpr;
                p.Img.color = new Color(1f, 1f, 1f, 0.88f);
                p.AnchorPos = new Vector2(Random.Range(0.04f, 0.96f), Random.Range(0.15f, 0.98f));
                p.Vel = new Vector2(Random.Range(-0.016f, 0.018f), -Random.Range(0.020f, 0.034f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = 0.022f;
                p.SwayFreq = 1.35f;
                p.RotSpeed = Random.Range(-45f, 45f);
                p.BaseSize = Random.Range(16f, 22f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.52f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == BiomeType.Brasil)
        {
            // 5 hojas tropicales cayendo lento con la brisa + 5 motas doradas suaves en el cielo soleado
            for (int i = 0; i < 5; i++)
            {
                AmbientParticle p = _ambient[i];
                p.Img.sprite = _leafSpr;
                p.Img.color = new Color(1f, 1f, 1f, 0.88f);
                p.AnchorPos = new Vector2(Random.Range(0.04f, 0.96f), Random.Range(0.08f, 0.98f));
                p.Vel = new Vector2(Random.Range(-0.014f, 0.022f), -Random.Range(0.022f, 0.036f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = 0.024f;
                p.SwayFreq = 1.4f;
                p.RotSpeed = Random.Range(-48f, 48f);
                p.BaseSize = Random.Range(18f, 24f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.52f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
            for (int i = 5; i < 10; i++)
            {
                AmbientParticle p = _ambient[i];
                p.Img.sprite = _grainSpr;
                p.Img.color = new Color(1f, 0.92f, 0.55f, 0.36f);
                p.AnchorPos = new Vector2(Random.Range(0.10f, 0.90f), Random.Range(0.68f, 0.96f));
                p.Vel = new Vector2(Random.Range(-0.008f, 0.012f), Random.Range(0.004f, 0.009f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = 0.010f;
                p.SwayFreq = 1.2f;
                p.RotSpeed = 24f;
                p.BaseSize = Random.Range(4f, 6f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == BiomeType.Japon)
        {
            // 14 pétalos de cerezo sakura cayendo suavemente en diagonal por toda la pantalla
            for (int i = 0; i < 14; i++)
            {
                AmbientParticle p = _ambient[i];
                p.Img.sprite = _sakuraSpr;
                p.Img.color = new Color(1f, 0.96f, 0.98f, Random.Range(0.78f, 0.96f));
                p.AnchorPos = new Vector2(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                p.Vel = new Vector2(Random.Range(0.020f, 0.042f), -Random.Range(0.026f, 0.048f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = Random.Range(0.018f, 0.032f);
                p.SwayFreq = Random.Range(1.2f, 2.1f);
                p.RotSpeed = Random.Range(-65f, 65f);
                p.BaseSize = Random.Range(13f, 20f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.80f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == BiomeType.Islandia)
        {
            // 16 copos de nieve pixel art cayendo suavemente con la brisa polar
            for (int i = 0; i < 16; i++)
            {
                AmbientParticle p = _ambient[i];
                p.Img.sprite = _snowSpr;
                p.Img.color = new Color(0.92f, 0.98f, 1f, Random.Range(0.55f, 0.90f));
                p.AnchorPos = new Vector2(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                p.Vel = new Vector2(Random.Range(-0.018f, -0.005f), -Random.Range(0.022f, 0.046f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = Random.Range(0.012f, 0.024f);
                p.SwayFreq = Random.Range(0.9f, 1.7f);
                p.RotSpeed = Random.Range(-35f, 35f);
                p.BaseSize = Random.Range(10f, 18f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        UpdateTilt(dt);
        if (_liveMat != null)
        {
            _liveMat.SetFloat("_ShaderTime", now);
            _liveMat.SetFloat("_TiltX", _tiltX);
            _liveMat.SetVector("_Ripple0", _ripples[0]);
            _liveMat.SetVector("_Ripple1", _ripples[1]);
            _liveMat.SetVector("_Ripple2", _ripples[2]);
            _liveMat.SetVector("_Ripple3", _ripples[3]);
        }

        UpdateAmbientParticles(dt, now);
        UpdateTapParticles(dt);
    }

    private void UpdateTilt(float dt)
    {
        float rawTilt = 0f;
        if (SystemInfo.supportsAccelerometer && Application.isMobilePlatform)
        {
            rawTilt = Mathf.Clamp(Input.acceleration.x * 1.4f, -1f, 1f);
        }
        else if (Screen.width > 10)
        {
            rawTilt = Mathf.Clamp((Input.mousePosition.x / Screen.width - 0.5f) * 0.55f, -0.45f, 0.45f);
        }
        _tiltX = Mathf.Lerp(_tiltX, rawTilt, Mathf.Clamp01(dt * 4f));
    }

    private void UpdateAmbientParticles(float dt, float now)
    {
        for (int i = 0; i < _ambient.Count; i++)
        {
            AmbientParticle p = _ambient[i];
            if (p.Rt == null || !p.Rt.gameObject.activeSelf) continue;

            if (p.IsGull)
            {
                p.AnchorPos.x += p.Vel.x * dt;
                if (p.AnchorPos.x > 1.12f) p.AnchorPos.x = -0.12f;
                if (p.AnchorPos.x < -0.12f) p.AnchorPos.x = 1.12f;

                float bob = Mathf.Sin(now * p.SwayFreq + p.Phase) * p.SwayAmp;
                Vector2 pos = new Vector2(p.AnchorPos.x - _tiltX * 0.012f, p.AnchorPos.y + bob);
                p.Rt.anchorMin = pos;
                p.Rt.anchorMax = pos;
                p.Rt.anchoredPosition = Vector2.zero;

                bool wingUp = Mathf.Sin(now * 3.8f + p.Phase) >= 0f;
                if (_gullUpSpr != null && _gullDownSpr != null)
                    p.Img.sprite = wingUp ? _gullUpSpr : _gullDownSpr;

                float flipX = p.Vel.x >= 0f ? 1f : -1f;
                p.Rt.localScale = new Vector3(flipX, 1f, 1f);
            }
            else
            {
                p.AnchorPos.x += (p.Vel.x + _tiltX * 0.014f) * dt;
                p.AnchorPos.y += p.Vel.y * dt;

                // Envolver suavemente en bordes
                if (p.AnchorPos.y < -0.06f)
                {
                    p.AnchorPos.y = 1.05f;
                    p.AnchorPos.x = Random.Range(0.02f, 0.98f);
                }
                else if (p.AnchorPos.y > 1.06f)
                {
                    p.AnchorPos.y = (_biome == BiomeType.Brasil) ? 0.66f : -0.04f;
                    p.AnchorPos.x = Random.Range(0.08f, 0.92f);
                }
                if (p.AnchorPos.x > 1.08f) p.AnchorPos.x = -0.06f;
                if (p.AnchorPos.x < -0.08f) p.AnchorPos.x = 1.06f;

                float swayX = Mathf.Sin(now * p.SwayFreq + p.Phase) * p.SwayAmp;
                Vector2 pos = new Vector2(p.AnchorPos.x + swayX, p.AnchorPos.y);
                p.Rt.anchorMin = pos;
                p.Rt.anchorMax = pos;
                p.Rt.anchoredPosition = Vector2.zero;

                if (Mathf.Abs(p.RotSpeed) > 0.1f)
                    p.Rt.Rotate(0f, 0f, p.RotSpeed * dt);
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        HandleTouch(eventData.position, false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        HandleTouch(eventData.position, true);
    }

    private void HandleTouch(Vector2 screenPos, bool isDrag)
    {
        if (_rootRt == null) return;
        Vector2 localPt;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRt, screenPos, _uiCam, out localPt))
            return;

        Rect r = _rootRt.rect;
        if (r.width <= 1f || r.height <= 1f) return;

        float u = Mathf.Clamp01((localPt.x - r.xMin) / r.width);
        float v = Mathf.Clamp01((localPt.y - r.yMin) / r.height);
        float now = Time.unscaledTime;

        float waterMinV = 0.35f, waterMaxV = 0.56f;
        if (_biome == BiomeType.Brasil) { waterMinV = 0.33f; waterMaxV = 0.61f; }
        else if (_biome == BiomeType.Japon) { waterMinV = 0.25f; waterMaxV = 0.56f; }
        else if (_biome == BiomeType.Islandia) { waterMinV = 0.29f; waterMaxV = 0.64f; }
        else if (_biome == BiomeType.Tienda) { waterMinV = 0.456f; waterMaxV = 0.632f; }

        if (_biome == BiomeType.Tienda && !isDrag && u >= 0.16f && u <= 0.48f && v >= 0.74f && v <= 0.92f)
        {
            // Tocar el sol gigante de la tienda dispara chispitas doradas cálidas
            SpawnTapBurst(new Vector2(u, v), 7, _grainSpr, new Color(1f, 0.94f, 0.52f, 0.95f), 90f, 90f, 6f, 0.45f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySunSparkleSound();
            return;
        }

        if (v >= waterMinV && v <= waterMaxV)
        {
            if (isDrag && now - _lastDragTime < 0.16f) return;
            _lastDragTime = now;
            _ripples[_nextRippleIdx] = new Vector4(u, v, now, isDrag ? 0.72f : 1.0f);
            _nextRippleIdx = (_nextRippleIdx + 1) & 3;
            Color dropCol = _biome == BiomeType.Islandia
                ? new Color(0.84f, 0.96f, 1f, 0.90f)
                : new Color(1f, 0.96f, 0.84f, 0.90f);
            SpawnTapBurst(new Vector2(u, v), isDrag ? 2 : 5, _grainSpr, dropCol, 65f, 150f, 6f, 0.35f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWaterSplashSound(isDrag);
            return;
        }

        if (v < waterMinV)
        {
            if (isDrag && now - _lastDragTime < 0.08f) return;
            _lastDragTime = now;

            Sprite spr = _grainSpr;
            Color col = new Color(0.96f, 0.84f, 0.52f, 1f);
            if (_biome == BiomeType.Japon)
            {
                spr = (Random.value > 0.55f && _sakuraSpr != null) ? _sakuraSpr : _grainSpr;
                col = spr == _sakuraSpr ? Color.white : new Color(0.98f, 0.86f, 0.66f, 1f);
            }
            else if (_biome == BiomeType.Islandia)
            {
                spr = (Random.value > 0.5f && _snowSpr != null) ? _snowSpr : _grainSpr;
                col = spr == _snowSpr ? new Color(0.88f, 0.97f, 1f, 0.95f) : new Color(0.32f, 0.38f, 0.48f, 0.95f);
            }

            SpawnTapBurst(new Vector2(u, v), isDrag ? 2 : 6, spr, col, 85f, 280f, spr == _grainSpr ? 6f : 12f, 0.44f);
            if (spr != _grainSpr && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayNatureRustleSound(isDrag);
            }
            else
            {
                PlaySandAudio(now);
            }
        }
    }

    private void PlaySandAudio(float now)
    {
        if (now - _lastSandAudioTime < 0.12f) return;
        _lastSandAudioTime = now;
        if (AudioManager.Instance != null)
        {
            if (AudioManager.Instance.sfxSource != null)
                AudioManager.Instance.sfxSource.pitch = Random.Range(0.94f, 1.08f);
            AudioManager.Instance.PlaySpawnSandSound();
        }
    }

    private void SpawnTapBurst(Vector2 anchorPos, int count, Sprite spr, Color col, float speed, float grav, float pxSize, float maxLife)
    {
        if (spr == null) return;
        for (int c = 0; c < count; c++)
        {
            TapParticle p = null;
            for (int i = 0; i < _tapPool.Count; i++)
            {
                if (_tapPool[i].Life <= 0f) { p = _tapPool[i]; break; }
            }
            if (p == null) break;

            float ang = Random.Range(0.25f, 2.89f);
            float spd = Random.Range(speed * 0.45f, speed * 1.15f);
            p.Img.sprite = spr;
            p.Img.color = col;
            p.BaseColor = col;
            p.Rt.anchorMin = anchorPos;
            p.Rt.anchorMax = anchorPos;
            p.Rt.anchoredPosition = Vector2.zero;
            p.Rt.sizeDelta = new Vector2(pxSize, pxSize);
            p.Rt.localRotation = Quaternion.identity;
            p.Vel = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd);
            p.Gravity = grav;
            p.MaxLife = maxLife;
            p.Life = maxLife;
            p.Rt.gameObject.SetActive(true);
        }
    }

    private void UpdateTapParticles(float dt)
    {
        for (int i = 0; i < _tapPool.Count; i++)
        {
            TapParticle p = _tapPool[i];
            if (p.Life <= 0f || p.Rt == null) continue;
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                p.Rt.gameObject.SetActive(false);
                continue;
            }
            p.Vel.y -= p.Gravity * dt;
            p.Rt.anchoredPosition += p.Vel * dt;
            Color c = p.BaseColor;
            c.a *= Mathf.Clamp01(p.Life / Mathf.Max(0.01f, p.MaxLife));
            p.Img.color = c;
        }
    }
}
