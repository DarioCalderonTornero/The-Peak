using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawning Shape")]
    [Tooltip("Arrastra aquí los Transforms en orden para dibujar la forma del spawn.")]
    [SerializeField] private List<Transform> spawnPathPoints = new List<Transform>();

    [Header("Spawning Settings")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private int amountToSpawn = 2;
    [SerializeField] private float spawnCooldown = 2f;
    [SerializeField] private float minDistance = 2f;

    [Header("Estado interno (debug)")]
    [SerializeField] private float spawnCooldownTimer = 0f;
    [SerializeField] private int spawnedCount = 0;
    [SerializeField] public bool isMaxCount = false;

    // Lista para guardar posiciones y evitar solapamientos
    private readonly List<Vector3> spawnedPositions = new List<Vector3>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Aseguramos que el cooldown corra siempre, o puedes llamarlo desde un Manager externo
        if (!isMaxCount)
        {
            SpawnClimbers();
        }
    }

    public void SpawnClimbers()
    {
        if (isMaxCount || spawnPathPoints.Count < 2)
            return;

        spawnCooldownTimer += Time.deltaTime;

        if (spawnCooldownTimer < spawnCooldown)
            return;

        Vector3 spawnPos = Vector3.zero;
        bool validPosition = false;
        int maxAttempts = 20;
        int attempts = 0;

        do
        {
            // OBTENER PUNTO ALEATORIO EN LA FORMA
            spawnPos = GetRandomPointOnMultiSegmentPath();

            // VALIDAR DISTANCIA
            validPosition = true;
            foreach (var pos in spawnedPositions)
            {
                if (Vector3.Distance(spawnPos, pos) < minDistance)
                {
                    validPosition = false;
                    break;
                }
            }
            attempts++;
        }
        while (!validPosition && attempts < maxAttempts);

        if (validPosition)
        {
            GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
            spawnedPositions.Add(spawnPos);
            spawnedCount++;
            spawnCooldownTimer = 0f;

            var climber = obj.GetComponent<ClimberMovement>();
            if (climber == null)
            {
                Debug.LogWarning("[SpawnManager] El prefab spawneado no tiene ClimberMovement.");
            }
        }

        if (spawnedCount >= amountToSpawn)
        {
            isMaxCount = true;
            spawnCooldownTimer = 0f;
        }
    }

    // --- LÓGICA MATEMÁTICA PARA MÚLTIPLES PUNTOS ---
    private Vector3 GetRandomPointOnMultiSegmentPath()
    {
        // 1. Calcular la longitud total de la "serpiente" o forma
        float totalLength = 0f;
        for (int i = 0; i < spawnPathPoints.Count - 1; i++)
        {
            if (spawnPathPoints[i] != null && spawnPathPoints[i + 1] != null)
                totalLength += Vector3.Distance(spawnPathPoints[i].position, spawnPathPoints[i + 1].position);
        }

        // 2. Elegir un punto aleatorio en esa longitud total
        float randomDist = Random.Range(0f, totalLength);

        // 3. Encontrar en qué segmento cae esa distancia
        for (int i = 0; i < spawnPathPoints.Count - 1; i++)
        {
            if (spawnPathPoints[i] == null || spawnPathPoints[i + 1] == null) continue;

            float segmentLength = Vector3.Distance(spawnPathPoints[i].position, spawnPathPoints[i + 1].position);

            // Si el punto aleatorio cae en este segmento
            if (randomDist <= segmentLength)
            {
                // Normalizamos la distancia para hacer el Lerp en este segmento específico
                float t = randomDist / segmentLength;
                return Vector3.Lerp(spawnPathPoints[i].position, spawnPathPoints[i + 1].position, t);
            }

            // Si no, restamos la longitud de este segmento y pasamos al siguiente
            randomDist -= segmentLength;
        }

        // Fallback (por errores de redondeo float): devolver el último punto
        return spawnPathPoints[spawnPathPoints.Count - 1].position;
    }

    public void ResetSpawner()
    {
        spawnCooldownTimer = 0f;
        spawnedCount = 0;
        isMaxCount = false;
        spawnedPositions.Clear();
    }

    public void StopSpawning()
    {
        isMaxCount = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- VISUALIZACIÓN EN EDITOR ---
    private void OnDrawGizmos()
    {
        if (spawnPathPoints == null || spawnPathPoints.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < spawnPathPoints.Count - 1; i++)
        {
            if (spawnPathPoints[i] != null && spawnPathPoints[i + 1] != null)
            {
                Gizmos.DrawLine(spawnPathPoints[i].position, spawnPathPoints[i + 1].position);
                Gizmos.DrawSphere(spawnPathPoints[i].position, 0.3f);
            }
        }
        // Dibujar el último punto
        if (spawnPathPoints[spawnPathPoints.Count - 1] != null)
            Gizmos.DrawSphere(spawnPathPoints[spawnPathPoints.Count - 1].position, 0.3f);
    }
}