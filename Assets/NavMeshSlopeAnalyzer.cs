using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways] // Para poder recalcular también en el editor si quieres
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

    void FindCampZones()
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

       
        for (int i = 0; i < triCount; i++)
        {
            for (int j = i + 1; j < triCount; j++)
            {
                int shared = 0;
                if (ApproximatelyEqual(tris[i].v0, tris[j].v0) ||
                    ApproximatelyEqual(tris[i].v0, tris[j].v1) ||
                    ApproximatelyEqual(tris[i].v0, tris[j].v2)) shared++;

                if (ApproximatelyEqual(tris[i].v1, tris[j].v0) ||
                    ApproximatelyEqual(tris[i].v1, tris[j].v1) ||
                    ApproximatelyEqual(tris[i].v1, tris[j].v2)) shared++;

                if (ApproximatelyEqual(tris[i].v2, tris[j].v0) ||
                    ApproximatelyEqual(tris[i].v2, tris[j].v1) ||
                    ApproximatelyEqual(tris[i].v2, tris[j].v2)) shared++;

                if (shared >= 2)
                {
                    tris[i].neighbors.Add(j);
                    tris[j].neighbors.Add(i);
                }
            }
        }

        
        bool[] visited = new bool[triCount];
        List<List<Triangle>> rawZones = new List<List<Triangle>>();

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
                    if (!visited[n] && tris[n].slope <= maxSlope)
                        stack.Push(n);
                }
            }

            if (zone.Count > 0)
            {
                rawZones.Add(zone);
            }
        }

        
        List<List<Triangle>> validZones = new List<List<Triangle>>();

        foreach (var zone in rawZones)
        {
            if (zone.Count < minTrianglesPerZone)
                continue;

            float totalArea = 0f;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

            foreach (var t in zone)
            {
                totalArea += t.area;

                
                minX = Mathf.Min(minX, t.v0.x, t.v1.x, t.v2.x);
                maxX = Mathf.Max(maxX, t.v0.x, t.v1.x, t.v2.x);
                minZ = Mathf.Min(minZ, t.v0.z, t.v1.z, t.v2.z);
                maxZ = Mathf.Max(maxZ, t.v0.z, t.v1.z, t.v2.z);
            }

            float width = maxX - minX;
            float depth = maxZ - minZ;

   
            if (width <= 0.01f || depth <= 0.01f)
                continue;

            float bboxArea = width * depth;
            float compactness = totalArea / bboxArea; 

            
            if (totalArea < minZoneArea)            
                continue;
            if (width < minZoneWidth || depth < minZoneDepth) 
                continue;
            if (compactness < minCompactness)      
                continue;

            validZones.Add(zone);
        }

        
        campZones.Clear();

        foreach (var zone in validZones)
        {
           
            Vector3 avg = Vector3.zero;
            foreach (var t in zone)
                avg += t.center;
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
            if (NavMesh.SamplePosition(bestCenter, out hit, 2f, NavMesh.AllAreas))
            {
                campZones.Add(hit.position);
            }
            else
            {
                campZones.Add(bestCenter);
            }

           
            if (drawDebug && Application.isPlaying)
            {
                Color c = new Color(Random.value, Random.value, Random.value);
                foreach (var t in zone)
                {
                    Debug.DrawLine(t.v0, t.v1, c, 5f);
                    Debug.DrawLine(t.v1, t.v2, c, 5f);
                    Debug.DrawLine(t.v2, t.v0, c, 5f);
                }
            }
        }
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
            Gizmos.DrawSphere(pos + Vector3.up * 0.05f, gizmoRadius);
        }
    }
}
