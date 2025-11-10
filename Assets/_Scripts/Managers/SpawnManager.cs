using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private int amountToSpawn = 3;
    [SerializeField] private float spawnCooldown = 2f;
    [SerializeField] private float minDistance = 2f; 

    [SerializeField] private float time = 0f;
    [SerializeField] private int spawnedCount = 0;

    [SerializeField] public bool isMaxCount = false;

 
    private List<Vector3> spawnedPositions = new List<Vector3>();

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnClimbers()
    {
        time += Time.deltaTime;

        if (time >= spawnCooldown && !isMaxCount)
        {
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
                // Instanciamos el escalador
                GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
                spawnedPositions.Add(spawnPos);
                spawnedCount++;
                time = 0f;

                // 🔹 Activamos inmediatamente el turno si ya estamos en ClimberTurn
                if (TurnManager.Instance != null && TurnManager.Instance.IsClimberTurn())
                {
                    ClimberMinimal climber = obj.GetComponent<ClimberMinimal>();
                    if (climber != null)
                    {
                        climber.SendMessage("HandleClimberTurnStart", SendMessageOptions.DontRequireReceiver);
                    }
                }
            }

            if (spawnedCount >= amountToSpawn)
            {
                isMaxCount = true;
                time = 0f;
            }
        }
    }


    public void ResetSpawner()
    {
        time = 0f;
        spawnedCount = 0;
        isMaxCount = false;
        spawnedPositions.Clear(); 
    }

    public void StopSpawning()
    {
        isMaxCount = true;
    }
}


