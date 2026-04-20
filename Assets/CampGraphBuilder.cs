using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(50)]
public class CampGraphBuilder : MonoBehaviour
{
    // Reemplazamos el Finder por una referencia opcional si quieres seguir usándolo, 
    // pero la prioridad será encontrar objetos en la escena.
    public Transform finalDestination;
    [Min(1)] public int maxNeighborsPerNode = 3;
    public bool drawConnections = true;
    public Color connectionColor = Color.yellow;
    public bool drawObstacleEdges = true;
    public Color obstacleEdgeColor = Color.red;
    public float lengthWeight = 1f;
    public float slopeWeight = 5f;
    public float heightWeight = 2f;
    public float obstaclePenalty = 10f;
    public bool drawEdgeWeights = true;
    public bool debugDrawStepsToSummit = false;

    private float minNodeHeight;
    private float maxNodeHeight;

    public class CampNode
    {
        public int id;
        public Vector3 position;
        public float height;
        public int stepsToSummit = 9999;
        public List<CampEdge> neighbors = new List<CampEdge>();

        public GameObject instantiatedTent;
        public int occupantsCount = 0;
        public List<ClimberMovement> presentClimbers = new List<ClimberMovement>();

        public bool HasTent => instantiatedTent != null;
    }

    public class CampEdge
    {
        public CampNode from, to;
        public float pathLength, heightDelta;
        public Vector3[] pathCorners;
        public bool hasObstacle;
        public ObstacleType obstacleType;
        public int obstacleCount;
        public float weight;
    }

    [HideInInspector] public List<CampNode> nodes = new List<CampNode>();
    [HideInInspector] public int finalDestinationNodeId = -1;

    [Header("🔹 Camp Collision")]
    [SerializeField] private string campLayerName = "Campamentos";
    [SerializeField] private float campCollisionRadius = 2f;
    private readonly List<GameObject> campCollisionObjects = new List<GameObject>();

    private void Start() => BuildGraph();

    public void BuildGraph()
    {
        nodes.Clear();
        finalDestinationNodeId = -1;
        minNodeHeight = float.MaxValue;
        maxNodeHeight = float.MinValue;

        // --- NUEVA LÓGICA: BUSCAR POR COMPONENTE EN LA ESCENA ---
        CampLocation[] foundLocations = FindObjectsOfType<CampLocation>();

        for (int i = 0; i < foundLocations.Length; i++)
        {
            Vector3 pos = foundLocations[i].transform.position + new Vector3(0, 2.9f, 0f);

            // Ajustamos al NavMesh la posición ya desplazada
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            nodes.Add(new CampNode { id = i, position = pos, height = pos.y });
            minNodeHeight = Mathf.Min(minNodeHeight, pos.y);
            maxNodeHeight = Mathf.Max(maxNodeHeight, pos.y);
        }
        // -------------------------------------------------------

        if (finalDestination != null)
        {
            CampNode summit = new CampNode { id = nodes.Count, position = finalDestination.position, height = finalDestination.position.y };
            nodes.Add(summit);
            finalDestinationNodeId = summit.id;
            minNodeHeight = Mathf.Min(minNodeHeight, summit.height);
            maxNodeHeight = Mathf.Max(maxNodeHeight, summit.height);
        }

        if (nodes.Count < 2) return;

        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (NavMesh.CalculatePath(nodes[i].position, nodes[j].position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    float len = CalculatePathLength(path.corners);
                    Vector3[] corners = (Vector3[])path.corners.Clone();
                    nodes[i].neighbors.Add(new CampEdge { from = nodes[i], to = nodes[j], pathLength = len, heightDelta = nodes[j].height - nodes[i].height, pathCorners = corners });
                    nodes[j].neighbors.Add(new CampEdge { from = nodes[j], to = nodes[i], pathLength = len, heightDelta = nodes[i].height - nodes[j].height, pathCorners = corners });
                }
            }
        }

        PruneNeighborsByDistance();
        AutoRegisterObstaclesOnEdges();
        RecalculateAllEdgeWeights();
        CalculateStepsToSummit();
        CreateCampCollisionObjects();

        Debug.Log($"[CampGraphBuilder] Grafo construido con {nodes.Count} nodos desde módulos.");
    }

    // El resto de funciones (CreateCampCollisionObjects, CalculateStepsToSummit, etc.) 
    // permanecen exactamente igual para mantener la funcionalidad original.

    private void CreateCampCollisionObjects()
    {
        ClearCampCollisionObjects();
        int layer = LayerMask.NameToLayer(campLayerName);
        if (layer == -1) return;
        foreach (var node in nodes)
        {
            if (node.id == finalDestinationNodeId) continue;
            GameObject go = new GameObject("Camp_INVISIBLE");
            go.transform.position = node.position;
            go.layer = layer;
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<SphereCollider>().isTrigger = true;
            go.AddComponent<SphereCollider>().radius = campCollisionRadius;
            campCollisionObjects.Add(go);
        }
    }

    private void ClearCampCollisionObjects()
    {
        foreach (var go in campCollisionObjects) if (go != null) DestroyImmediate(go);
        campCollisionObjects.Clear();
        GameObject[] leftovers = GameObject.FindObjectsOfType<GameObject>(true);
        foreach (var o in leftovers) if (o.name == "Camp_INVISIBLE") DestroyImmediate(o);
    }

    public void RecalculateObstaclesOnEdges()
    {
        foreach (var n in nodes) foreach (var e in n.neighbors) { e.hasObstacle = false; e.obstacleCount = 0; }
        AutoRegisterObstaclesOnEdges(); RecalculateAllEdgeWeights();
    }

    public void CalculateStepsToSummit()
    {
        if (finalDestinationNodeId == -1) return;
        foreach (var n in nodes) n.stepsToSummit = 9999;
        CampNode summit = nodes.Find(n => n.id == finalDestinationNodeId);
        if (summit == null) return;
        Queue<CampNode> q = new Queue<CampNode>(); summit.stepsToSummit = 0; q.Enqueue(summit);
        while (q.Count > 0)
        {
            CampNode c = q.Dequeue();
            foreach (var e in c.neighbors) if (e.to.stepsToSummit > c.stepsToSummit + 1) { e.to.stepsToSummit = c.stepsToSummit + 1; q.Enqueue(e.to); }
        }
    }

    private float CalculatePathLength(Vector3[] c) { float l = 0; for (int i = 0; i < c.Length - 1; i++) l += Vector3.Distance(c[i], c[i + 1]); return l; }

    private void PruneNeighborsByDistance()
    {
        foreach (var n in nodes) if (n.neighbors.Count > maxNeighborsPerNode)
            {
                n.neighbors.Sort((a, b) => a.pathLength.CompareTo(b.pathLength));
                n.neighbors.RemoveRange(maxNeighborsPerNode, n.neighbors.Count - maxNeighborsPerNode);
            }
    }

    private void RecalculateAllEdgeWeights()
    {
        foreach (var n in nodes) foreach (var e in n.neighbors)
            {
                float w = e.pathLength * lengthWeight;
                if (e.pathLength > 0.01f) w += (Mathf.Max(e.heightDelta, 0f) / e.pathLength) * slopeWeight;
                if (maxNodeHeight > minNodeHeight + 0.01f) w += (1f - Mathf.InverseLerp(minNodeHeight, maxNodeHeight, e.to.height)) * heightWeight;
                w += e.obstacleCount * obstaclePenalty; e.weight = w;
            }
    }

    public void AutoRegisterObstaclesOnEdges()
    {
        EdgeObstacleMarker[] ms = FindObjectsOfType<EdgeObstacleMarker>();
        if (ms == null) return;
        foreach (var m in ms)
        {
            if (m == null || m.Obstacle == null) continue;
            foreach (var n in nodes) foreach (var e in n.neighbors)
                {
                    if (e.from.id < e.to.id && e.pathCorners != null && DistancePointToPath(m.transform.position, e.pathCorners) <= m.obstacleRadius)
                    {
                        e.hasObstacle = true; e.obstacleCount++;
                        CampEdge rev = e.to.neighbors.Find(x => x.to == e.from);
                        if (rev != null) { rev.hasObstacle = true; rev.obstacleCount++; }
                    }
                }
        }
    }

    private float DistancePointToPath(Vector3 p, Vector3[] c)
    {
        float min = float.MaxValue;
        for (int i = 0; i < c.Length - 1; i++)
        {
            Vector3 ab = c[i + 1] - c[i];
            float t = Mathf.Clamp01(Vector3.Dot(p - c[i], ab) / ab.sqrMagnitude);
            min = Mathf.Min(min, Vector3.Distance(p, c[i] + ab * t));
        }
        return min;
    }

    private void OnDrawGizmosSelected()
    {
        if (nodes == null) return;
        foreach (var n in nodes) foreach (var e in n.neighbors)
            {
                if (e.from.id > e.to.id) continue;
                bool isObs = e.hasObstacle && drawObstacleEdges;
                if (!drawConnections && !isObs) continue;
                Gizmos.color = isObs ? obstacleEdgeColor : connectionColor;
                Gizmos.DrawLine(e.from.position + Vector3.up * 0.2f, e.to.position + Vector3.up * 0.2f);
#if UNITY_EDITOR
                if (drawEdgeWeights) Handles.Label(Vector3.Lerp(e.from.position, e.to.position, 0.5f) + Vector3.up * 0.5f, debugDrawStepsToSummit ? $"S:{e.to.stepsToSummit}" : e.weight.ToString("F1"));
#endif
            }
    }
}