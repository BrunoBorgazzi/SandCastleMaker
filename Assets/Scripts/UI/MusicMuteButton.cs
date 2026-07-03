using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MusicMuteButton : MonoBehaviour
{
    [Header("UI Visuals")]
    [Tooltip("La imagen que cambiará dependiendo del estado de la música")]
    public Image iconImage;
    public Sprite musicOnSprite;
    public Sprite musicOffSprite;

    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ToggleMusic);

        // Actualizar el sprite inicial según el estado guardado
        UpdateVisuals();
    }

    private void ToggleMusic()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.ToggleMusic();
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (MusicManager.Instance != null && iconImage != null)
        {
            if (musicOnSprite != null && musicOffSprite != null)
            {
                iconImage.sprite = MusicManager.Instance.IsMuted ? musicOffSprite : musicOnSprite;
            }
        }
    }
}
