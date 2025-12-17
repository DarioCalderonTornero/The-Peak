using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways]
public class NavMeshCampZoneFinder : MonoBehaviour
{
    [Header("Instanciación (Visual)")] // <<< NUEVO >>>
    public GameObject flagPrefab;      // <<< NUEVO: Arrastra tu prefab de bandera aquí
    public Transform flagsContainer;   // <<< NUEVO: Opcional, para organizar la jerarquía
    private List<GameObject> spawnedFlags = new List<GameObject>(); // <<< NUEVO: Lista para control

    [Header("Detección de zonas planas")]
    public float maxSlope = 5f;
    public int minTrianglesPerZone = 10;

    [Header("Tamaño mínimo de campamento")]
    public float minZoneArea = 10f;
    public float minZoneWidth = 3f;
    public float minZoneDepth = 3f;
    [Range(0f, 1f)]
    public float minCompactness = 0.3f;

    [Header("Restricciones y Agrupamiento")]
    public float minAltitude = 0.5f;

    [Tooltip("Distancia máxima entre cualquier vértice de dos zonas para considerarlas 'conectadas'")]
    public float mergeProximity = 5.0f;

    [Tooltip("Diferencia máxima de altura entre vértices cercanos para fusionar")]
    public float mergeHeightThreshold = 1.5f;

    [Header("Debug")]
    public bool drawDebug = true;
    public float gizmoRadius = 0.75f;

    [SerializeField, HideInInspector]
    public List<Vector3> campZones = new List<Vector3>();

    struct Triangle
    {
        public Vector3 v0, v1, v2;
        public Vector3 center;
        public float slope;
        public float area;
        public List<int> neighbors;
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            FindCampZones();
        }
    }

    // Método público por si quieres llamarlo desde un botón de inspector custom
    public void FindCampZones()
    {
        NavMeshTriangulation data = NavMesh.CalculateTriangulation();
        int triCount = data.indices.Length / 3;
        Triangle[] tris = new Triangle[triCount];

        // 1. Procesar Triángulos
        for (int i = 0; i < triCount; i++)
        {
            Vector3 v0 = data.vertices[data.indices[i * 3]];
            Vector3 v1 = data.vertices[data.indices[i * 3 + 1]];
            Vector3 v2 = data.vertices[data.indices[i * 3 + 2]];

            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            float area = 0.5f * n.magnitude;
            Vector3 normal = n.sqrMagnitude > 0.000001f ? n.normalized : Vector3.up;
            float slope = Vector3.Angle(normal, Vector3.up);
            Vector3 center = (v0 + v1 + v2) / 3f;

            tris[i] = new Triangle
            {
                v0 = v0,
                v1 = v1,
                v2 = v2,
                center = center,
                slope = slope,
                area = area,
                neighbors = new List<int>()
            };
        }

        // 2. Buscar Vecinos
        for (int i = 0; i < triCount; i++)
        {
            for (int j = i + 1; j < triCount; j++)
            {
                int sharedV = 0;
                if (ApproximatelyEqual(tris[i].v0, tris[j].v0) || ApproximatelyEqual(tris[i].v0, tris[j].v1) || ApproximatelyEqual(tris[i].v0, tris[j].v2)) sharedV++;
                if (ApproximatelyEqual(tris[i].v1, tris[j].v0) || ApproximatelyEqual(tris[i].v1, tris[j].v1) || ApproximatelyEqual(tris[i].v1, tris[j].v2)) sharedV++;
                if (ApproximatelyEqual(tris[i].v2, tris[j].v0) || ApproximatelyEqual(tris[i].v2, tris[j].v1) || ApproximatelyEqual(tris[i].v2, tris[j].v2)) sharedV++;

                if (sharedV >= 2)
                {
                    tris[i].neighbors.Add(j);
                    tris[j].neighbors.Add(i);
                }
            }
        }

        // 3. Agrupar zonas iniciales (Flood Fill)
        bool[] visited = new bool[triCount];
        List<List<Triangle>> potentialZones = new List<List<Triangle>>();

        for (int i = 0; i < triCount; i++)
        {
            if (visited[i] || tris[i].slope > maxSlope) continue;

            List<Triangle> zone = new List<Triangle>();
            Stack<int> stack = new Stack<int>();
            stack.Push(i);

            while (stack.Count > 0)
            {
                int t = stack.Pop();
                if (visited[t]) continue;
                visited[t] = true;
                if (tris[t].slope > maxSlope) continue;

                zone.Add(tris[t]);
                foreach (int n in tris[t].neighbors)
                {
                    if (!visited[n] && tris[n].slope <= maxSlope) stack.Push(n);
                }
            }
            if (zone.Count > 0) potentialZones.Add(zone);
        }

        // 4. Filtrar zonas inválidas
        List<List<Triangle>> validZones = new List<List<Triangle>>();
        foreach (var zone in potentialZones)
        {
            if (zone.Count < minTrianglesPerZone) continue;

            float totalArea = 0f;
            Bounds bounds = GetZoneBounds(zone);
            float minY = float.PositiveInfinity;

            foreach (var t in zone)
            {
                totalArea += t.area;
                minY = Mathf.Min(minY, t.v0.y, t.v1.y, t.v2.y);
            }

            if (minY <= minAltitude) continue;

            float width = bounds.size.x;
            float depth = bounds.size.z;

            if (width <= 0.01f || depth <= 0.01f) continue;

            float bboxArea = width * depth;
            float compactness = totalArea / bboxArea;

            if (totalArea < minZoneArea) continue;
            if (width < minZoneWidth || depth < minZoneDepth) continue;
            if (compactness < minCompactness) continue;

            validZones.Add(zone);
        }

        // 5. Fusión (Clustering)
        bool mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            for (int i = 0; i < validZones.Count; i++)
            {
                for (int j = i + 1; j < validZones.Count; j++)
                {
                    if (AreZonesConnectable(validZones[i], validZones[j]))
                    {
                        validZones[i].AddRange(validZones[j]);
                        validZones.RemoveAt(j);
                        mergedAny = true;
                        break;
                    }
                }
                if (mergedAny) break;
            }
        }

        // 6. Calcular puntos finales
        campZones.Clear();

        foreach (var zone in validZones)
        {
            Vector3 avg = Vector3.zero;
            foreach (var t in zone) avg += t.center;
            avg /= zone.Count;

            float bestDist = float.MaxValue;
            Vector3 bestCenter = avg;

            foreach (var t in zone)
            {
                float d = (t.center - avg).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestCenter = t.center;
                }
            }

            NavMeshHit hit;
            if (NavMesh.SamplePosition(bestCenter, out hit, 10f, NavMesh.AllAreas))
            {
                campZones.Add(hit.position);
            }
            else
            {
                campZones.Add(bestCenter);
            }
        }

        // <<< NUEVO: Instanciar las banderas al terminar el cálculo >>>
        SpawnFlags();
    }

    // --- LÓGICA DE INSTANCIACIÓN --- // <<< NUEVO >>>
    void SpawnFlags()
    {
        // 1. Limpiar banderas antiguas
        if (spawnedFlags == null) spawnedFlags = new List<GameObject>();

        // Eliminamos objetos nulos de la lista por seguridad
        for (int i = spawnedFlags.Count - 1; i >= 0; i--)
        {
            if (spawnedFlags[i] != null)
            {
                if (Application.isPlaying) Destroy(spawnedFlags[i]);
                else DestroyImmediate(spawnedFlags[i]);
            }
        }
        spawnedFlags.Clear();

        // 2. Si no hay prefab asignado, salir
        if (flagPrefab == null)
        {
            Debug.LogWarning("NavMeshCampZoneFinder: No has asignado el 'Flag Prefab'.");
            return;
        }

        // 3. Crear contenedor si no existe (para no ensuciar la jerarquía)
        if (flagsContainer == null)
        {
            GameObject container = GameObject.Find("CampFlags_Container");
            if (container == null) container = new GameObject("CampFlags_Container");
            flagsContainer = container.transform;
        }

        // 4. Instanciar nuevas banderas
        foreach (Vector3 pos in campZones)
        {
            GameObject newFlag = Instantiate(flagPrefab, pos, Quaternion.identity);

            // Asignar padre
            newFlag.transform.SetParent(flagsContainer);

            // Opcional: Alinear con la normal del terreno si quieres que no estén rectas
            // Raycast abajo para detectar pendiente si fuera necesario, pero Identity suele ir bien.

            spawnedFlags.Add(newFlag);
        }
    }

    // --- FUNCIONES AUXILIARES ---

    bool AreZonesConnectable(List<Triangle> zoneA, List<Triangle> zoneB)
    {
        Bounds bA = GetZoneBounds(zoneA);
        Bounds bB = GetZoneBounds(zoneB);
        bA.Expand(mergeProximity * 2);
        if (!bA.Intersects(bB)) return false;

        float distSq = mergeProximity * mergeProximity;

        foreach (var tA in zoneA)
        {
            foreach (var tB in zoneB)
            {
                if ((tA.center - tB.center).sqrMagnitude < distSq * 4)
                {
                    if (CheckVerts(tA.v0, tB) || CheckVerts(tA.v1, tB) || CheckVerts(tA.v2, tB))
                        return true;
                }
            }
        }
        return false;
    }

    bool CheckVerts(Vector3 pA, Triangle tB)
    {
        if (IsNear(pA, tB.v0)) return true;
        if (IsNear(pA, tB.v1)) return true;
        if (IsNear(pA, tB.v2)) return true;
        return false;
    }

    bool IsNear(Vector3 a, Vector3 b)
    {
        float hDiff = Mathf.Abs(a.y - b.y);
        if (hDiff > mergeHeightThreshold) return false;

        float dSq = (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z);
        return dSq < (mergeProximity * mergeProximity);
    }

    Bounds GetZoneBounds(List<Triangle> zone)
    {
        if (zone.Count == 0) return new Bounds();
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var t in zone)
        {
            min = Vector3.Min(min, Vector3.Min(t.v0, Vector3.Min(t.v1, t.v2)));
            max = Vector3.Max(max, Vector3.Max(t.v0, Vector3.Max(t.v1, t.v2)));
        }
        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    bool ApproximatelyEqual(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) < 0.0001f;
    }

    void OnDrawGizmos()
    {
        if (campZones == null) return;

        Gizmos.color = Color.cyan;
        foreach (var pos in campZones)
        {
            Gizmos.DrawSphere(pos + Vector3.up * 0.5f, gizmoRadius);
        }
    }
}