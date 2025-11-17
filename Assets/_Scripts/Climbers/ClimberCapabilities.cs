using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ClimberCapabilities : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // Conjunto de tipos de obstáculo que este escalador puede manejar
    private HashSet<ObstacleType> capableTypes = new HashSet<ObstacleType>();

    /// <summary>
    /// Se llama desde ClimberLoadout para actualizar las capacidades
    /// a partir de la lista de tipos soportados por el equipo.
    /// </summary>
    public void SetCapabilitiesFromTypes(List<ObstacleType> types)
    {
        capableTypes.Clear();

        if (types != null)
        {
            foreach (var t in types)
            {
                if (t == ObstacleType.None)
                    continue;

                capableTypes.Add(t);
            }
        }

        if (debugLogs)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var t in capableTypes)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(t.ToString());
            }

            Debug.Log($"[ClimberCapabilities] Capabilities updated: {sb}");
        }
    }

    /// <summary>
    /// Devuelve true si este escalador puede manejar este tipo de obstáculo.
    /// </summary>
    public bool CanHandleObstacleType(ObstacleType type)
    {
        if (type == ObstacleType.None)
            return true; // Nada que manejar

        return capableTypes.Contains(type);
    }
}
