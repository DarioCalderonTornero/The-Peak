using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(50)]
public class CampGraphBuilder : MonoBehaviour
{
    [Header("Referencia al detector de campamentos")]
    public NavMeshCampZoneFinder campZoneFinder;

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
    }

    // Oculto para evitar freeze del inspector al seleccionarlo
    [HideInInspector]
    public List<CampNode> nodes = new List<CampNode>();

    private void Start()
    {
        BuildGraph();
    }

    public void BuildGraph()
    {
        nodes.Clear();

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
        Debug.Log("[CampGraphBuilder] Construyendo grafo con " + count + " campamentos.");

        // 1) Crear nodos del grafo
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

                // Crear conexión A→B
                a.neighbors.Add(new CampEdge
                {
                    from = a,
                    to = b,
                    pathLength = length,
                    heightDelta = b.height - a.height,
                    pathCorners = (Vector3[])navPath.corners.Clone()
                });

                // Crear conexión B→A
                b.neighbors.Add(new CampEdge
                {
                    from = b,
                    to = a,
                    pathLength = length,
                    heightDelta = a.height - b.height,
                    pathCorners = (Vector3[])navPath.corners.Clone()
                });
            }
        }

        Debug.Log("[CampGraphBuilder] Grafo completado.");
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

    // Dibuja las líneas SOLO al seleccionar el objeto
    private void OnDrawGizmosSelected()
    {
        if (!drawConnections || nodes == null || nodes.Count == 0)
            return;

        Gizmos.color = connectionColor;

        foreach (var node in nodes)
        {
            foreach (var edge in node.neighbors)
            {
                Vector3 from = edge.from.position + Vector3.up * 0.1f;
                Vector3 to = edge.to.position + Vector3.up * 0.1f;

                Gizmos.DrawLine(from, to);
            }
        }
    }
}
