using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona las defensas colocadas por turnos.
/// - RegisterPlaced(GameObject go): registra un GameObject como colocado durante el turno actual.
/// - AdvanceTurn(): llama cuando comienza un nuevo turno (ej. cuando comienza ClimberTurn). Archiva la lista del turno que acaba
///   y, si hay más de 2 turnos en historial, destruye las defensas de hace dos turnos.
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
    /// - Si hay más de 2 turnos en historial, destruye los objetos del turno más antiguo (hace 2 turnos).
    /// Llamar esto cuando empiece el nuevo turno (por ejemplo, al comenzar ClimberTurn).
    /// </summary>
    public void AdvanceTurn()
    {
        // Guardar el turno que acaba de terminar
        placedHistory.Add(new List<GameObject>(currentTurnPlaced));

        // Reiniciar la lista del turno actual para acumular los nuevos objetos del siguiente turno
        currentTurnPlaced = new List<GameObject>();

        // Si hay más de 2 turnos guardados, el primero es el de hace 2 turnos -> destruirlo
        if (placedHistory.Count > 2)
        {
            List<GameObject> toDestroy = placedHistory[0];
            for (int i = 0; i < toDestroy.Count; i++)
            {
                GameObject g = toDestroy[i];
                if (g != null)
                {
                    Destroy(g);
                }
            }
            placedHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// (Opcional) Reinicia completamente el gestor destruyendo todo lo registrado.
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
