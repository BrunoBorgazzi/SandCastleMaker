using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestor centralizado del sistema de DLCs y persistencia de compras.
/// Maneja la comprobación de paquetes adquiridos, la lógica de compra simulada/real
/// y notifica a la UI mediante eventos desacoplados.
/// </summary>
public class DLCManager : MonoBehaviour
{
    private static DLCManager _instance;

    public static DLCManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<DLCManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("DLCManager");
                    _instance = go.AddComponent<DLCManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Catálogo Oficial de DLCs")]
    [Tooltip("Todos los paquetes de DLC configurados en el proyecto")]
    public List<DLCData> registeredDLCs = new List<DLCData>();

    [Header("Configuración de Depuración")]
    [Tooltip("Si está activo, simula que todos los DLCs están comprados (para testing rápido en Editor)")]
    public bool debugUnlockAll = false;

    // Eventos desacoplados para la UI (S-02 Tabs, S-03 Tienda, etc.)
    public static event Action<DLCData> OnDLCPurchased;
    public static event Action OnDLCStateChanged;

    // Cache local en memoria para consultas O(1) de alto rendimiento
    private readonly HashSet<string> _ownedDLCSet = new HashSet<string>();

    private const string PREF_KEY_PREFIX = "SANDCASTLE_DLC_OWNED_";

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            // Persistir entre escenas si está en la raíz, o dejar que viva en la escena principal
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (registeredDLCs == null) registeredDLCs = new List<DLCData>();
            if (registeredDLCs.Count == 0)
            {
                DLCData[] resourceDLCs = Resources.LoadAll<DLCData>("DLC");
                if (resourceDLCs != null)
                {
                    foreach (DLCData d in resourceDLCs)
                    {
                        if (d != null && !registeredDLCs.Contains(d))
                        {
                            registeredDLCs.Add(d);
                        }
                    }
                }
            }

            #if UNITY_EDITOR
            if (registeredDLCs.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DLCData");
                foreach (string g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    DLCData d = UnityEditor.AssetDatabase.LoadAssetAtPath<DLCData>(path);
                    if (d != null && !registeredDLCs.Contains(d))
                    {
                        registeredDLCs.Add(d);
                    }
                }
            }
            #endif

            LoadDLCStates();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Carga el estado de posesión de cada DLC desde PlayerPrefs.
    /// </summary>
    public void LoadDLCStates()
    {
        _ownedDLCSet.Clear();

        if (registeredDLCs != null)
        {
            foreach (var dlc in registeredDLCs)
            {
                if (dlc == null || string.IsNullOrEmpty(dlc.dlcID)) continue;

                string key = PREF_KEY_PREFIX + dlc.dlcID;
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    _ownedDLCSet.Add(dlc.dlcID);
                }
            }
        }

        Debug.Log($"[DLCManager] Estados cargados. DLCs desbloqueados: {_ownedDLCSet.Count}");
    }

    /// <summary>
    /// Comprueba si un DLC específico está en propiedad del jugador.
    /// </summary>
    public bool IsDLCOwned(DLCData dlc)
    {
        if (dlc == null) return false;
        return IsDLCOwned(dlc.dlcID);
    }

    /// <summary>
    /// Comprueba por ID si un DLC está en propiedad del jugador.
    /// </summary>
    public bool IsDLCOwned(string dlcID)
    {
        if (debugUnlockAll) return true;
        if (string.IsNullOrEmpty(dlcID)) return false;

        return _ownedDLCSet.Contains(dlcID);
    }

    /// <summary>
    /// Realiza la compra/desbloqueo de un DLC y guarda la persistencia de forma inmediata.
    /// </summary>
    public bool PurchaseDLC(DLCData dlc)
    {
        if (dlc == null)
        {
            Debug.LogError("[DLCManager] Intento de compra con DLCData nulo.");
            return false;
        }

        if (IsDLCOwned(dlc.dlcID))
        {
            Debug.LogWarning($"[DLCManager] El DLC '{dlc.packTitle}' ({dlc.dlcID}) ya fue adquirido previamente.");
            return true;
        }

        // Registrar en PlayerPrefs y en memoria
        string key = PREF_KEY_PREFIX + dlc.dlcID;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        _ownedDLCSet.Add(dlc.dlcID);
        Debug.Log($"<color=green>[DLCManager] ¡Compra exitosa!</color> DLC desbloqueado: {dlc.packTitle} ({dlc.dlcID})");

        // Disparar eventos para actualizar las pantallas de UI en caliente
        OnDLCPurchased?.Invoke(dlc);
        OnDLCStateChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Verifica si un nivel específico está accesible (si pertenece a un DLC, valida que esté comprado).
    /// </summary>
    public bool IsLevelUnlocked(LevelData level)
    {
        if (level == null) return false;

        DLCData parentDLC = GetDLCForLevel(level);
        if (parentDLC == null)
        {
            // El nivel pertenece a la campaña base del MVP (desbloqueado por defecto o controlado por LevelManager)
            return true;
        }

        return IsDLCOwned(parentDLC);
    }

    /// <summary>
    /// Busca a qué paquete de DLC pertenece un nivel determinado. Retorna null si es de la campaña base.
    /// </summary>
    public DLCData GetDLCForLevel(LevelData level)
    {
        if (level == null || registeredDLCs == null) return null;

        foreach (var dlc in registeredDLCs)
        {
            if (dlc != null && dlc.levels.Contains(level))
            {
                return dlc;
            }
        }
        return null;
    }

    /// <summary>
    /// Obtiene el objeto DLCData a partir de su identificador ID.
    /// </summary>
    public DLCData GetDLCByID(string dlcID)
    {
        if (registeredDLCs == null) return null;
        return registeredDLCs.Find(d => d != null && d.dlcID == dlcID);
    }

    // --- Métodos de utilidad y QA / Testing ---

    /// <summary>
    /// Borra todas las compras de DLCs guardadas (útil para pruebas en Unity Editor).
    /// </summary>
    [ContextMenu("Restablecer Compras (Reset PlayerPrefs)")]
    public void ResetAllPurchases()
    {
        if (registeredDLCs != null)
        {
            foreach (var dlc in registeredDLCs)
            {
                if (dlc != null && !string.IsNullOrEmpty(dlc.dlcID))
                {
                    PlayerPrefs.DeleteKey(PREF_KEY_PREFIX + dlc.dlcID);
                }
            }
        }
        PlayerPrefs.Save();
        _ownedDLCSet.Clear();
        Debug.Log("[DLCManager] Todas las compras de DLC han sido restablecidas.");
        OnDLCStateChanged?.Invoke();
    }

    /// <summary>
    /// Desbloquea todos los DLCs registrados y guarda su estado.
    /// </summary>
    [ContextMenu("Desbloquear Todos los DLCs")]
    public void UnlockAllPurchases()
    {
        if (registeredDLCs != null)
        {
            foreach (var dlc in registeredDLCs)
            {
                if (dlc != null && !string.IsNullOrEmpty(dlc.dlcID))
                {
                    PlayerPrefs.SetInt(PREF_KEY_PREFIX + dlc.dlcID, 1);
                    _ownedDLCSet.Add(dlc.dlcID);
                }
            }
        }
        PlayerPrefs.Save();
        Debug.Log("[DLCManager] Todos los DLCs fueron desbloqueados forzosamente.");
        OnDLCStateChanged?.Invoke();
    }
}
