using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways]
public class NavMeshCampZoneFinder : MonoBehaviour
{
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

    // Estructura para guardar datos extra de cada triangulo
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

    void FindCampZones()
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

        // 2. Buscar Vecinos (Topología)
        for (int i = 0; i < triCount; i++)
        {
            for (int j = i + 1; j < triCount; j++)
            {
                int shared = 0;
                if (IsSameVert(tris[i].v0, tris[j]) || IsSameVert(tris[i].v1, tris[j]) || IsSameVert(tris[i].v2, tris[j])) shared++;
                // Nota: Simplifiqué la lógica de vecinos para rendimiento, asumiendo que comparten al menos 2 vertices
                // Pero mantendremos la lógica robusta si prefieres:
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

        // 4. Filtrar zonas inválidas (área, forma, altura mínima)
        List<List<Triangle>> validZones = new List<List<Triangle>>();
        foreach (var zone in potentialZones)
        {
            if (zone.Count < minTrianglesPerZone) continue;

            float totalArea = 0f;
            Bounds bounds = GetZoneBounds(zone); // Usamos Bounds de Unity para facilitar cálculos
            float minY = float.PositiveInfinity;

            foreach (var t in zone)
            {
                totalArea += t.area;
                minY = Mathf.Min(minY, t.v0.y, t.v1.y, t.v2.y);
            }

            if (minY <= minAltitude) continue; // Filtro suelo base

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

        // ---------------------------------------------------------
        // 5. NUEVA FASE DE FUSIÓN (CLUSTERING POR VÉRTICES)
        // ---------------------------------------------------------

        bool mergedAny = true;
        // Repetimos el proceso hasta que no se puedan fusionar más zonas
        while (mergedAny)
        {
            mergedAny = false;
            for (int i = 0; i < validZones.Count; i++)
            {
                for (int j = i + 1; j < validZones.Count; j++)
                {
                    // Comprobamos si la Zona A y la Zona B tienen vértices cercanos
                    if (AreZonesConnectable(validZones[i], validZones[j]))
                    {
                        // Fusionar B dentro de A
                        validZones[i].AddRange(validZones[j]);

                        // Eliminar B
                        validZones.RemoveAt(j);

                        // Reiniciar bucles porque la lista ha cambiado
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
            // Calcular centroide de la zona fusionada
            Vector3 avg = Vector3.zero;
            foreach (var t in zone) avg += t.center;
            avg /= zone.Count;

            // Encontrar el punto más cercano al promedio que sea válido
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

            // Muestrear NavMesh para asegurar posición válida
            NavMeshHit hit;
            if (NavMesh.SamplePosition(bestCenter, out hit, 10f, NavMesh.AllAreas))
            {
                campZones.Add(hit.position);
            }
            else
            {
                campZones.Add(bestCenter);
            }

            // Debug visual
            if (drawDebug && Application.isPlaying)
            {
                Color c = new Color(Random.value, Random.value, Random.value);
                foreach (var t in zone)
                {
                    Debug.DrawLine(t.v0, t.v1, c, 500f);
                    Debug.DrawLine(t.v1, t.v2, c, 500f);
                    Debug.DrawLine(t.v2, t.v0, c, 500f);
                }
            }
        }
    }

    // --- FUNCIONES AUXILIARES ---

    // Comprueba si dos zonas deben unirse basándose en sus vértices
    bool AreZonesConnectable(List<Triangle> zoneA, List<Triangle> zoneB)
    {
        // 1. Optimización con Bounds: Si las cajas delimitadoras están lejos, ni miramos los vértices
        Bounds bA = GetZoneBounds(zoneA);
        Bounds bB = GetZoneBounds(zoneB);

        // Expandimos una caja por la distancia de merge para ver si intersecta
        bA.Expand(mergeProximity * 2);
        if (!bA.Intersects(bB)) return false;

        // 2. Comprobación detallada de vértices
        // Buscamos SI EXISTE al menos UN par de vértices (uno de A y uno de B)
        // que cumplan la condición de distancia y altura.

        float distSq = mergeProximity * mergeProximity;

        // Para optimizar, no comparamos todos contra todos si son muchos,
        // pero para NavMesh zones suele ser aceptable.
        foreach (var tA in zoneA)
        {
            foreach (var tB in zoneB)
            {
                // Comparamos centros de triangulos primero (rápido)
                if ((tA.center - tB.center).sqrMagnitude < distSq * 4) // *4 para dar margen
                {
                    // Si los triángulos están cerca, miramos sus vértices
                    if (CheckVerts(tA.v0, tB) || CheckVerts(tA.v1, tB) || CheckVerts(tA.v2, tB))
                        return true;
                }
            }
        }
        return false;
    }

    bool CheckVerts(Vector3 pA, Triangle tB)
    {
        // Compara un punto A con los 3 puntos de B
        if (IsNear(pA, tB.v0)) return true;
        if (IsNear(pA, tB.v1)) return true;
        if (IsNear(pA, tB.v2)) return true;
        return false;
    }

    bool IsNear(Vector3 a, Vector3 b)
    {
        float hDiff = Mathf.Abs(a.y - b.y);
        if (hDiff > mergeHeightThreshold) return false; // Diferencia de altura excesiva

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

    bool IsSameVert(Vector3 v, Triangle t)
    {
        return ApproximatelyEqual(v, t.v0) || ApproximatelyEqual(v, t.v1) || ApproximatelyEqual(v, t.v2);
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