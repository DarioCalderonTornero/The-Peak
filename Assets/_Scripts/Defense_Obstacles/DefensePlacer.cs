using UnityEngine;

public class DefensePlacer : MonoBehaviour
{
    public static DefensePlacer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    // Versión antigua (por si algún código la usa todavía)
    // Usa rotación identidad por defecto
    public GameObject PlaceDefense(GameObject prefab, Vector3 position)
    {
        return PlaceDefense(prefab, position, Quaternion.identity);
    }

    // NUEVA: recibe la rotación YA calculada (por ejemplo desde DragCardUI)
    public GameObject PlaceDefense(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instance = Instantiate(prefab, position, rotation);

        var defense = instance.GetComponent<BaseDefense>();
        if (defense != null)
        {
            defense.Initialize();
        }

        // Si esta defensa tiene un EdgeObstacleMarker, solo recalculamos obstáculos en el grafo,
        // NO reconstruimos todo el grafo para no invalidar las referencias de los escaladores.
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
