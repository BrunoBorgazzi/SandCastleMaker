using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PolygonCollider2D))]
public class GoalArea : MonoBehaviour
{
    [Header("Configuración del Objetivo")]
    [Tooltip("Cantidad de arena necesaria para considerar el castillo lleno al 100%")]
    public int targetSandCount = 100;

    private PolygonCollider2D polyCollider;
    private ContactFilter2D contactFilter;
    private List<Collider2D> overlapResults = new List<Collider2D>();

    private void Awake()
    {
        polyCollider = GetComponent<PolygonCollider2D>();
        polyCollider.isTrigger = true; // Aseguramos que sea invisible a colisiones físicas
        
        contactFilter = new ContactFilter2D();
        contactFilter.NoFilter();
    }

    /// <summary>
    /// Devuelve cuántos granos de arena (SandBlock) están actualmente tocando o dentro de esta área.
    /// </summary>
    public int GetSandInsideCount()
    {
        int sandCount = 0;
        
        // Barrido de físicas de todo lo que toca el PolygonCollider2D
        int numColliders = Physics2D.OverlapCollider(polyCollider, contactFilter, overlapResults);
        
        for (int i = 0; i < numColliders; i++)
        {
            // Solo contamos los que tengan el script SandBlock (arena, ya sea seca o húmeda)
            if (overlapResults[i].GetComponent<SandBlock>() != null)
            {
                sandCount++;
            }
        }
        
        return sandCount;
    }
}
