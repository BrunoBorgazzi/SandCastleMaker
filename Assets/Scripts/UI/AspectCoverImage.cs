using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mantiene un fondo (Image o RawImage) cubriendo todo el contenedor padre sin deformar su aspecto real,
/// actualizando automáticamente el AspectRatioFitter en modo EnvelopeParent cuando cambia el sprite o textura.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class AspectCoverImage : MonoBehaviour
{
    [Tooltip("Aspecto de respaldo si aún no hay sprite o textura asignada")]
    public float fallbackAspectRatio = 0.5625f;

    private RectTransform _rectTransform;
    private AspectRatioFitter _fitter;
    private Image _image;
    private RawImage _rawImage;
    private float _lastAppliedAspect = -1f;

    private void Awake()
    {
        CacheComponents();
        RefreshAspect();
    }

    private void OnEnable()
    {
        CacheComponents();
        RefreshAspect();
    }

    private void LateUpdate()
    {
        RefreshAspect();
    }

    private void CacheComponents()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_fitter == null) _fitter = GetComponent<AspectRatioFitter>();
        if (_fitter == null) _fitter = gameObject.AddComponent<AspectRatioFitter>();
        if (_image == null) _image = GetComponent<Image>();
        if (_rawImage == null) _rawImage = GetComponent<RawImage>();
    }

    public void RefreshAspect()
    {
        CacheComponents();
        if (_rectTransform == null || _fitter == null) return;

        float targetAspect = fallbackAspectRatio;
        if (_image != null && _image.sprite != null && _image.sprite.rect.height > 1f)
        {
            targetAspect = _image.sprite.rect.width / _image.sprite.rect.height;
            if (_image.preserveAspect) _image.preserveAspect = false;
        }
        else if (_rawImage != null && _rawImage.texture != null && _rawImage.texture.height > 1)
        {
            targetAspect = (float)_rawImage.texture.width / _rawImage.texture.height;
        }

        if (targetAspect <= 0.05f) targetAspect = 0.5625f;

        if (_fitter.aspectMode != AspectRatioFitter.AspectMode.EnvelopeParent)
        {
            _fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        if (Mathf.Abs(_lastAppliedAspect - targetAspect) > 0.0005f ||
            Mathf.Abs(_fitter.aspectRatio - targetAspect) > 0.0005f)
        {
            _fitter.aspectRatio = targetAspect;
            _lastAppliedAspect = targetAspect;
        }

        if (_rectTransform.anchorMin != Vector2.zero) _rectTransform.anchorMin = Vector2.zero;
        if (_rectTransform.anchorMax != Vector2.one) _rectTransform.anchorMax = Vector2.one;
        if (_rectTransform.pivot != new Vector2(0.5f, 0.5f)) _rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    public static AspectCoverImage Attach(GameObject target, float fallbackAspect = 0.5625f)
    {
        if (target == null) return null;
        AspectCoverImage cover = target.GetComponent<AspectCoverImage>();
        if (cover == null) cover = target.AddComponent<AspectCoverImage>();
        if (fallbackAspect > 0.05f) cover.fallbackAspectRatio = fallbackAspect;
        cover.RefreshAspect();
        return cover;
    }
}

