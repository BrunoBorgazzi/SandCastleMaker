using UnityEngine;

// Esto añade una opción en el menú de Unity (click derecho) para crear estos archivos
[CreateAssetMenu(fileName = "Nivel_1", menuName = "SandCastle/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Configuración del Nivel")]
    public int levelID;

    [Tooltip("Tema visual regional para botones y carteles UI ('base', 'brasil', etc.)")]
    public string regionThemeId = "base";
    
    [Tooltip("Si está activo, el jugador tiene Arena Húmeda ilimitada para defender el castillo contra las olas")]
    public bool unlimitedWetSand = true;

    [Tooltip("Cantidad de unidades de arena húmeda disponibles cuando unlimitedWetSand está desactivado")]
    public int wetSandAmount = 100; 
    
    public Sprite backgroundImage;
    
    [Tooltip("El prefab con el contorno/sprite del castillo fantasma a esculpir")]
    public GameObject targetShapePrefab;

    [Header("Criterios de Estrellas")]
    [Tooltip("Tiempo objetivo en segundos para obtener la estrella de velocidad")]
    public float targetTimeSeconds = 40f;

    [Tooltip("Exceso máximo permitido fuera de la silueta (0.14 = 14%) para ganar la 3ra estrella por prolijidad cuando la arena es ilimitada")]
    [Range(0.02f, 0.5f)]
    public float maxOverdrawRatioForStar = 0.14f;

    [Tooltip("Proporción mínima de arena húmeda inicial que debe sobrar si el balde es limitado (0.25 = 25%)")]
    [Range(0f, 1f)]
    public float wetSandSaveRatio = 0.25f;

    [Header("Sistema de Marea Viva y Olas (Timer Diegético)")]
    [Tooltip("Tiempo total en segundos que tarda la marea base en cubrir todo el castillo y causar derrota")]
    public float tideMaxDurationSeconds = 65f;

    [Tooltip("Distancia (en unidades de mundo) que avanza y retrocede cada ola sobre la arena")]
    public float waveSwingAmplitude = 0.85f;

    [Tooltip("Frecuencia de las olas en ciclos por segundo (ej: 0.25 = una ola cada 4 segundos)")]
    public float waveFrequency = 0.24f;

    [Tooltip("Intensidad del viento costero que genera olas impredecibles más grandes (0 = calma, 1 = huracán)")]
    [Range(0f, 1f)]
    public float windHazardIntensity = 0.0f;

    [Header("Configuración del Pincel de Arena")]
    [Tooltip("Radio del dedo al esculpir en la arena (en unidades de mundo, ej: 0.14)")]
    public float brushRadiusWorld = 0.14f;
}
