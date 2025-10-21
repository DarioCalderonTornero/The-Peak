using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MeshSlopeScannerSimple:
/// Escanea un MeshFilter (por ejemplo, una montaña),
/// calcula la normal de **cara**, el centro, la pendiente (grados) y el área de cada triángulo.
/// Dibuja los gizmos justo en la posición real del triángulo.
/// </summary>
public class MeshSlopeScannerSimple : MonoBehaviour
{
    [Header("Opciones")]
    public float vertexPrecision = 0.001f; // (No usado por ahora; se mantiene a petición)
    public float gizmoOffset = 0.05f;      // Desplazamiento visual para dibujar los gizmos sobre la montaña

    [Header("Resultado")]
    public List<TriangleData> triangles; // Datos de cada triángulo

    // Guardamos la referencia del transform real de la montaña
    private Transform mountainTransform;

    // Umbral interno para filtrar triángulos degenerados (área ~ 0)
    private const float AREA_EPS = 1e-6f;

    [System.Serializable]
    public class TriangleData
    {
        public int id;                // Índice del triángulo
        public Vector3 v0, v1, v2;    // Vértices en espacio MUNDIAL
        public Vector3 center;        // Centro geométrico (mundo)
        public Vector3 normal;        // Normal de la CARA (mundo)
        public int highestVertexIndex; // Índice (0,1,2) del vértice más alto

        // NUEVO: métricas para siguientes fases
        public float slopeDeg;        // Ángulo respecto a Vector3.up (pendiente en grados)
        public float area;            // Área del triángulo (mundo)
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Buscar automáticamente la montaña (tag "Mountain")
            GameObject mountain = GameObject.FindWithTag("Mountain");
            if (mountain == null)
            {
                Debug.LogError("No se encontró ningún objeto con tag 'Mountain'.");
                return;
            }

            MeshFilter meshFilter = mountain.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("El objeto 'Mountain' no tiene MeshFilter o Mesh asignado.");
                return;
            }

            mountainTransform = meshFilter.transform;
            ScanMesh(meshFilter);
            Debug.Log($"Escaneo completado. Triángulos detectados: {triangles.Count}");
        }
    }

    public void ScanMesh(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] tris = mesh.triangles;
        Vector3[] normals = mesh.normals; // (Se mantiene aunque no se use, para no tocar el punto 4)

        triangles = new List<TriangleData>();
        int triCount = tris.Length / 3;
        Transform t = meshFilter.transform;

        for (int i = 0; i < triCount; i++)
        {
            int idx0 = tris[i * 3 + 0];
            int idx1 = tris[i * 3 + 1];
            int idx2 = tris[i * 3 + 2];

            // Convertimos los vértices a coordenadas de MUNDO
            Vector3 v0 = t.TransformPoint(vertices[idx0]);
            Vector3 v1 = t.TransformPoint(vertices[idx1]);
            Vector3 v2 = t.TransformPoint(vertices[idx2]);

            // === CAMBIO 1: Normal de CARA en espacio de mundo ===
            // Usamos la normal del triángulo derivada de los vértices en MUNDO
            Vector3 e0 = v1 - v0;
            Vector3 e1 = v2 - v0;
            Vector3 faceCross = Vector3.Cross(e0, e1);
            float area = 0.5f * faceCross.magnitude;

            // === CAMBIO 3: Filtrar triángulos degenerados ===
            if (area < AREA_EPS)
            {
                continue; // Saltamos triángulos con área casi nula
            }

            Vector3 normal = faceCross.normalized;

            // Centro del triángulo en espacio de mundo
            Vector3 center = (v0 + v1 + v2) / 3f;

            // Determinar el vértice más alto
            float y0 = v0.y;
            float y1 = v1.y;
            float y2 = v2.y;
            int highestIndex = 0;
            float highestY = y0;

            if (y1 > highestY) { highestY = y1; highestIndex = 1; }
            if (y2 > highestY) { highestY = y2; highestIndex = 2; }

            // === CAMBIO 2: Métricas clave ===
            float slopeDeg = Vector3.Angle(normal, Vector3.up); // 0° plano horizontal; 90° vertical

            triangles.Add(new TriangleData
            {
                id = i,
                v0 = v0,
                v1 = v1,
                v2 = v2,
                center = center,
                normal = normal,
                highestVertexIndex = highestIndex,
                slopeDeg = slopeDeg,
                area = area
            });
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (triangles == null || triangles.Count == 0)
            return;

        foreach (var t in triangles)
        {
            // Desplazamos el gizmo sobre la superficie real del triángulo
            Vector3 offsetPos = t.center + t.normal * gizmoOffset;

            // Dibujamos todos los gizmos del mismo color
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(offsetPos, 0.04f);

            // Dibujar la normal en azul
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(offsetPos, offsetPos + t.normal * 0.25f);
        }
    }
}
