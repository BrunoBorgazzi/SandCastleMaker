using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))] // Fuerza a Unity a asegurarnos que el objeto tendrá un BoxCollider2D
public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. Verificamos si lo que acaba de chocar (collision) es un bloque de arena
        SandBlock sand = collision.GetComponent<SandBlock>();
        
        // 2. Si efectivamente es un bloque de arena...
        if (sand != null)
        {
            // Si es arena húmeda (tiene WetSandBehavior o joints), la destruimos
            // para no contaminar el Object Pool de arena seca.
            if (sand.isWet || sand.GetComponent<WetSandBehavior>() != null)
            {
                Destroy(sand.gameObject);
            }
            else if (SandPoolManager.Instance != null)
            {
                // 3. Si es arena seca, la devolvemos al pool para reciclarla
                SandPoolManager.Instance.ReturnSandToPool(sand.gameObject);
            }
        }
    }
}
