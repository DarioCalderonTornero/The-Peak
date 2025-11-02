using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MountainPathfinder (ACTUALIZADO)
/// --------------------------------
/// - Grafo dual por caras (adyacencias por aristas compartidas).
/// - Walkability = (pendiente <= maxSlopeDeg) AND (no bloqueada externamente).
/// - A* sobre caras, coste con penalización por inclinación y subida opcional.
/// - API para planificar DESDE una posición mundo hasta la CIMA (no pisa la ruta global).
/// - Integra sistema de obstáculos vía ObstacleNavBlocker:
///     * Recalcula walkability al cambiar bloqueos
///     * Emite OnNavTopologyChanged para que la IA replantee rutas.
/// - Tecla P mantiene un flujo manual de debug (ruta base→cima) para gizmos.
/// </summary>
public class MountainPathfinder : MonoBehaviour
{
    // ==========================
    // Configuración
    // ==========================
    [Header("Referencias")]
    public MeshSlopeScannerSimple scanner;
    public MeshFilter mountainMeshFilter;

    [Header("Navegación")]
    [Range(0f, 89.9f)] public float maxSlopeDeg = 60f;
    [Range(0f, 5f)] public float slopeCostAlpha = 1.0f;
    public bool penalizeUphill = false;
    [Range(0f, 2f)] public float uphillExtra = 0.25f;

    [Header("Controles (debug)")]
    public KeyCode buildAndSolveKey = KeyCode.P;

    [Header("Debug draw")]
    public bool drawWalkableFaces = true;
    public bool drawPath = true;
    public Color walkableColor = new Color(0f, 1f, 0f, 0.15f);
    public Color unwalkableColor = new Color(1f, 0f, 0f, 0.15f);
    public Color pathColor = Color.cyan;

    // ==========================
    // Estado interno
    // ==========================
    private List<int>[] neighbors;   // adyacencias entre caras
    private bool[] isWalkable;       // caras transitables

    // Ruta “global” (solo para debug/gizmos; la API de agentes usa rutas locales)
    private List<int> pathFaceIds;
    private int startFaceId = -1;
    private int goalFaceId = -1;

    // Bloqueos externos (obstáculos)
    private System.Func<int, bool> _isExternallyBlocked = null;

    // Eventos
    /// <summary>
    /// Se emite cuando cambia la topología de navegación (p.ej., se bloquea/ desbloquea una cara).
    /// Los agentes deberían replanificar al recibir este evento.
    /// </summary>
    public event System.Action OnNavTopologyChanged;

    // ==========================
    // Ciclo de vida
    // ==========================
    private void Start()
    {
        // Integración con el sistema de obstáculos (si está en la escena)
        var nb = ObstacleNavBlocker.Instance;
        if (nb != null)
        {
            _isExternallyBlocked = nb.IsBlocked;
            nb.OnBlockedFacesChanged += HandleBlockedChanged;
        }
    }

    private void OnDestroy()
    {
        var nb = ObstacleNavBlocker.Instance;
        if (nb != null) nb.OnBlockedFacesChanged -= HandleBlockedChanged;
    }

    private void Update()
    {
        // Debug manual con tecla P (ruta base→cima para gizmos)
        if (Input.GetKeyDown(buildAndSolveKey))
            TryBuildGraphAndSolve();
    }

    // ==========================
    // Flujo debug (tecla P)
    // ==========================
    private void TryBuildGraphAndSolve()
    {
        if (!ValidateScannerAndMesh()) return;

        SelectStartAndGoalByHeight();
        BuildAdjacencyDualGraph();
        ComputeWalkability();

        pathFaceIds = RunAStar(startFaceId, goalFaceId);
        if (pathFaceIds == null || pathFaceIds.Count == 0)
            Debug.LogWarning("[MountainPathfinder] No se encontró ruta (P).");
        else
            Debug.Log($"[MountainPathfinder] Ruta (P) OK. Caras: {pathFaceIds.Count}");
    }

    // ==========================
    // API pública (para agentes)
    // ==========================

    /// <summary>
    /// Asegura que el grafo y el array de walkability están listos.
    /// No calcula rutas.
    /// </summary>
    public void EnsureGraphReady()
    {
        if (!ValidateScannerAndMesh()) return;
        if (neighbors == null) BuildAdjacencyDualGraph();
        if (isWalkable == null || isWalkable.Length != scanner.triangles.Count) ComputeWalkability();
    }

    /// <summary> Id de la cara cuyo centro está más cerca de worldPos (no filtra por walkable). </summary>
    public int GetClosestFaceId(Vector3 worldPos)
    {
        if (scanner?.triangles == null || scanner.triangles.Count == 0) return -1;
        int best = -1; float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            float d2 = (scanner.triangles[i].center - worldPos).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = i; }
        }
        return best;
    }

    /// <summary> Id de la cara walkable más cercana a worldPos. </summary>
    public int GetClosestWalkableFaceId(Vector3 worldPos)
    {
        EnsureGraphReady();
        if (scanner?.triangles == null || isWalkable == null) return -1;

        int best = -1; float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            if (i >= isWalkable.Length || !isWalkable[i]) continue;
            float d2 = (scanner.triangles[i].center - worldPos).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = i; }
        }
        return best;
    }

    /// <summary> Devuelve una ruta de caras entre startFace y goalFace (no pisa la ruta global). </summary>
    public List<int> ComputeFacePath(int startFace, int goalFace)
    {
        EnsureGraphReady();
        return RunAStar(startFace, goalFace);
    }

    /// <summary> Convierte una ruta de caras a puntos (centros) en mundo. </summary>
    public List<Vector3> BuildCenterPath(List<int> facePath)
    {
        var pts = new List<Vector3>();
        if (facePath == null || scanner?.triangles == null) return pts;
        foreach (var f in facePath) pts.Add(scanner.triangles[f].center);
        return pts;
    }

    /// <summary>
    /// Planifica desde worldPos hasta la cima (cara con mayor Y).
    /// Devuelve puntos (centros). No modifica la ruta global de debug.
    /// </summary>
    public List<Vector3> PlanCentersFromWorldToSummit(Vector3 worldPos)
    {
        EnsureGraphReady();
        int start = GetClosestWalkableFaceId(worldPos);
        int goal = GetHighestFaceId();
        var faces = ComputeFacePath(start, goal);
        return BuildCenterPath(faces);
    }

    /// <summary>
    /// Construye grafo y resuelve base→cima (para debug/gizmos).
    /// </summary>
    public bool BuildGraphAndSolveAuto()
    {
        if (!ValidateScannerAndMesh()) return false;
        SelectStartAndGoalByHeight();
        BuildAdjacencyDualGraph();
        ComputeWalkability();
        pathFaceIds = RunAStar(startFaceId, goalFaceId);
        if (pathFaceIds == null || pathFaceIds.Count == 0)
        {
            Debug.LogWarning("[MountainPathfinder] Auto: sin ruta.");
            return false;
        }
        Debug.Log($"[MountainPathfinder] Auto ruta OK. Caras: {pathFaceIds.Count}");
        return true;
    }

    public IReadOnlyList<int> LastFacePathIds => pathFaceIds;

    public List<Vector3> GetLastRouteCenters()
    {
        var pts = new List<Vector3>();
        if (pathFaceIds == null || scanner?.triangles == null) return pts;
        foreach (var f in pathFaceIds) pts.Add(scanner.triangles[f].center);
        return pts;
    }

    public int GetLowestFaceId()
    {
        int id = -1; float minY = float.PositiveInfinity;
        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            float y = scanner.triangles[i].center.y;
            if (y < minY) { minY = y; id = i; }
        }
        return id;
    }

    public int GetHighestFaceId()
    {
        int id = -1; float maxY = float.NegativeInfinity;
        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            float y = scanner.triangles[i].center.y;
            if (y > maxY) { maxY = y; id = i; }
        }
        return id;
    }

    // ==========================
    // Internos (grafo / walkability / A*)
    // ==========================
    private bool ValidateScannerAndMesh()
    {
        if (scanner == null || mountainMeshFilter == null)
        {
            Debug.LogError("[MountainPathfinder] Falta scanner o mountainMeshFilter.");
            return false;
        }
        if (scanner.triangles == null || scanner.triangles.Count == 0)
        {
            Debug.LogError("[MountainPathfinder] 'triangles' vacío. Ejecuta el escaneo antes.");
            return false;
        }
        if (mountainMeshFilter.sharedMesh == null)
        {
            Debug.LogError("[MountainPathfinder] MeshFilter sin mesh.");
            return false;
        }
        return true;
    }

    private void SelectStartAndGoalByHeight()
    {
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        int minId = -1, maxId = -1;
        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            float y = scanner.triangles[i].center.y;
            if (y < minY) { minY = y; minId = i; }
            if (y > maxY) { maxY = y; maxId = i; }
        }
        startFaceId = minId;
        goalFaceId = maxId;
        Debug.Log($"[MountainPathfinder] startFace={startFaceId} y={minY:F2} → goalFace={goalFaceId} y={maxY:F2}");
    }

    private void BuildAdjacencyDualGraph()
    {
        Mesh mesh = mountainMeshFilter.sharedMesh;
        Vector3[] vertsLocal = mesh.vertices;
        int[] tris = mesh.triangles;
        int faceCount = tris.Length / 3;

        neighbors = new List<int>[faceCount];
        for (int i = 0; i < faceCount; i++) neighbors[i] = new List<int>(3);

        float q = Mathf.Max(1e-6f, scanner.vertexPrecision);
        var edgeOwner = new Dictionary<EdgeKey, int>(faceCount * 3);

        for (int face = 0; face < faceCount; face++)
        {
            int i0 = tris[face * 3 + 0], i1 = tris[face * 3 + 1], i2 = tris[face * 3 + 2];
            Vector3Int q0 = Quantize(vertsLocal[i0], q);
            Vector3Int q1 = Quantize(vertsLocal[i1], q);
            Vector3Int q2 = Quantize(vertsLocal[i2], q);
            TryRegisterEdge(edgeOwner, new EdgeKey(q0, q1), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q1, q2), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q2, q0), face);
        }

        Debug.Log($"[MountainPathfinder] Adyacencias construidas. Caras: {faceCount}");
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
                if (!neighbors[face].Contains(other)) neighbors[face].Add(other);
                if (!neighbors[other].Contains(face)) neighbors[other].Add(face);
            }
        }
        else edgeOwner.Add(key, face);
    }

    private void ComputeWalkability()
    {
        int n = scanner.triangles.Count;
        isWalkable = new bool[n];
        int walk = 0;
        for (int i = 0; i < n; i++)
        {
            bool slopeOK = scanner.triangles[i].slopeDeg <= maxSlopeDeg;
            bool blocked = _isExternallyBlocked != null && _isExternallyBlocked(i);
            bool w = slopeOK && !blocked;
            isWalkable[i] = w;
            if (w) walk++;
        }
        Debug.Log($"[MountainPathfinder] Walkables: {walk}/{n} (maxSlope={maxSlopeDeg}°)");
    }

    private List<int> RunAStar(int start, int goal)
    {
        if (start < 0 || goal < 0) return null;
        if (neighbors == null || isWalkable == null) return null;
        if (!CheckFaceIndex(start) || !CheckFaceIndex(goal)) return null;
        if (!isWalkable[start] || !isWalkable[goal]) return null;

        var tri = scanner.triangles;
        var open = new PriorityQueue<int>();
        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float>();
        var fScore = new Dictionary<int, float>();

        open.Push(start, 0f);
        gScore[start] = 0f;
        fScore[start] = Heuristic(tri[start].center, tri[goal].center);

        while (open.Count > 0)
        {
            int current = open.Pop();
            if (current == goal) return ReconstructPath(cameFrom, current);

            foreach (int nb in neighbors[current])
            {
                if (!isWalkable[nb]) continue;
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
        return null;
    }

    private bool CheckFaceIndex(int id) => id >= 0 && id < scanner.triangles.Count;
    private float Heuristic(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

    private float TransitionCost(int fromFace, int toFace)
    {
        var tri = scanner.triangles;
        float baseDist = Vector3.Distance(tri[fromFace].center, tri[toFace].center);
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
        var path = new List<int> { current };
        while (cameFrom.TryGetValue(current, out int prev))
        {
            current = prev;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    // ==========================
    // Eventos de bloqueo externo
    // ==========================
    private void HandleBlockedChanged()
    {
        // No hace falta rehacer el grafo; basta con recomputar walkability
        if (neighbors == null) BuildAdjacencyDualGraph();
        ComputeWalkability();
        OnNavTopologyChanged?.Invoke();
    }

    // ==========================
    // Gizmos (debug)
    // ==========================
    private void OnDrawGizmosSelected()
    {
        if (scanner == null || scanner.triangles == null) return;

        if (drawWalkableFaces && isWalkable != null)
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

        if (drawPath && pathFaceIds != null && pathFaceIds.Count > 1)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < pathFaceIds.Count - 1; i++)
            {
                var a = scanner.triangles[pathFaceIds[i]].center;
                var b = scanner.triangles[pathFaceIds[i + 1]].center;
                Gizmos.DrawLine(a, b);
            }
            var s = scanner.triangles[pathFaceIds[0]].center;
            var g = scanner.triangles[pathFaceIds[^1]].center;
            Gizmos.DrawSphere(s, 0.15f);
            Gizmos.DrawSphere(g, 0.15f);
        }
    }

    // ==========================
    // Soporte (estructuras)
    // ==========================
    private struct EdgeKey : System.IEquatable<EdgeKey>
    {
        public Vector3Int a, b;
        public EdgeKey(Vector3Int p1, Vector3Int p2)
        {
            if (p2.x < p1.x || (p2.x == p1.x && (p2.y < p1.y || (p2.y == p1.y && p2.z < p1.z))))
            { a = p2; b = p1; }
            else { a = p1; b = p2; }
        }
        public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => a.GetHashCode() ^ (b.GetHashCode() * 486187739);
    }

    private class PriorityQueue<T>
    {
        private readonly List<(T item, float pri)> heap = new();
        public int Count => heap.Count;

        public void Push(T item, float priority)
        {
            heap.Add((item, priority));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heap[i].pri >= heap[p].pri) break;
                (heap[i], heap[p]) = (heap[p], heap[i]);
                i = p;
            }
        }

        public T Pop()
        {
            var root = heap[0].item;
            heap[0] = heap[^1];
            heap.RemoveAt(heap.Count - 1);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < heap.Count && heap[l].pri < heap[m].pri) m = l;
                if (r < heap.Count && heap[r].pri < heap[m].pri) m = r;
                if (m == i) break;
                (heap[i], heap[m]) = (heap[m], heap[i]);
                i = m;
            }
            return root;
        }
    }
}
