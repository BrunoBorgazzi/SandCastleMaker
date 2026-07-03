using UnityEngine;
using UnityEngine.EventSystems; // Necesario para detectar clicks sobre la UI

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
        // Si el juego ya terminó, no permitimos ninguna interacción
        if (LevelManager.Instance != null && LevelManager.Instance.isGameOver)
        {
            return;
        }

        // ATAJO PARA PC: Usamos la barra espaciadora para cambiar entre Arena Húmeda y Seca
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ToggleSandType();
            }
        }

        // Verificamos si el puntero (o dedo) está sobre la UI
        bool isPointerOverUI = false;
        if (EventSystem.current != null)
        {
            isPointerOverUI = EventSystem.current.IsPointerOverGameObject(); // PC
            if (Input.touchCount > 0)
            {
                isPointerOverUI = EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId); // Mobile
            }
        }

        // Solo spawneamos si NO estamos sobre la UI
        if (Input.GetMouseButton(0) && !isPointerOverUI)
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
            if (LevelManager.Instance.TryUseWetSand())
            {
                // Instanciamos el prefab de arena húmeda (este no va a la pool)
                Instantiate(wetSandPrefab, worldPosition, Quaternion.identity);
                
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySpawnSandSound();
                }
            }
            else
            {
                // Si nos quedamos sin arena, ignoramos el input y ponemos sonido de error
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayErrorNoSandSound();
                }
            }
        }
        else
        {
            // Si usamos arena seca, pedimos una a la Pool como siempre
            if (SandPoolManager.Instance != null)
            {
                GameObject spawnedSand = SandPoolManager.Instance.SpawnSand(worldPosition);
                if (spawnedSand != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySpawnSandSound();
                }
            }
        }
    }
}
