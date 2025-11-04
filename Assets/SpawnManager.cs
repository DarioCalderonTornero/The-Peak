using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameObject prefab;
    public Transform startPoint;
    public Transform endPoint;
    public int amountToSpawn = 5;     
    public float spawnCooldown = 2f;  

    public float time = 0f;
    private int spawnedCount = 0;    


    public void SpawnClimbers()
    {
        
        if (spawnedCount >= amountToSpawn)
        {
            spawnedCount = 0;
        }
            

        
        time += Time.deltaTime;

        
        if (time >= spawnCooldown)
        {
            float t = Random.Range(0f, 1f);
            Vector3 spawnPos = Vector3.Lerp(startPoint.position, endPoint.position, t);

            Instantiate(prefab, spawnPos, Quaternion.identity);

            spawnedCount++; 
            
            time = 0f;
        }
    }
}

