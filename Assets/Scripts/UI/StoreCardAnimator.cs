using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Convierte cada tarjeta coleccionable de la Tienda (Img_Card_Ilustrada) en una postal viva a 60 FPS:
/// - Capa 1: Shader GPU UI/StoreCardLive estrictamente acotado al rectángulo interior de la ilustración + brillo foil sutil.
/// - Capa 2: Micro-partículas pixel art temáticas recortadas dentro de la ventana mediante RectMask2D.
/// - Capa 3: Flotación coleccionable sutil, inclinación con acelerómetro e interacción táctil en el paisaje.
/// </summary>
public class StoreCardAnimator : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public enum CardBiome
    {
        PlayaLocal = 0,
        Brasil = 1,
        Japon = 2,
        Islandia = 3
    }

    private class CardAmbientParticle
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

    private class CardTapParticle
    {
        public RectTransform Rt;
        public Image Img;
        public Vector2 Vel;
        public float Gravity;
        public float Life;
        public float MaxLife;
        public Color BaseColor;
    }

    // Rectángulos exactos (uMin, vMin, uMax, vMax) de la ventana interior de cada tarjeta
    private static readonly Vector4 WIN_PLAYA    = new Vector4(0.119f, 0.302f, 0.915f, 0.899f);
    private static readonly Vector4 WIN_BRASIL   = new Vector4(0.134f, 0.286f, 0.858f, 0.794f);
    private static readonly Vector4 WIN_JAPON    = new Vector4(0.122f, 0.242f, 0.912f, 0.806f);
    private static readonly Vector4 WIN_ISLANDIA = new Vector4(0.079f, 0.256f, 0.921f, 0.862f);

    // Rectángulos exactos (uMin, vMin, uMax, vMax) del zócalo hundido para el botón de acción de cada tarjeta
    private static readonly Vector4 SLOT_PLAYA    = new Vector4(122f / 637f, 1f - 941f / 1016f,  515f / 637f, 1f - 836f / 1016f);
    private static readonly Vector4 SLOT_BRASIL   = new Vector4(134f / 701f, 1f - 1007f / 1096f, 567f / 701f, 1f - 890f / 1096f);
    private static readonly Vector4 SLOT_JAPON    = new Vector4(148f / 674f, 1f - 865f / 933f,   521f / 674f, 1f - 766f / 933f);
    private static readonly Vector4 SLOT_ISLANDIA = new Vector4(126f / 760f, 1f - 1263f / 1374f, 635f / 760f, 1f - 1126f / 1374f);

    private Image _cardImage;
    private RectTransform _cardRt;
    private RectTransform _maskRt;
    private RectTransform _boundButtonRt;
    private Image _boundButtonImg;
    private Camera _uiCam;
    private Material _cardMat;

    private CardBiome _biome = CardBiome.PlayaLocal;
    private Vector4 _winRect = WIN_PLAYA;
    private Vector4 _slotRect = SLOT_PLAYA;
    private float _frostAmount;
    private bool _initialized;
    private bool _externalAnimPaused;
    private Vector2 _baseAnchoredPos;
    private bool _hasBasePos;

    private float _tiltX;
    private float _tapBounce;
    private float _lastDragTime = -10f;
    private float _lastSandAudioTime = -10f;
    private Vector2 _lastCardRectSize;
    private Sprite _lastSprite;

    private readonly Vector4[] _ripples = new Vector4[3];
    private int _nextRippleIdx;

    private Sprite _gullUpSpr;
    private Sprite _gullDownSpr;
    private Sprite _leafSpr;
    private Sprite _sakuraSpr;
    private Sprite _snowSpr;
    private Sprite _grainSpr;

    private readonly List<CardAmbientParticle> _ambient = new List<CardAmbientParticle>(10);
    private readonly List<CardTapParticle> _tapPool = new List<CardTapParticle>(10);

    public static StoreCardAnimator Attach(GameObject cardGO)
    {
        if (cardGO == null) return null;
        StoreCardAnimator anim = cardGO.GetComponent<StoreCardAnimator>();
        if (anim == null) anim = cardGO.AddComponent<StoreCardAnimator>();
        anim.EnsureInitialized();
        return anim;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (_cardMat != null)
        {
            Destroy(_cardMat);
            _cardMat = null;
        }
    }

    private void OnDisable()
    {
        _tapBounce = 0f;
        for (int i = 0; i < _tapPool.Count; i++)
        {
            _tapPool[i].Life = 0f;
            if (_tapPool[i].Rt != null) _tapPool[i].Rt.gameObject.SetActive(false);
        }
        ResetCardTransformToBase();
    }

    public void EnsureInitialized()
    {
        if (_initialized) return;

        _cardRt = GetComponent<RectTransform>();
        _cardImage = GetComponent<Image>();

        if (_cardRt != null && !_hasBasePos)
        {
            _baseAnchoredPos = _cardRt.anchoredPosition;
            _hasBasePos = true;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        _uiCam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (_cardImage != null)
        {
            _cardImage.raycastTarget = true;
            Shader sh = Shader.Find("UI/StoreCardLive");
            if (sh != null)
            {
                _cardMat = new Material(sh);
                _cardMat.SetFloat("_SheenStrength", 1.0f);
                _cardImage.material = _cardMat;
            }
        }

        for (int i = 0; i < _ripples.Length; i++)
            _ripples[i] = new Vector4(0.5f, 0.5f, -50f, 0f);

        _gullUpSpr = Resources.Load<Sprite>("MenuLayers/Menu_Gull_0");
        _gullDownSpr = Resources.Load<Sprite>("MenuLayers/Menu_Gull_1");
        _leafSpr = Resources.Load<Sprite>("MenuLayers/Menu_Palm_Leaf");
        _sakuraSpr = Resources.Load<Sprite>("MenuLayers/World_Sakura_Petal");
        _snowSpr = Resources.Load<Sprite>("MenuLayers/World_Snowflake");
        _grainSpr = Resources.Load<Sprite>("MenuLayers/Menu_Sand_Grain");

        BuildMaskedParticlePools();
        _initialized = true;
        ApplyCardBiome(_biome, true);
    }

    public void SetExternalAnimationPaused(bool paused)
    {
        EnsureInitialized();
        _externalAnimPaused = paused;
        if (paused)
        {
            _tapBounce = 0f;
            ResetCardTransformToBase();
        }
    }

    private void ResetCardTransformToBase()
    {
        if (_cardRt == null || !_hasBasePos) return;
        _cardRt.anchoredPosition = _baseAnchoredPos;
        _cardRt.localEulerAngles = Vector3.zero;
        _cardRt.localScale = Vector3.one;
    }

    public void SetPackById(string packId)
    {
        EnsureInitialized();
        CardBiome target = CardBiome.PlayaLocal;
        if (!string.IsNullOrEmpty(packId))
        {
            string lower = packId.ToLowerInvariant();
            if (lower.Contains("brasil")) target = CardBiome.Brasil;
            else if (lower.Contains("japon")) target = CardBiome.Japon;
            else if (lower.Contains("islandia")) target = CardBiome.Islandia;
        }
        ApplyCardBiome(target, false);
    }

    private void BuildMaskedParticlePools()
    {
        Transform old = transform.Find("CardFX_WindowMask");
        if (old != null) Destroy(old.gameObject);

        // RectMask2D recorta limpiamente todas las partículas al borde negro exacto de la ventana interior
        GameObject maskGO = new GameObject("CardFX_WindowMask", typeof(RectTransform), typeof(RectMask2D));
        maskGO.transform.SetParent(transform, false);
        maskGO.transform.SetAsFirstSibling();

        _maskRt = maskGO.GetComponent<RectTransform>();
        _maskRt.anchorMin = new Vector2(0.5f, 0.5f);
        _maskRt.anchorMax = new Vector2(0.5f, 0.5f);
        _maskRt.pivot = new Vector2(0.5f, 0.5f);

        _ambient.Clear();
        for (int i = 0; i < 10; i++)
        {
            GameObject go = new GameObject("CardAmb_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_maskRt, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.SetActive(false);

            _ambient.Add(new CardAmbientParticle
            {
                Rt = go.GetComponent<RectTransform>(),
                Img = img
            });
        }

        _tapPool.Clear();
        for (int i = 0; i < 10; i++)
        {
            GameObject go = new GameObject("CardTap_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_maskRt, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.SetActive(false);

            _tapPool.Add(new CardTapParticle
            {
                Rt = go.GetComponent<RectTransform>(),
                Img = img
            });
        }
    }

    public void SetComingSoonFrost(float amount)
    {
        EnsureInitialized();
        _frostAmount = Mathf.Clamp01(amount);
        if (_cardMat != null)
            _cardMat.SetFloat("_FrostAmount", _frostAmount);
    }

    public void BindActionButton(RectTransform btnRt, Image btnImg)
    {
        EnsureInitialized();
        _boundButtonRt = btnRt;
        _boundButtonImg = btnImg;
        SyncActionButtonToSlot();
    }

    private void ApplyCardBiome(CardBiome biome, bool force)
    {
        _biome = biome;
        switch (_biome)
        {
            case CardBiome.Brasil:   _winRect = WIN_BRASIL;   _slotRect = SLOT_BRASIL;   break;
            case CardBiome.Japon:    _winRect = WIN_JAPON;    _slotRect = SLOT_JAPON;    break;
            case CardBiome.Islandia: _winRect = WIN_ISLANDIA; _slotRect = SLOT_ISLANDIA; break;
            default:                 _winRect = WIN_PLAYA;    _slotRect = SLOT_PLAYA;    break;
        }

        if (_cardMat != null)
        {
            _cardMat.SetFloat("_CardMode", (float)(int)_biome);
            _cardMat.SetVector("_WinRect", _winRect);
            _cardMat.SetFloat("_FrostAmount", _frostAmount);
        }

        for (int i = 0; i < _ripples.Length; i++)
            _ripples[i] = new Vector4(0.5f, 0.5f, -50f, 0f);

        for (int i = 0; i < _tapPool.Count; i++)
        {
            _tapPool[i].Life = 0f;
            if (_tapPool[i].Rt != null) _tapPool[i].Rt.gameObject.SetActive(false);
        }

        SyncMaskToPortraitWindow(true);
        SyncActionButtonToSlot();
        ConfigureAmbientParticlesForCard(_biome);

        if (_maskRt != null) _maskRt.SetAsFirstSibling();
    }

    private bool ComputeFittedSpriteRect(out float fitLeft, out float fitBottom, out float fitW, out float fitH)
    {
        fitLeft = 0f; fitBottom = 0f; fitW = 0f; fitH = 0f;
        if (_cardRt == null) return false;
        Rect r = _cardRt.rect;
        if (r.width <= 1f || r.height <= 1f) return false;

        Sprite spr = _cardImage != null ? _cardImage.sprite : null;
        if (spr != null && _cardImage.preserveAspect && spr.rect.height > 1f)
        {
            float sprAspect = spr.rect.width / spr.rect.height;
            float rectAspect = r.width / r.height;
            if (sprAspect > rectAspect)
            {
                fitW = r.width;
                fitH = r.width / sprAspect;
            }
            else
            {
                fitH = r.height;
                fitW = r.height * sprAspect;
            }
            fitLeft = r.xMin + (r.width - fitW) * 0.5f;
            fitBottom = r.yMin + (r.height - fitH) * 0.5f;
        }
        else
        {
            fitLeft = r.xMin;
            fitBottom = r.yMin;
            fitW = r.width;
            fitH = r.height;
        }
        return true;
    }

    private void SyncMaskToPortraitWindow(bool force)
    {
        if (_maskRt == null || _cardRt == null) return;
        Sprite curSpr = _cardImage != null ? _cardImage.sprite : null;
        Vector2 curSize = _cardRt.rect.size;
        if (!force && curSpr == _lastSprite && (curSize - _lastCardRectSize).sqrMagnitude < 0.25f) return;

        _lastSprite = curSpr;
        _lastCardRectSize = curSize;

        float fitLeft, fitBottom, fitW, fitH;
        if (!ComputeFittedSpriteRect(out fitLeft, out fitBottom, out fitW, out fitH)) return;

        float winLeft = fitLeft + fitW * _winRect.x;
        float winRight = fitLeft + fitW * _winRect.z;
        float winBottom = fitBottom + fitH * _winRect.y;
        float winTop = fitBottom + fitH * _winRect.w;

        _maskRt.anchoredPosition = new Vector2((winLeft + winRight) * 0.5f, (winBottom + winTop) * 0.5f);
        _maskRt.sizeDelta = new Vector2(Mathf.Max(4f, winRight - winLeft), Mathf.Max(4f, winTop - winBottom));
        SyncActionButtonToSlot();
    }

    public void SyncActionButtonToSlot()
    {
        if (_boundButtonRt == null || _cardRt == null) return;
        float fitLeft, fitBottom, fitW, fitH;
        if (!ComputeFittedSpriteRect(out fitLeft, out fitBottom, out fitW, out fitH)) return;

        float slLeft = fitLeft + fitW * _slotRect.x;
        float slRight = fitLeft + fitW * _slotRect.z;
        float slBottom = fitBottom + fitH * _slotRect.y;
        float slTop = fitBottom + fitH * _slotRect.w;

        float slW = Mathf.Max(20f, slRight - slLeft);
        float slH = Mathf.Max(12f, slTop - slBottom);

        // Dejar un borde hundido limpio y parejo (~3px) alrededor de la cápsula del botón
        float maxW = Mathf.Max(20f, slW - 6f);
        float maxH = Mathf.Max(12f, slH - 5f);

        float btnAspect = 660f / 168f;
        if (_boundButtonImg != null && _boundButtonImg.sprite != null && _boundButtonImg.sprite.rect.height > 1f)
        {
            btnAspect = _boundButtonImg.sprite.rect.width / _boundButtonImg.sprite.rect.height;
        }

        float bw = maxW;
        float bh = bw / btnAspect;
        if (bh > maxH)
        {
            bh = maxH;
            bw = bh * btnAspect;
        }

        _boundButtonRt.anchorMin = new Vector2(0.5f, 0.5f);
        _boundButtonRt.anchorMax = new Vector2(0.5f, 0.5f);
        _boundButtonRt.pivot = new Vector2(0.5f, 0.5f);
        _boundButtonRt.anchoredPosition = new Vector2((slLeft + slRight) * 0.5f, (slBottom + slTop) * 0.5f);
        _boundButtonRt.sizeDelta = new Vector2(bw, bh);
    }

    private void ConfigureAmbientParticlesForCard(CardBiome biome)
    {
        for (int i = 0; i < _ambient.Count; i++)
        {
            CardAmbientParticle p = _ambient[i];
            p.IsGull = false;
            p.Rt.localRotation = Quaternion.identity;
            p.Rt.localScale = Vector3.one;
            p.Rt.gameObject.SetActive(false);
        }

        if (biome == CardBiome.PlayaLocal)
        {
            // 2 mini gaviotas pixel art volando por el cielo soleado de la postal
            for (int i = 0; i < 2; i++)
            {
                CardAmbientParticle p = _ambient[i];
                p.IsGull = true;
                p.Img.sprite = (i % 2 == 0) ? _gullUpSpr : _gullDownSpr;
                p.Img.color = new Color(1f, 0.99f, 0.94f, 0.95f);
                p.AnchorPos = new Vector2(0.18f + i * 0.52f, 0.78f + i * 0.10f);
                float dir = (i % 2 == 0) ? 1f : -1f;
                p.Vel = new Vector2(dir * (0.08f + i * 0.02f), 0f);
                p.Phase = i * 2.4f;
                p.SwayAmp = 0.018f;
                p.SwayFreq = 2.0f;
                p.RotSpeed = 0f;
                p.BaseSize = 18f - i * 2f;
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.42f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == CardBiome.Brasil)
        {
            // 3 mini hojas tropicales cayendo suave por la postal de Río Caliente
            for (int i = 0; i < 3; i++)
            {
                CardAmbientParticle p = _ambient[i];
                p.Img.sprite = _leafSpr;
                p.Img.color = new Color(1f, 1f, 1f, 0.90f);
                p.AnchorPos = new Vector2(Random.Range(0.08f, 0.92f), Random.Range(0.15f, 0.95f));
                p.Vel = new Vector2(Random.Range(-0.04f, 0.05f), -Random.Range(0.06f, 0.10f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = 0.045f;
                p.SwayFreq = 1.6f;
                p.RotSpeed = Random.Range(-55f, 55f);
                p.BaseSize = Random.Range(12f, 16f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.52f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == CardBiome.Japon)
        {
            // 8 mini pétalos de cerezo sakura cayendo en diagonal dentro de la postal de Okinawa
            for (int i = 0; i < 8; i++)
            {
                CardAmbientParticle p = _ambient[i];
                p.Img.sprite = _sakuraSpr;
                p.Img.color = new Color(1f, 0.96f, 0.98f, Random.Range(0.82f, 0.96f));
                p.AnchorPos = new Vector2(Random.Range(0.02f, 0.98f), Random.Range(0.02f, 0.98f));
                p.Vel = new Vector2(Random.Range(0.05f, 0.10f), -Random.Range(0.07f, 0.12f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = Random.Range(0.03f, 0.06f);
                p.SwayFreq = Random.Range(1.4f, 2.2f);
                p.RotSpeed = Random.Range(-70f, 70f);
                p.BaseSize = Random.Range(9f, 13f);
                p.Rt.sizeDelta = new Vector2(p.BaseSize, p.BaseSize * 0.80f);
                p.Rt.gameObject.SetActive(p.Img.sprite != null);
            }
        }
        else if (biome == CardBiome.Islandia)
        {
            // 10 mini copos de nieve cayendo suavemente dentro de la postal de Playa Helada
            for (int i = 0; i < 10; i++)
            {
                CardAmbientParticle p = _ambient[i];
                p.Img.sprite = _snowSpr;
                p.Img.color = new Color(0.94f, 0.99f, 1f, Random.Range(0.62f, 0.92f));
                p.AnchorPos = new Vector2(Random.Range(0.02f, 0.98f), Random.Range(0.02f, 0.98f));
                p.Vel = new Vector2(Random.Range(-0.045f, -0.015f), -Random.Range(0.065f, 0.12f));
                p.Phase = Random.Range(0f, 6.28f);
                p.SwayAmp = Random.Range(0.025f, 0.045f);
                p.SwayFreq = Random.Range(1.0f, 1.8f);
                p.RotSpeed = Random.Range(-40f, 40f);
                p.BaseSize = Random.Range(8f, 13f);
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

        SyncMaskToPortraitWindow(false);
        UpdateTilt(dt);

        if (_cardMat != null)
        {
            _cardMat.SetFloat("_ShaderTime", now);
            _cardMat.SetFloat("_TiltX", _tiltX);
            _cardMat.SetVector("_Ripple0", _ripples[0]);
            _cardMat.SetVector("_Ripple1", _ripples[1]);
            _cardMat.SetVector("_Ripple2", _ripples[2]);
        }

        UpdateCardFloatAndBounce(dt, now);
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
            rawTilt = Mathf.Clamp((Input.mousePosition.x / Screen.width - 0.5f) * 0.65f, -0.5f, 0.5f);
        }
        _tiltX = Mathf.Lerp(_tiltX, rawTilt, Mathf.Clamp01(dt * 4.5f));
    }

    private void UpdateCardFloatAndBounce(float dt, float now)
    {
        if (_externalAnimPaused || _cardRt == null || !_hasBasePos) return;

        float bobY = Mathf.Sin(now * 1.45f) * 2.5f;
        float tiltZ = -_tiltX * 1.35f + Mathf.Sin(now * 0.92f) * 0.40f;

        float scaleBoost = 1f;
        if (_tapBounce > 0f)
        {
            _tapBounce = Mathf.Max(0f, _tapBounce - dt * 4.2f);
            scaleBoost = 1f + Mathf.Sin(_tapBounce * Mathf.PI) * 0.032f;
        }

        _cardRt.anchoredPosition = _baseAnchoredPos + new Vector2(0f, bobY);
        _cardRt.localEulerAngles = new Vector3(0f, 0f, tiltZ);
        _cardRt.localScale = new Vector3(scaleBoost, scaleBoost, 1f);
    }

    private void UpdateAmbientParticles(float dt, float now)
    {
        for (int i = 0; i < _ambient.Count; i++)
        {
            CardAmbientParticle p = _ambient[i];
            if (p.Rt == null || !p.Rt.gameObject.activeSelf) continue;

            if (p.IsGull)
            {
                p.AnchorPos.x += p.Vel.x * dt;
                if (p.AnchorPos.x > 1.15f) p.AnchorPos.x = -0.15f;
                if (p.AnchorPos.x < -0.15f) p.AnchorPos.x = 1.15f;

                float bob = Mathf.Sin(now * p.SwayFreq + p.Phase) * p.SwayAmp;
                Vector2 pos = new Vector2(p.AnchorPos.x, p.AnchorPos.y + bob);
                p.Rt.anchorMin = pos;
                p.Rt.anchorMax = pos;
                p.Rt.anchoredPosition = Vector2.zero;

                bool wingUp = Mathf.Sin(now * 4.0f + p.Phase) >= 0f;
                if (_gullUpSpr != null && _gullDownSpr != null)
                    p.Img.sprite = wingUp ? _gullUpSpr : _gullDownSpr;

                p.Rt.localScale = new Vector3(p.Vel.x >= 0f ? 1f : -1f, 1f, 1f);
            }
            else
            {
                p.AnchorPos.x += (p.Vel.x + _tiltX * 0.03f) * dt;
                p.AnchorPos.y += p.Vel.y * dt;

                if (p.AnchorPos.y < -0.08f)
                {
                    p.AnchorPos.y = 1.08f;
                    p.AnchorPos.x = Random.Range(0.02f, 0.98f);
                }
                if (p.AnchorPos.x > 1.10f) p.AnchorPos.x = -0.08f;
                if (p.AnchorPos.x < -0.10f) p.AnchorPos.x = 1.08f;

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
        if (_externalAnimPaused || _cardRt == null || _maskRt == null) return;

        Vector2 localPt;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_cardRt, screenPos, _uiCam, out localPt))
            return;

        float fitLeft, fitBottom, fitW, fitH;
        if (!ComputeFittedSpriteRect(out fitLeft, out fitBottom, out fitW, out fitH)) return;
        if (fitW <= 1f || fitH <= 1f) return;

        float u = (localPt.x - fitLeft) / fitW;
        float v = (localPt.y - fitBottom) / fitH;

        // Reaccionar solo al tocar dentro de la ventana de ilustración (nunca en el marco ni botones)
        if (u < _winRect.x || u > _winRect.z || v < _winRect.y || v > _winRect.w)
            return;

        float now = Time.unscaledTime;
        if (isDrag && now - _lastDragTime < 0.14f) return;
        _lastDragTime = now;

        if (!isDrag) _tapBounce = 1f;

        // Coordenadas normalizadas (0..1) dentro de CardFX_WindowMask
        float wu = Mathf.Clamp01((u - _winRect.x) / Mathf.Max(0.01f, _winRect.z - _winRect.x));
        float wv = Mathf.Clamp01((v - _winRect.y) / Mathf.Max(0.01f, _winRect.w - _winRect.y));

        // Detectar banda de agua por tarjeta para generar ondas GPU + gotitas
        float waterMinV = 0.52f, waterMaxV = 0.71f;
        if (_biome == CardBiome.Brasil) { waterMinV = 0.29f; waterMaxV = 0.56f; }
        else if (_biome == CardBiome.Japon) { waterMinV = 0.24f; waterMaxV = 0.42f; }
        else if (_biome == CardBiome.Islandia) { waterMinV = 0.45f; waterMaxV = 0.57f; }

        if (v >= waterMinV && v <= waterMaxV)
        {
            _ripples[_nextRippleIdx] = new Vector4(u, v, now, isDrag ? 0.75f : 1.0f);
            _nextRippleIdx = (_nextRippleIdx + 1) % _ripples.Length;
            Color dropCol = _biome == CardBiome.Islandia
                ? new Color(0.84f, 0.97f, 1f, 0.92f)
                : new Color(1f, 0.97f, 0.86f, 0.92f);
            SpawnTapBurst(new Vector2(wu, wv), isDrag ? 2 : 5, _grainSpr, dropCol, 52f, 130f, 5f, 0.34f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWaterSplashSound(isDrag);
            return;
        }

        // Estallido temático dentro de la postal (sol/arena, hojas, sakura o nieve)
        Sprite spr = _grainSpr;
        Color col = new Color(1f, 0.92f, 0.56f, 0.95f);
        float pxSize = 5f;
        bool playSand = false;
        bool playSun = false;

        if (_biome == CardBiome.PlayaLocal)
        {
            playSand = (v < waterMinV);
            playSun = (!playSand && !isDrag);
            col = playSand ? new Color(0.96f, 0.84f, 0.52f, 1f) : new Color(1f, 0.95f, 0.55f, 0.95f);
        }
        else if (_biome == CardBiome.Brasil)
        {
            spr = _leafSpr != null ? _leafSpr : _grainSpr;
            col = Color.white;
            pxSize = spr == _leafSpr ? 11f : 5f;
        }
        else if (_biome == CardBiome.Japon)
        {
            spr = _sakuraSpr != null ? _sakuraSpr : _grainSpr;
            col = Color.white;
            pxSize = spr == _sakuraSpr ? 10f : 5f;
        }
        else if (_biome == CardBiome.Islandia)
        {
            spr = _snowSpr != null ? _snowSpr : _grainSpr;
            col = new Color(0.92f, 0.99f, 1f, 0.95f);
            pxSize = spr == _snowSpr ? 10f : 5f;
            playSand = (v < waterMinV);
        }

        SpawnTapBurst(new Vector2(wu, wv), isDrag ? 2 : 6, spr, col, 65f, 175f, pxSize, 0.42f);
        if (playSand)
        {
            PlaySandAudio(now);
        }
        else if (playSun && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySunSparkleSound();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayNatureRustleSound(isDrag);
        }
    }

    private void PlaySandAudio(float now)
    {
        if (now - _lastSandAudioTime < 0.14f) return;
        _lastSandAudioTime = now;
        if (AudioManager.Instance != null)
        {
            if (AudioManager.Instance.sfxSource != null)
                AudioManager.Instance.sfxSource.pitch = Random.Range(0.94f, 1.08f);
            AudioManager.Instance.PlaySpawnSandSound();
        }
    }

    private void SpawnTapBurst(Vector2 winAnchorPos, int count, Sprite spr, Color col, float speed, float grav, float pxSize, float maxLife)
    {
        if (spr == null) return;
        for (int c = 0; c < count; c++)
        {
            CardTapParticle p = null;
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
            p.Rt.anchorMin = winAnchorPos;
            p.Rt.anchorMax = winAnchorPos;
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
            CardTapParticle p = _tapPool[i];
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
