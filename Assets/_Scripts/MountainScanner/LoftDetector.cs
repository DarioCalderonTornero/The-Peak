using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AltilloDetector:
/// - Detecta zonas planas (“altillos”) a partir de los triángulos escaneados por MeshSlopeScannerSimple.
/// - Agrupa triángulos contiguos con pendiente baja.
/// - Calcula centro y área de cada altillo.
/// </summary>
public class AltilloDetector : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Script que escanea la montaña y devuelve triángulos.")]
    public MeshSlopeScannerSimple scanner;

    [Header("Parámetros")]
    [Tooltip("Pendiente máxima (grados) para considerar un triángulo como plano.")]
    public float maxSlopeDeg = 5f;

    [Tooltip("Área mínima total de un altillo para considerarlo válido.")]
    public float minAltilloArea = 0.5f;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.green;

    [System.Serializable]
    public class AltilloData
    {
        public List<MeshSlopeScannerSimple.TriangleData> triangles;
        public Vector3 center;
        public float area;
        public Vector3 averageNormal;
    }

    public List<AltilloData> altillos = new List<AltilloData>();

    /// <summary>
    /// Detecta los altillos a partir de los triángulos escaneados.
    /// </summary>
    public void DetectAltillos()
    {
        altillos.Clear();

        if (scanner == null || scanner.triangles == null || scanner.triangles.Count == 0)
        {
            Debug.LogWarning("[AltilloDetector] No hay triángulos para analizar.");
            return;
        }

        int n = scanner.triangles.Count;
        bool[] visited = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var tri = scanner.triangles[i];
            if (visited[i]) continue;
            if (tri.slopeDeg > maxSlopeDeg) continue; // no plano

            // Nuevo altillo
            AltilloData altillo = new AltilloData
            {
                triangles = new List<MeshSlopeScannerSimple.TriangleData>()
            };

            // BFS / Flood-fill para agrupar triángulos planos conectados
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                var t = scanner.triangles[idx];
                altillo.triangles.Add(t);

                // Buscar vecinos contiguos (comparten un vértice)
                for (int j = 0; j < n; j++)
                {
                    if (visited[j]) continue;
                    var neighbor = scanner.triangles[j];
                    if (neighbor.slopeDeg > maxSlopeDeg) continue;

                    if (ShareVertex(t, neighbor))
                    {
                        visited[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            // Calcular propiedades del altillo
            float totalArea = 0f;
            Vector3 sumCenter = Vector3.zero;
            Vector3 sumNormal = Vector3.zero;

            foreach (var tr in altillo.triangles)
            {
                totalArea += tr.area;
                sumCenter += tr.center;
                sumNormal += tr.normal;
            }

            altillo.area = totalArea;
            altillo.center = sumCenter / altillo.triangles.Count;
            altillo.averageNormal = (sumNormal / altillo.triangles.Count).normalized;

            if (altillo.area >= minAltilloArea)
                altillos.Add(altillo);
        }

        Debug.Log($"[AltilloDetector] Altillos detectados: {altillos.Count}");
    }

    /// <summary>
    /// Comprueba si dos triángulos comparten al menos un vértice (aproximado con precisión 1e-5)
    /// </summary>
    private bool ShareVertex(MeshSlopeScannerSimple.TriangleData a, MeshSlopeScannerSimple.TriangleData b)
    {
        float eps = 1e-5f;
        return
            Vector3.SqrMagnitude(a.v0 - b.v0) < eps ||
            Vector3.SqrMagnitude(a.v0 - b.v1) < eps ||
            Vector3.SqrMagnitude(a.v0 - b.v2) < eps ||
            Vector3.SqrMagnitude(a.v1 - b.v0) < eps ||
            Vector3.SqrMagnitude(a.v1 - b.v1) < eps ||
            Vector3.SqrMagnitude(a.v1 - b.v2) < eps ||
            Vector3.SqrMagnitude(a.v2 - b.v0) < eps ||
            Vector3.SqrMagnitude(a.v2 - b.v1) < eps ||
            Vector3.SqrMagnitude(a.v2 - b.v2) < eps;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || altillos == null) return;

        Gizmos.color = gizmoColor;
        foreach (var alt in altillos)
        {
            Gizmos.DrawSphere(alt.center, 0.1f);
        }
    }
}
