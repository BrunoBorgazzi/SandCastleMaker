using System;
using UnityEngine;

/// <summary>
/// Adapta dinámicamente orthographicSize y encuadre de la cámara 2D usando escalado vertical base
/// más expansión automática horizontal en pantallas angostas (9:20, 9:21) o tablets (3:4),
/// asegurando que el castillo completo entre siempre dentro de la zona segura libre de HUD.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ResponsiveViewportCamera : MonoBehaviour
{
    [Header("Escalado Vertical + Fit Horizontal")]
    [Tooltip("Semialtura vertical mínima base de la cámara (4.6 = castillo protagonista en el tercio inferior)")]
    public float minVerticalOrthoSize = 4.6f;

    [Tooltip("Margen horizontal mínimo a cada lado del castillo en unidades de mundo")]
    public float horizontalSidePaddingWorld = 0.35f;

    [Tooltip("Fracción horizontal usable dentro del SafeArea (ej. 0.90 deja ~5% libre por lado)")]
    [Range(0.5f, 0.96f)]
    public float horizontalUsableFraction = 0.90f;

    [Tooltip("Altura normalizada desde el fondo donde apoya la base del castillo (deja espacio inferior para el setup mapa+balde)")]
    [Range(0.08f, 0.30f)]
    public float castleBottomScreenFraction = 0.19f;

    [Tooltip("Fracción vertical reservada arriba para HUD superior y origen de olas")]
    [Range(0.10f, 0.35f)]
    public float verticalTopReserveFraction = 0.20f;

    private Camera _camera;
    private Bounds _targetBounds;
    private bool _hasTargetBounds;
    private Vector2Int _lastScreenSize = Vector2Int.zero;
    private Rect _lastSafeArea = Rect.zero;
    private float _lastOrtho = -1f;

    public event Action OnViewportAdapted;

    public static ResponsiveViewportCamera EnsureOnCamera(Camera cam)
    {
        if (cam == null) return null;
        ResponsiveViewportCamera comp = cam.GetComponent<ResponsiveViewportCamera>();
        if (comp == null) comp = cam.gameObject.AddComponent<ResponsiveViewportCamera>();
        return comp;
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera != null) _camera.orthographic = true;
    }

    public void ConfigureTargetBounds(Bounds tightCastleBounds)
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        _targetBounds = tightCastleBounds;
        _hasTargetBounds = true;
        UpdateViewport(force: true);
    }

    private void Update()
    {
        if (!_hasTargetBounds) return;
        UpdateViewport(force: false);
    }

    public void UpdateViewport(bool force)
    {
        if (!_hasTargetBounds) return;
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null || Screen.width <= 0 || Screen.height <= 0) return;

        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (!force && screenSize == _lastScreenSize && safeArea == _lastSafeArea)
        {
            return;
        }

        _lastScreenSize = screenSize;
        _lastSafeArea = safeArea;

        float aspect = Mathf.Max((float)Screen.width / Screen.height, 0.2f);
        float safeLeft = Mathf.Clamp01(safeArea.xMin / Screen.width);
        float safeRight = Mathf.Clamp01(1f - safeArea.xMax / Screen.width);
        float safeBottom = Mathf.Clamp01(safeArea.yMin / Screen.height);
        float safeTop = Mathf.Clamp01(1f - safeArea.yMax / Screen.height);

        float usableWidthFrac = Mathf.Max(0.35f, (1f - safeLeft - safeRight) * horizontalUsableFraction);
        float desiredWorldWidth = Mathf.Max(1f, _targetBounds.size.x + horizontalSidePaddingWorld * 2f);
        float requiredTotalWidth = desiredWorldWidth / usableWidthFrac;
        float orthoForWidth = (requiredTotalWidth / aspect) * 0.5f;

        float bottomFrac = castleBottomScreenFraction + safeBottom;
        float topReserve = verticalTopReserveFraction + safeTop;
        float usableHeightFrac = Mathf.Max(0.28f, 1f - bottomFrac - topReserve);
        float orthoForHeight = (_targetBounds.size.y / usableHeightFrac) * 0.5f;

        float finalOrtho = Mathf.Max(minVerticalOrthoSize, orthoForWidth, orthoForHeight);
        float fullHeight = finalOrtho * 2f;
        float fullWidth = fullHeight * aspect;

        // Centrar horizontalmente dentro del rectángulo útil de safeArea
        float safeCenterShiftU = (safeLeft - safeRight) * 0.5f;
        float targetCamX = _targetBounds.center.x - safeCenterShiftU * fullWidth;

        // Apoyar la base del castillo en bottomFrac para dejar toda la zona superior al mar/UI
        float camBottomY = _targetBounds.min.y - bottomFrac * fullHeight;
        float targetCamY = camBottomY + finalOrtho;

        bool changed = force ||
            Mathf.Abs(_lastOrtho - finalOrtho) > 0.001f ||
            Mathf.Abs(transform.position.x - targetCamX) > 0.001f ||
            Mathf.Abs(transform.position.y - targetCamY) > 0.001f;

        _camera.orthographic = true;
        _camera.orthographicSize = finalOrtho;
        transform.position = new Vector3(targetCamX, targetCamY, transform.position.z);
        _lastOrtho = finalOrtho;

        if (changed)
        {
            OnViewportAdapted?.Invoke();
        }
    }
}

