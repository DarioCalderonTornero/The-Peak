using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class SimpleMountainGenerator : MonoBehaviour
{
    
    [Header("Grid / Scale")]
    public int resolution = 256;           // Número de vértices por lado (mejor potencia de 2 + 1)
    public float radius = 30f;             // Radio de la base de la montaña
    public float height = 60f;             // Altura máxima de la montaña

    [Header("Noise / Shape")]
    public float baseFreq = 0.06f;         // Frecuencia base para el ruido fractal
    public int octaves = 5;                // Número de octavas para fBM
    public float lacunarity = 2f;          // Factor de frecuencia para cada octava
    public float persistence = 0.5f;       // Amplitud decreciente por octava
    public float noiseAmplitude = 8f;      // Amplitud del ruido
    [Range(1.0f, 4.0f)] public float falloffPower = 2.2f; // Exponente para caída radial

    [Header("Erosion")]
    public int erosionIterations = 300;    // Iteraciones de erosión
    [Range(0.01f, 1f)] public float erosionTalus = 0.5f; // Fracción de altura a mover en erosión
    public bool applyErosion = true;       // Activar/desactivar erosión

    [Header("Single Peak Enforcement")]
    [Range(0.5f, 0.98f)] public float secondaryPeakThreshold = 0.9f; // Controla otros picos

    [Header("Paths & Camping")]
    public float handholdMinSlopeDeg = 35f;
    public float handholdMaxSlopeDeg = 75f;
    public GameObject handholdPrefab;
    public int maxHandholds = 300;
    public float handholdPlacementJitter = 0.6f;

    [Header("Mesh / Optimization")]
    public bool generateCollider = true;
    public bool recalcNormals = true;

    [Header("Aleatoriedad")]
    public bool randomizeSeed = true;
    public int seed = 0;

    [Header("Pico Principal")]
    [Range(0.5f, 5f)] public float peakSharpness = 2.5f; // Controla la agudeza del pico

    [Header("Variabilidad de altura")]
    [Range(0f, 0.5f)] public float heightVariation = 0.2f; // Variación aleatoria de la altura

    // --- Offsets aleatorios para ruido ---
    private float offsetX;
    private float offsetY;

    // --- Datos internos ---
    float[,] heights;
    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;
    List<GameObject> spawnedHandholds = new List<GameObject>();

    void Start()
    {
        GenerateMountain();
    }

    [ContextMenu("Regenerar Montaña")] // Botón en el Inspector para regenerar
    public void GenerateMountain()
    {
        // Inicializa semilla aleatoria si está activado
        if (randomizeSeed)
            seed = Random.Range(0, int.MaxValue);

        Random.InitState(seed);

        // Genera offsets aleatorios para el ruido, asegurando variaciones
        offsetX = Random.Range(-10000f, 10000f);
        offsetY = Random.Range(-10000f, 10000f);

        // Aplica variación de altura
        float originalHeight = height;
        float heightOffset = Random.Range(-heightVariation, heightVariation) * height;
        height += heightOffset;

        // Prepara la matriz de alturas
        PrepareHeightArray();

        // Genera heightmap base con caída radial y ruido fractal
        GenerateBaseHeightmap();

        // Refuerza un único pico principal
        EnforceSinglePeak();

        // Aplica erosión hidráulica si está activada
        if (applyErosion) ApplyHydraulicErosion();

        // Construye la malla a partir del heightmap
        BuildMesh();

        // Recalcula normales si está activado
        if (recalcNormals) mesh.RecalculateNormals();

        // Actualiza MeshCollider si está activado
        if (generateCollider) UpdateCollider();

        // Coloca handholds según la pendiente
        PlaceHandholds();

        // Restaura la altura original para futuras regeneraciones
        height = originalHeight;

        Debug.Log($"Mountain generated (seed={seed})");
    }

    /// Inicializa la matriz de alturas
    void PrepareHeightArray()
    {
        heights = new float[resolution, resolution];
    }

    /// Convierte coordenadas de la cuadrícula a posiciones en el mundo
    Vector2 GridToWorldXY(int gx, int gy)
    {
        float fracX = gx / (float)(resolution - 1);
        float fracY = gy / (float)(resolution - 1);
        float wx = (fracX - 0.5f) * 2f * radius;
        float wz = (fracY - 0.5f) * 2f * radius;
        return new Vector2(wx, wz);
    }

    /// Genera heightmap base con tres secciones, pico desplazado, variabilidad de pendiente y zonas planas/empinadas
    void GenerateBaseHeightmap()
    {
        float maxOffset = radius * 0.2f;
        float peakOffsetX = Random.Range(-maxOffset, maxOffset);
        float peakOffsetY = Random.Range(-maxOffset, maxOffset);

        float[,] slopeVariationMap = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float sampleX = (x + offsetX) / (radius * 2f);
                float sampleY = (y + offsetY) / (radius * 2f);
                slopeVariationMap[x, y] = Mathf.Clamp01(0.8f + 0.4f * FBM(sampleX, sampleY, 2, 2f, 0.5f));
            }

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 w = GridToWorldXY(x, y);
                Vector2 wOffset = new Vector2(w.x - peakOffsetX, w.y - peakOffsetY);
                float r = wOffset.magnitude;
                if (r > radius) { heights[x, y] = 0f; continue; }

                float normalizedR = Mathf.Clamp01(r / radius);

                float falloff;
                if (normalizedR > 0.66f)
                {
                    float t = (normalizedR - 0.66f) / 0.34f;
                    falloff = Mathf.Lerp(0.7f, 1f, t);
                }
                else if (normalizedR > 0.33f)
                {
                    float t = (normalizedR - 0.33f) / 0.33f;
                    falloff = Mathf.Lerp(0.3f, 0.7f, t);
                }
                else
                {
                    float t = normalizedR / 0.33f;
                    falloff = Mathf.Lerp(0f, 0.3f, t);
                }

                // Variación local controlada
                falloff *= slopeVariationMap[x, y] * Random.Range(0.9f, 1.1f);

                // Clamp duro para evitar fallos numéricos
                falloff = Mathf.Clamp01(falloff);
                falloff = Mathf.Pow(1f - falloff, peakSharpness);

                float n = FBM((w.x + offsetX) * baseFreq, (w.y + offsetY) * baseFreq, octaves, lacunarity, persistence);

                // Protección contra NaN o valores absurdos
                if (float.IsNaN(n) || float.IsInfinity(n)) n = 0f;

                float h = height * falloff + n * noiseAmplitude * falloff;
                if (float.IsNaN(h) || float.IsInfinity(h)) h = 0f;
                heights[x, y] = Mathf.Max(0f, h);
            }
        }

        // Asegurar que el centro del pico esté dentro del rango del array
        int centerX = Mathf.Clamp(resolution / 2 + Mathf.RoundToInt((peakOffsetX / radius) * (resolution / 2)), 0, resolution - 1);
        int centerY = Mathf.Clamp(resolution / 2 + Mathf.RoundToInt((peakOffsetY / radius) * (resolution / 2)), 0, resolution - 1);

        ApplyGaussianBoost(centerX, centerY, Mathf.Max(2f, resolution * 0.03f), height * 0.3f);
    }

    /// Ruido fractal simple (fBm) usando PerlinNoise de Unity
    float FBM(float x, float y, int octs, float lac, float pers)
    {
        float sum = 0f;
        float amp = 1f;
        float freq = 1f;
        float norm = 0f;
        for (int i = 0; i < octs; i++)
        {
            float sample = (Mathf.PerlinNoise(x * freq, y * freq) - 0.5f) * 2f;
            sum += amp * sample;
            norm += amp;
            amp *= pers;
            freq *= lac;
        }
        if (norm != 0f) sum /= norm;
        return sum;
    }

    /// Aplica un pequeño boost gaussiano en el centro para asegurar pico principal
    void ApplyGaussianBoost(int cx, int cy, float sigma, float strength)
    {
        float twoSigma2 = 2f * sigma * sigma;
        for (int y = Mathf.Max(0, (int)(cy - 3 * sigma)); y <= Mathf.Min(resolution - 1, (int)(cy + 3 * sigma)); y++)
            for (int x = Mathf.Max(0, (int)(cx - 3 * sigma)); x <= Mathf.Min(resolution - 1, (int)(cx + 3 * sigma)); x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float g = Mathf.Exp(-(dx * dx + dy * dy) / twoSigma2);
                heights[x, y] += g * strength;
            }
    }
    void EnforceSinglePeak()
    {
        int gx = 0, gy = 0;
        float gval = -Mathf.Infinity;
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                if (heights[x, y] > gval) { gval = heights[x, y]; gx = x; gy = y; }

        // detect local maxima and suppress if too high relative to global
        for (int y = 1; y < resolution - 1; y++)
        {
            for (int x = 1; x < resolution - 1; x++)
            {
                bool localMax = true;
                float val = heights[x, y];
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (!(ox == 0 && oy == 0) && heights[x + ox, y + oy] >= val) localMax = false;

                if (localMax)
                {
                    if (!(Mathf.Abs(x - gx) < 4 && Mathf.Abs(y - gy) < 4) && val > secondaryPeakThreshold * gval)
                    {
                        // apply gaussian negative bump centered on this local max
                        ApplyGaussianNegative(x, y, Mathf.Max(2f, resolution * 0.02f), 0.5f * (val));
                    }
                }
            }
        }

        // Small final smoothing radially to remove artifacts
        SmoothHeights(2);
    }

    void ApplyGaussianNegative(int cx, int cy, float sigma, float strength)
    {
        float twoSigma2 = 2f * sigma * sigma;
        for (int y = Mathf.Max(0, (int)(cy - 3 * sigma)); y <= Mathf.Min(resolution - 1, (int)(cy + 3 * sigma)); y++)
            for (int x = Mathf.Max(0, (int)(cx - 3 * sigma)); x <= Mathf.Min(resolution - 1, (int)(cx + 3 * sigma)); x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float g = Mathf.Exp(-(dx * dx + dy * dy) / twoSigma2);
                heights[x, y] = Mathf.Max(0f, heights[x, y] - g * strength);
            }
    }

    void SmoothHeights(int passes)
    {
        for (int p = 0; p < passes; p++)
        {
            float[,] tmp = new float[resolution, resolution];
            for (int y = 1; y < resolution - 1; y++)
                for (int x = 1; x < resolution - 1; x++)
                {
                    float sum = 0f;
                    int cnt = 0;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            sum += heights[x + ox, y + oy];
                            cnt++;
                        }
                    tmp[x, y] = sum / cnt;
                }
            // copy back (skip borders)
            for (int y = 1; y < resolution - 1; y++)
                for (int x = 1; x < resolution - 1; x++)
                    heights[x, y] = tmp[x, y];
        }
    }
    void ApplyHydraulicErosion()
    {
        int iters = Mathf.Max(1, erosionIterations);
        for (int iter = 0; iter < iters; iter++)
        {
            // For performance: operate on a reduced sample every few iterations (but we keep simple here)
            for (int y = 1; y < resolution - 1; y++)
            {
                for (int x = 1; x < resolution - 1; x++)
                {
                    // find lowest neighbor
                    float h = heights[x, y];
                    int lx = x, ly = y;
                    float lowest = h;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0) continue;
                            float nh = heights[x + ox, y + oy];
                            if (nh < lowest) { lowest = nh; lx = x + ox; ly = y + oy; }
                        }
                    }
                    if (lowest < h)
                    {
                        float diff = h - lowest;
                        // amount to move this iteration
                        float move = diff * erosionTalus * 0.5f; // 0.5 to soften
                        heights[x, y] -= move;
                        heights[lx, ly] += move;
                    }
                }
            }
            // occasional smoothing to simulate deposition
            if ((iter & 7) == 0) SmoothHeights(1);
        }
    }
    void BuildMesh()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GetComponent<MeshFilter>().mesh = mesh;

        vertices = new Vector3[resolution * resolution];
        Vector2[] uvs = new Vector2[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                Vector2 w = GridToWorldXY(x, y);
                float h = heights[x, y];
                vertices[y * resolution + x] = new Vector3(w.x, h, w.y);
                uvs[y * resolution + x] = new Vector2(x / (float)(resolution - 1), y / (float)(resolution - 1));
            }
        }

        triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        int idx = 0;
        for (int y = 0; y < resolution - 1; y++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int i = y * resolution + x;
                triangles[idx++] = i;
                triangles[idx++] = i + resolution;
                triangles[idx++] = i + resolution + 1;
                triangles[idx++] = i;
                triangles[idx++] = i + resolution + 1;
                triangles[idx++] = i + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        if (recalcNormals) mesh.RecalculateNormals();
    }
    void UpdateCollider()
    {
        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = mesh;
    }
    void PlaceHandholds()
    {
        // destroy prior
        for (int i = 0; i < spawnedHandholds.Count; i++)
            if (spawnedHandholds[i] != null) DestroyImmediate(spawnedHandholds[i]);
        spawnedHandholds.Clear();

        if (handholdPrefab == null) return;

        int placed = 0;
        // iterate over grid cells (skip boundary)
        for (int y = 1; y < resolution - 1 && placed < maxHandholds; y++)
        {
            for (int x = 1; x < resolution - 1 && placed < maxHandholds; x++)
            {
                // compute gradient using central differences
                float dhdx = (heights[x + 1, y] - heights[x - 1, y]) * 0.5f / ((2f * radius) / (resolution - 1));
                float dhdz = (heights[x, y + 1] - heights[x, y - 1]) * 0.5f / ((2f * radius) / (resolution - 1));
                // slope magnitude
                float slopeTan = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz);
                float slopeDeg = Mathf.Atan(slopeTan) * Mathf.Rad2Deg;

                if (slopeDeg >= handholdMinSlopeDeg && slopeDeg <= handholdMaxSlopeDeg)
                {
                    // compute normal approximate
                    Vector3 n = new Vector3(-dhdx, 1f, -dhdz).normalized;

                    // place some jitter and skip by probability to avoid dense placement
                    float prob = Mathf.InverseLerp(handholdMinSlopeDeg, handholdMaxSlopeDeg, slopeDeg);
                    if (Random.value > Mathf.Lerp(0.6f, 0.95f, prob)) continue; // more likely in mid-slopes

                    Vector2 world = GridToWorldXY(x, y);
                    Vector3 pos = transform.TransformPoint(new Vector3(world.x, heights[x, y], world.y));
                    // jitter along tangent plane
                    Vector3 tangent = Vector3.Cross(n, Vector3.up).normalized;
                    pos += tangent * (Random.value * handholdPlacementJitter - handholdPlacementJitter * 0.5f);
                    pos += Vector3.up * (Random.value * 0.2f); // small vertical jitter

                    Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(-n, Vector3.up), Vector3.up);

                    GameObject inst = Instantiate(handholdPrefab, pos, rot, this.transform);
                    spawnedHandholds.Add(inst);
                    placed++;
                }
            }
        }
        Debug.Log("Handholds placed: " + placed);
    }
}


