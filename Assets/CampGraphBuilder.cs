using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor; // para Handles.Label
#endif

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

    [Header("Pesos de aristas")]
    [Tooltip("Factor de peso por cada metro de longitud de la arista.")]
    public float lengthWeight = 1f;

    [Tooltip("Factor de peso por inclinación (solo subida).")]
    public float slopeWeight = 5f;

    [Tooltip("Factor de peso según altura relativa del campamento destino (más bajo = más peso).")]
    public float heightWeight = 2f;

    [Tooltip("Penalización adicional por CADA obstáculo que atraviesa la arista.")]
    public float obstaclePenalty = 10f;

    [Header("Debug pesos")]
    public bool drawEdgeWeights = true;

    // Altura mínima/máxima de los nodos para normalizar
    private float minNodeHeight;
    private float maxNodeHeight;

    // ----------------- CLASES DEL GRAFO -----------------
    public class CampNode
    {
        public int id;
        public Vector3 position;
        public float height;
        public List<CampEdge> neighbors = new List<CampEdge>();

        // Potencial hacia la cima (coste mínimo acumulado hasta la cima)
        public float potential;
    }

    public class CampEdge
    {
        public CampNode from;
        public CampNode to;
        public float pathLength;
        public float heightDelta;
        public Vector3[] pathCorners;

        public bool hasObstacle;
        public ObstacleType obstacleType;

        // Cuántos obstáculos atraviesa esta arista
        public int obstacleCount;

        // Peso total de esta arista (coste)
        public float weight;
    }

    [HideInInspector] public List<CampNode> nodes = new List<CampNode>();
    [HideInInspector] public int finalDestinationNodeId = -1;

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

        // Inicializar min/max altura
        minNodeHeight = float.MaxValue;
        maxNodeHeight = float.MinValue;

        // 1) Crear nodos
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = campZoneFinder.campZones[i];
            float h = pos.y;

            CampNode node = new CampNode
            {
                id = i,
                position = pos,
                height = h,
                potential = float.PositiveInfinity
            };

            nodes.Add(node);

            if (h < minNodeHeight) minNodeHeight = h;
            if (h > maxNodeHeight) maxNodeHeight = h;
        }

        // 1b) Añadir cima
        if (finalDestination != null)
        {
            Vector3 pos = finalDestination.position;
            float h = pos.y;

            CampNode summitNode = new CampNode
            {
                id = nodes.Count,
                position = pos,
                height = h,
                potential = float.PositiveInfinity
            };

            nodes.Add(summitNode);
            finalDestinationNodeId = summitNode.id;

            if (h < minNodeHeight) minNodeHeight = h;
            if (h > maxNodeHeight) maxNodeHeight = h;

            Debug.Log("[CampGraphBuilder] Nodo extra añadido para FinalDestination con id " + finalDestinationNodeId);
        }

        // 2) Conectar nodos con NavMesh
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

                // A→B
                a.neighbors.Add(new CampEdge
                {
                    from = a,
                    to = b,
                    pathLength = length,
                    heightDelta = b.height - a.height,
                    pathCorners = cornersCopy,
                    hasObstacle = false,
                    obstacleType = ObstacleType.None,
                    obstacleCount = 0,
                    weight = 0f
                });

                // B→A
                b.neighbors.Add(new CampEdge
                {
                    from = b,
                    to = a,
                    pathLength = length,
                    heightDelta = a.height - b.height,
                    pathCorners = cornersCopy,
                    hasObstacle = false,
                    obstacleType = ObstacleType.None,
                    obstacleCount = 0,
                    weight = 0f
                });
            }
        }

        // 3) Limitar vecinos
        PruneNeighborsByDistance();

        // 4) Asociar obstáculos
        AutoRegisterObstaclesOnEdges();

        // 5) Recalcular pesos de todas las aristas
        RecalculateAllEdgeWeights();

        // 6) Calcular potencial de cada nodo hacia la cima
        ComputeNodePotentialsFromSummit();

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

    // ================== PESOS ==================

    private void RecalculateAllEdgeWeights()
    {
        if (nodes == null || nodes.Count == 0)
            return;

        foreach (var node in nodes)
        {
            foreach (var edge in node.neighbors)
            {
                edge.weight = CalculateEdgeWeight(edge);
            }
        }
    }

    private float CalculateEdgeWeight(CampEdge edge)
    {
        if (edge == null)
            return 0f;

        float w = 0f;

        // 1) Longitud
        w += edge.pathLength * lengthWeight;

        // 2) Inclinación (solo subida)
        float positiveSlope = 0f;
        if (edge.pathLength > 0.01f)
        {
            float climb = Mathf.Max(edge.heightDelta, 0f); // solo subida
            positiveSlope = climb / edge.pathLength;
        }
        w += positiveSlope * slopeWeight;

        // 3) Altura relativa del nodo destino (más alto = menos peso)
        if (maxNodeHeight > minNodeHeight + 0.01f)
        {
            float normalizedHeight = Mathf.InverseLerp(minNodeHeight, maxNodeHeight, edge.to.height);
            float heightCost = 1f - normalizedHeight; // más bajo = 1, más alto = 0
            w += heightCost * heightWeight;
        }

        // 4) Penalización por obstáculos: por cada obstáculo
        if (edge.obstacleCount > 0)
        {
            w += obstaclePenalty * edge.obstacleCount;
        }

        return w;
    }

    // ================== POTENCIAL HACIA LA CIMA ==================

    private void ComputeNodePotentialsFromSummit()
    {
        if (nodes == null || nodes.Count == 0)
            return;

        if (finalDestinationNodeId < 0 || finalDestinationNodeId >= nodes.Count)
        {
            Debug.LogWarning("[CampGraphBuilder] finalDestinationNodeId inválido. No se puede calcular potenciales.");
            // Potenciales a infinito
            foreach (var node in nodes)
            {
                node.potential = float.PositiveInfinity;
            }
            return;
        }

        // Inicializar potenciales
        foreach (var node in nodes)
        {
            node.potential = float.PositiveInfinity;
        }

        CampNode summitNode = nodes[finalDestinationNodeId];
        summitNode.potential = 0f;

        // Conjunto de nodos "no visitados"
        List<CampNode> unvisited = new List<CampNode>(nodes);

        while (unvisited.Count > 0)
        {
            // 1) Buscar el nodo no visitado con menor potencial
            CampNode current = null;
            float bestPot = float.PositiveInfinity;

            foreach (var n in unvisited)
            {
                if (n.potential < bestPot)
                {
                    bestPot = n.potential;
                    current = n;
                }
            }

            if (current == null || float.IsPositiveInfinity(current.potential))
            {
                // No quedan nodos alcanzables
                break;
            }

            unvisited.Remove(current);

            // 2) Relajar sus vecinos
            foreach (var edge in current.neighbors)
            {
                CampNode neighbor = edge.to;
                if (!unvisited.Contains(neighbor))
                    continue;

                float edgeCost = Mathf.Max(edge.weight, 0.01f);
                float newPotential = current.potential + edgeCost;

                if (newPotential < neighbor.potential)
                {
                    neighbor.potential = newPotential;
                }
            }
        }

        Debug.Log("[CampGraphBuilder] Potenciales hacia la cima calculados.");
    }

    public void RecalculateObstaclesOnEdges()
    {
        // limpiar todo antes
        foreach (var node in nodes)
        {
            foreach (var edge in node.neighbors)
            {
                edge.hasObstacle = false;
                edge.obstacleType = ObstacleType.None;
                edge.obstacleCount = 0;
            }
        }

        AutoRegisterObstaclesOnEdges();

        // actualizar pesos con la nueva info de obstáculos
        RecalculateAllEdgeWeights();

        // actualizar potenciales, ya que cambian los pesos
        ComputeNodePotentialsFromSummit();
    }

    // ----------------- ASOCIAR OBSTÁCULOS CON ARISTAS -----------------

    public void AutoRegisterObstaclesOnEdges()
    {
        EdgeObstacleMarker[] markers = FindObjectsOfType<EdgeObstacleMarker>();

        if (markers == null || markers.Length == 0)
        {
            Debug.Log("[CampGraphBuilder] No se han encontrado EdgeObstacleMarker en la escena.");
            return;
        }

        int edgesMarkedTotal = 0;

        foreach (var marker in markers)
        {
            if (marker == null || marker.Obstacle == null)
                continue;

            Vector3 obstaclePos = marker.transform.position;
            float radius = marker.obstacleRadius;
            ObstacleType type = marker.Obstacle.obstacleType;

            // 1) PRIMER PASO: encontrar la distancia mínima (mejor arista)
            float bestDistance = float.MaxValue;

            foreach (var node in nodes)
            {
                foreach (var edge in node.neighbors)
                {
                    if (edge.pathCorners == null || edge.pathCorners.Length < 2)
                        continue;

                    float d = DistancePointToPath(obstaclePos, edge.pathCorners);
                    if (d < bestDistance)
                        bestDistance = d;
                }
            }

            // Si la mejor arista está más lejos que el radio, no marcamos nada
            if (bestDistance > radius)
            {
                if (marker.debugLog)
                {
                    Debug.Log($"[CampGraphBuilder] Obstacle '{marker.name}' está demasiado lejos de cualquier arista. bestDist={bestDistance:F2}, radius={radius:F2}");
                }
                continue;
            }

            // 2) DEFINIR UMBRAL ESTRECHO ALREDEDOR DE ESA MEJOR DISTANCIA
            float extraTolerance = radius * 0.3f;
            float maxDistToMark = Mathf.Min(radius, bestDistance + extraTolerance);

            int edgesMarkedForThisMarker = 0;

            // 3) SEGUNDO PASO: marcar solo las aristas dentro de ese umbral
            foreach (var node in nodes)
            {
                foreach (var edge in node.neighbors)
                {
                    // Procesar cada camino solo una vez (A->B, no B->A de nuevo)
                    if (edge.from.id > edge.to.id)
                        continue;

                    if (edge.pathCorners == null || edge.pathCorners.Length < 2)
                        continue;

                    float d = DistancePointToPath(obstaclePos, edge.pathCorners);
                    if (d <= maxDistToMark)
                    {
                        // Dirección A → B
                        edge.hasObstacle = true;
                        edge.obstacleType = type;
                        edge.obstacleCount++;

                        // Dirección B → A
                        CampEdge reverse = edge.to.neighbors.Find(e => e.to == edge.from);
                        if (reverse != null)
                        {
                            reverse.hasObstacle = true;
                            reverse.obstacleType = type;
                            reverse.obstacleCount++;
                        }

                        edgesMarkedForThisMarker += 2;
                        edgesMarkedTotal += 2;
                    }
                }
            }

            if (marker.debugLog)
            {
                Debug.Log($"[CampGraphBuilder] Obstacle '{marker.name}' ({type}) asignado a {edgesMarkedForThisMarker} aristas. bestDist={bestDistance:F2}, maxDistToMark={maxDistToMark:F2}, radius={radius:F2}");
            }
        }

        Debug.Log($"[CampGraphBuilder] Asociación de obstáculos completada. Aristas marcadas: {edgesMarkedTotal}");
    }

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

    private float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector3 closest = a + ab * t;
        return Vector3.Distance(point, closest);
    }

    private void OnDrawGizmosSelected()
    {
        if (nodes == null || nodes.Count == 0)
            return;

        // Aristas normales (sin obstáculo)
        if (drawConnections)
        {
            Gizmos.color = connectionColor;

            foreach (var node in nodes)
            {
                foreach (var edge in node.neighbors)
                {
                    if (drawObstacleEdges && edge.hasObstacle)
                        continue;

                    Vector3 from = edge.from.position + Vector3.up * 0.1f;
                    Vector3 to = edge.to.position + Vector3.up * 0.1f;

                    Gizmos.DrawLine(from, to);

#if UNITY_EDITOR
                    if (drawEdgeWeights && edge.from.id < edge.to.id)
                    {
                        Vector3 mid = (from + to) * 0.5f + Vector3.up * 0.2f;
                        Handles.Label(mid, edge.weight.ToString("F1"));
                    }
#endif
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

#if UNITY_EDITOR
                    if (drawEdgeWeights && edge.from.id < edge.to.id)
                    {
                        Vector3 mid = (from + to) * 0.5f + Vector3.up * 0.25f;
                        Handles.Label(mid, edge.weight.ToString("F1"));
                    }
#endif
                }
            }
        }
    }
}
