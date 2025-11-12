using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshCampZoneFinder : MonoBehaviour
{
    public float maxSlope = 5f;
    public int minTrianglesPerZone = 10;
    public bool drawDebug = true;

    public List<Vector3> campZones = new List<Vector3>();

    struct Triangle
    {
        public Vector3 v0, v1, v2;
        public Vector3 center;
        public float slope;
        public List<int> neighbors;
    }

    void Start()
    {
        FindCampZones();
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

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            float slope = Vector3.Angle(normal, Vector3.up);
            Vector3 center = (v0 + v1 + v2) / 3f;

            tris[i] = new Triangle
            {
                v0 = v0,
                v1 = v1,
                v2 = v2,
                center = center,
                slope = slope,
                neighbors = new List<int>()
            };
        }


        for (int i = 0; i < triCount; i++)
        {
            for (int j = i + 1; j < triCount; j++)
            {
                int shared = 0;
                if (ApproximatelyEqual(tris[i].v0, tris[j].v0) || ApproximatelyEqual(tris[i].v0, tris[j].v1) || ApproximatelyEqual(tris[i].v0, tris[j].v2)) shared++;
                if (ApproximatelyEqual(tris[i].v1, tris[j].v0) || ApproximatelyEqual(tris[i].v1, tris[j].v1) || ApproximatelyEqual(tris[i].v1, tris[j].v2)) shared++;
                if (ApproximatelyEqual(tris[i].v2, tris[j].v0) || ApproximatelyEqual(tris[i].v2, tris[j].v1) || ApproximatelyEqual(tris[i].v2, tris[j].v2)) shared++;
                if (shared >= 2)
                {
                    tris[i].neighbors.Add(j);
                    tris[j].neighbors.Add(i);
                }
            }
        }

        
        bool[] visited = new bool[triCount];
        List<List<Triangle>> zones = new List<List<Triangle>>();

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

            if (zone.Count >= minTrianglesPerZone)
                zones.Add(zone);
        }

        campZones.Clear();
        foreach (var zone in zones)
        {
            Vector3 avg = Vector3.zero;
            foreach (var t in zone)
                avg += t.center;
            avg /= zone.Count;
            campZones.Add(avg);
        }

        if (drawDebug)
        {
            foreach (var zone in zones)
            {
                Color c = new Color(Random.value, Random.value, Random.value);
                foreach (var t in zone)
                {
                    Debug.DrawLine(t.v0, t.v1, c, 100);
                    Debug.DrawLine(t.v1, t.v2, c, 100);
                    Debug.DrawLine(t.v2, t.v0, c, 100);
                }
            }
        }

        
    }

    bool ApproximatelyEqual(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) < 0.0001f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var pos in campZones)
            Gizmos.DrawSphere(pos + Vector3.up * 0.05f, 1f);
    }
}
