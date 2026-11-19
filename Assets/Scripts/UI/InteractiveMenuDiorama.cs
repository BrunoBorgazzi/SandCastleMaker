using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Diorama interactivo en tiempo real (60 FPS) para Menú Principal y Créditos.
/// Combina el shader de mar en perspectiva (MenuOceanUI.shader) con capas 2.5D parallax,
/// acelerómetro / mouse fallback, nubes, sol, gaviotas, palmera con hojas/cocos y arena táctil.
/// </summary>
public class InteractiveMenuDiorama : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private class UiParticle
    {
        public RectTransform Rt;
        public Image Img;
        public Vector2 Vel;
        public float Gravity;
        public float RotSpeed;
        public float Life;
        public float MaxLife;
        public Color BaseColor;
    }

    private struct GullState
    {
        public RectTransform Rt;
        public Image Img;
        public Vector2 AnchorPos;
        public float Speed;
        public float Phase;
        public float BoostTimer;
        public Vector2 BaseSize;
    }

    private bool _isCreditsMode;
    private bool _initialized;
    private RectTransform _rootRt;
    private Camera _uiCam;

    // Capas principales para parallax y animación
    private RectTransform _sunRt;
    private RectTransform _cloud1Rt;
    private RectTransform _cloud2Rt;
    private RectTransform _oceanRt;
    private RectTransform _beachRt;
    private RectTransform _castleRt;
    private RectTransform[] _castleFlags;
    private RectTransform _creditsNameRt;
    private RectTransform _creditsPropsRt;
    private RectTransform _palmRt;

    // Shader del mar en perspectiva y ripples táctiles
    private Material _oceanMat;
    private readonly Vector4[] _ripples = new Vector4[4];
    private int _nextRippleIdx;
    private float _lastDragRippleTime = -10f;
    private float _lastSandAudioTime = -10f;

    // Estado de inclinación (acelerómetro / mouse / teclas en Editor)
    private float _tiltX;
    private Vector2 _sunBaseAnchor;
    private Vector2 _castleBaseAnchorCenter;
    private Vector2 _palmBaseAnchorCenter;

    // Contadores reactivos de tap
    private float _sunBounceT;
    private float _castleBounceT;
    private float _palmBounceT;
    private float _nameBounceT;
    private float _propsBounceT;

    // Nubes y gaviotas
    private float _cloud1X = 0.24f;
    private float _cloud2X = 0.72f;
    private GullState[] _gulls = new GullState[2];
    private Sprite _gullUpSprite;
    private Sprite _gullDownSprite;
    private Sprite _leafSprite;
    private Sprite _coconutSprite;
    private Sprite _grainSprite;

    // Pool de partículas sin allocations por frame
    private readonly List<UiParticle> _pool = new List<UiParticle>(28);

    public static InteractiveMenuDiorama Attach(GameObject targetBgGO, bool isCreditsMode)
    {
        if (targetBgGO == null) return null;
        InteractiveMenuDiorama diorama = targetBgGO.GetComponent<InteractiveMenuDiorama>();
        if (diorama == null) diorama = targetBgGO.AddComponent<InteractiveMenuDiorama>();
        diorama.Initialize(isCreditsMode);
        return diorama;
    }

    public void Initialize(bool isCreditsMode)
    {
        if (_initialized && _isCreditsMode == isCreditsMode) return;
        _isCreditsMode = isCreditsMode;
        _rootRt = GetComponent<RectTransform>();
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        _uiCam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        // Mantener raycastTarget en el contenedor raíz invisible para capturar toques en cielo/agua/arena
        // sin interferir con SafeArea_Content (que está por encima en jerarquía)
        Image rootImg = GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = null;
            rootImg.color = new Color(1f, 1f, 1f, 0f);
            rootImg.raycastTarget = true;
        }

        BuildOrRebuildLayers();
        _initialized = true;
    }

    private void OnDisable()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            _pool[i].Life = 0f;
            if (_pool[i].Rt != null) _pool[i].Rt.gameObject.SetActive(false);
        }
    }

    private void BuildOrRebuildLayers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        _pool.Clear();

        for (int i = 0; i < 4; i++)
        {
            _ripples[i] = new Vector4(0.5f, 0.5f, -50f, 0f);
        }

        Sprite skySpr = Resources.Load<Sprite>("MenuLayers/Menu_Sky_Back");
        Sprite sunSpr = Resources.Load<Sprite>("MenuLayers/Menu_Sun");
        Sprite c1Spr = Resources.Load<Sprite>("MenuLayers/Menu_Cloud_1");
        Sprite c2Spr = Resources.Load<Sprite>("MenuLayers/Menu_Cloud_2");
        _gullUpSprite = Resources.Load<Sprite>("MenuLayers/Menu_Gull_0");
        _gullDownSprite = Resources.Load<Sprite>("MenuLayers/Menu_Gull_1");
        Sprite beachSpr = Resources.Load<Sprite>("MenuLayers/Menu_Beach_Sand");
        Sprite castleSpr = Resources.Load<Sprite>("MenuLayers/Menu_Castle_Prop");
        Sprite palmSpr = Resources.Load<Sprite>("MenuLayers/Menu_Palm_Tree");
        _leafSprite = Resources.Load<Sprite>("MenuLayers/Menu_Palm_Leaf");
        _coconutSprite = Resources.Load<Sprite>("MenuLayers/Menu_Coconut");
        _grainSprite = Resources.Load<Sprite>("MenuLayers/Menu_Sand_Grain");

        // 1. Cielo pixel art escalonado limpio (sin nubes fijas)
        CreateLayerImage("Diorama_Sky", skySpr, new Vector2(-0.04f, 0.70f), new Vector2(1.04f, 1.02f), false);

        // 2. Sol interactivo en esquina superior izquierda libre del logo
        _sunBaseAnchor = _isCreditsMode ? new Vector2(0.17f, 0.89f) : new Vector2(0.125f, 0.855f);
        float sunSize = _isCreditsMode ? 88f : 74f;
        _sunRt = CreateCenteredProp("Diorama_Sun", sunSpr, _sunBaseAnchor, new Vector2(sunSize, sunSize));

        // 3. Nubes flotantes
        _cloud1Rt = CreateCenteredProp("Diorama_Cloud_1", c1Spr, new Vector2(_cloud1X, 0.915f), new Vector2(128f, 52f));
        _cloud2Rt = CreateCenteredProp("Diorama_Cloud_2", c2Spr, new Vector2(_cloud2X, 0.815f), new Vector2(96f, 40f));

        // 4. Gaviotas aleteando
        _gulls[0] = new GullState
        {
            AnchorPos = new Vector2(0.28f, 0.91f),
            Speed = 0.036f,
            Phase = 0.2f,
            BaseSize = new Vector2(36f, 14f)
        };
        _gulls[0].Rt = CreateCenteredProp("Diorama_Gull_0", _gullUpSprite, _gulls[0].AnchorPos, _gulls[0].BaseSize);
        _gulls[0].Img = _gulls[0].Rt.GetComponent<Image>();

        _gulls[1] = new GullState
        {
            AnchorPos = new Vector2(0.68f, 0.845f),
            Speed = -0.028f,
            Phase = 2.4f,
            BaseSize = new Vector2(30f, 12f)
        };
        _gulls[1].Rt = CreateCenteredProp("Diorama_Gull_1", _gullDownSprite, _gulls[1].AnchorPos, _gulls[1].BaseSize);
        _gulls[1].Img = _gulls[1].Rt.GetComponent<Image>();

        // 5. Playa de arena cálida (debajo del shader del mar para que la espuma y la huella húmeda se dibujen SOBRE la arena!)
        _beachRt = CreateLayerImage("Diorama_Beach_Sand", beachSpr, new Vector2(-0.04f, -0.02f), new Vector2(1.04f, 1.04f), false);

        // 6. Océano interactivo en perspectiva 60 FPS (MenuOceanUI.shader) lavando suavemente sobre la playa
        _oceanRt = CreateLayerImage("Diorama_Ocean", null, new Vector2(-0.04f, 0.495f), new Vector2(1.04f, 0.755f), false);
        Image oceanImg = _oceanRt.GetComponent<Image>();
        Shader oceanShader = Shader.Find("UI/MenuOceanPerspective");
        if (oceanShader != null)
        {
            _oceanMat = new Material(oceanShader);
            oceanImg.material = _oceanMat;
            oceanImg.color = Color.white;
        }
        else
        {
            oceanImg.color = new Color(0.08f, 0.58f, 0.86f, 0.96f);
        }

        // 7. Props temáticos de pantalla
        if (!_isCreditsMode)
        {
            // Castillo centrado en el medio visual de la pantalla (entre el logo SandCastle Maker arriba y JUGAR abajo)
            _castleRt = CreateLayerImage("Diorama_Castle", castleSpr, new Vector2(0.19f, 0.372f), new Vector2(0.81f, 0.685f), true);
            _castleBaseAnchorCenter = new Vector2(0.50f, 0.5285f);

            Sprite flagSpr = Resources.Load<Sprite>("MenuLayers/Menu_Flag");
            Vector2[] flagAnchors = new Vector2[]
            {
                new Vector2(0.528f, 0.935f), // Torre central
                new Vector2(0.360f, 0.692f), // Torreta izq
                new Vector2(0.688f, 0.692f), // Torreta der
                new Vector2(0.212f, 0.432f), // Almena izq
                new Vector2(0.842f, 0.432f)  // Almena der
            };
            float[] flagScales = new float[] { 1.35f, 0.92f, 0.92f, 0.85f, 0.85f };
            _castleFlags = new RectTransform[flagAnchors.Length];
            for (int f = 0; f < flagAnchors.Length; f++)
            {
                GameObject fgo = new GameObject("Castle_Flag_" + f, typeof(RectTransform), typeof(Image));
                fgo.transform.SetParent(_castleRt, false);
                RectTransform frt = fgo.GetComponent<RectTransform>();
                frt.anchorMin = flagAnchors[f];
                frt.anchorMax = flagAnchors[f];
                frt.pivot = new Vector2(0f, 0.45f);
                frt.anchoredPosition = Vector2.zero;
                frt.sizeDelta = new Vector2(22f * flagScales[f], 13f * flagScales[f]);
                Image fImg = fgo.GetComponent<Image>();
                fImg.sprite = flagSpr;
                fImg.preserveAspect = true;
                fImg.raycastTarget = false;
                _castleFlags[f] = frt;
            }
        }
        else
        {
            Sprite nameSpr = Resources.Load<Sprite>("MenuLayers/Credits_Name_BrunoBorgazzi");
            Sprite propsSpr = Resources.Load<Sprite>("MenuLayers/Credits_Props_Bottom");
            _creditsNameRt = CreateLayerImage("Diorama_Credits_Name", nameSpr, new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.49f), true);
            _creditsPropsRt = CreateLayerImage("Diorama_Credits_Props", propsSpr, new Vector2(0.01f, 0.025f), new Vector2(0.99f, 0.135f), true);
        }

        // 8. Palmera interactiva (esquina superior derecha con pivote cerca de la base del tronco)
        _palmRt = CreateLayerImage("Diorama_Palm", palmSpr, new Vector2(0.59f, 0.65f), new Vector2(1.04f, 1.02f), true);
        _palmRt.pivot = new Vector2(0.95f, 0.15f);
        _palmBaseAnchorCenter = new Vector2(0.815f, 0.835f);

        // 9. Contenedor pool de partículas
        GameObject partRoot = new GameObject("Diorama_Particles", typeof(RectTransform));
        partRoot.transform.SetParent(transform, false);
        RectTransform prt = partRoot.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        for (int i = 0; i < 28; i++)
        {
            GameObject pObj = new GameObject("P_" + i, typeof(RectTransform), typeof(Image));
            pObj.transform.SetParent(partRoot.transform, false);
            Image pi = pObj.GetComponent<Image>();
            pi.raycastTarget = false;
            pi.preserveAspect = true;
            pObj.SetActive(false);
            _pool.Add(new UiParticle
            {
                Rt = pObj.GetComponent<RectTransform>(),
                Img = pi
            });
        }
    }

    private RectTransform CreateLayerImage(string goName, Sprite spr, Vector2 aMin, Vector2 aMax, bool preserveAspect)
    {
        GameObject go = new GameObject(goName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = spr;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = false;
        return rt;
    }

    private RectTransform CreateCenteredProp(string goName, Sprite spr, Vector2 anchorCenter, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(goName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorCenter;
        rt.anchorMax = anchorCenter;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;

        Image img = go.GetComponent<Image>();
        img.sprite = spr;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }

    private void Update()
    {
        if (!_initialized) return;

        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        UpdateTiltInput(dt);
        UpdateOceanShader(now);
        UpdateSkyAndGulls(dt, now);
        UpdateParallaxAndBounces(dt, now);
        UpdateParticles(dt);
    }

    private void UpdateTiltInput(float dt)
    {
        float rawTilt = 0f;
        if (SystemInfo.supportsAccelerometer && Application.isMobilePlatform)
        {
            rawTilt = Mathf.Clamp(Input.acceleration.x * 1.65f, -1f, 1f);
        }
        else
        {
            float keyAxis = Input.GetAxisRaw("Horizontal");
            float mouseNorm = Screen.width > 10 ? Mathf.Clamp((Input.mousePosition.x / Screen.width - 0.5f) * 0.70f, -0.55f, 0.55f) : 0f;
            rawTilt = Mathf.Abs(keyAxis) > 0.05f ? keyAxis : mouseNorm;
        }
        _tiltX = Mathf.Lerp(_tiltX, rawTilt, Mathf.Clamp01(dt * 4.5f));
    }

    private void UpdateOceanShader(float now)
    {
        if (_oceanMat == null) return;
        _oceanMat.SetFloat("_ShaderTime", now);
        _oceanMat.SetFloat("_TiltX", _tiltX);
        _oceanMat.SetVector("_Ripple0", _ripples[0]);
        _oceanMat.SetVector("_Ripple1", _ripples[1]);
        _oceanMat.SetVector("_Ripple2", _ripples[2]);
        _oceanMat.SetVector("_Ripple3", _ripples[3]);
    }

    private void UpdateSkyAndGulls(float dt, float now)
    {
        _cloud1X += dt * 0.010f;
        if (_cloud1X > 1.22f) _cloud1X = -0.22f;
        if (_cloud1Rt != null)
        {
            float px = _cloud1X - _tiltX * 0.014f;
            _cloud1Rt.anchorMin = new Vector2(px, 0.915f);
            _cloud1Rt.anchorMax = new Vector2(px, 0.915f);
        }

        _cloud2X += dt * 0.0065f;
        if (_cloud2X > 1.22f) _cloud2X = -0.22f;
        if (_cloud2Rt != null)
        {
            float px = _cloud2X - _tiltX * 0.009f;
            _cloud2Rt.anchorMin = new Vector2(px, 0.815f);
            _cloud2Rt.anchorMax = new Vector2(px, 0.815f);
        }

        for (int i = 0; i < _gulls.Length; i++)
        {
            if (_gulls[i].Rt == null) continue;
            bool boosted = _gulls[i].BoostTimer > 0f;
            if (boosted) _gulls[i].BoostTimer = Mathf.Max(0f, _gulls[i].BoostTimer - dt);

            float mul = boosted ? 3.2f : 1.0f;
            _gulls[i].AnchorPos.x += _gulls[i].Speed * mul * dt;
            if (_gulls[i].AnchorPos.x > 1.15f) _gulls[i].AnchorPos.x = -0.15f;
            if (_gulls[i].AnchorPos.x < -0.15f) _gulls[i].AnchorPos.x = 1.15f;

            float bob = Mathf.Sin(now * 2.2f + _gulls[i].Phase) * 0.012f;
            Vector2 ap = new Vector2(_gulls[i].AnchorPos.x - _tiltX * 0.012f, _gulls[i].AnchorPos.y + bob);
            _gulls[i].Rt.anchorMin = ap;
            _gulls[i].Rt.anchorMax = ap;

            float flapFreq = boosted ? 12f : 4.2f;
            bool wingUp = Mathf.Sin(now * flapFreq + _gulls[i].Phase) >= 0f;
            if (_gulls[i].Img != null && _gullUpSprite != null && _gullDownSprite != null)
            {
                _gulls[i].Img.sprite = wingUp ? _gullUpSprite : _gullDownSprite;
            }
            float flipX = _gulls[i].Speed >= 0f ? 1f : -1f;
            float bScale = boosted ? 1.25f : 1f;
            _gulls[i].Rt.localScale = new Vector3(flipX * bScale, bScale, 1f);
        }
    }

    private void UpdateParallaxAndBounces(float dt, float now)
    {
        if (_sunRt != null)
        {
            Vector2 ap = _sunBaseAnchor + new Vector2(-_tiltX * 0.010f, 0f);
            _sunRt.anchorMin = ap;
            _sunRt.anchorMax = ap;
            float rot = Mathf.Sin(now * 0.9f) * 5f;
            float s = 1f + Mathf.Sin(now * 1.8f) * 0.025f;
            if (_sunBounceT > 0f)
            {
                _sunBounceT = Mathf.Max(0f, _sunBounceT - dt);
                float k = _sunBounceT / 0.45f;
                s += Mathf.Sin(k * Mathf.PI * 3f) * 0.24f * k;
                rot += k * 25f;
            }
            _sunRt.localRotation = Quaternion.Euler(0f, 0f, rot);
            _sunRt.localScale = new Vector3(s, s, 1f);
        }

        if (_oceanRt != null)
        {
            float shift = _tiltX * 0.012f;
            _oceanRt.anchorMin = new Vector2(-0.04f + shift, 0.495f);
            _oceanRt.anchorMax = new Vector2(1.04f + shift, 0.755f);
        }

        if (_beachRt != null)
        {
            float shift = _tiltX * 0.018f;
            _beachRt.anchorMin = new Vector2(-0.04f + shift, -0.02f);
            _beachRt.anchorMax = new Vector2(1.04f + shift, 1.04f);
        }

        if (_castleRt != null)
        {
            float shift = _tiltX * 0.022f;
            _castleRt.anchorMin = new Vector2(0.19f + shift, 0.372f);
            _castleRt.anchorMax = new Vector2(0.81f + shift, 0.685f);
            Vector3 sc = Vector3.one;
            if (_castleBounceT > 0f)
            {
                _castleBounceT = Mathf.Max(0f, _castleBounceT - dt);
                float u = _castleBounceT / 0.38f;
                float osc = Mathf.Sin(u * Mathf.PI * 3f) * u * 0.12f;
                sc = new Vector3(1f + osc, 1f - osc * 0.85f, 1f);
            }
            _castleRt.localScale = sc;

            if (_castleFlags != null)
            {
                float flutterFreq = _castleBounceT > 0f ? 14f : 4.8f;
                float amp = _castleBounceT > 0f ? 12f : 5.5f;
                for (int f = 0; f < _castleFlags.Length; f++)
                {
                    if (_castleFlags[f] == null) continue;
                    float ph = now * flutterFreq + f * 1.15f;
                    float sx = 1f + Mathf.Sin(ph) * 0.12f;
                    float rotZ = Mathf.Cos(ph * 0.85f) * amp + _tiltX * 3.5f;
                    _castleFlags[f].localScale = new Vector3(sx, 1f, 1f);
                    _castleFlags[f].localRotation = Quaternion.Euler(0f, 0f, rotZ);
                }
            }
        }

        if (_creditsNameRt != null)
        {
            float shift = _tiltX * 0.022f;
            _creditsNameRt.anchorMin = new Vector2(0.06f + shift, 0.26f);
            _creditsNameRt.anchorMax = new Vector2(0.94f + shift, 0.49f);
            float sc = 1f;
            if (_nameBounceT > 0f)
            {
                _nameBounceT = Mathf.Max(0f, _nameBounceT - dt);
                float u = _nameBounceT / 0.36f;
                sc += Mathf.Sin(u * Mathf.PI * 2.5f) * u * 0.14f;
            }
            _creditsNameRt.localScale = new Vector3(sc, sc, 1f);
        }

        if (_creditsPropsRt != null)
        {
            float shift = _tiltX * 0.026f;
            _creditsPropsRt.anchorMin = new Vector2(0.01f + shift, 0.025f);
            _creditsPropsRt.anchorMax = new Vector2(0.99f + shift, 0.135f);
            float sc = 1f;
            if (_propsBounceT > 0f)
            {
                _propsBounceT = Mathf.Max(0f, _propsBounceT - dt);
                float u = _propsBounceT / 0.30f;
                sc += Mathf.Sin(u * Mathf.PI * 2f) * u * 0.10f;
            }
            _creditsPropsRt.localScale = new Vector3(sc, sc, 1f);
        }

        if (_palmRt != null)
        {
            float shift = _tiltX * 0.028f;
            _palmRt.anchorMin = new Vector2(0.59f + shift, 0.65f);
            _palmRt.anchorMax = new Vector2(1.04f + shift, 1.02f);

            float breeze = Mathf.Sin(now * 1.35f) * 2.2f + _tiltX * 2.5f;
            if (_palmBounceT > 0f)
            {
                _palmBounceT = Mathf.Max(0f, _palmBounceT - dt);
                float u = _palmBounceT / 0.55f;
                breeze += Mathf.Sin(u * Mathf.PI * 4.5f) * u * 11f;
            }
            _palmRt.localRotation = Quaternion.Euler(0f, 0f, breeze);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        HandleTouchAtScreenPos(eventData.position, isDrag: false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        HandleTouchAtScreenPos(eventData.position, isDrag: true);
    }

    private void HandleTouchAtScreenPos(Vector2 screenPos, bool isDrag)
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

        if (!isDrag)
        {
            // 1. Sol
            if (Vector2.Distance(new Vector2(u, v), _sunBaseAnchor) < 0.12f)
            {
                _sunBounceT = 0.45f;
                SpawnBurst(_sunBaseAnchor, 8, _grainSprite, new Color(1f, 0.92f, 0.45f, 1f), 90f, 260f, 10f, 0.55f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySunSparkleSound();
                return;
            }

            // 2. Gaviotas
            for (int i = 0; i < _gulls.Length; i++)
            {
                if (Vector2.Distance(new Vector2(u, v), _gulls[i].AnchorPos) < 0.08f)
                {
                    _gulls[i].BoostTimer = 0.85f;
                    _gulls[i].Speed = -_gulls[i].Speed;
                    SpawnBurst(_gulls[i].AnchorPos, 5, _grainSprite, new Color(1f, 0.96f, 0.84f, 1f), 70f, 120f, 8f, 0.4f);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayGullChirpSound();
                    return;
                }
            }

            // 3. Palmera (esquina superior derecha)
            if (u > 0.64f && v > 0.68f)
            {
                _palmBounceT = 0.55f;
                SpawnFallingLeafOrCoco(new Vector2(0.80f, 0.83f), _leafSprite, new Vector2(-65f, -30f), new Vector2(28f, 14f));
                SpawnFallingLeafOrCoco(new Vector2(0.88f, 0.85f), _leafSprite, new Vector2(35f, -20f), new Vector2(28f, 14f));
                SpawnFallingLeafOrCoco(new Vector2(0.84f, 0.80f), _coconutSprite, new Vector2(-22f, -45f), new Vector2(20f, 18f));
                if (AudioManager.Instance != null) AudioManager.Instance.PlayPalmCocoSound();
                return;
            }

            // 4. Castillo (Menú) o Nombre/Balde (Créditos)
            if (!_isCreditsMode && u >= 0.21f && u <= 0.79f && v >= 0.37f && v <= 0.69f)
            {
                _castleBounceT = 0.38f;
                SpawnBurst(new Vector2(u, v), 10, _grainSprite, new Color(0.95f, 0.82f, 0.48f, 1f), 110f, 340f, 8f, 0.50f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayCastlePatSound();
                return;
            }
            if (_isCreditsMode && u >= 0.10f && u <= 0.90f && v >= 0.26f && v <= 0.49f)
            {
                _nameBounceT = 0.36f;
                SpawnBurst(new Vector2(u, v), 10, _grainSprite, new Color(1f, 0.86f, 0.38f, 1f), 110f, 220f, 10f, 0.55f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySunSparkleSound();
                return;
            }
            if (_isCreditsMode && v < 0.14f && (u < 0.30f || u > 0.70f))
            {
                _propsBounceT = 0.30f;
                SpawnBurst(new Vector2(u, v), 8, _grainSprite, new Color(1f, 0.90f, 0.55f, 1f), 90f, 260f, 8f, 0.48f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayCastlePatSound();
                return;
            }
        }

        // 5. Agua en perspectiva (tocar o deslizar el dedo genera ondas espumosas!)
        if (v >= 0.52f && v <= 0.755f)
        {
            if (isDrag && now - _lastDragRippleTime < 0.16f) return;
            _lastDragRippleTime = now;
            float oceanU = Mathf.Clamp01((u + 0.04f) / 1.08f);
            float oceanV = Mathf.Clamp01((v - 0.495f) / (0.755f - 0.495f));
            _ripples[_nextRippleIdx] = new Vector4(oceanU, oceanV, now, isDrag ? 0.75f : 1.0f);
            _nextRippleIdx = (_nextRippleIdx + 1) & 3;
            SpawnBurst(new Vector2(u, v), isDrag ? 2 : 5, _grainSprite, new Color(1f, 0.96f, 0.84f, 0.9f), 65f, 140f, 7f, 0.36f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWaterSplashSound(isDrag);
            return;
        }

        // 6. Arena abierta en playa -> partículas pixel art de arena + Sand Position.mp3
        if (v < 0.52f)
        {
            if (isDrag && now - _lastDragRippleTime < 0.07f) return;
            _lastDragRippleTime = now;
            Color sandGrainColor = Random.value > 0.5f
                ? new Color(0.98f, 0.86f, 0.56f, 1f)
                : new Color(0.84f, 0.65f, 0.35f, 1f);
            SpawnBurst(new Vector2(u, v), isDrag ? 3 : 8, _grainSprite, sandGrainColor, 95f, 340f, 7f, 0.45f);
            PlaySandTouchAudio(now);
        }
    }

    private void PlaySandTouchAudio(float now)
    {
        if (now - _lastSandAudioTime < 0.11f) return;
        _lastSandAudioTime = now;
        if (AudioManager.Instance != null)
        {
            if (AudioManager.Instance.sfxSource != null)
            {
                AudioManager.Instance.sfxSource.pitch = Random.Range(0.94f, 1.08f);
            }
            AudioManager.Instance.PlaySpawnSandSound();
        }
    }

    private void SpawnBurst(Vector2 anchorPos, int count, Sprite spr, Color col, float speed, float grav, float pxSize, float maxLife)
    {
        if (spr == null) return;
        for (int i = 0; i < count; i++)
        {
            UiParticle p = AcquireParticle();
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
            p.RotSpeed = Random.Range(-180f, 180f);
            p.MaxLife = maxLife;
            p.Life = maxLife;
            p.Rt.gameObject.SetActive(true);
        }
    }

    private void SpawnFallingLeafOrCoco(Vector2 anchorPos, Sprite spr, Vector2 initVel, Vector2 sizeDelta)
    {
        if (spr == null) return;
        UiParticle p = AcquireParticle();
        if (p == null) return;
        p.Img.sprite = spr;
        p.Img.color = Color.white;
        p.BaseColor = Color.white;
        p.Rt.anchorMin = anchorPos;
        p.Rt.anchorMax = anchorPos;
        p.Rt.anchoredPosition = Vector2.zero;
        p.Rt.sizeDelta = sizeDelta;
        p.Rt.localRotation = Quaternion.identity;
        p.Vel = initVel;
        p.Gravity = spr == _coconutSprite ? 420f : 140f;
        p.RotSpeed = spr == _coconutSprite ? 90f : 210f;
        p.MaxLife = 1.45f;
        p.Life = 1.45f;
        p.Rt.gameObject.SetActive(true);
    }

    private UiParticle AcquireParticle()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i].Life <= 0f) return _pool[i];
        }
        return null;
    }

    private void UpdateParticles(float dt)
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            UiParticle p = _pool[i];
            if (p.Life <= 0f || p.Rt == null) continue;
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                p.Rt.gameObject.SetActive(false);
                continue;
            }
            p.Vel.y -= p.Gravity * dt;
            p.Rt.anchoredPosition += p.Vel * dt;
            p.Rt.Rotate(0f, 0f, p.RotSpeed * dt);
            float alpha = Mathf.Clamp01(p.Life / Mathf.Max(0.01f, p.MaxLife));
            Color c = p.BaseColor;
            c.a *= alpha;
            p.Img.color = c;
        }
    }
}
