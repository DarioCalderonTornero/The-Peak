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

    [Tooltip("Dibujar en rojo las aristas que tengan algún obstáculo asociado.")]
    public bool drawObstacleEdges = true;
    public Color obstacleEdgeColor = Color.red;

    // ----------------- CLASES DEL GRAFO -----------------
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

        // 🔹 NUEVO: información de obstáculo en este camino
        public bool hasObstacle;
        public ObstacleType obstacleType;
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
                    pathCorners = cornersCopy,
                    hasObstacle = false,
                    obstacleType = ObstacleType.None
                });

                // Crear conexión B→A
                b.neighbors.Add(new CampEdge
                {
                    from = b,
                    to = a,
                    pathLength = length,
                    heightDelta = a.height - b.height,
                    pathCorners = cornersCopy,
                    hasObstacle = false,
                    obstacleType = ObstacleType.None
                });
            }
        }

        // 3) Limitar vecinos a los caminos más cercanos
        PruneNeighborsByDistance();

        // 4) Asociar obstáculos a aristas (EdgeObstacleMarker)
        AutoRegisterObstaclesOnEdges();

        Debug.Log("[CampGraphBuilder] Grafo completado. (Nodos totales: " + nodes.Count +
                  ", Máx vecinos por nodo: " + maxNeighborsPerNode + ")");
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

            node.neighbors.Sort((a, b) => a.pathLength.CompareTo(b.pathLength));

            if (node.neighbors.Count > maxNeighborsPerNode)
            {
                node.neighbors.RemoveRange(maxNeighborsPerNode, node.neighbors.Count - maxNeighborsPerNode);
            }
        }
    }

    // ----------------- NUEVO: ASOCIAR OBSTÁCULOS A ARISTAS -----------------

    private void AutoRegisterObstaclesOnEdges()
    {
        EdgeObstacleMarker[] markers = FindObjectsOfType<EdgeObstacleMarker>();

        if (markers == null || markers.Length == 0)
        {
            Debug.Log("[CampGraphBuilder] No se han encontrado EdgeObstacleMarker en la escena.");
            return;
        }

        int linksCount = 0;

        foreach (var marker in markers)
        {
            if (marker == null || marker.Obstacle == null)
                continue;

            Vector3 obstaclePos = marker.transform.position;
            float radius = marker.obstacleRadius;
            ObstacleType type = marker.Obstacle.obstacleType;

            // Buscar la arista cuyo camino pasa más cerca de esta roca
            CampEdge bestEdge = null;
            float bestDistance = float.MaxValue;

            foreach (var node in nodes)
            {
                foreach (var edge in node.neighbors)
                {
                    if (edge.pathCorners == null || edge.pathCorners.Length < 2)
                        continue;

                    float d = DistancePointToPath(obstaclePos, edge.pathCorners);

                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        bestEdge = edge;
                    }
                }
            }

            // Si la distancia mínima es menor o igual al radio, consideramos que la roca bloquea ese camino
            if (bestEdge != null && bestDistance <= radius)
            {
                bestEdge.hasObstacle = true;
                bestEdge.obstacleType = type;
                linksCount++;

                if (marker.debugLog)
                {
                    Debug.Log($"[CampGraphBuilder] Obstacle '{marker.name}' ({type}) asignado a edge {bestEdge.from.id} -> {bestEdge.to.id} (dist {bestDistance:F2}, radius {radius:F2})");
                }
            }
            else if (marker.debugLog)
            {
                Debug.LogWarning($"[CampGraphBuilder] No se ha encontrado ninguna arista cercana para el obstáculo '{marker.name}'. Distancia mínima: {bestDistance:F2}, radius: {radius:F2}");
            }
        }

        Debug.Log($"[CampGraphBuilder] Asociación de obstáculos completada. Aristas marcadas: {linksCount}");
    }

    /// <summary>
    /// Distancia mínima entre un punto y una polilínea (array de corners).
    /// </summary>
    private float DistancePointToPath(Vector3 point, Vector3[] corners)
    {
        float minDist = float.MaxValue;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            float d = DistancePointToSegment(point, corners[i], corners[i + 1]);
            if (d < minDist)
                minDist = d;
        }

        return minDist;
    }

    /// <summary>
    /// Distancia de un punto a un segmento (3D).
    /// </summary>
    private float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector3 closest = a + ab * t;
        return Vector3.Distance(point, closest);
    }

    // ----------------- GIZMOS -----------------

    private void OnDrawGizmosSelected()
    {
        if (nodes == null || nodes.Count == 0)
            return;

        // Aristas normales
        if (drawConnections)
        {
            Gizmos.color = connectionColor;

            foreach (var node in nodes)
            {
                foreach (var edge in node.neighbors)
                {
                    if (drawObstacleEdges && edge.hasObstacle)
                        continue; // las rojas se dibujan aparte

                    Vector3 from = edge.from.position + Vector3.up * 0.1f;
                    Vector3 to = edge.to.position + Vector3.up * 0.1f;

                    Gizmos.DrawLine(from, to);
                }
            }
        }

        // Aristas con obstáculo (rojas)
        if (drawObstacleEdges)
        {
            Gizmos.color = obstacleEdgeColor;

            foreach (var node in nodes)
            {
                foreach (var edge in node.neighbors)
                {
                    if (!edge.hasObstacle)
                        continue;

                    Vector3 from = edge.from.position + Vector3.up * 0.15f;
                    Vector3 to = edge.to.position + Vector3.up * 0.15f;

                    Gizmos.DrawLine(from, to);
                }
            }
        }
    }
}
