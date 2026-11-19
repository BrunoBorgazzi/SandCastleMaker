using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ajusta este RectTransform al Screen.safeArea normalizado para proteger HUD y botones
/// contra notches, islas dinámicas y barras de gestos, manteniendo los fondos fuera del contenedor.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    public bool applyLeft = true;
    public bool applyRight = true;
    public bool applyTop = true;
    public bool applyBottom = true;

    private RectTransform _rectTransform;
    private Rect _lastSafeArea = Rect.zero;
    private Vector2Int _lastScreenSize = Vector2Int.zero;
    private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnEnable()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (gameObject.activeInHierarchy)
        {
            ApplySafeAreaIfNeeded();
        }
    }

    private void Update()
    {
        ApplySafeAreaIfNeeded();
    }

    public void ApplySafeAreaIfNeeded()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (safeArea == _lastSafeArea &&
            screenSize == _lastScreenSize &&
            Screen.orientation == _lastOrientation)
        {
            return;
        }

        ApplySafeArea();
    }

    public void ApplySafeArea()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;

        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _lastOrientation = Screen.orientation;

        float minX = applyLeft ? Mathf.Clamp01(safeArea.xMin / Screen.width) : 0f;
        float minY = applyBottom ? Mathf.Clamp01(safeArea.yMin / Screen.height) : 0f;
        float maxX = applyRight ? Mathf.Clamp01(safeArea.xMax / Screen.width) : 1f;
        float maxY = applyTop ? Mathf.Clamp01(safeArea.yMax / Screen.height) : 1f;

        if (maxX < minX) maxX = minX;
        if (maxY < minY) maxY = minY;

        Vector2 anchorMin = new Vector2(minX, minY);
        Vector2 anchorMax = new Vector2(maxX, maxY);

        if (_rectTransform.anchorMin != anchorMin) _rectTransform.anchorMin = anchorMin;
        if (_rectTransform.anchorMax != anchorMax) _rectTransform.anchorMax = anchorMax;
        if (_rectTransform.offsetMin != Vector2.zero) _rectTransform.offsetMin = Vector2.zero;
        if (_rectTransform.offsetMax != Vector2.zero) _rectTransform.offsetMax = Vector2.zero;
        if (_rectTransform.pivot != new Vector2(0.5f, 0.5f)) _rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Crea o reutiliza un contenedor SafeArea hijo de panelRoot y mueve adentro los hijos interactivos
    /// que no figuren en excludedNames (dejando fondos full-bleed y overlays globales intactos).
    /// Conserva todas las referencias serializadas de controladores UI.
    /// </summary>
    public static RectTransform EnsureSafeAreaContainer(
        Transform panelRoot,
        string containerName = "SafeArea_Content",
        params string[] excludedNames)
    {
        if (panelRoot == null) return null;

        HashSet<string> excluded = new HashSet<string> { containerName };
        if (excludedNames != null)
        {
            for (int i = 0; i < excludedNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(excludedNames[i]))
                {
                    excluded.Add(excludedNames[i]);
                }
            }
        }

        Transform existing = panelRoot.Find(containerName);
        GameObject containerGO;
        if (existing != null)
        {
            containerGO = existing.gameObject;
        }
        else
        {
            containerGO = new GameObject(containerName, typeof(RectTransform), typeof(SafeAreaFitter));
            containerGO.transform.SetParent(panelRoot, false);
        }

        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        if (containerRT == null) containerRT = containerGO.AddComponent<RectTransform>();

        SafeAreaFitter fitter = containerGO.GetComponent<SafeAreaFitter>();
        if (fitter == null) fitter = containerGO.AddComponent<SafeAreaFitter>();
        fitter.ApplySafeArea();

        // Posicionar inmediatamente después de los fondos excluidos y antes de overlays globales
        List<Transform> toMove = new List<Transform>();
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            Transform child = panelRoot.GetChild(i);
            if (!excluded.Contains(child.name))
            {
                toMove.Add(child);
            }
        }

        for (int i = 0; i < toMove.Count; i++)
        {
            toMove[i].SetParent(containerRT, false);
        }

        return containerRT;
    }

    public static Transform FindInScope(Transform root, string childName, string containerName = "SafeArea_Content")
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        Transform direct = root.Find(childName);
        if (direct != null) return direct;

        Transform container = root.Find(containerName);
        return container != null ? container.Find(childName) : null;
    }
}

