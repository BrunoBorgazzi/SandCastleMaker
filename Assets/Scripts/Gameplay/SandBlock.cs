using UnityEngine;

/// <summary>
/// Componente adherido a cada bloque físico de arena (SandDryPrefab y SandWetPrefab).
/// Funciona como etiqueta para GoalArea y KillZone, y dispara feedback de impacto VFX
/// calibrado para evitar saturación de draw-calls.
/// </summary>
public class SandBlock : MonoBehaviour
{
    [Tooltip("¿Es arena húmeda?")]
    public bool isWet = false;

    private float _lastImpactTime;
    private const float IMPACT_COOLDOWN = 0.18f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Enfriamiento individual por grano para evitar saturar el pool en rozamientos repetidos
        if (Time.time - _lastImpactTime < IMPACT_COOLDOWN) return;

        float relativeSpeed = collision.relativeVelocity.magnitude;
        bool isCollidingWithAnotherSand = collision.gameObject.GetComponent<SandBlock>() != null;

        // Umbral diferenciado: impactos contra el suelo/estructuras requieren menos velocidad (1.2f)
        // mientras que entre granos de arena se requiere un golpe más contundente (2.4f) para no emitir mientras se asienta.
        float requiredSpeed = isCollidingWithAnotherSand ? 2.4f : 1.2f;

        if (relativeSpeed > requiredSpeed)
        {
            _lastImpactTime = Time.time;
            if (collision.contactCount > 0)
            {
                Vector2 contactPoint = collision.GetContact(0).point;
                float intensity = Mathf.Clamp(relativeSpeed / 4f, 0.5f, 1.8f);
                SandVFXManager.Instance.PlayImpactVFX(contactPoint, isWet, intensity);
            }
        }
    }
}
