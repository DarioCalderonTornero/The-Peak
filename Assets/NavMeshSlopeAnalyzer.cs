using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways]
public class NavMeshCampZoneFinder : MonoBehaviour
{
    [Header("Instanciación (Visual)")]
    public GameObject flagPrefab;
    public Transform flagsContainer;
    private List<GameObject> spawnedFlags = new List<GameObject>();

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
    public float mergeProximity = 5.0f;
    public float mergeHeightThreshold = 1.5f;

    [Header("Debug")]
    public bool drawDebug = true;

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

    void Start() { if (Application.isPlaying) FindCampZones(); }

    public void FindCampZones()
    {
        NavMeshTriangulation data = NavMesh.CalculateTriangulation();
        int triCount = data.indices.Length / 3;
        Triangle[] tris = new Triangle[triCount];

        for (int i = 0; i < triCount; i++)
        {
            Vector3 v0 = data.vertices[data.indices[i * 3]];
            Vector3 v1 = data.vertices[data.indices[i * 3 + 1]];
            Vector3 v2 = data.vertices[data.indices[i * 3 + 2]];
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            tris[i] = new Triangle
            {
                v0 = v0,
                v1 = v1,
                v2 = v2,
                center = (v0 + v1 + v2) / 3f,
                slope = Vector3.Angle(n.sqrMagnitude > 0.000001f ? n.normalized : Vector3.up, Vector3.up),
                area = 0.5f * n.magnitude,
                neighbors = new List<int>()
            };
        }

        for (int i = 0; i < triCount; i++)
        {
            for (int j = i + 1; j < triCount; j++)
            {
                int sharedV = 0;
                if (ApproximatelyEqual(tris[i].v0, tris[j].v0) || ApproximatelyEqual(tris[i].v0, tris[j].v1) || ApproximatelyEqual(tris[i].v0, tris[j].v2)) sharedV++;
                if (ApproximatelyEqual(tris[i].v1, tris[j].v0) || ApproximatelyEqual(tris[i].v1, tris[j].v1) || ApproximatelyEqual(tris[i].v1, tris[j].v2)) sharedV++;
                if (ApproximatelyEqual(tris[i].v2, tris[j].v0) || ApproximatelyEqual(tris[i].v2, tris[j].v1) || ApproximatelyEqual(tris[i].v2, tris[j].v2)) sharedV++;
                if (sharedV >= 2) { tris[i].neighbors.Add(j); tris[j].neighbors.Add(i); }
            }
        }

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
                zone.Add(tris[t]);
                foreach (int n in tris[t].neighbors) if (!visited[n] && tris[n].slope <= maxSlope) stack.Push(n);
            }
            if (zone.Count > 0) potentialZones.Add(zone);
        }

        List<List<Triangle>> validZones = new List<List<Triangle>>();
        foreach (var zone in potentialZones)
        {
            if (zone.Count < minTrianglesPerZone) continue;
            float totalArea = 0f; float minY = float.PositiveInfinity;
            Bounds b = GetZoneBounds(zone);
            foreach (var t in zone) { totalArea += t.area; minY = Mathf.Min(minY, t.v0.y, t.v1.y, t.v2.y); }
            if (minY <= minAltitude || b.size.x < minZoneWidth || b.size.z < minZoneDepth || (totalArea / (b.size.x * b.size.z)) < minCompactness) continue;
            validZones.Add(zone);
        }

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
                        validZones.RemoveAt(j); mergedAny = true; break;
                    }
                }
                if (mergedAny) break;
            }
        }

        campZones.Clear();
        foreach (var zone in validZones)
        {
            Bounds b = GetZoneBounds(zone);
            Vector3 absCenter = b.center; // CENTRO MATEMÁTICO PURO

            // Promedio de altura de los triángulos para evitar que flote
            float avgY = 0;
            foreach (var t in zone) avgY += t.center.y;
            absCenter.y = avgY / zone.Count;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(absCenter, out hit, 5f, NavMesh.AllAreas))
            {
                campZones.Add(hit.position);
            }
            else
            {
                // Fallback al triángulo más cercano solo si el centro está fuera del NavMesh
                float bestD = float.MaxValue; Vector3 fallback = absCenter;
                foreach (var t in zone)
                {
                    float d = (t.center - absCenter).sqrMagnitude;
                    if (d < bestD) { bestD = d; fallback = t.center; }
                }
                campZones.Add(fallback);
            }
        }
        SpawnFlags();
    }

    void SpawnFlags()
    {
        if (spawnedFlags == null) spawnedFlags = new List<GameObject>();
        for (int i = spawnedFlags.Count - 1; i >= 0; i--) if (spawnedFlags[i] != null) DestroyImmediate(spawnedFlags[i]);
        spawnedFlags.Clear();
        if (flagPrefab == null) return;
        if (flagsContainer == null)
        {
            GameObject c = GameObject.Find("CampFlags_Container");
            if (c == null) c = new GameObject("CampFlags_Container");
            flagsContainer = c.transform;
        }
        foreach (Vector3 pos in campZones) spawnedFlags.Add(Instantiate(flagPrefab, pos, Quaternion.identity, flagsContainer));
    }

    bool AreZonesConnectable(List<Triangle> zA, List<Triangle> zB)
    {
        Bounds bA = GetZoneBounds(zA); bA.Expand(mergeProximity * 2);
        if (!bA.Intersects(GetZoneBounds(zB))) return false;
        foreach (var tA in zA) foreach (var tB in zB)
            {
                if (IsNear(tA.v0, tB.v0) || IsNear(tA.v1, tB.v1) || IsNear(tA.v2, tB.v2)) return true;
            }
        return false;
    }

    bool IsNear(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < mergeHeightThreshold && (new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude < mergeProximity * mergeProximity);

    Bounds GetZoneBounds(List<Triangle> zone)
    {
        Vector3 min = Vector3.one * float.MaxValue; Vector3 max = Vector3.one * float.MinValue;
        foreach (var t in zone)
        {
            min = Vector3.Min(min, Vector3.Min(t.v0, Vector3.Min(t.v1, t.v2)));
            max = Vector3.Max(max, Vector3.Max(t.v0, Vector3.Max(t.v1, t.v2)));
        }
        Bounds b = new Bounds(); b.SetMinMax(min, max); return b;
    }

    bool ApproximatelyEqual(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.0001f;
}