using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MountainPathfinder:
/// - Construye el grafo de adyacencias de caras (dual) a partir del Mesh de la montaña.
/// - Marca walkable por pendiente.
/// - Ejecuta A* sobre caras con un coste que prioriza menor inclinación.
/// - Dibuja la ruta resultante entre centros de triángulo.
/// Uso:
///  1) Ejecuta el escáner (E) para llenar "triangles" en el scanner.
///  2) Pulsa P para construir el grafo y planificar ruta (de triángulo más bajo a más alto).
/// </summary>
public class MountainPathfinder : MonoBehaviour
{
    [Header("Referencias")]
    public MeshSlopeScannerSimple scanner;       // arrastra el componente existente
    public MeshFilter mountainMeshFilter;        // el mismo MeshFilter de la montaña

    [Header("Parámetros de navegación (solo inclinación)")]
    [Range(0f, 89.9f)]
    public float maxSlopeDeg = 60f;              // por encima, la cara es no transitable
    [Range(0f, 5f)]
    public float slopeCostAlpha = 1.0f;          // peso de penalización por pendiente
    public bool penalizeUphill = false;          // opcional: penalizar subir (deltaY>0)
    [Range(0f, 2f)]
    public float uphillExtra = 0.25f;            // penalización extra relativa si se sube

    [Header("Controles")]
    public KeyCode buildAndSolveKey = KeyCode.P; // tecla para construir grafo y resolver ruta

    [Header("Debug draw")]
    public bool drawWalkableFaces = true;
    public bool drawPath = true;
    public Color walkableColor = new Color(0f, 1f, 0f, 0.15f);
    public Color unwalkableColor = new Color(1f, 0f, 0f, 0.15f);
    public Color pathColor = Color.cyan;
    public float lineWidth = 0.02f; // no afecta a Gizmos; es orientativo si usas otros dibujados

    // --- Datos internos ---
    // Adyacencias: por cada cara, lista de ids de caras vecinas
    private List<int>[] neighbors;
    // Para coste: distancia entre centros precomputada (opcional) o se calcula al vuelo
    // Aquí lo calculamos al vuelo; si quieres performance, cachea.
    private bool[] isWalkable;
    private List<int> pathFaceIds;  // resultado A*
    private int startFaceId = -1;
    private int goalFaceId = -1;

    // Para construir adyacencias
    private struct EdgeKey : IEquatable<EdgeKey>
    {
        public Vector3Int a;
        public Vector3Int b;
        public EdgeKey(Vector3Int p1, Vector3Int p2)
        {
            // Ordenar extremos para que (A,B)==(B,A)
            if (p2.x < p1.x || (p2.x == p1.x && (p2.y < p1.y || (p2.y == p1.y && p2.z < p1.z))))
            {
                a = p2; b = p1;
            }
            else
            {
                a = p1; b = p2;
            }
        }
        public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => a.GetHashCode() ^ (b.GetHashCode() * 486187739);
    }

    private void Update()
    {
        if (Input.GetKeyDown(buildAndSolveKey))
        {
            TryBuildGraphAndSolve();
        }
    }

    private void TryBuildGraphAndSolve()
    {
        // Validaciones mínimas
        if (scanner == null || mountainMeshFilter == null)
        {
            Debug.LogError("[MountainPathfinder] Falta scanner o mountainMeshFilter.");
            return;
        }
        if (scanner.triangles == null || scanner.triangles.Count == 0)
        {
            Debug.LogError("[MountainPathfinder] La lista 'triangles' del scanner está vacía. ¿Pulsaste E para escanear?");
            return;
        }
        if (mountainMeshFilter.sharedMesh == null)
        {
            Debug.LogError("[MountainPathfinder] El MeshFilter no tiene mesh.");
            return;
        }

        // 1) Elegir start/goal (por ahora: minY / maxY sobre centros de cara)
        SelectStartAndGoalByHeight();

        // 2) Construir adyacencias de caras (dual graph)
        BuildAdjacencyDualGraph();

        // 3) Walkability por pendiente
        ComputeWalkability();

        // 4) A* sobre caras
        pathFaceIds = RunAStar(startFaceId, goalFaceId);

        if (pathFaceIds == null || pathFaceIds.Count == 0)
        {
            Debug.LogWarning("[MountainPathfinder] No se encontró ruta. ¿maxSlopeDeg demasiado bajo? ¿Malla desconectada?");
        }
        else
        {
            Debug.Log($"[MountainPathfinder] Ruta encontrada. Caras en ruta: {pathFaceIds.Count}");
        }
    }

    // --- (1) Start/Goal automáticos ---
    private void SelectStartAndGoalByHeight()
    {
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        int minId = -1, maxId = -1;

        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            var c = scanner.triangles[i].center.y;
            if (c < minY) { minY = c; minId = i; }
            if (c > maxY) { maxY = c; maxId = i; }
        }

        startFaceId = minId;
        goalFaceId = maxId;

        Debug.Log($"[MountainPathfinder] startFace={startFaceId} (y={minY:F2}), goalFace={goalFaceId} (y={maxY:F2})");
    }

    // --- (2) Dual graph con hash de aristas por POSICIÓN local cuantizada ---
    private void BuildAdjacencyDualGraph()
    {
        Mesh mesh = mountainMeshFilter.sharedMesh;
        var vertsLocal = mesh.vertices;   // espacio LOCAL del mesh
        var tris = mesh.triangles;
        int faceCount = tris.Length / 3;

        neighbors = new List<int>[faceCount];
        for (int i = 0; i < faceCount; i++) neighbors[i] = new List<int>(3);

        // factor de cuantización: reutilizamos el "vertexPrecision" de tu scanner para tolerar duplicados
        float q = Mathf.Max(1e-6f, scanner.vertexPrecision);

        // diccionario: arista -> faceId que la “reclamó” primero
        var edgeOwner = new Dictionary<EdgeKey, int>(faceCount * 3);

        for (int face = 0; face < faceCount; face++)
        {
            int i0 = tris[face * 3 + 0];
            int i1 = tris[face * 3 + 1];
            int i2 = tris[face * 3 + 2];

            // posiciones locales (no mundo)
            Vector3 p0 = vertsLocal[i0];
            Vector3 p1 = vertsLocal[i1];
            Vector3 p2 = vertsLocal[i2];

            // cuantiza a enteros
            Vector3Int q0 = Quantize(p0, q);
            Vector3Int q1 = Quantize(p1, q);
            Vector3Int q2 = Quantize(p2, q);

            TryRegisterEdge(edgeOwner, new EdgeKey(q0, q1), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q1, q2), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q2, q0), face);
        }

        // Cuando una arista ya existía, TryRegisterEdge habrá unido face <-> otherFace
        // Nada más que hacer aquí.
        Debug.Log($"[MountainPathfinder] Adyacencias construidas. Caras: {faceCount}");
    }

    private static Vector3Int Quantize(Vector3 p, float step)
    {
        // Redondeo simétrico a rejilla de tamaño "step"
        return new Vector3Int(
            Mathf.RoundToInt(p.x / step),
            Mathf.RoundToInt(p.y / step),
            Mathf.RoundToInt(p.z / step)
        );
    }

    private void TryRegisterEdge(Dictionary<EdgeKey, int> edgeOwner, EdgeKey key, int face)
    {
        if (edgeOwner.TryGetValue(key, out int otherFace))
        {
            // Ya había una cara que compartía esta arista adyacencia bidireccional
            if (otherFace != face)
            {
                var nA = neighbors[face];
                var nB = neighbors[otherFace];
                if (!nA.Contains(otherFace)) nA.Add(otherFace);
                if (!nB.Contains(face)) nB.Add(face);
            }
        }
        else
        {
            edgeOwner.Add(key, face);
        }
    }

    // --- (3) Walkability por pendiente ---
    private void ComputeWalkability()
    {
        int n = scanner.triangles.Count;
        isWalkable = new bool[n];
        int walkables = 0;

        for (int i = 0; i < n; i++)
        {
            float slope = scanner.triangles[i].slopeDeg;
            bool w = slope <= maxSlopeDeg;
            isWalkable[i] = w;
            if (w) walkables++;
        }

        Debug.Log($"[MountainPathfinder] Walkables: {walkables}/{n} con maxSlopeDeg={maxSlopeDeg}");
    }

    // --- (4) A* sobre caras ---
    private List<int> RunAStar(int start, int goal)
    {
        if (start < 0 || goal < 0) return null;
        if (!CheckFaceIndex(start) || !CheckFaceIndex(goal)) return null;
        if (!isWalkable[start] || !isWalkable[goal])
        {
            Debug.LogWarning("[MountainPathfinder] Start o Goal no walkable por pendiente.");
            return null;
        }

        var tri = scanner.triangles;

        // Típico A*
        var open = new PriorityQueue<int>();
        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float>();
        var fScore = new Dictionary<int, float>();

        open.Push(start, 0f);
        gScore[start] = 0f;
        fScore[start] = Heuristic(tri[start].center, tri[goal].center);

        while (open.Count > 0)
        {
            int current = open.Pop(); // menor fScore

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (int nb in neighbors[current])
            {
                if (!isWalkable[nb]) continue; // bloqueado por pendiente

                float tentative = gScore[current] + TransitionCost(current, nb);

                if (!gScore.ContainsKey(nb) || tentative < gScore[nb])
                {
                    cameFrom[nb] = current;
                    gScore[nb] = tentative;
                    fScore[nb] = tentative + Heuristic(tri[nb].center, tri[goal].center);
                    open.Push(nb, fScore[nb]);
                }
            }
        }

        return null; // sin ruta
    }

    private bool CheckFaceIndex(int id) => id >= 0 && id < scanner.triangles.Count;

    private float Heuristic(Vector3 a, Vector3 b)
    {
        // Distancia euclídea en mundo (admisible)
        return Vector3.Distance(a, b);
    }

    private float TransitionCost(int fromFace, int toFace)
    {
        var tri = scanner.triangles;
        // Base: distancia entre centros (aprox geodésica razonable)
        float baseDist = Vector3.Distance(tri[fromFace].center, tri[toFace].center);

        // Penalización por pendiente de la cara destino (más costoso pisar cara más inclinada)
        float slopeNorm = Mathf.Clamp01(tri[toFace].slopeDeg / Mathf.Max(0.0001f, maxSlopeDeg));
        float cost = baseDist * (1f + slopeCostAlpha * slopeNorm);

        if (penalizeUphill)
        {
            float dy = tri[toFace].center.y - tri[fromFace].center.y;
            if (dy > 0f) cost *= (1f + uphillExtra);
        }

        return cost;
    }

    private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        var path = new List<int>();
        path.Add(current);
        while (cameFrom.TryGetValue(current, out int prev))
        {
            current = prev;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    // --- (5) Dibujado de debug ---
    private void OnDrawGizmosSelected()
    {
        if (scanner == null || scanner.triangles == null) return;

        // Pintar walkables / unwalkables (caras como triángulos semi-transparentes)
        if (drawWalkableFaces && isWalkable != null && neighbors != null)
        {
            for (int i = 0; i < scanner.triangles.Count; i++)
            {
                var t = scanner.triangles[i];
                Gizmos.color = (i < isWalkable.Length && isWalkable[i]) ? walkableColor : unwalkableColor;
                Gizmos.DrawLine(t.v0, t.v1);
                Gizmos.DrawLine(t.v1, t.v2);
                Gizmos.DrawLine(t.v2, t.v0);
            }
        }

        // Pintar ruta como polilínea entre centros
        if (drawPath && pathFaceIds != null && pathFaceIds.Count > 1)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < pathFaceIds.Count - 1; i++)
            {
                var a = scanner.triangles[pathFaceIds[i]].center;
                var b = scanner.triangles[pathFaceIds[i + 1]].center;
                Gizmos.DrawLine(a, b);
            }

            // Marcar start/goal
            var s = scanner.triangles[pathFaceIds[0]].center;
            var g = scanner.triangles[pathFaceIds[pathFaceIds.Count - 1]].center;
            Gizmos.DrawSphere(s, 0.15f);
            Gizmos.DrawSphere(g, 0.15f);
        }
    }

    // --- Cola de prioridad mínima para A* ---
    private class PriorityQueue<T>
    {
        private readonly List<(T item, float pri)> heap = new();
        public int Count => heap.Count;

        public void Push(T item, float priority)
        {
            heap.Add((item, priority));
            SiftUp(heap.Count - 1);
        }
        public T Pop()
        {
            var root = heap[0].item;
            heap[0] = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            SiftDown(0);
            return root;
        }
        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heap[i].pri >= heap[p].pri) break;
                (heap[i], heap[p]) = (heap[p], heap[i]);
                i = p;
            }
        }
        private void SiftDown(int i)
        {
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < heap.Count && heap[l].pri < heap[m].pri) m = l;
                if (r < heap.Count && heap[r].pri < heap[m].pri) m = r;
                if (m == i) break;
                (heap[i], heap[m]) = (heap[m], heap[i]);
                i = m;
            }
        }
    }
}
