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
            // 3. Le decimos al Manager que lo agarre, lo apague y lo devuelva a la fila
            if (SandPoolManager.Instance != null)
            {
                SandPoolManager.Instance.ReturnSandToPool(sand.gameObject);
            }
        }
    }
}
