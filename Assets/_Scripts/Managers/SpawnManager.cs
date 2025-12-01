using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawning")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private int amountToSpawn = 2;
    [SerializeField] private float spawnCooldown = 2f;
    [SerializeField] private float minDistance = 2f;

    [Header("Tipos de escalador")]
    [Tooltip("Lista de arquetipos posibles. Ej: sin pico, con pico, etc.")]

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

    public void SpawnClimbers()
    {
        if (isMaxCount)
            return;

        spawnCooldownTimer += Time.deltaTime;

        if (spawnCooldownTimer < spawnCooldown)
            return;

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

            /*
            //Elegir arquetipo aleatorio (si hay)
            var config = obj.GetComponent<ClimberConfig>();
            if (config != null && possibleArchetypes != null && possibleArchetypes.Length > 0)
            {
                ClimberArchetypeSO chosen = possibleArchetypes[Random.Range(0, possibleArchetypes.Length)];
                if (chosen != null)
                {
                    config.ApplyArchetype(chosen);
                }
            }
            else if (config == null)
            {
                //Debug.LogWarning("[SpawnManager] El prefab spawneado no tiene ClimberConfig.");
            }
            */

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
