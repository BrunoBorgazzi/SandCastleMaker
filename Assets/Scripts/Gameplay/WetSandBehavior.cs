using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class WetSandBehavior : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Tiempo en segundos que tarda la arena húmeda en secarse")]
    public float dryingTime = 6f;
    
    [Tooltip("Color de la arena cuando se seca")]
    public Color dryColor = new Color(0.9f, 0.8f, 0.6f); // Color arena por defecto
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // La arena húmeda empieza estática (como un pincelazo sólido)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.velocity = Vector2.zero;
        
        StartCoroutine(DryRoutine());
    }

    private IEnumerator DryRoutine()
    {
        yield return new WaitForSeconds(dryingTime);
        
        // Al secarse, se vuelve afectada por la gravedad como la arena seca
        rb.bodyType = RigidbodyType2D.Dynamic;
        
        // Cambiar color para dar feedback visual de que se secó
        spriteRenderer.color = dryColor;
    }
}
