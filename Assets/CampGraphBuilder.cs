using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(50)]
public class CampGraphBuilder : MonoBehaviour
{
    [Header("Referencia al detector de campamentos")]
    public NavMeshCampZoneFinder campZoneFinder;

    [Header("Destino final como campamento")]
    [Tooltip("Destino final (cima) que se tratará como un campamento más en el grafo.")]
    public Transform finalDestination;

    [Header("Conectividad del grafo")]
    [Tooltip("Número máximo de vecinos por campamento. Se quedará con los caminos más cortos.")]
    [Min(1)]
    public int maxNeighborsPerNode = 3;

    [Header("Debug")]
    public bool drawConnections = true;
    public Color connectionColor = Color.yellow;

    // Ya NO son serializables → el inspector no intenta dibujarlos
    public class CampNode
    {
        public int id;
        public Vector3 position;
        public float height;
        public List<CampEdge> neighbors = new List<CampEdge>();
    }

    public class CampEdge
    {
        public CampNode from;
        public CampNode to;
        public float pathLength;
        public float heightDelta;
        public Vector3[] pathCorners;

        // Obstáculos que afectan a este edge (rocas, hielo, etc.)
        public List<EdgeObstacleMarker> blockingObstacles = new List<EdgeObstacleMarker>();
    }

    [HideInInspector]
    public List<CampNode> nodes = new List<CampNode>();

    [HideInInspector]
    public int finalDestinationNodeId = -1; // id del nodo que representa la cima (si existe)

    private void Start()
    {
        BuildGraph();
    }

    public void BuildGraph()
    {
        nodes.Clear();
        finalDestinationNodeId = -1;

        if (campZoneFinder == null)
        {
            Debug.LogError("[CampGraphBuilder] campZoneFinder es NULL.");
            return;
        }

        if (campZoneFinder.campZones == null || campZoneFinder.campZones.Count == 0)
        {
            Debug.LogWarning("[CampGraphBuilder] campZoneFinder.campZones está vacío.");
            return;
        }

        int count = campZoneFinder.campZones.Count;
        Debug.Log("[CampGraphBuilder] Construyendo grafo con " + count + " campamentos base.");

        // 1) Crear nodos del grafo a partir de campZones
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = campZoneFinder.campZones[i];

            CampNode node = new CampNode
            {
                id = i,
                position = pos,
                height = pos.y
            };

            nodes.Add(node);
        }

        // 1b) Añadir la cima como campamento extra (si se ha asignado)
        if (finalDestination != null)
        {
            Vector3 pos = finalDestination.position;

            CampNode summitNode = new CampNode
            {
                id = nodes.Count,
                position = pos,
                height = pos.y
            };

            nodes.Add(summitNode);
            finalDestinationNodeId = summitNode.id;

            Debug.Log("[CampGraphBuilder] Nodo extra añadido para FinalDestination con id " + finalDestinationNodeId);
        }

        // 2) Conectar nodos físicamente usando NavMesh.CalculatePath
        NavMeshPath navPath = new NavMeshPath();

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                CampNode a = nodes[i];
                CampNode b = nodes[j];

                bool success = NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, navPath);

                if (!success || navPath.status != NavMeshPathStatus.PathComplete)
                    continue;

                float length = CalculatePathLength(navPath.corners);
                var cornersCopy = (Vector3[])navPath.corners.Clone();

                // Crear conexión A→B
                a.neighbors.Add(new CampEdge
                {
                    from = a,
                    to = b,
                    pathLength = length,
                    heightDelta = b.height - a.height,
                    pathCorners = cornersCopy
                });

                // Crear conexión B→A
                b.neighbors.Add(new CampEdge
                {
                    from = b,
                    to = a,
                    pathLength = length,
                    heightDelta = a.height - b.height,
                    pathCorners = cornersCopy
                });
            }
        }

        // 3) Limitar vecinos a los caminos más cercanos
        PruneNeighborsByDistance();

        Debug.Log("[CampGraphBuilder] Grafo completado. (Nodos totales: " + nodes.Count +
                  ", Máx vecinos por nodo: " + maxNeighborsPerNode + ")");

        // 4) Auto-asociar obstáculos que ya estén en escena (rocas precolocadas)
        AutoRegisterObstaclesOnEdges();
    }

    private float CalculatePathLength(Vector3[] corners)
    {
        if (corners == null || corners.Length < 2)
            return 0f;

        float length = 0f;
        for (int i = 0; i < corners.Length - 1; i++)
        {
            length += Vector3.Distance(corners[i], corners[i + 1]);
        }
        return length;
    }

    /// <summary>
    /// Para cada nodo, ordena sus vecinos por longitud de camino y se queda solo con los más cercanos.
    /// </summary>
    private void PruneNeighborsByDistance()
    {
        foreach (var node in nodes)
        {
            if (node.neighbors == null || node.neighbors.Count <= maxNeighborsPerNode)
                continue;

            // Ordenar caminos por longitud (de menor a mayor)
            node.neighbors.Sort((a, b) => a.pathLength.CompareTo(b.pathLength));

            // Si hay más vecinos que el máximo, eliminar los últimos (los más lejanos)
            if (node.neighbors.Count > maxNeighborsPerNode)
            {
                node.neighbors.RemoveRange(maxNeighborsPerNode, node.neighbors.Count - maxNeighborsPerNode);
            }
        }
    }

    /// <summary>
    /// Permite a un EdgeObstacleMarker registrar que está bloqueando un edge concreto.
    /// </summary>
    public void RegisterObstacleOnEdge(EdgeObstacleMarker marker, CampEdge edge)
    {
        if (marker == null || edge == null)
            return;

        if (edge.blockingObstacles == null)
            edge.blockingObstacles = new List<EdgeObstacleMarker>();

        if (!edge.blockingObstacles.Contains(marker))
        {
            edge.blockingObstacles.Add(marker);
            Debug.Log($"[CampGraphBuilder] Obstacle '{marker.name}' registrado en edge " +
                      $"from node {edge.from.id} to node {edge.to.id} (type={marker.obstacleType}).");
        }
    }

    /// <summary>
    /// Busca todos los EdgeObstacleMarker que ya existan en escena
    /// y los asocia al edge más cercano. Útil si tienes rocas precolocadas.
    /// (Para las rocas instanciadas en partida, se encargan ellas mismas en su Start()).
    /// </summary>
    private void AutoRegisterObstaclesOnEdges()
    {
        // Limpiamos bindings previos por si se reconstruye el grafo
        foreach (var node in nodes)
        {
            if (node.neighbors == null) continue;
            foreach (var edge in node.neighbors)
            {
                if (edge.blockingObstacles != null)
                    edge.blockingObstacles.Clear();
            }
        }

        // Unity API nueva
        EdgeObstacleMarker[] markers =
            Object.FindObjectsByType<EdgeObstacleMarker>(FindObjectsSortMode.None);

        if (markers == null || markers.Length == 0)
        {
            Debug.Log("[CampGraphBuilder] No se han encontrado EdgeObstacleMarker en la escena.");
            return;
        }

        foreach (var marker in markers)
        {
            if (marker == null) continue;
            marker.TryBindToClosestEdge(this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawConnections || nodes == null || nodes.Count == 0)
            return;

        Gizmos.color = connectionColor;

        foreach (var node in nodes)
        {
            if (node.neighbors == null) continue;

            foreach (var edge in node.neighbors)
            {
                Vector3 from = edge.from.position + Vector3.up * 0.1f;
                Vector3 to = edge.to.position + Vector3.up * 0.1f;

                Gizmos.DrawLine(from, to);
            }
        }
    }
}
