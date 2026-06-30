using System.Collections.Generic;
using UnityEngine;

public class SandPoolManager : MonoBehaviour
{
    public static SandPoolManager Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject sandPrefab;
    public int poolSize = 200;

    private Queue<GameObject> sandPool;

    private void Awake()
    {
        // Patrón Singleton para acceder fácilmente desde cualquier script
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePool();
    }

    private void InitializePool()
    {
        sandPool = new Queue<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            // Se instancian como hijos de este manager para mantener la jerarquía ordenada
            GameObject sandObj = Instantiate(sandPrefab, transform); 
            sandObj.SetActive(false);
            sandPool.Enqueue(sandObj);
        }
    }

    public GameObject SpawnSand(Vector2 position)
    {
        if (sandPool.Count == 0)
        {
            Debug.LogWarning("¡La Pool de Arena está vacía! Considera aumentar el poolSize.");
            return null; 
        }

        GameObject sandObj = sandPool.Dequeue();
        
        // Al reciclar, ES VITAL resetear la velocidad física si el objeto tiene un Rigidbody2D
        Rigidbody2D rb = sandObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        sandObj.transform.position = position;
        sandObj.SetActive(true);

        return sandObj;
    }

    public void ReturnSandToPool(GameObject sandObj)
    {
        sandObj.SetActive(false);
        sandPool.Enqueue(sandObj);
    }
}
