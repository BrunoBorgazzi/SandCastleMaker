using System;
using System.Collections.Generic;
using UnityEngine;

public enum ClimateHazardType
{
    None,
    TropicalHurricane,
    Tsunami,
    PolarBlizzard
}

/// <summary>
/// ScriptableObject que define los datos de un Mundo / Pack temático de la Gira Mundial de Playas.
/// 100% Data-Driven: permite incorporar nuevas playas y niveles sin tocar código.
/// </summary>
[CreateAssetMenu(fileName = "WorldPack_", menuName = "SandCastle/World Pack Data")]
public class WorldPackData : ScriptableObject
{
    [Header("Identificación del Mundo")]
    [Tooltip("ID único para guardar estado y PlayerPrefs (ej: pack_base, dlc_brasil)")]
    public string packId = "pack_base";

    [Tooltip("Título del mundo mostrado al jugador")]
    public string packTitle = "PLAYA LOCAL";

    [Tooltip("Etiqueta informativa de dificultad")]
    public string difficultyLabel = "Dificultad Base";

    [Tooltip("Precio en monedas del juego (0 si es gratuito/base)")]
    public int coinPrice = 0;

    [Tooltip("Si es verdadero, está desbloqueado desde el inicio sin compra")]
    public bool isUnlockedByDefault = true;

    [Tooltip("Si es verdadero, esta playa todavía está en desarrollo y muestra cartel de Próximamente en lugar del botón de compra")]
    public bool isComingSoon = false;

    [Header("Monetización / Release")]
    [Tooltip("ID de producto IAP para tiendas móviles (Google Play / App Store)")]
    public string iapProductId = "";

    [Header("Visuales Temáticos")]
    [Tooltip("Fondo vertical 9:16 para el selector de niveles de esta playa")]
    public Sprite worldBackground;

    [Tooltip("Placa de cabecera de madera/bambú con el nombre de la playa")]
    public Sprite headerPlaque;

    [Tooltip("Tarjeta de exhibición vertical para la Tienda de Playas")]
    public Sprite storeCard;

    [Header("Configuración de Niveles (Exactamente 4)")]
    [Tooltip("Nombres de las escenas de Unity a cargar por cada nivel")]
    public string[] sceneNames = new string[4] { "Nivel1", "Nivel2", "Nivel3", "Nivel4" };

    [Tooltip("Nombres visibles de respaldo si el mundo aún no tiene botones con texto embebido")]
    public string[] levelDisplayNames = new string[4] { "SOLEADO", "ATARDECER", "NOCHE VENTOSA", "TORMENTA SUAVE" };

    [Tooltip("Sprites individuales de los 4 botones de nivel con nombre e ícono climático embebido (Pixel Art)")]
    public Sprite[] levelButtonSprites = new Sprite[4];

    [Header("Clímax Climático (Nivel 4)")]
    [Tooltip("Peligro ambiental que afecta la física de la arena en el nivel 4")]
    public ClimateHazardType hazardType = ClimateHazardType.None;
}

