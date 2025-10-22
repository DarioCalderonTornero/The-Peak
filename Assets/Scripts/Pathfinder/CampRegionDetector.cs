using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CampRegionDetector
/// ------------------
/// Detecta "zonas de campamento" agrupando caras contiguas con pendiente <= umbral.
/// Calcula área total, centroide y normal media de cada región válida y las dibuja con Gizmos.
/// No modifica rutas; solo descubre y visualiza posibles campamentos.
/// 
/// Uso:
/// 1) Entra en Play, pulsa E (tu escáner).
/// 2) Pulsa C para detectar regiones/campamentos.
/// 3) Selecciona el objeto con este componente y activa Gizmos.
/// </summary>
public class CampRegionDetector : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Escáner que ya tienes en escena (debe contener triangles tras pulsar E).")]
    public MeshSlopeScannerSimple scanner;

    [Tooltip("MeshFilter de la montaña (mismo mesh que escaneas).")]
    public MeshFilter mountainMeshFilter;

    [Header("Criterios de campamento")]
    [Tooltip("Pendiente máxima (grados) para considerar una cara como candidata a campamento.")]
    [Range(0f, 89.9f)]
    public float campMaxSlopeDeg = 8f;

    [Tooltip("Área mínima (m² en unidades del mundo) para que una región sea válida.")]
    public float minRegionArea = 4.0f;

    [Tooltip("Número mínimo de triángulos por región (robustez).")]
    public int minRegionTriangles = 4;

    [Header("Controles")]
    [Tooltip("Tecla para ejecutar la detección de regiones planas.")]
    public KeyCode detectKey = KeyCode.C;

    [Header("Debug / Gizmos")]
    public bool drawRegions = true;
    public bool drawRegionCenters = true;
    public bool labelArea = true;
    [Tooltip("Semilla de color para regiones distintas.")]
    public int colorSeed = 12345;

    // ------------ Datos internos ------------
    private List<int>[] neighbors; // adyacencias por cara
    private bool[] isCandidate;     // si una cara es candidata (pendiente <= umbral)
    private List<CampRegion> regions; // lista de regiones detectadas

    // Región de campamento con métricas agregadas
    [System.Serializable]
    public class CampRegion
    {
        public int id;
        public List<int> faces = new List<int>();
        public float area;          // suma de áreas
        public Vector3 centroid;    // centroide ponderado por área
        public Vector3 normalMean;  // normal media (normalizada)
        public float slopeMean;     // pendiente media (grados)
    }

    // Estructura auxiliar para hash de aristas (como en pathfinder)
    private struct EdgeKey : System.IEquatable<EdgeKey>
    {
        public Vector3Int a, b;
        public EdgeKey(Vector3Int p1, Vector3Int p2)
        {
            if (p2.x < p1.x || (p2.x == p1.x && (p2.y < p1.y || (p2.y == p1.y && p2.z < p1.z))))
            { a = p2; b = p1; }
            else
            { a = p1; b = p2; }
        }
        public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => a.GetHashCode() ^ (b.GetHashCode() * 486187739);
    }

    private void Update()
    {
        if (Input.GetKeyDown(detectKey))
            DetectRegions();
    }

    /// <summary>
    /// Paso principal: construye adyacencias, marca candidatas por pendiente,
    /// agrupa por contigüidad, computa métricas y filtra por área/triángulos.
    /// </summary>
    public void DetectRegions()
    {
        // Validación
        if (scanner == null || mountainMeshFilter == null)
        {
            Debug.LogError("[CampRegionDetector] Falta 'scanner' o 'mountainMeshFilter' en el Inspector.");
            return;
        }
        if (scanner.triangles == null || scanner.triangles.Count == 0)
        {
            Debug.LogError("[CampRegionDetector] 'scanner.triangles' está vacío. ¿Pulsaste E para escanear?");
            return;
        }
        if (mountainMeshFilter.sharedMesh == null)
        {
            Debug.LogError("[CampRegionDetector] El MeshFilter no tiene mesh asignado.");
            return;
        }

        // 1) Grafo de adyacencias entre caras
        BuildAdjacencyDualGraph();

        // 2) Candidatas por pendiente
        MarkCandidatesBySlope();

        // 3) Agrupar candidatas contiguas (BFS/DFS)
        regions = BuildRegionsFromCandidates();

        // 4) Calcular métricas agregadas de cada región
        ComputeRegionMetrics(regions);

        // 5) Filtrar por mínimos (área, triángulos)
        int before = regions.Count;
        regions.RemoveAll(r => r.area < minRegionArea || r.faces.Count < minRegionTriangles);
        Debug.Log($"[CampRegionDetector] Regiones detectadas: {before} → válidas: {regions.Count} " +
                  $"(slope ≤ {campMaxSlopeDeg}°, area ≥ {minRegionArea}, tris ≥ {minRegionTriangles})");
    }

    // ------------------------------------------------------------
    // 1) Adyacencias (dual graph)
    // ------------------------------------------------------------
    private void BuildAdjacencyDualGraph()
    {
        Mesh mesh = mountainMeshFilter.sharedMesh;
        var vertsLocal = mesh.vertices;  // espacio local
        var tris = mesh.triangles;
        int faceCount = tris.Length / 3;

        neighbors = new List<int>[faceCount];
        for (int i = 0; i < faceCount; i++) neighbors[i] = new List<int>(3);

        float q = Mathf.Max(1e-6f, scanner.vertexPrecision); // misma precisión que el escáner
        var edgeOwner = new Dictionary<EdgeKey, int>(faceCount * 3);

        for (int face = 0; face < faceCount; face++)
        {
            int i0 = tris[face * 3 + 0], i1 = tris[face * 3 + 1], i2 = tris[face * 3 + 2];
            Vector3 p0 = vertsLocal[i0], p1 = vertsLocal[i1], p2 = vertsLocal[i2];

            Vector3Int q0 = Quantize(p0, q);
            Vector3Int q1 = Quantize(p1, q);
            Vector3Int q2 = Quantize(p2, q);

            TryRegisterEdge(edgeOwner, new EdgeKey(q0, q1), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q1, q2), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q2, q0), face);
        }
    }

    private static Vector3Int Quantize(Vector3 p, float step)
    {
        return new Vector3Int(
            Mathf.RoundToInt(p.x / step),
            Mathf.RoundToInt(p.y / step),
            Mathf.RoundToInt(p.z / step)
        );
    }

    private void TryRegisterEdge(Dictionary<EdgeKey, int> edgeOwner, EdgeKey key, int face)
    {
        if (edgeOwner.TryGetValue(key, out int other))
        {
            if (other != face)
            {
                var a = neighbors[face];
                var b = neighbors[other];
                if (!a.Contains(other)) a.Add(other);
                if (!b.Contains(face)) b.Add(face);
            }
        }
        else edgeOwner.Add(key, face);
    }

    // ------------------------------------------------------------
    // 2) Candidatas por pendiente
    // ------------------------------------------------------------
    private void MarkCandidatesBySlope()
    {
        int n = scanner.triangles.Count;
        isCandidate = new bool[n];

        for (int i = 0; i < n; i++)
        {
            float slope = scanner.triangles[i].slopeDeg;
            isCandidate[i] = slope <= campMaxSlopeDeg;
        }
    }

    // ------------------------------------------------------------
    // 3) Region growing (BFS/DFS)
    // ------------------------------------------------------------
    private List<CampRegion> BuildRegionsFromCandidates()
    {
        int n = scanner.triangles.Count;
        var visited = new bool[n];
        var outRegions = new List<CampRegion>();
        int rid = 0;

        for (int i = 0; i < n; i++)
        {
            if (!isCandidate[i] || visited[i]) continue;

            var reg = new CampRegion { id = rid++ };
            var q = new Queue<int>();
            visited[i] = true;
            q.Enqueue(i);

            while (q.Count > 0)
            {
                int f = q.Dequeue();
                reg.faces.Add(f);

                foreach (var nb in neighbors[f])
                {
                    if (!visited[nb] && isCandidate[nb])
                    {
                        visited[nb] = true;
                        q.Enqueue(nb);
                    }
                }
            }
            outRegions.Add(reg);
        }
        return outRegions;
    }

    // ------------------------------------------------------------
    // 4) Métricas agregadas
    // ------------------------------------------------------------
    private void ComputeRegionMetrics(List<CampRegion> regs)
    {
        if (regs == null) return;

        foreach (var r in regs)
        {
            float areaSum = 0f;
            Vector3 centroidAcc = Vector3.zero;
            Vector3 normalAcc = Vector3.zero;
            float slopeSum = 0f;

            foreach (var f in r.faces)
            {
                var t = scanner.triangles[f];
                areaSum += t.area;

                // Centroide ponderado por área
                centroidAcc += t.center * t.area;

                // Normal media no ponderada (o si quieres ponderar por área, usa t.area * t.normal)
                normalAcc += t.normal;

                slopeSum += t.slopeDeg;
            }

            r.area = areaSum;
            r.centroid = (areaSum > 0f) ? (centroidAcc / areaSum) : Vector3.zero;
            r.normalMean = normalAcc.sqrMagnitude > 0f ? normalAcc.normalized : Vector3.up;
            r.slopeMean = r.faces.Count > 0 ? (slopeSum / r.faces.Count) : 0f;
        }
    }

    // ------------------------------------------------------------
    // Gizmos
    // ------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        if (!drawRegions || regions == null || scanner == null) return;

        // Asignación de colores reproducibles por región
        var rng = new System.Random(colorSeed);

        foreach (var r in regions)
        {
            // Color base aleatorio y algo transparente
            Color c = new Color(
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                0.35f
            );

            // Dibuja contorno de todas las caras de la región
            Gizmos.color = c;
            foreach (var f in r.faces)
            {
                var t = scanner.triangles[f];
                Gizmos.DrawLine(t.v0, t.v1);
                Gizmos.DrawLine(t.v1, t.v2);
                Gizmos.DrawLine(t.v2, t.v0);
            }

            // Centroide y etiqueta
            if (drawRegionCenters)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(r.centroid + r.normalMean * 0.05f, 0.15f);
            }

#if UNITY_EDITOR
            if (labelArea)
            {
                // Etiqueta con área y pendiente media (solo SceneView)
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(
                    r.centroid + r.normalMean * 0.25f,
                    $"Camp #{r.id}\nArea: {r.area:F1}\nSlope: {r.slopeMean:F1}°"
                );
            }
#endif
        }
    }
}
