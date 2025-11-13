using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawning")]
    [SerializeField] private GameObject prefab;    // Prefab del escalador (con ClimberController + NavMeshAgent)
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private int amountToSpawn = 2;      // Escaladores por turno
    [SerializeField] private float spawnCooldown = 2f;   // Tiempo entre spawns dentro del mismo turno
    [SerializeField] private float minDistance = 2f;     // Distancia mínima entre puntos de spawn

    [Header("Estado interno (debug)")]
    [SerializeField] private float spawnCooldownTimer = 0f;
    [SerializeField] private int spawnedCount = 0;
    [SerializeField] public bool isMaxCount = false;

    private readonly List<Vector3> spawnedPositions = new List<Vector3>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Llamado en cada frame durante el ClimberTurn (desde TurnManager.UpdateClimberTurn).
    /// Se encarga de spawnear escaladores con cooldown y número máximo por turno.
    /// </summary>
    public void SpawnClimbers()
    {
        if (isMaxCount)
            return;

        // Avanzar el temporizador del cooldown
        spawnCooldownTimer += Time.deltaTime;

        if (spawnCooldownTimer < spawnCooldown)
            return;

        // Intentar encontrar una posición válida
        Vector3 spawnPos;
        bool validPosition = false;
        int maxAttempts = 20;
        int attempts = 0;

        do
        {
            float t = Random.Range(0f, 1f);
            spawnPos = Vector3.Lerp(startPoint.position, endPoint.position, t);
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

            // Opcional: comprobación de que lleva ClimberController
            var climber = obj.GetComponent<ClimberMovement>();
            if (climber == null)
            {
                Debug.LogWarning("[SpawnManager] El prefab spawneado no tiene ClimberController.");
            }
        }

        // Comprobamos si ya hemos alcanzado el máximo de spawns de este turno
        if (spawnedCount >= amountToSpawn)
        {
            isMaxCount = true;
            spawnCooldownTimer = 0f;
        }
    }

    /// <summary>
    /// Se llama al comenzar cada ClimberTurn para reiniciar el conteo de spawns del turno.
    /// </summary>
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
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
