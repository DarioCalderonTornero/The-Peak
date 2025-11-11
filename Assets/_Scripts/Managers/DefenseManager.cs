using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DefenseManager - Gestión centralizada de defensas
/// ================================================
/// Responsabilidades:
/// - Colocación de defensas desde prefabs (DefensePlacer)
/// - Registro y historial de defensas por turno (DefensePlacementManager)
/// - Limpieza automática de defensas antiguas
/// - Reinicio completo del sistema
/// 
/// Uso:
/// 1. PlaceDefense(prefab, position) - Crea defensa sin registrar
/// 2. RegisterPlaced(gameObject) - Registra defensa manualmente
/// 3. PlaceDefenseAndRegister(prefab, position) - Crea y registra (recomendado)
/// 4. AdvanceTurn() - Llamar cuando comienza nuevo turno
/// 5. ResetAll() - Destruir todo (reset de partida)
/// </summary>
public class DefenseManager : MonoBehaviour
{
    public static DefenseManager Instance { get; private set; }

    // Historial de defensas por turno
    private readonly List<List<GameObject>> placedHistory = new List<List<GameObject>>();
    private List<GameObject> currentTurnPlaced = new List<GameObject>();

    [SerializeField] private bool logInitialization = true;
    [SerializeField] private bool logDefensePlacement = true;
    [SerializeField] private bool logTurnAdvance = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (logInitialization)
            Debug.Log("[DefenseManager] Initialized");
    }

    /// <summary>
    /// Crea una defensa a partir de un prefab en la posición indicada.
    /// Inicializa el componente BaseDefense si existe.
    /// No registra automáticamente (ver PlaceDefenseAndRegister).
    /// </summary>
    public GameObject PlaceDefense(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
        {
            Debug.LogError("[DefenseManager] Prefab is null!");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);

        // Inicializar defensa si tiene componente BaseDefense
        BaseDefense defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

        if (logDefensePlacement)
            Debug.Log($"[DefenseManager] Defense placed at {position}");

        return instance;
    }

    /// <summary>
    /// Registra un GameObject como colocado en el turno actual.
    /// Evita duplicados.
    /// </summary>
    public void RegisterPlaced(GameObject go)
    {
        if (go == null)
            return;
        
        if (!currentTurnPlaced.Contains(go))
        {
            currentTurnPlaced.Add(go);
            if (logDefensePlacement)
                Debug.Log($"[DefenseManager] Defense registered: {go.name}");
        }
    }

    /// <summary>
    /// Crea una defensa Y la registra automáticamente en el turno actual.
    /// Esta es la forma recomendada de colocar defensas.
    /// </summary>
    public GameObject PlaceDefenseAndRegister(GameObject prefab, Vector3 position)
    {
        GameObject instance = PlaceDefense(prefab, position);
        if (instance != null)
        {
            RegisterPlaced(instance);
        }
        return instance;
    }

    /// <summary>
    /// Avanza el turno:
    /// - Guarda la lista actual en el historial
    /// - Reinicia la lista actual para el nuevo turno
    /// - Si hay más de 2 turnos, destruye el turno más antiguo
    /// </summary>
    public void AdvanceTurn()
    {
        // Guardar turno que acaba de terminar
        placedHistory.Add(new List<GameObject>(currentTurnPlaced));

        // Reiniciar lista para nuevo turno
        currentTurnPlaced = new List<GameObject>();

        // Si hay más de 2 turnos, destruir el más antiguo
        if (placedHistory.Count > 2)
        {
            List<GameObject> toDestroy = placedHistory[0];
            for (int i = 0; i < toDestroy.Count; i++)
            {
                GameObject g = toDestroy[i];
                if (g != null)
                {
                    if (logDefensePlacement)
                        Debug.Log($"[DefenseManager] Destroying old defense: {g.name}");
                    Destroy(g);
                }
            }
            placedHistory.RemoveAt(0);
        }

        if (logTurnAdvance)
            Debug.Log($"[DefenseManager] Turn advanced. Placed: {currentTurnPlaced.Count}");
    }

    /// <summary>
    /// Reinicia completamente el manager, destruyendo todas las defensas.
    /// </summary>
    public void ResetAll()
    {
        // Destruir defensas en historial
        foreach (var list in placedHistory)
        {
            foreach (var go in list)
            {
                if (go != null)
                    Destroy(go);
            }
        }

        // Destruir defensas del turno actual
        foreach (var go in currentTurnPlaced)
        {
            if (go != null)
                Destroy(go);
        }

        // Limpiar listas
        placedHistory.Clear();
        currentTurnPlaced.Clear();

        if (logInitialization)
            Debug.Log("[DefenseManager] Reset complete");
    }

    /// <summary>
    /// Obtiene la cantidad de defensas colocadas en el turno actual.
    /// </summary>
    public int GetCurrentTurnDefenseCount()
    {
        return currentTurnPlaced.Count;
    }

    /// <summary>
    /// Obtiene la cantidad total de turnos registrados.
    /// </summary>
    public int GetTurnsInHistory()
    {
        return placedHistory.Count;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
