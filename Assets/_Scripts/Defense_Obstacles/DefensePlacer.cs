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

    // NUEVA: recibe la rotación YA calculada
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return PlaceDefense(prefab, position, rotation, null);
    }

    // ✅ NUEVA: permite configurar la instancia ANTES de Initialize()
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> beforeInitialize)
    {
        GameObject instance = Instantiate(prefab, position, rotation);

        // 👇 aquí metes el seed / flags / lo que quieras ANTES de Initialize
        beforeInitialize?.Invoke(instance);

        var defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

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
