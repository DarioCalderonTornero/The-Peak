using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private int amountToSpawn = 3;     
    [SerializeField] private float spawnCooldown = 2f;  

    [SerializeField] private float time = 0f;
    [SerializeField] private int spawnedCount = 0;


    [SerializeField] public bool isMaxCount = false;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnClimbers()
    {
        time += Time.deltaTime;

        if (time >= spawnCooldown && !isMaxCount)
        {

            float t = Random.Range(0f, 1f);
            Vector3 spawnPos = Vector3.Lerp(startPoint.position, endPoint.position, t);

            Instantiate(prefab, spawnPos, Quaternion.identity);

            spawnedCount++;

            time = 0f;

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
    }

    public void StopSpawning()
    {
        isMaxCount = true;
    }


}

