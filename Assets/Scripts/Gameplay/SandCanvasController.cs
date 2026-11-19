using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controla el lienzo interactivo de arena donde el jugador dibuja/esculpe a mano alzada con el dedo
/// guiándose por el Plano Objetivo, mide la exactitud de la forma (cobertura menos exceso fuera del molde)
/// y procesa la erosión en tiempo real cuando las olas tocan el castillo.
/// </summary>
public class SandCanvasController : MonoBehaviour
{
    public static SandCanvasController Instance { get; private set; }

    public const int MAX_GRID_DIMENSION = 224;
    private int _gridWidth = 192;
    private int _gridHeight = 192;

    [Header("🖌️ TAMAÑO DEL PINCEL DEL DEDO (AJUSTAR AQUÍ)")]
    [Tooltip("Radio del trazo al dibujar con el dedo en la arena (0.06 = fino, 0.14 = ideal, 0.30 = grueso)")]
    [Range(0.04f, 0.45f)]
    public float brushRadiusWorld = 0.14f;

    [Tooltip("Si está activo, prioriza el tamaño de pincel de este Inspector por encima del LevelData")]
    public bool useInspectorBrushSize = true;

    [Tooltip("Multiplicador de ancho de pincel para Arena Seca (más ancha para armar rápido la base)")]
    [Range(0.75f, 1.8f)]
    public float drySandBrushMultiplier = 1.32f;

    [Tooltip("Multiplicador de ancho de pincel para Arena Húmeda (25% más fina que base para sellar bordes al agua)")]
    [Range(0.55f, 1.3f)]
    public float wetSandBrushMultiplier = 0.75f;

    [Tooltip("Visibilidad de la guía punteada cálida clásica en mapas de arena clara (0.07..0.12 = sutil)")]
    [Range(0f, 0.4f)]
    public float faintGuideVisibility = 0.08f;

    [Tooltip("Visibilidad de la guía de piedritas oscuras en mapas difíciles de tierra oscura (noche/tormenta)")]
    [Range(0f, 1f)]
    public float darkPebbleGuideVisibility = 0.85f;

    [Tooltip("Umbral de luminancia de la arena del fondo por debajo del cual se activa automáticamente la guía de piedritas oscuras")]
    [Range(0.25f, 0.8f)]
    public float darkGroundLuminanceThreshold = 0.65f;

    [Header("Configuración del Lienzo")]
    [Tooltip("Material que utiliza el shader SandCastle/SandSculpt")]
    public Material sandSculptMaterial;

    [Tooltip("Margen extra alrededor del castillo para permitir esculpir y ver las olas en la playa")]
    public Vector2 canvasPaddingMultiplier = new Vector2(1.45f, 1.35f);

    [Header("Parámetros de Exactitud y Erosión")]
    [Tooltip("Cantidad de texels nuevos reforzados con arena húmeda que equivalen a 1 unidad del balde")]
    public int texelsPerWetSandUnit = 14;

    [Tooltip("Penalización de exactitud por pintar muy afuera de la silueta del castillo (0 = sin penalización, 0.6 = estricto)")]
    [Range(0f, 1f)]
    public float outsideDrawingPenaltyWeight = 0.45f;

    [Tooltip("Velocidad a la que una ola borra la Arena Seca desprotegida (0..255 por segundo)")]
    public float drySandWaveErosionSpeed = 1900f;

    [Tooltip("Velocidad a la que una ola desgasta el escudo de Arena Húmeda (0..255 por segundo, ~58 = ~3.8s de aguante)")]
    public float wetSandWaveErosionSpeed = 58f;

    [Tooltip("Espesor del frente de choque que bloquea la ola antes de saturarse (en texels)")]
    [Range(1.5f, 4.5f)]
    public float wetContactThicknessTexels = 2.6f;

    [Tooltip("Columnas vecinas con curvatura suave cuando el agua rodea un dique de arena mojada")]
    [Range(1, 6)]
    public int waterPoolingLateralSpreadCols = 4;

    [Tooltip("Velocidad a la que se seca la huella húmeda que deja la ola en la playa al retroceder (~34/s = ~7s independiente de FPS)")]
    public float beachWetTrailDecaySpeed = 34f;

    // Estado de la textura y grilla en CPU (0 GC Alloc en Update)
    private Texture2D _sculptMaskTex;
    private Color32[] _pixels;
    private bool[] _isGhostTexel;
    private bool[] _isToleranceTexel;
    private int _totalGhostTexels;
    private int _filledGhostTexels;
    private int _outsideOverdrawTexels;
    private bool _textureDirty;

    // Mapa 1D de barrera de arena húmeda frente a olas (sin allocations por frame)
    private const byte WET_CRUMBLE_THRESHOLD = 32;
    private Texture2D _waveBarrierTex;
    private Color32[] _barrierPixels;
    private float[] _columnIncomingShoreY;
    private float[] _columnRawWetTopY;
    private bool[] _columnBlockedByWet;
    private float[] _columnVisualStopY;
    private float[] _columnImpact01;
    private float _unitPerTexel;

    // Geometría en espacio de mundo
    private Bounds _canvasWorldBounds;
    private Bounds _castleWorldBounds;
    private Vector4 _castleUVRect;
    private SpriteRenderer _canvasRenderer;
    private Material _runtimeMaterial;
    private Camera _mainCamera;
    private Sprite _targetCastleSprite;

    // Estado del trazo del dedo
    private bool _wasDragging;
    private Vector2 _lastFingerWorldPos;
    private int _wetTexelAccumulator;
    private float _vfxCooldownTimer;
    private float _erosionVfxCooldownTimer;
    private float _wetErosionVfxCooldownTimer;
    private float _wetTrailDecayAccum;
    private float _bounceVfxCooldownTimer;
    private float _crumbleVfxCooldownTimer;
    private bool _useDarkPebbleGuide;
    private bool _isInitialized;

    public Bounds CanvasWorldBounds => _canvasWorldBounds;
    public Bounds CastleWorldBounds => _castleWorldBounds;
    public Sprite TargetCastleSprite => _targetCastleSprite;
    public bool IsInitialized => _isInitialized;
    public float CurrentOverdrawRatio => _totalGhostTexels <= 0 ? 0f : (float)_outsideOverdrawTexels / _totalGhostTexels;

    /// <summary>
    /// Exactitud actual (0.0 a 1.0) del dibujo del jugador comparada con el castillo objetivo.
    /// Combina el porcentaje cubierto de la silueta menos la penalización por salirse mucho de la forma.
    /// </summary>
    public float CurrentFillPercentage
    {
        get
        {
            if (_totalGhostTexels <= 0) return 0f;
            float coverage = (float)_filledGhostTexels / _totalGhostTexels;
            float overdrawRatio = (float)_outsideOverdrawTexels / _totalGhostTexels;
            float accuracy = coverage - (overdrawRatio * outsideDrawingPenaltyWeight);
            return Mathf.Clamp01(accuracy);
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    /// <summary>
    /// Inicializa el lienzo tomando el Sprite, posición y escala del prefab objetivo del nivel.
    /// </summary>
    public void InitializeFromTargetShape(GameObject targetShapeInstance, LevelData levelData)
    {
        _mainCamera = Camera.main;
        if (!useInspectorBrushSize && levelData != null && levelData.brushRadiusWorld > 0.03f)
        {
            brushRadiusWorld = levelData.brushRadiusWorld;
        }

        SpriteRenderer targetSR = targetShapeInstance != null
            ? targetShapeInstance.GetComponent<SpriteRenderer>()
            : null;

        if (targetSR == null || targetSR.sprite == null)
        {
            Debug.LogError("[SandCanvasController] El targetShapePrefab no contiene un SpriteRenderer con sprite válido.");
            return;
        }

        _targetCastleSprite = targetSR.sprite;

        // Desactivar el PolygonCollider2D y ocultar el SpriteRenderer gris viejo del MVP
        PolygonCollider2D poly = targetShapeInstance.GetComponent<PolygonCollider2D>();
        if (poly != null) poly.enabled = false;
        targetSR.enabled = false;

        // 1. Extraer el rectángulo opaco real del castillo (sin márgenes transparentes inflados)
        ExtractTightCastleSpriteData(
            targetSR,
            out Bounds tightCastleBounds,
            out Color32[] tightPixels,
            out int tightWidth,
            out int tightHeight,
            out RectInt opaqueBox
        );
        _castleWorldBounds = tightCastleBounds;

        // 2. Ajustar cámara responsive (escalado vertical + fit horizontal) antes de dimensionar lienzo/marea
        if (_mainCamera != null)
        {
            ResponsiveViewportCamera viewportCam = ResponsiveViewportCamera.EnsureOnCamera(_mainCamera);
            if (viewportCam != null)
            {
                viewportCam.ConfigureTargetBounds(_castleWorldBounds);
            }
        }

        // 3. Dimensionar el lienzo de playa cubriendo toda la zona visible sin estirar texels (grilla 1:1)
        float camHeight = _mainCamera != null ? _mainCamera.orthographicSize * 2f : 14f;
        float camWidth = _mainCamera != null ? camHeight * _mainCamera.aspect : 8f;
        Vector3 camPos = _mainCamera != null ? _mainCamera.transform.position : _castleWorldBounds.center;

        float rawCanvasW = Mathf.Max(_castleWorldBounds.size.x * Mathf.Max(canvasPaddingMultiplier.x, 1.55f), camWidth * 1.45f, camHeight * 0.90f);
        float rawCanvasH = Mathf.Max(_castleWorldBounds.size.y * canvasPaddingMultiplier.y, _castleWorldBounds.size.y + 4.4f, camHeight * 0.74f);

        float maxWorldSpan = Mathf.Max(rawCanvasW, rawCanvasH);
        float unitPerTexel = maxWorldSpan / MAX_GRID_DIMENSION;
        _gridWidth = Mathf.Clamp(Mathf.CeilToInt(rawCanvasW / unitPerTexel), 96, 256);
        _gridHeight = Mathf.Clamp(Mathf.CeilToInt(rawCanvasH / unitPerTexel), 96, 256);

        float canvasWidth = _gridWidth * unitPerTexel;
        float canvasHeight = _gridHeight * unitPerTexel;

        float canvasBottomY = Mathf.Min(_castleWorldBounds.min.y - 2.3f, camPos.y - camHeight * 0.52f);
        Vector3 canvasCenter = new Vector3(
            _castleWorldBounds.center.x,
            canvasBottomY + canvasHeight * 0.5f,
            _castleWorldBounds.center.z
        );

        _canvasWorldBounds = new Bounds(canvasCenter, new Vector3(canvasWidth, canvasHeight, 0.1f));

        // Calcular en qué sub-rectángulo UV [0..1] del lienzo cae exactamente la silueta real 1:1 del castillo
        float minU = (_castleWorldBounds.min.x - _canvasWorldBounds.min.x) / _canvasWorldBounds.size.x;
        float minV = (_castleWorldBounds.min.y - _canvasWorldBounds.min.y) / _canvasWorldBounds.size.y;
        float sizeU = _castleWorldBounds.size.x / _canvasWorldBounds.size.x;
        float sizeV = _castleWorldBounds.size.y / _canvasWorldBounds.size.y;
        _castleUVRect = new Vector4(minU, minV, sizeU, sizeV);

        _canvasRenderer = GetComponent<SpriteRenderer>();
        if (_canvasRenderer == null)
        {
            _canvasRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        transform.position = canvasCenter;
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(canvasWidth, canvasHeight, 1f);

        _canvasRenderer.sprite = CreateUnitWhiteSprite();
        _canvasRenderer.sortingOrder = 2;

        // Preparar textura de estado Pixel Art con texels cuadrados 1:1
        _sculptMaskTex = new Texture2D(_gridWidth, _gridHeight, TextureFormat.RGBA32, false);
        _sculptMaskTex.filterMode = FilterMode.Point;
        _sculptMaskTex.wrapMode = TextureWrapMode.Clamp;

        int totalCells = _gridWidth * _gridHeight;
        _pixels = new Color32[totalCells];
        _isGhostTexel = new bool[totalCells];
        _isToleranceTexel = new bool[totalCells];

        _unitPerTexel = unitPerTexel;
        _waveBarrierTex = new Texture2D(_gridWidth, 1, TextureFormat.RGBA32, false);
        _waveBarrierTex.filterMode = FilterMode.Point;
        _waveBarrierTex.wrapMode = TextureWrapMode.Clamp;
        _barrierPixels = new Color32[_gridWidth];
        _columnIncomingShoreY = new float[_gridWidth];
        _columnRawWetTopY = new float[_gridWidth];
        _columnBlockedByWet = new bool[_gridWidth];
        _columnVisualStopY = new float[_gridWidth];
        _columnImpact01 = new float[_gridWidth];

        BakeGhostMaskFromData(tightPixels, tightWidth, tightHeight, opaqueBox, unitPerTexel);

        _sculptMaskTex.SetPixels32(_pixels);
        _sculptMaskTex.Apply(false);

        Shader sculptShader = Shader.Find("SandCastle/SandSculpt");
        if (sandSculptMaterial != null)
        {
            _runtimeMaterial = new Material(sandSculptMaterial);
        }
        else if (sculptShader != null)
        {
            _runtimeMaterial = new Material(sculptShader);
        }
        else
        {
            _runtimeMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        _runtimeMaterial.SetTexture("_CastleTex", _targetCastleSprite.texture);
        _runtimeMaterial.SetTexture("_SculptMaskTex", _sculptMaskTex);
        _runtimeMaterial.SetVector("_CastleUVRect", _castleUVRect);
        _runtimeMaterial.SetFloat("_PixelGrid", Mathf.Max(_gridWidth, _gridHeight));
        _runtimeMaterial.SetVector("_PixelGridXY", new Vector4(_gridWidth, _gridHeight, 1f / _gridWidth, 1f / _gridHeight));

        // Detectar automáticamente si el mapa tiene tierra/arena oscura (noche, tormenta, etc.)
        // Mapas oscuros usan piedritas/tierra oscura; mapas claros conservan la guía punteada cálida clásica.
        _useDarkPebbleGuide = DetectDarkGroundBeach(levelData, out float measuredSandLuminance);
        ApplyGuideStyleToMaterial();

        _canvasRenderer.material = _runtimeMaterial;
        _isInitialized = true;

        Debug.Log($"[SandCanvasController] Lienzo 1:1 listo ({_gridWidth}x{_gridHeight}, texel={unitPerTexel:F3}u). Castillo={_castleWorldBounds.size.x:F2}x{_castleWorldBounds.size.y:F2}u. Texels Objetivo={_totalGhostTexels}. Guía={(_useDarkPebbleGuide ? "PiedritasOscuras" : "ClasicaArenaClara")} (Lum={measuredSandLuminance:F2})");
    }

    private void ApplyGuideStyleToMaterial()
    {
        if (_runtimeMaterial == null) return;

        if (_useDarkPebbleGuide)
        {
            _runtimeMaterial.SetFloat("_UsePebbleGuide", 1f);
            _runtimeMaterial.SetFloat("_ShowFaintGuide", darkPebbleGuideVisibility);
            _runtimeMaterial.SetColor("_BuildZoneGuideColor", new Color(0.17f, 0.12f, 0.08f, 0.88f));
        }
        else
        {
            _runtimeMaterial.SetFloat("_UsePebbleGuide", 0f);
            _runtimeMaterial.SetFloat("_ShowFaintGuide", Mathf.Clamp(faintGuideVisibility, 0.05f, 0.35f));
            _runtimeMaterial.SetColor("_BuildZoneGuideColor", new Color(0.52f, 0.36f, 0.18f, 0.45f));
        }
    }

    /// <summary>
    /// Mide la luminancia real de la zona de arena del fondo del nivel (sin exigir Read/Write en la textura).
    /// Devuelve true en mapas con suelo oscuro (ej. Noche / Tormenta) y false en playas soleadas / atardecer.
    /// </summary>
    private bool DetectDarkGroundBeach(LevelData levelData, out float averageSandLuminance)
    {
        averageSandLuminance = 0.8f;

        if (levelData != null && levelData.backgroundImage != null && levelData.backgroundImage.texture != null)
        {
            Sprite bgSprite = levelData.backgroundImage;
            Texture2D srcTex = bgSprite.texture;
            Rect texRect = bgSprite.textureRect;

            const int sampleSize = 16;
            RenderTexture sampleRT = RenderTexture.GetTemporary(sampleSize, sampleSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prevRT = RenderTexture.active;

            Vector2 uvScale = new Vector2(texRect.width / Mathf.Max(1, srcTex.width), texRect.height / Mathf.Max(1, srcTex.height));
            Vector2 uvOffset = new Vector2(texRect.x / Mathf.Max(1, srcTex.width), texRect.y / Mathf.Max(1, srcTex.height));
            Graphics.Blit(srcTex, sampleRT, uvScale, uvOffset);
            RenderTexture.active = sampleRT;

            Texture2D sampleTex = new Texture2D(sampleSize, sampleSize, TextureFormat.RGBA32, false);
            sampleTex.ReadPixels(new Rect(0, 0, sampleSize, sampleSize), 0, 0);
            sampleTex.Apply(false);

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(sampleRT);

            Color32[] samplePixels = sampleTex.GetPixels32();
            Destroy(sampleTex);

            // Zona inferior-central de la playa donde se dibuja el castillo (en coordenadas Unity Y=0 abajo -> filas 2..7)
            float sumLum = 0f;
            int count = 0;
            for (int y = 2; y <= 7; y++)
            {
                int row = y * sampleSize;
                for (int x = 3; x <= 12; x++)
                {
                    Color32 px = samplePixels[row + x];
                    sumLum += (0.299f * px.r + 0.587f * px.g + 0.114f * px.b) / 255f;
                    count++;
                }
            }

            if (count > 0)
            {
                averageSandLuminance = sumLum / count;
                return averageSandLuminance < darkGroundLuminanceThreshold;
            }
        }

        // Respaldo si el nivel no tuviera backgroundImage asignado: niveles 3 y 4 de cada mundo (Noche/Tormenta)
        if (levelData != null)
        {
            int levelInWorld = levelData.levelID % 100;
            bool fallbackDark = levelInWorld >= 3;
            averageSandLuminance = fallbackDark ? 0.45f : 0.85f;
            return fallbackDark;
        }

        return false;
    }

    private void ExtractTightCastleSpriteData(
        SpriteRenderer targetSR,
        out Bounds tightWorldBounds,
        out Color32[] croppedPixels,
        out int croppedWidth,
        out int croppedHeight,
        out RectInt opaqueBox)
    {
        Sprite sprite = targetSR.sprite;
        Texture2D srcTex = sprite.texture;
        Rect texRect = sprite.textureRect;

        croppedWidth = Mathf.Max(1, Mathf.RoundToInt(texRect.width));
        croppedHeight = Mathf.Max(1, Mathf.RoundToInt(texRect.height));

        RenderTexture tightRT = RenderTexture.GetTemporary(croppedWidth, croppedHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture prevRT = RenderTexture.active;

        Vector2 uvScale = new Vector2(texRect.width / srcTex.width, texRect.height / srcTex.height);
        Vector2 uvOffset = new Vector2(texRect.x / srcTex.width, texRect.y / srcTex.height);
        Graphics.Blit(srcTex, tightRT, uvScale, uvOffset);
        RenderTexture.active = tightRT;

        Texture2D readableCopy = new Texture2D(croppedWidth, croppedHeight, TextureFormat.RGBA32, false);
        readableCopy.ReadPixels(new Rect(0, 0, croppedWidth, croppedHeight), 0, 0);
        readableCopy.Apply();

        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(tightRT);

        croppedPixels = readableCopy.GetPixels32();
        Destroy(readableCopy);

        int minPx = croppedWidth;
        int maxPx = -1;
        int minPy = croppedHeight;
        int maxPy = -1;

        for (int y = 0; y < croppedHeight; y++)
        {
            int rowOffset = y * croppedWidth;
            for (int x = 0; x < croppedWidth; x++)
            {
                if (croppedPixels[rowOffset + x].a > 32)
                {
                    if (x < minPx) minPx = x;
                    if (x > maxPx) maxPx = x;
                    if (y < minPy) minPy = y;
                    if (y > maxPy) maxPy = y;
                }
            }
        }

        if (maxPx < minPx || maxPy < minPy)
        {
            opaqueBox = new RectInt(0, 0, croppedWidth, croppedHeight);
            tightWorldBounds = targetSR.bounds;
            return;
        }

        opaqueBox = new RectInt(minPx, minPy, maxPx - minPx + 1, maxPy - minPy + 1);

        float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
        Vector2 offsetInSprite = sprite.textureRectOffset;
        Vector2 pivotPx = sprite.pivot;
        Vector3 lossyScale = targetSR.transform.lossyScale;
        Vector3 worldPos = targetSR.transform.position;
        float sx = Mathf.Abs(lossyScale.x);
        float sy = Mathf.Abs(lossyScale.y);

        float leftPx = offsetInSprite.x + opaqueBox.xMin;
        float rightPx = offsetInSprite.x + opaqueBox.xMax;
        float bottomPx = offsetInSprite.y + opaqueBox.yMin;
        float topPx = offsetInSprite.y + opaqueBox.yMax;

        float wMinX = worldPos.x + ((leftPx - pivotPx.x) / ppu) * sx;
        float wMaxX = worldPos.x + ((rightPx - pivotPx.x) / ppu) * sx;
        float wMinY = worldPos.y + ((bottomPx - pivotPx.y) / ppu) * sy;
        float wMaxY = worldPos.y + ((topPx - pivotPx.y) / ppu) * sy;

        Vector3 center = new Vector3((wMinX + wMaxX) * 0.5f, (wMinY + wMaxY) * 0.5f, targetSR.bounds.center.z);
        Vector3 size = new Vector3(Mathf.Max(wMaxX - wMinX, 0.25f), Mathf.Max(wMaxY - wMinY, 0.25f), 0.1f);
        tightWorldBounds = new Bounds(center, size);
    }

    private void BakeGhostMaskFromData(
        Color32[] croppedPixels,
        int croppedWidth,
        int croppedHeight,
        RectInt opaqueBox,
        float unitPerTexel)
    {
        _totalGhostTexels = 0;
        _filledGhostTexels = 0;
        _outsideOverdrawTexels = 0;

        float halfTexelU = 0.5f / _gridWidth;
        float halfTexelV = 0.5f / _gridHeight;

        for (int y = 0; y < _gridHeight; y++)
        {
            float canvasV = (y + 0.5f) / _gridHeight;
            float relV0 = (canvasV - halfTexelV - _castleUVRect.y) / Mathf.Max(_castleUVRect.w, 0.0001f);
            float relV1 = (canvasV + halfTexelV - _castleUVRect.y) / Mathf.Max(_castleUVRect.w, 0.0001f);

            for (int x = 0; x < _gridWidth; x++)
            {
                int idx = y * _gridWidth + x;
                float canvasU = (x + 0.5f) / _gridWidth;
                float relU0 = (canvasU - halfTexelU - _castleUVRect.x) / Mathf.Max(_castleUVRect.z, 0.0001f);
                float relU1 = (canvasU + halfTexelU - _castleUVRect.x) / Mathf.Max(_castleUVRect.z, 0.0001f);

                byte ghostAlpha = 0;
                if (relU1 >= 0f && relU0 <= 1f && relV1 >= 0f && relV0 <= 1f)
                {
                    float uA = Mathf.Clamp01(relU0);
                    float uB = Mathf.Clamp01(relU1);
                    float vA = Mathf.Clamp01(relV0);
                    float vB = Mathf.Clamp01(relV1);

                    int pxMin = Mathf.Clamp(opaqueBox.xMin + Mathf.FloorToInt(uA * opaqueBox.width), opaqueBox.xMin, opaqueBox.xMax - 1);
                    int pxMax = Mathf.Clamp(opaqueBox.xMin + Mathf.FloorToInt(uB * opaqueBox.width), pxMin, opaqueBox.xMax - 1);
                    int pyMin = Mathf.Clamp(opaqueBox.yMin + Mathf.FloorToInt(vA * opaqueBox.height), opaqueBox.yMin, opaqueBox.yMax - 1);
                    int pyMax = Mathf.Clamp(opaqueBox.yMin + Mathf.FloorToInt(vB * opaqueBox.height), pyMin, opaqueBox.yMax - 1);

                    int sumAlpha = 0;
                    int maxSampleAlpha = 0;
                    int samples = 0;
                    for (int py = pyMin; py <= pyMax; py++)
                    {
                        int rowOffset = py * croppedWidth;
                        for (int px = pxMin; px <= pxMax; px++)
                        {
                            int a = croppedPixels[rowOffset + px].a;
                            sumAlpha += a;
                            if (a > maxSampleAlpha) maxSampleAlpha = a;
                            samples++;
                        }
                    }

                    int avgAlpha = samples > 0 ? sumAlpha / samples : 0;
                    // Conserva puntas/mástiles finos (max >= 160 cuando cubre parcial) sin inflar ruido transparente
                    ghostAlpha = (byte)((avgAlpha >= 64 || maxSampleAlpha >= 180) ? Mathf.Max(avgAlpha, maxSampleAlpha >= 180 ? 180 : avgAlpha) : 0);
                }

                bool insideMold = ghostAlpha > 32;
                _isGhostTexel[idx] = insideMold;
                if (insideMold)
                {
                    _totalGhostTexels++;
                }

                _pixels[idx] = new Color32(0, 0, 0, ghostAlpha);
            }
        }

        // Construir banda de tolerancia constante en espacio de mundo (~0.28u alrededor del contorno)
        int tolRadius = Mathf.Clamp(Mathf.RoundToInt(0.28f / Mathf.Max(unitPerTexel, 0.01f)), 4, 8);
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                int idx = y * _gridWidth + x;
                if (_isGhostTexel[idx])
                {
                    _isToleranceTexel[idx] = true;
                    continue;
                }

                bool nearGhost = false;
                int yMin = Mathf.Max(0, y - tolRadius);
                int yMax = Mathf.Min(_gridHeight - 1, y + tolRadius);
                int xMin = Mathf.Max(0, x - tolRadius);
                int xMax = Mathf.Min(_gridWidth - 1, x + tolRadius);

                for (int ny = yMin; ny <= yMax && !nearGhost; ny++)
                {
                    for (int nx = xMin; nx <= xMax; nx++)
                    {
                        if (_isGhostTexel[ny * _gridWidth + nx])
                        {
                            nearGhost = true;
                            break;
                        }
                    }
                }
                _isToleranceTexel[idx] = nearGhost;
            }
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;
        if (LevelManager.Instance != null && (LevelManager.Instance.isGameOver || LevelManager.Instance.isPausedByUI)) return;
        if (Time.timeScale <= 0.001f) return;

        if (_runtimeMaterial != null)
        {
            float activeGuide = _useDarkPebbleGuide
                ? darkPebbleGuideVisibility
                : Mathf.Clamp(faintGuideVisibility, 0.05f, 0.35f);
            _runtimeMaterial.SetFloat("_ShowFaintGuide", activeGuide);
        }

        if (_vfxCooldownTimer > 0f) _vfxCooldownTimer -= Time.deltaTime;
        if (_erosionVfxCooldownTimer > 0f) _erosionVfxCooldownTimer -= Time.deltaTime;
        if (_wetErosionVfxCooldownTimer > 0f) _wetErosionVfxCooldownTimer -= Time.deltaTime;
        if (_bounceVfxCooldownTimer > 0f) _bounceVfxCooldownTimer -= Time.deltaTime;
        if (_crumbleVfxCooldownTimer > 0f) _crumbleVfxCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space) && LevelManager.Instance != null)
        {
            LevelManager.Instance.ToggleSandType();
        }

        HandleFingerSculptingInput();

        if (_textureDirty)
        {
            _sculptMaskTex.SetPixels32(_pixels);
            _sculptMaskTex.Apply(false);
            _textureDirty = false;
        }
    }

    private void HandleFingerSculptingInput()
    {
        bool isPointerOverUI = false;
        if (EventSystem.current != null)
        {
            isPointerOverUI = EventSystem.current.IsPointerOverGameObject();
            if (Input.touchCount > 0)
            {
                isPointerOverUI = EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }
        }

        bool leftDown = Input.GetMouseButton(0);
        bool rightEraseDown = Input.GetMouseButton(1); // Click derecho en PC permite alisar/borrar arena

        if ((leftDown || rightEraseDown) && !isPointerOverUI)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector2 currentWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);

            if (!_wasDragging)
            {
                _wasDragging = true;
                _lastFingerWorldPos = currentWorldPos;
                PaintSandStrokeSegment(currentWorldPos, currentWorldPos, rightEraseDown);
            }
            else
            {
                PaintSandStrokeSegment(_lastFingerWorldPos, currentWorldPos, rightEraseDown);
                _lastFingerWorldPos = currentWorldPos;
            }
        }
        else
        {
            _wasDragging = false;
        }
    }

    private void PaintSandStrokeSegment(Vector2 fromWorld, Vector2 toWorld, bool eraseMode)
    {
        bool wantsWetSand = LevelManager.Instance != null && LevelManager.Instance.isUsingWetSand;
        float brushMult = eraseMode ? 1f : (wantsWetSand ? wetSandBrushMultiplier : drySandBrushMultiplier);
        float effectiveRadius = brushRadiusWorld * brushMult;

        float dist = Vector2.Distance(fromWorld, toWorld);
        float stepSize = Mathf.Max(effectiveRadius * 0.28f, 0.03f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / stepSize));

        bool paintedAny = false;
        bool blockedByWater = false;

        for (int s = 0; s <= steps; s++)
        {
            float t = steps == 0 ? 1f : (float)s / steps;
            Vector2 sampleWorld = Vector2.Lerp(fromWorld, toWorld, t);
            bool hitWaterZone;
            if (PaintBrushCircleAtWorld(sampleWorld, wantsWetSand, eraseMode, effectiveRadius, out hitWaterZone))
            {
                paintedAny = true;
            }
            if (hitWaterZone)
            {
                blockedByWater = true;
            }
        }

        if (paintedAny && _vfxCooldownTimer <= 0f)
        {
            _vfxCooldownTimer = 0.065f;
            bool isUnlimited = LevelManager.Instance != null && LevelManager.Instance.IsUnlimitedWetSand;
            bool effectivelyWet = wantsWetSand && (isUnlimited || (LevelManager.Instance != null && LevelManager.Instance.currentWetSand > 0));

            if (SandVFXManager.Instance != null)
            {
                SandVFXManager.Instance.PlaySpawnVFX(toWorld, effectivelyWet);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySpawnSandSound(effectivelyWet);
            }
        }
        else if (!paintedAny && blockedByWater && _bounceVfxCooldownTimer <= 0f)
        {
            _bounceVfxCooldownTimer = 0.35f;
            if (SandVFXManager.Instance != null)
            {
                SandVFXManager.Instance.PlayWaterBounceVFX(toWorld);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayWaterSplashSound(softDrag: true);
            }
        }
    }

    private bool PaintBrushCircleAtWorld(Vector2 worldCenter, bool wantsWetSand, bool eraseMode, float effectiveRadius, out bool hitWaterCeiling)
    {
        hitWaterCeiling = false;
        float normX = (worldCenter.x - _canvasWorldBounds.min.x) / _canvasWorldBounds.size.x;
        float normY = (worldCenter.y - _canvasWorldBounds.min.y) / _canvasWorldBounds.size.y;

        if (normX < -0.05f || normX > 1.05f || normY < -0.05f || normY > 1.05f)
            return false;

        int centerX = Mathf.RoundToInt(normX * (_gridWidth - 1));
        int centerY = Mathf.RoundToInt(normY * (_gridHeight - 1));

        int radiusX = Mathf.Max(1, Mathf.RoundToInt((effectiveRadius / _canvasWorldBounds.size.x) * _gridWidth));
        int radiusY = Mathf.Max(1, Mathf.RoundToInt((effectiveRadius / _canvasWorldBounds.size.y) * _gridHeight));

        int minX = Mathf.Clamp(centerX - radiusX, 0, _gridWidth - 1);
        int maxX = Mathf.Clamp(centerX + radiusX, 0, _gridWidth - 1);
        int minY = Mathf.Clamp(centerY - radiusY, 0, _gridHeight - 1);
        int maxY = Mathf.Clamp(centerY + radiusY, 0, _gridHeight - 1);

        TideController tide = TideController.Instance;
        bool enforceWaterLimit = !eraseMode && tide != null && tide.IsInitialized;
        float canvasMinX = _canvasWorldBounds.min.x;
        float canvasSizeX = _canvasWorldBounds.size.x;
        float canvasMinY = _canvasWorldBounds.min.y;
        float canvasSizeY = _canvasWorldBounds.size.y;

        bool modified = false;

        for (int y = minY; y <= maxY; y++)
        {
            float dy = (float)(y - centerY) / radiusY;
            float worldY = canvasMinY + ((y + 0.5f) / _gridHeight) * canvasSizeY;

            for (int x = minX; x <= maxX; x++)
            {
                float dx = (float)(x - centerX) / radiusX;
                float distSq = dx * dx + dy * dy;
                if (distSq > 1.0f) continue;

                if (enforceWaterLimit)
                {
                    float worldX = canvasMinX + ((x + 0.5f) / _gridWidth) * canvasSizeX;
                    if (worldY > tide.GetMaxBuildableWorldY(worldX))
                    {
                        hitWaterCeiling = true;
                        continue;
                    }
                }

                int idx = y * _gridWidth + x;
                Color32 c = _pixels[idx];
                bool wasSculptedBefore = (c.r >= 120 || c.g >= 120);

                if (eraseMode)
                {
                    if (c.r > 0 || c.g > 0)
                    {
                        c.r = 0;
                        c.g = 0;
                        UpdateAccuracyCounters(idx, wasSculptedBefore, false);
                        _pixels[idx] = c;
                        modified = true;
                    }
                    continue;
                }

                float falloff = Mathf.Clamp01(1.15f - distSq * 0.65f);
                byte targetValue = (byte)Mathf.Clamp(Mathf.RoundToInt(falloff * 255f), 140, 255);

                if (wantsWetSand)
                {
                    bool isUnlimited = LevelManager.Instance != null && LevelManager.Instance.IsUnlimitedWetSand;
                    bool hasSand = isUnlimited || (LevelManager.Instance != null && LevelManager.Instance.currentWetSand > 0);
                    if (hasSand)
                    {
                        if (c.g < targetValue)
                        {
                            if (!isUnlimited && c.g < 140)
                            {
                                _wetTexelAccumulator++;
                                if (_wetTexelAccumulator >= texelsPerWetSandUnit)
                                {
                                    _wetTexelAccumulator = 0;
                                    LevelManager.Instance.TryUseWetSand();
                                }
                            }
                            c.g = targetValue;
                            modified = true;
                        }
                        if (c.r < targetValue)
                        {
                            c.r = targetValue;
                            modified = true;
                        }
                    }
                    else
                    {
                        if (_vfxCooldownTimer <= 0f && AudioManager.Instance != null)
                        {
                            _vfxCooldownTimer = 0.25f;
                            AudioManager.Instance.PlayErrorNoSandSound();
                        }
                        return false;
                    }
                }
                else
                {
                    if (c.r < targetValue)
                    {
                        c.r = targetValue;
                        modified = true;
                    }
                }

                if (modified)
                {
                    bool isSculptedNow = (c.r >= 120 || c.g >= 120);
                    UpdateAccuracyCounters(idx, wasSculptedBefore, isSculptedNow);
                    _pixels[idx] = c;
                }
            }
        }

        if (modified)
        {
            _textureDirty = true;
        }
        return modified;
    }

    private void UpdateAccuracyCounters(int idx, bool wasSculptedBefore, bool isSculptedNow)
    {
        if (wasSculptedBefore == isSculptedNow) return;

        int delta = isSculptedNow ? 1 : -1;
        if (_isGhostTexel[idx])
        {
            _filledGhostTexels = Mathf.Max(0, _filledGhostTexels + delta);
        }
        else if (!_isToleranceTexel[idx])
        {
            _outsideOverdrawTexels = Mathf.Max(0, _outsideOverdrawTexels + delta);
        }
    }

    public void ApplyWaveErosion(TideController tide, float deltaTime)
    {
        if (!_isInitialized || tide == null) return;

        int dryStep = Mathf.Max(1, Mathf.RoundToInt(drySandWaveErosionSpeed * deltaTime));
        int wetStep = Mathf.Max(1, Mathf.RoundToInt(wetSandWaveErosionSpeed * deltaTime));
        _wetTrailDecayAccum += beachWetTrailDecaySpeed * deltaTime;
        int trailDecay = Mathf.FloorToInt(_wetTrailDecayAccum);
        if (trailDecay > 0)
        {
            _wetTrailDecayAccum -= trailDecay;
        }

        float canvasMinY = _canvasWorldBounds.min.y;
        float canvasSizeY = _canvasWorldBounds.size.y;
        float canvasMinX = _canvasWorldBounds.min.x;
        float canvasSizeX = _canvasWorldBounds.size.x;
        float texelWorldY = canvasSizeY / Mathf.Max(1, _gridHeight);
        float texelStep = Mathf.Max(_unitPerTexel, texelWorldY);
        float wetContactDepthWorld = texelStep * wetContactThicknessTexels;

        // 1. Detectar por columna dónde la Arena Húmeda forma un dique activo frente al avance del agua
        for (int x = 0; x < _gridWidth; x++)
        {
            float u = (x + 0.5f) / _gridWidth;
            float worldX = canvasMinX + u * canvasSizeX;
            float incomingY = tide.GetShoreYAtWorldX(worldX);
            _columnIncomingShoreY[x] = incomingY;

            float barrierSafetyMaxY = incomingY + 0.90f;
            int topWetYIdx = -1;
            int wetClusterCount = 0;
            for (int y = _gridHeight - 1; y >= 0; y--)
            {
                float cellWorldY = canvasMinY + ((y + 0.5f) / _gridHeight) * canvasSizeY;
                if (cellWorldY > barrierSafetyMaxY) continue;

                Color32 cell = _pixels[y * _gridWidth + x];
                if (cell.g > WET_CRUMBLE_THRESHOLD)
                {
                    if (topWetYIdx < 0) topWetYIdx = y;
                    wetClusterCount++;
                    if (wetClusterCount >= 2 || cell.r >= 150) break;
                }
                else
                {
                    topWetYIdx = -1;
                    wetClusterCount = 0;
                }
            }

            if (topWetYIdx >= 0)
            {
                float wetDamTopWorldY = canvasMinY + ((topWetYIdx + 1f) / _gridHeight) * canvasSizeY;
                if (incomingY <= wetDamTopWorldY + texelStep * 0.25f)
                {
                    _columnBlockedByWet[x] = true;
                    _columnRawWetTopY[x] = wetDamTopWorldY;
                }
                else
                {
                    _columnBlockedByWet[x] = false;
                    _columnRawWetTopY[x] = incomingY;
                }
            }
            else
            {
                _columnBlockedByWet[x] = false;
                _columnRawWetTopY[x] = incomingY;
            }

            _columnVisualStopY[x] = _columnRawWetTopY[x];
        }

        // 2. Suavizado lateral de embalse para que el agua rodee naturalmente los extremos del escudo húmedo
        float lateralSlope = texelStep * 1.45f;
        int spreadPasses = Mathf.Clamp(waterPoolingLateralSpreadCols, 1, 6);
        for (int p = 0; p < spreadPasses; p++)
        {
            for (int x = 1; x < _gridWidth - 1; x++)
            {
                float maxNeighbor = Mathf.Max(_columnVisualStopY[x - 1], _columnVisualStopY[x + 1]);
                float pooled = maxNeighbor - lateralSlope;
                if (pooled > _columnVisualStopY[x])
                {
                    _columnVisualStopY[x] = pooled;
                }
            }
            for (int x = _gridWidth - 2; x >= 1; x--)
            {
                float maxNeighbor = Mathf.Max(_columnVisualStopY[x - 1], _columnVisualStopY[x + 1]);
                float pooled = maxNeighbor - lateralSlope;
                if (pooled > _columnVisualStopY[x])
                {
                    _columnVisualStopY[x] = pooled;
                }
            }
        }

        // 3. Aplicar erosión localizada al frente en contacto y codificar barrera 1D de 16 bits para TideWater.shader
        bool anyChange = false;
        int dryErodeCount = 0;
        Vector2 drySampleA = Vector2.zero;
        Vector2 drySampleB = Vector2.zero;
        int wetErodeCount = 0;
        Vector2 wetSamplePos = Vector2.zero;
        int wetCrumbleCount = 0;
        Vector2 crumbleSamplePos = Vector2.zero;
        Vector2 bounceSamplePos = Vector2.zero;
        float maxBounceImpact = 0f;

        float barrierMinY = canvasMinY - 1.2f;
        float barrierSpanY = Mathf.Max(canvasSizeY + 5.2f, 6.0f);

        for (int x = 0; x < _gridWidth; x++)
        {
            float u = (x + 0.5f) / _gridWidth;
            float worldX = canvasMinX + u * canvasSizeX;
            float incomingY = _columnIncomingShoreY[x];
            bool isBlocked = _columnBlockedByWet[x];
            float contactFloorY = isBlocked ? (_columnRawWetTopY[x] - wetContactDepthWorld) : incomingY;
            float visualWaterY = _columnVisualStopY[x];

            for (int y = _gridHeight - 1; y >= 0; y--)
            {
                int idx = y * _gridWidth + x;
                Color32 c = _pixels[idx];

                float v = (y + 0.5f) / _gridHeight;
                float worldY = canvasMinY + v * canvasSizeY;

                if (worldY >= contactFloorY)
                {
                    if (c.b < 235)
                    {
                        c.b = 240;
                        anyChange = true;
                    }

                    if (c.r > 0 || c.g > 0)
                    {
                        bool wasSculptedBefore = (c.r >= 120 || c.g >= 120);

                        if (c.g > WET_CRUMBLE_THRESHOLD)
                        {
                            int nextG = c.g - wetStep;
                            if (nextG <= WET_CRUMBLE_THRESHOLD)
                            {
                                // Tras 3.5-4s de golpes de ola, el escudo cede y se desmorona
                                c.g = 0;
                                c.r = (byte)Mathf.Max(0, c.r - 175);
                                wetCrumbleCount++;
                                if (wetCrumbleCount == 1 || (wetCrumbleCount & 15) == 0)
                                {
                                    crumbleSamplePos = new Vector2(worldX, worldY);
                                }
                            }
                            else
                            {
                                c.g = (byte)nextG;
                                wetErodeCount++;
                                if (wetErodeCount == 1 || (wetErodeCount & 31) == 0)
                                {
                                    wetSamplePos = new Vector2(worldX, worldY);
                                }
                            }
                        }
                        else
                        {
                            c.g = 0;
                            c.r = (byte)Mathf.Max(0, c.r - dryStep);
                            dryErodeCount++;
                            if (dryErodeCount == 1)
                            {
                                drySampleA = new Vector2(worldX, worldY);
                            }
                            else if ((dryErodeCount & 15) == 0)
                            {
                                drySampleB = new Vector2(worldX, worldY);
                            }
                        }

                        bool isSculptedNow = (c.r >= 120 || c.g >= 120);
                        UpdateAccuracyCounters(idx, wasSculptedBefore, isSculptedNow);

                        _pixels[idx] = c;
                        anyChange = true;
                    }
                    else
                    {
                        _pixels[idx] = c;
                    }
                }
                else
                {
                    if (trailDecay > 0 && worldY < visualWaterY && c.b > 0)
                    {
                        c.b = (byte)Mathf.Max(0, c.b - trailDecay);
                        _pixels[idx] = c;
                        anyChange = true;
                    }
                }
            }

            if (isBlocked)
            {
                float penetration = Mathf.Max(0f, _columnRawWetTopY[x] - incomingY);
                float targetImpact = Mathf.Clamp01(0.45f + penetration * 1.15f + (tide.isWaveSurging ? 0.25f : 0f));
                _columnImpact01[x] = Mathf.MoveTowards(_columnImpact01[x], targetImpact, deltaTime * 8.5f);
                if (_columnImpact01[x] > maxBounceImpact)
                {
                    maxBounceImpact = _columnImpact01[x];
                    bounceSamplePos = new Vector2(worldX, _columnRawWetTopY[x]);
                }
            }
            else
            {
                _columnImpact01[x] = Mathf.MoveTowards(_columnImpact01[x], 0f, deltaTime * 2.8f);
            }

            float normY = Mathf.Clamp01((visualWaterY - barrierMinY) / barrierSpanY);
            ushort qY = (ushort)Mathf.RoundToInt(normY * 65535f);
            byte hi = (byte)(qY >> 8);
            byte lo = (byte)(qY & 0xFF);
            byte impByte = (byte)Mathf.Clamp(Mathf.RoundToInt(_columnImpact01[x] * 255f), 0, 255);
            _barrierPixels[x] = new Color32(hi, lo, impByte, 255);
        }

        if (_waveBarrierTex != null)
        {
            _waveBarrierTex.SetPixels32(_barrierPixels);
            _waveBarrierTex.Apply(false);
            tide.UpdateWaveBarrier(_waveBarrierTex, _canvasWorldBounds, barrierMinY, barrierSpanY);
        }

        if (anyChange)
        {
            _textureDirty = true;
        }

        if (SandVFXManager.Instance != null)
        {
            if (dryErodeCount > 0 && _erosionVfxCooldownTimer <= 0f)
            {
                _erosionVfxCooldownTimer = 0.09f;
                SandVFXManager.Instance.PlayWaterErosionVFX(drySampleA, false);
                if (dryErodeCount > 12)
                {
                    SandVFXManager.Instance.PlayWaterErosionVFX(drySampleB, false);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySandCrumbleSound();
                }
            }

            if (wetCrumbleCount > 0 && _crumbleVfxCooldownTimer <= 0f)
            {
                _crumbleVfxCooldownTimer = 0.14f;
                SandVFXManager.Instance.PlayWetCrumbleVFX(crumbleSamplePos);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySandCrumbleSound();
            }

            if (maxBounceImpact > 0.42f && _bounceVfxCooldownTimer <= 0f)
            {
                _bounceVfxCooldownTimer = 0.16f;
                SandVFXManager.Instance.PlayWaterBounceVFX(bounceSamplePos);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayWaveBlockSound();
            }
            else if (wetErodeCount > 0 && _wetErosionVfxCooldownTimer <= 0f)
            {
                _wetErosionVfxCooldownTimer = 0.24f;
                SandVFXManager.Instance.PlayWaterErosionVFX(wetSamplePos, true);
            }
        }
    }

    private static Sprite _cachedWhiteSprite;
    public static Sprite CreateUnitWhiteSprite()
    {
        if (_cachedWhiteSprite != null) return _cachedWhiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color32[] cols = new Color32[16];
        for (int i = 0; i < 16; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols);
        tex.Apply();

        _cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _cachedWhiteSprite;
    }
}
