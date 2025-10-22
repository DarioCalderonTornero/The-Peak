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
    // ============================================================
    // === CONFIGURACIÓN PRINCIPAL ===
    // ============================================================

    [Header("Referencias")]
    [Tooltip("Referencia al componente que escanea la montaña y guarda los triángulos.")]
    public MeshSlopeScannerSimple scanner;

    [Tooltip("MeshFilter que contiene el mesh real de la montaña.")]
    public MeshFilter mountainMeshFilter;

    [Header("Parámetros de navegación (solo inclinación)")]
    [Tooltip("Caras con pendiente mayor a este ángulo no serán transitables.")]
    [Range(0f, 89.9f)]
    public float maxSlopeDeg = 60f;

    [Tooltip("Peso de la penalización por inclinación (1 = equilibrio, >1 = evita más las pendientes).")]
    [Range(0f, 5f)]
    public float slopeCostAlpha = 1.0f;

    [Tooltip("Si está activado, subir cuesta más que bajar.")]
    public bool penalizeUphill = false;

    [Tooltip("Porcentaje extra de coste al subir (se multiplica sobre el coste base).")]
    [Range(0f, 2f)]
    public float uphillExtra = 0.25f;

    [Header("Controles")]
    [Tooltip("Tecla para construir el grafo y resolver la ruta.")]
    public KeyCode buildAndSolveKey = KeyCode.P;

    [Header("Debug draw")]
    [Tooltip("Dibujar triángulos walkables y no walkables.")]
    public bool drawWalkableFaces = true;

    [Tooltip("Dibujar la ruta hallada por A*.")]
    public bool drawPath = true;

    [Tooltip("Color para las caras transitables.")]
    public Color walkableColor = new Color(0f, 1f, 0f, 0.15f);

    [Tooltip("Color para las caras no transitables.")]
    public Color unwalkableColor = new Color(1f, 0f, 0f, 0.15f);

    [Tooltip("Color para la línea de la ruta.")]
    public Color pathColor = Color.cyan;

    [Tooltip("Grosor orientativo para la ruta (no afecta a Gizmos, solo referencia).")]
    public float lineWidth = 0.02f;

    // ============================================================
    // === VARIABLES INTERNAS ===
    // ============================================================

    // Lista de adyacencias: para cada triángulo, qué triángulos son sus vecinos.
    private List<int>[] neighbors;

    // Caras transitables según la pendiente
    private bool[] isWalkable;

    // Ruta resultante del A*
    private List<int> pathFaceIds;

    // Identificadores de la cara inicial y final
    private int startFaceId = -1;
    private int goalFaceId = -1;

    // ============================================================
    // === ESTRUCTURAS AUXILIARES ===
    // ============================================================

    /// <summary>
    /// Representa una arista entre dos vértices (ordenada y cuantizada).
    /// Se usa como clave para detectar qué triángulos comparten borde.
    /// </summary>
    private struct EdgeKey : IEquatable<EdgeKey>
    {
        public Vector3Int a;
        public Vector3Int b;

        public EdgeKey(Vector3Int p1, Vector3Int p2)
        {
            // Ordena los puntos para que (A,B) == (B,A)
            if (p2.x < p1.x || (p2.x == p1.x && (p2.y < p1.y || (p2.y == p1.y && p2.z < p1.z))))
            {
                a = p2;
                b = p1;
            }
            else
            {
                a = p1;
                b = p2;
            }
        }

        public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => a.GetHashCode() ^ (b.GetHashCode() * 486187739);
    }

    // ============================================================
    // === CICLO DE EJECUCIÓN ===
    // ============================================================

    private void Update()
    {
        // Pulsa "P" para ejecutar el algoritmo
        if (Input.GetKeyDown(buildAndSolveKey))
            TryBuildGraphAndSolve();
    }

    // ============================================================
    // === FLUJO PRINCIPAL ===
    // ============================================================

    /// <summary>
    /// Paso principal: construye el grafo, marca walkables y calcula la ruta.
    /// </summary>
    private void TryBuildGraphAndSolve()
    {
        // --- VALIDACIONES ---
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

        // --- PASOS PRINCIPALES ---
        SelectStartAndGoalByHeight(); // 1. Escoger punto inicial/final
        BuildAdjacencyDualGraph();    // 2. Crear grafo de caras vecinas
        ComputeWalkability();         // 3. Marcar caras transitables
        pathFaceIds = RunAStar(startFaceId, goalFaceId); // 4. Calcular ruta

        if (pathFaceIds == null || pathFaceIds.Count == 0)
        {
            Debug.LogWarning("[MountainPathfinder] No se encontró ruta. Puede que maxSlopeDeg sea muy bajo o la malla esté desconectada.");
        }
        else
        {
            Debug.Log($"[MountainPathfinder] Ruta encontrada. Caras en ruta: {pathFaceIds.Count}");
        }
    }

    // ============================================================
    // === (1) SELECCIÓN START/GOAL ===
    // ============================================================

    /// <summary>
    /// Selecciona como punto inicial el triángulo con menor Y (más bajo)
    /// y como destino el de mayor Y (más alto). Es una forma sencilla
    /// de simular “base” y “cima”.
    /// </summary>
    private void SelectStartAndGoalByHeight()
    {
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        int minId = -1, maxId = -1;

        for (int i = 0; i < scanner.triangles.Count; i++)
        {
            float y = scanner.triangles[i].center.y;
            if (y < minY) { minY = y; minId = i; }
            if (y > maxY) { maxY = y; maxId = i; }
        }

        startFaceId = minId;
        goalFaceId = maxId;

        Debug.Log($"[MountainPathfinder] startFace={startFaceId} (y={minY:F2}), goalFace={goalFaceId} (y={maxY:F2})");
    }

    // ============================================================
    // === (2) CONSTRUCCIÓN DEL GRAFO (DUAL GRAPH) ===
    // ============================================================

    /// <summary>
    /// Crea las adyacencias entre caras del mesh.
    /// Dos caras son vecinas si comparten una arista.
    /// </summary>
    private void BuildAdjacencyDualGraph()
    {
        Mesh mesh = mountainMeshFilter.sharedMesh;
        Vector3[] vertsLocal = mesh.vertices;
        int[] tris = mesh.triangles;
        int faceCount = tris.Length / 3;

        neighbors = new List<int>[faceCount];
        for (int i = 0; i < faceCount; i++)
            neighbors[i] = new List<int>(3);

        // Usamos "vertexPrecision" del scanner para igualar vértices cercanos
        float q = Mathf.Max(1e-6f, scanner.vertexPrecision);

        // Diccionario: arista -> faceId que la reclamó primero
        var edgeOwner = new Dictionary<EdgeKey, int>(faceCount * 3);

        // Recorre cada triángulo y registra sus tres aristas
        for (int face = 0; face < faceCount; face++)
        {
            int i0 = tris[face * 3 + 0];
            int i1 = tris[face * 3 + 1];
            int i2 = tris[face * 3 + 2];

            Vector3 p0 = vertsLocal[i0];
            Vector3 p1 = vertsLocal[i1];
            Vector3 p2 = vertsLocal[i2];

            // Cuantiza posiciones locales (evita errores de precisión)
            Vector3Int q0 = Quantize(p0, q);
            Vector3Int q1 = Quantize(p1, q);
            Vector3Int q2 = Quantize(p2, q);

            // Registra aristas (en cualquier orden)
            TryRegisterEdge(edgeOwner, new EdgeKey(q0, q1), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q1, q2), face);
            TryRegisterEdge(edgeOwner, new EdgeKey(q2, q0), face);
        }

        Debug.Log($"[MountainPathfinder] Adyacencias construidas. Caras: {faceCount}");
    }

    private static Vector3Int Quantize(Vector3 p, float step)
    {
        // Convierte una posición en valores enteros según la precisión deseada
        return new Vector3Int(
            Mathf.RoundToInt(p.x / step),
            Mathf.RoundToInt(p.y / step),
            Mathf.RoundToInt(p.z / step)
        );
    }

    /// <summary>
    /// Asocia una arista a una cara. Si la arista ya estaba en el diccionario,
    /// significa que dos caras comparten ese borde → se marcan como vecinas.
    /// </summary>
    private void TryRegisterEdge(Dictionary<EdgeKey, int> edgeOwner, EdgeKey key, int face)
    {
        if (edgeOwner.TryGetValue(key, out int otherFace))
        {
            // Si otra cara ya registró esta arista, son vecinas
            if (otherFace != face)
            {
                if (!neighbors[face].Contains(otherFace)) neighbors[face].Add(otherFace);
                if (!neighbors[otherFace].Contains(face)) neighbors[otherFace].Add(face);
            }
        }
        else
        {
            edgeOwner.Add(key, face);
        }
    }

    // ============================================================
    // === (3) MARCAR CARAS WALKABLE ===
    // ============================================================

    private void ComputeWalkability()
    {
        int n = scanner.triangles.Count;
        isWalkable = new bool[n];
        int walkables = 0;

        for (int i = 0; i < n; i++)
        {
            float slope = scanner.triangles[i].slopeDeg;
            bool w = slope <= maxSlopeDeg; // transitable si pendiente <= umbral
            isWalkable[i] = w;
            if (w) walkables++;
        }

        Debug.Log($"[MountainPathfinder] Walkables: {walkables}/{n} con maxSlopeDeg={maxSlopeDeg}");
    }

    // ============================================================
    // === (4) A* SOBRE CARAS ===
    // ============================================================

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

        // Estructuras para A*
        var open = new PriorityQueue<int>(); // lista de nodos abiertos
        var cameFrom = new Dictionary<int, int>(); // de dónde venimos
        var gScore = new Dictionary<int, float>(); // coste acumulado
        var fScore = new Dictionary<int, float>(); // g + heurística

        open.Push(start, 0f);
        gScore[start] = 0f;
        fScore[start] = Heuristic(tri[start].center, tri[goal].center);

        // Bucle principal de A*
        while (open.Count > 0)
        {
            int current = open.Pop(); // el nodo con menor fScore

            // Caso base: hemos llegado
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (int nb in neighbors[current])
            {
                if (!isWalkable[nb]) continue; // cara bloqueada

                float tentative = gScore[current] + TransitionCost(current, nb);

                // Si encontramos un camino mejor hacia nb
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

    /// <summary>
    /// Coste de transición entre dos caras vecinas.
    /// - Base: distancia entre centros.
    /// - Penalización por pendiente (más caro pisar inclinadas).
    /// - Extra opcional por subir.
    /// </summary>
    private float TransitionCost(int fromFace, int toFace)
    {
        var tri = scanner.triangles;

        float baseDist = Vector3.Distance(tri[fromFace].center, tri[toFace].center);
        float slopeNorm = Mathf.Clamp01(tri[toFace].slopeDeg / Mathf.Max(0.0001f, maxSlopeDeg));
        float cost = baseDist * (1f + slopeCostAlpha * slopeNorm);

        // Penaliza subir si está activado
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

    // ============================================================
    // === (5) VISUALIZACIÓN (GIZMOS) ===
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (scanner == null || scanner.triangles == null) return;

        // Dibuja contornos de triángulos walkables / no walkables
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

        // Dibuja la ruta final (polilínea)
        if (drawPath && pathFaceIds != null && pathFaceIds.Count > 1)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < pathFaceIds.Count - 1; i++)
            {
                var a = scanner.triangles[pathFaceIds[i]].center;
                var b = scanner.triangles[pathFaceIds[i + 1]].center;
                Gizmos.DrawLine(a, b);
            }

            // Esferas para inicio y fin
            var s = scanner.triangles[pathFaceIds[0]].center;
            var g = scanner.triangles[pathFaceIds[^1]].center;
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
