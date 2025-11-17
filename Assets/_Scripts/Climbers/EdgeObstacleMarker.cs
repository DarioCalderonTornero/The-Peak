using System.Collections;
using UnityEngine;

public class EdgeObstacleMarker : MonoBehaviour
{
    [Header("Obstacle")]
    public ObstacleType obstacleType = ObstacleType.None;

    [Header("Binding con el grafo")]
    [Tooltip("Distancia máxima desde el camino (edge) para que este obstáculo se considere asociado.")]
    public float maxBindDistance = 8f;

    [Tooltip("Mostrar información de binding en la consola.")]
    public bool debugBinding = true;

    [Header("Auto binding")]
    [Tooltip("Intentar asociarse automáticamente al grafo en Start().")]
    public bool autoBindOnStart = true;

    [Tooltip("Si el grafo aún no está listo al primer intento, reintentar varias veces.")]
    public bool retryIfGraphNotReady = true;

    [Tooltip("Número máximo de reintentos si el grafo todavía no está preparado.")]
    public int maxRetries = 10;

    [Tooltip("Tiempo entre reintentos (segundos).")]
    public float retryDelay = 0.25f;

    private bool _isBound = false;

    private void Start()
    {
        if (autoBindOnStart)
        {
            StartCoroutine(AutoBindRoutine());
        }
    }

    private IEnumerator AutoBindRoutine()
    {
        int attempts = 0;

        while (!_isBound && attempts < maxRetries)
        {
            CampGraphBuilder graph = Object.FindFirstObjectByType<CampGraphBuilder>();

            if (graph != null && graph.nodes != null && graph.nodes.Count > 0)
            {
                TryBindToClosestEdge(graph);

                if (_isBound)
                    yield break;
            }

            attempts++;

            if (!retryIfGraphNotReady)
                break;

            yield return new WaitForSeconds(retryDelay);
        }

        if (!_isBound && debugBinding)
        {
            Debug.LogWarning($"[EdgeObstacleMarker] {name}: No se pudo hacer binding después de {attempts} intentos.");
        }
    }

    public ObstacleType GetObstacleType()
    {
        return obstacleType;
    }

    public void TryBindToClosestEdge(CampGraphBuilder graph)
    {
        if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
        {
            if (debugBinding)
                Debug.LogWarning($"[EdgeObstacleMarker] {name}: Grafo nulo o sin nodos. No se puede hacer binding.");
            return;
        }

        CampGraphBuilder.CampEdge bestEdge = null;
        float bestDist = float.MaxValue;

        Vector3 point = transform.position;

        foreach (var node in graph.nodes)
        {
            if (node.neighbors == null)
                continue;

            foreach (var edge in node.neighbors)
            {
                if (edge == null || edge.pathCorners == null || edge.pathCorners.Length < 2)
                    continue;

                float dist = GetMinDistanceToPathXZ(point, edge.pathCorners);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestEdge = edge;
                }
            }
        }

        if (bestEdge == null)
        {
            if (debugBinding)
                Debug.LogWarning($"[EdgeObstacleMarker] {name}: No se ha encontrado ningún edge cercano.");
            return;
        }

        if (bestDist > maxBindDistance)
        {
            if (debugBinding)
            {
                Debug.LogWarning(
                    $"[EdgeObstacleMarker] {name}: El edge más cercano está a {bestDist:F2}m en XZ " +
                    $"(límite {maxBindDistance:F2}m). No se hace binding."
                );
            }
            return;
        }

        graph.RegisterObstacleOnEdge(this, bestEdge);
        _isBound = true;

        if (debugBinding)
        {
            Debug.Log(
                $"[EdgeObstacleMarker] {name}: asociado al edge " +
                $"from node {bestEdge.from.id} to node {bestEdge.to.id}, " +
                $"distancia XZ {bestDist:F2}m, tipo={obstacleType}."
            );
        }
    }

    private float GetMinDistanceToPathXZ(Vector3 point, Vector3[] corners)
    {
        if (corners == null || corners.Length < 2)
            return float.MaxValue;

        float minDist = float.MaxValue;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            float d = DistancePointToSegmentXZ(point, corners[i], corners[i + 1]);
            if (d < minDist)
                minDist = d;
        }

        return minDist;
    }

    private float DistancePointToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 p2 = new Vector2(p.x, p.z);
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);

        Vector2 ab = b2 - a2;
        float abSqrMag = ab.sqrMagnitude;

        if (abSqrMag < 1e-6f)
            return Vector2.Distance(p2, a2);

        float t = Vector2.Dot(p2 - a2, ab) / abSqrMag;
        t = Mathf.Clamp01(t);

        Vector2 closest = a2 + ab * t;
        return Vector2.Distance(p2, closest);
    }
}
