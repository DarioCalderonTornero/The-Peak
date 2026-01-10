// SpawnManager.cs
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

    [Header("Intro Spawn (5 tipos únicos al inicio de la partida)")]
    [Tooltip("Estos equipos se forzarán en los primeros spawns de TODA la partida (no por turno). Orden = orden de aparición.")]
    [SerializeField] private List<EquipmentDefinitionSO> introUniqueEquipments = new List<EquipmentDefinitionSO>();

    [Header("Estado interno (debug)")]
    [SerializeField] private float spawnCooldownTimer = 0f;
    [SerializeField] private int spawnedCount = 0;
    [SerializeField] public bool isMaxCount = false;

    // Progreso global de la intro (NO se resetea por turno)
    [SerializeField] private int introSpawnIndex = 0;

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

    /*
    private void Update()
    {
        if (!isMaxCount)
        {
            SpawnClimbers();
        }
    }
    */

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
            spawnPos = GetRandomPointOnMultiSegmentPath();

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

            // --- NUEVO: Forzar equipamiento en los primeros spawns globales ---
            var loadout = obj.GetComponent<ClimberLoadout>();
            if (loadout == null)
            {
                Debug.LogWarning("[SpawnManager] El prefab spawneado no tiene ClimberLoadout.");
            }
            else
            {
                // Si todavía estamos en la intro y hay lista válida, forzamos equipo.
                if (introUniqueEquipments != null &&
                    introSpawnIndex < introUniqueEquipments.Count &&
                    introUniqueEquipments[introSpawnIndex] != null)
                {
                    loadout.InitializeForcedSingleEquipment(introUniqueEquipments[introSpawnIndex]);
                    introSpawnIndex++;
                }
                else
                {
                    // Si no hay intro o ya terminó, inicializamos normal.
                    loadout.InitializeRandomLoadoutIfNeeded();
                }
            }

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
        float totalLength = 0f;
        for (int i = 0; i < spawnPathPoints.Count - 1; i++)
        {
            if (spawnPathPoints[i] != null && spawnPathPoints[i + 1] != null)
                totalLength += Vector3.Distance(spawnPathPoints[i].position, spawnPathPoints[i + 1].position);
        }

        float randomDist = Random.Range(0f, totalLength);

        for (int i = 0; i < spawnPathPoints.Count - 1; i++)
        {
            if (spawnPathPoints[i] == null || spawnPathPoints[i + 1] == null) continue;

            float segmentLength = Vector3.Distance(spawnPathPoints[i].position, spawnPathPoints[i + 1].position);

            if (randomDist <= segmentLength)
            {
                float t = randomDist / segmentLength;
                return Vector3.Lerp(spawnPathPoints[i].position, spawnPathPoints[i + 1].position, t);
            }

            randomDist -= segmentLength;
        }

        return spawnPathPoints[spawnPathPoints.Count - 1].position;
    }

    public void ResetSpawner()
    {
        spawnCooldownTimer = 0f;
        spawnedCount = 0;
        isMaxCount = false;
        spawnedPositions.Clear();

        // IMPORTANTE: NO reseteamos introSpawnIndex aquí, porque ResetSpawner se llama cada turno.
    }

    public void ResetIntroSequence()
    {
        // Llamar SOLO cuando empiece una nueva partida de verdad
        introSpawnIndex = 0;
    }

    public void StopSpawning()
    {
        isMaxCount = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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

        if (spawnPathPoints[spawnPathPoints.Count - 1] != null)
            Gizmos.DrawSphere(spawnPathPoints[spawnPathPoints.Count - 1].position, 0.3f);
    }
}
