// DefensePlacer.cs
using UnityEngine;
using System;

public class DefensePlacer : MonoBehaviour
{
    public static DefensePlacer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    // Versión antigua (por si algún código la usa todavía)
    public GameObject PlaceDefense(GameObject prefab, Vector3 position)
    {
        return PlaceDefense(prefab, position, Quaternion.identity);
    }

    // Versión con rotación
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return PlaceDefense(prefab, position, rotation, null, null);
    }

    // Mantener firma antigua "beforeInitialize"
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> beforeInitialize)
    {
        return PlaceDefense(prefab, position, rotation, beforeInitialize, null);
    }

    // ✅ NUEVA: hooks antes y después de Initialize()
    public GameObject PlaceDefense(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Action<GameObject> beforeInitialize,
        Action<GameObject> afterInitialize)
    {
        GameObject instance = Instantiate(prefab, position, rotation);

        // 👇 aquí metes seed/flags/etc ANTES de Initialize
        beforeInitialize?.Invoke(instance);

        var defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

        // ✅ aquí metes cosas que NO quieres que Initialize pise (ej: escala final)
        afterInitialize?.Invoke(instance);

        // Recalcular obstáculos en aristas si procede
        var marker = instance.GetComponent<EdgeObstacleMarker>();
        if (marker != null)
        {
            CampGraphBuilder graph = FindObjectOfType<CampGraphBuilder>();
            if (graph != null)
            {
                Debug.Log("[DefensePlacer] Nueva defensa con obstáculo colocada. Recalculando obstáculos en las aristas...");
                graph.RecalculateObstaclesOnEdges();
            }
            else
            {
                Debug.LogWarning("[DefensePlacer] No se ha encontrado CampGraphBuilder al intentar recalcular obstáculos.");
            }
        }

        return instance;
    }
}
