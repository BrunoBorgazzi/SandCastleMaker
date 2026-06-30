using UnityEngine;

// Esto añade una opción en el menú de Unity (click derecho) para crear estos archivos
[CreateAssetMenu(fileName = "Nivel_1", menuName = "SandCastle/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Configuración del Nivel")]
    public int levelID;
    
    [Tooltip("Cantidad de bloques de arena húmeda disponibles para este nivel")]
    public int wetSandAmount = 50; 
    
    public Sprite backgroundImage;
    
    [Tooltip("El prefab con el contorno del castillo a rellenar")]
    public GameObject targetShapePrefab;
}
