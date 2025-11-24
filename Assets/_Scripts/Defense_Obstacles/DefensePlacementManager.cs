using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona las defensas colocadas por turnos.
/// - RegisterPlaced(GameObject go): registra un GameObject como colocado durante el turno actual.
/// - AdvanceTurn(): llama cuando comienza un nuevo turno (ej. cuando comienza ClimberTurn).
///   Actualmente **no destruye ninguna defensa antigua**, solo archiva el historial.
/// </summary>
public class DefensePlacementManager : MonoBehaviour
{
    public static DefensePlacementManager Instance { get; private set; }

    // historial por turnos: cada entrada es la lista de GameObjects colocados en ese turno
    private readonly List<List<GameObject>> placedHistory = new List<List<GameObject>>();

    // lista de objetos del turno actualmente en curso
    private List<GameObject> currentTurnPlaced = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        // Si quieres que este manager sobreviva entre escenas, activa esto:
        // DontDestroyOnLoad(this.gameObject);
    }

    /// <summary>
    /// Registrar un GameObject como colocado en el turno actual.
    /// Evita duplicados.
    /// </summary>
    public void RegisterPlaced(GameObject go)
    {
        if (go == null) return;
        if (!currentTurnPlaced.Contains(go))
            currentTurnPlaced.Add(go);
    }

    /// <summary>
    /// Avanza el estado de turnos:
    /// - Añade la lista del turno actual al historial.
    /// - Reinicia la lista actual.
    /// 
    /// IMPORTANTE: ya NO destruye defensas antiguas.
    /// </summary>
    public void AdvanceTurn()
    {
        // Guardar el turno que acaba de terminar
        placedHistory.Add(new List<GameObject>(currentTurnPlaced));

        // Reiniciar la lista del turno actual para acumular los nuevos objetos del siguiente turno
        currentTurnPlaced = new List<GameObject>();

        // Antes aquí se destruían las defensas de hace dos turnos.
        // Eso se ha eliminado para que las defensas sean permanentes.
    }

    /// <summary>
    /// Reinicia completamente el gestor destruyendo todo lo registrado.
    /// (Solo se llama si tú lo invocas explícitamente.)
    /// </summary>
    public void ResetAll()
    {
        foreach (var list in placedHistory)
        {
            foreach (var go in list)
            {
                if (go != null) Destroy(go);
            }
        }

        foreach (var go in currentTurnPlaced)
        {
            if (go != null) Destroy(go);
        }

        placedHistory.Clear();
        currentTurnPlaced.Clear();
    }
}
