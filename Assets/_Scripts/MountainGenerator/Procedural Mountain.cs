using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMountain : MonoBehaviour
{
    [Header("Mountain Shape Settings")]
    public int resolution = 50;
    public float height = 10f;
    public float radius = 5f;

    [Header("Noise Settings")]
    public float baseNoiseScale = 1f;
    public float detailNoiseScale = 5f;
    public float noiseStrength = 2f;
    public float terraceVariation = 0.3f; // Variabilidad entre terrazas

    [Header("Terraces")]
    public int terraceCount = 6;
    [Range(0f, 1f)] public float terraceBlend = 0.9f;

    [Header("Aleatoriedad")]
    public bool randomizeSeed = true;
    public int seed = 0;

    private Mesh mesh;
    private Vector2 baseNoiseOffset;
    private Vector2 detailNoiseOffset;

    void Start()
    {
        InitializeSeed();
        GenerateMountain();
    }

    void InitializeSeed()
    {
        if (randomizeSeed)
        {
            seed = Random.Range(0, 1000000);
        }

        Random.InitState(seed);
        baseNoiseOffset = new Vector2(Random.Range(0f, 9999f), Random.Range(0f, 9999f));
        detailNoiseOffset = new Vector2(Random.Range(0f, 9999f), Random.Range(0f, 9999f));
    }

    public void GenerateMountain()
    {
        // Re-inicializa la semilla si está activado el modo aleatorio
        InitializeSeed();

        mesh = new Mesh();
        mesh.name = "Procedural Mountain";

        var vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        var triangles = new int[resolution * resolution * 6];
        var uv = new Vector2[vertices.Length];

        int i = 0;
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float xPos = (x / (float)resolution - 0.5f) * radius * 2f;
                float zPos = (z / (float)resolution - 0.5f) * radius * 2f;

                float dist = Vector2.Distance(new Vector2(xPos, zPos), Vector2.zero);
                float heightFactor = Mathf.Clamp01(1 - dist / radius);

                // Variación entre terrazas
                float terraceNoise = Mathf.PerlinNoise(
                    x * 0.2f + baseNoiseOffset.x,
                    z * 0.2f + baseNoiseOffset.y
                ) * terraceVariation;

                float noise = Mathf.PerlinNoise(
                    (x * baseNoiseScale / resolution) + baseNoiseOffset.x,
                    (z * baseNoiseScale / resolution) + baseNoiseOffset.y
                );

                float detailNoise = Mathf.PerlinNoise(
                    (x * detailNoiseScale / resolution) + detailNoiseOffset.x,
                    (z * detailNoiseScale / resolution) + detailNoiseOffset.y
                );

                float yPos = height * (heightFactor + terraceNoise);
                Vector3 pos = new Vector3(xPos, yPos, zPos);

                Vector3 radialDir = new Vector3(xPos, 0f, zPos).normalized;
                float noiseModifier = 1f - (yPos / height);

                pos += radialDir * ((noise - 0.5f) + (detailNoise - 0.5f)) *
                       noiseStrength * heightFactor * noiseModifier;

                float step = height / Mathf.Max(1, terraceCount);
                float quantizedY = Mathf.Round(pos.y / step) * step;
                pos.y = Mathf.Lerp(pos.y, quantizedY, terraceBlend);

                vertices[i] = pos;
                uv[i] = new Vector2(x / (float)resolution, z / (float)resolution);
                i++;
            }
        }

        int t = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int start = z * (resolution + 1) + x;

                triangles[t++] = start;
                triangles[t++] = start + resolution + 1;
                triangles[t++] = start + 1;

                triangles[t++] = start + 1;
                triangles[t++] = start + resolution + 1;
                triangles[t++] = start + resolution + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        FlatShading(mesh);

        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void FlatShading(Mesh mesh)
    {
        Vector3[] oldVerts = mesh.vertices;
        int[] triangles = mesh.triangles;                               
        Vector3[] newVerts = new Vector3[triangles.Length];
        Vector2[] newUVs = new Vector2[triangles.Length];

        for (int i = 0; i < triangles.Length; i++)
        {
            newVerts[i] = oldVerts[triangles[i]];
            newUVs[i] = mesh.uv[triangles[i]];
            triangles[i] = i;
        }

        mesh.vertices = newVerts;
        mesh.uv = newUVs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
