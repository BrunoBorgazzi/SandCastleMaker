using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que define la estructura y contenido de un paquete de expansión (DLC).
/// Permite parametrizar paquetes de contenido desde el inspector de Unity sin tocar código.
/// </summary>
[CreateAssetMenu(fileName = "DLC_Pack", menuName = "SandCastle/DLC Data")]
public class DLCData : ScriptableObject
{
    [Header("Identificación del DLC")]
    [Tooltip("ID único y seguro del paquete (ej: dlc_castillos_fantasia, dlc_desierto)")]
    public string dlcID = "dlc_pack_1";

    [Tooltip("Nombre comercial visible para el jugador en la tienda")]
    public string packTitle = "Pack de Expansión";

    [TextArea(2, 4)]
    [Tooltip("Descripción de los niveles, mecánicas o elementos que incluye")]
    public string description = "Nuevos y desafiantes moldes de castillos de arena con temáticas exclusivas.";

    [Tooltip("Precio de exhibición en la tienda o SKU")]
    public string priceText = "$1.99";

    [Header("Arte y Visuales")]
    [Tooltip("Banner o miniatura promocional para la tarjeta de la tienda")]
    public Sprite packBanner;

    [Tooltip("Ícono pequeño para la pestaña en la selección de niveles")]
    public Sprite tabIcon;

    [Header("Contenido Incluido")]
    [Tooltip("Lista de niveles (LevelData) que forman parte de este DLC")]
    public List<LevelData> levels = new List<LevelData>();

    [Tooltip("Nombres de las escenas asociadas a este DLC para SceneManager")]
    public List<string> sceneNames = new List<string>();
}
