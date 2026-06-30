using UnityEngine;

public class SandSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public float spawnInterval = 0.05f; 
    public GameObject wetSandPrefab; // Prefab específico para la arena húmeda (no se recicla)
    
    private float spawnTimer;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // ATAJO PARA PC: Usamos la barra espaciadora para cambiar entre Arena Húmeda y Seca
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ToggleSandType();
            }
        }

        if (Input.GetMouseButton(0))
        {
            spawnTimer -= Time.deltaTime;
            
            if (spawnTimer <= 0f)
            {
                SpawnSandAtInputPosition();
                spawnTimer = spawnInterval;
            }
        }
        else
        {
            spawnTimer = 0f; 
        }
    }

    private void SpawnSandAtInputPosition()
    {
        Vector3 inputPosition = Input.mousePosition;
        Vector2 worldPosition = mainCamera.ScreenToWorldPoint(inputPosition);

        // Consultamos al LevelManager qué tipo de arena estamos usando
        if (LevelManager.Instance != null && LevelManager.Instance.isUsingWetSand)
        {
            // Intentamos gastar 1 de arena húmeda
            if (LevelManager.Instance.TryUseWetSand())
            {
                // Instanciamos el prefab de arena húmeda (este no va a la pool)
                Instantiate(wetSandPrefab, worldPosition, Quaternion.identity);
            }
            else
            {
                // Si nos quedamos sin arena, ignoramos el input
                // Aquí en el futuro podemos poner un sonido de "error"
            }
        }
        else
        {
            // Si usamos arena seca, pedimos una a la Pool como siempre
            if (SandPoolManager.Instance != null)
            {
                SandPoolManager.Instance.SpawnSand(worldPosition);
            }
        }
    }
}
