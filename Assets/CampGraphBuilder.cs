using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// CampGraphBuilder
/// ----------------
/// Construye un grafo a partir de las regiones planas detectadas por CampRegionDetector.
/// Cada región es un nodo (centroide). Se conecta con sus vecinos más cercanos,
/// evitando colisiones y penalizando descensos. Calcula el camino más corto entre
/// el campamento más bajo y el más alto.
/// </summary>
public class CampGraphBuilder : MonoBehaviour
{
    [Header("Referencias")]
    public CampRegionDetector regionDetector;
    public LayerMask terrainLayer;

    [Header("Parámetros del grafo")]
    public int kNearest = 5;
    public KeyCode buildKey = KeyCode.B;

    [Header("Visualización")]
    public bool drawConnections = true;
    public bool drawBestPath = true;

    private List<Node> nodes = new List<Node>();
    private List<(Node, Node)> edges = new List<(Node, Node)>();
    private List<Node> shortestPath = new List<Node>();

    private void Update()
    {
        if (Input.GetKeyDown(buildKey))
        {
            BuildGraph();
            shortestPath = ComputeShortestPath();
        }
    }

    // Nodo del grafo = región de campamento
    [System.Serializable]
    public class Node
    {
        public Vector3 position;
        public List<Edge> connections = new List<Edge>();
    }

    [System.Serializable]
    public class Edge
    {
        public Node target;
        public float cost;
    }

    private void BuildGraph()
    {
        nodes.Clear();
        edges.Clear();

        if (regionDetector == null)
        {
            Debug.LogError("[CampGraphBuilder] Falta referencia a CampRegionDetector.");
            return;
        }

        var regions = regionDetector.GetType()
            .GetField("regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(regionDetector) as List<CampRegionDetector.CampRegion>;

        if (regions == null || regions.Count == 0)
        {
            Debug.LogWarning("[CampGraphBuilder] No hay regiones detectadas (pulsa C primero).");
            return;
        }

        // Crear nodos (uno por región)
        foreach (var r in regions)
        {
            nodes.Add(new Node { position = r.centroid });
        }

        // Conectar con vecinos más cercanos
        foreach (var nodeA in nodes)
        {
            var neighbors = nodes
                .Where(n => n != nodeA)
                .OrderBy(n => Vector3.Distance(nodeA.position, n.position))
                .Take(kNearest);

            foreach (var nodeB in neighbors)
            {
                // Línea de visión: sin atravesar montaña
                if (Physics.Linecast(nodeA.position + Vector3.up * 0.5f,
                                     nodeB.position + Vector3.up * 0.5f,
                                     out RaycastHit hit,
                                     terrainLayer))
                {
                    continue; // si hay colisión, no conectar
                }

                float distancia = Vector3.Distance(nodeA.position, nodeB.position);
                float alturaDiff = nodeB.position.y - nodeA.position.y;

                // Penaliza descender
                float penalizacion = alturaDiff < 0 ? Mathf.Abs(alturaDiff) * 3f : 0f;
                float cost = distancia + penalizacion;

                nodeA.connections.Add(new Edge { target = nodeB, cost = cost });
                edges.Add((nodeA, nodeB));
            }
        }

        Debug.Log($"[CampGraphBuilder] Grafo creado con {nodes.Count} nodos y {edges.Count} conexiones válidas.");
    }

    private List<Node> ComputeShortestPath()
    {
        if (nodes.Count == 0)
        {
            Debug.LogWarning("[CampGraphBuilder] No hay nodos para calcular camino.");
            return new List<Node>();
        }

        // Nodo más bajo y más alto
        Node start = nodes.OrderBy(n => n.position.y).First();
        Node goal = nodes.OrderByDescending(n => n.position.y).First();

        Dictionary<Node, float> dist = new Dictionary<Node, float>();
        Dictionary<Node, Node> prev = new Dictionary<Node, Node>();
        HashSet<Node> unvisited = new HashSet<Node>(nodes);

        foreach (var n in nodes)
            dist[n] = Mathf.Infinity;
        dist[start] = 0f;

        while (unvisited.Count > 0)
        {
            var current = unvisited.OrderBy(n => dist[n]).First();
            unvisited.Remove(current);
            if (current == goal) break;

            foreach (var e in current.connections)
            {
                float alt = dist[current] + e.cost;
                if (alt < dist[e.target])
                {
                    dist[e.target] = alt;
                    prev[e.target] = current;
                }
            }
        }

        // reconstrucción del camino
        List<Node> path = new List<Node>();
        var u = goal;
        while (prev.ContainsKey(u))
        {
            path.Insert(0, u);
            u = prev[u];
        }
        path.Insert(0, start);

        Debug.Log($"[CampGraphBuilder] Camino más corto: {path.Count} nodos, coste total {dist[goal]:F2}");
        return path;
    }

    private void OnDrawGizmos()
    {
        if (edges == null || nodes == null) return;

        if (drawConnections)
        {
            Gizmos.color = Color.red;
            foreach (var (a, b) in edges)
                Gizmos.DrawLine(a.position, b.position);
        }

        if (drawBestPath && shortestPath != null && shortestPath.Count > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < shortestPath.Count - 1; i++)
                Gizmos.DrawLine(shortestPath[i].position, shortestPath[i + 1].position);
        }
    }
}
