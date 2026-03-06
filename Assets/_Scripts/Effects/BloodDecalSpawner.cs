using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class BloodDecalSpawner : MonoBehaviour
{
    [Header("Configuración")]
    public GameObject bloodDecalPrefab;

    [Header("Optimización (Object Pool)")]
    public int maxDecals = 100;
    [Range(0f, 1f)] public float spawnProbability = 0.3f;

    [Header("Distribución Realista")]
    public float minDistance = 0.5f;

    private ParticleSystem partSystem;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

    private GameObject[] decalPool;
    private int poolIndex = 0;

    void Start()
    {
        partSystem = GetComponent<ParticleSystem>();
        decalPool = new GameObject[maxDecals];

        // Pre-instanciamos los decals
        for (int i = 0; i < maxDecals; i++)
        {
            GameObject decal = Instantiate(bloodDecalPrefab, Vector3.zero, Quaternion.identity);
            decal.SetActive(false);
            decalPool[i] = decal;
        }
    }

    void OnParticleCollision(GameObject other)
    {
        int numCollisionEvents = partSystem.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numCollisionEvents; i++)
        {
            if (Random.value > spawnProbability) continue;

            Vector3 pos = collisionEvents[i].intersection;

            // Revisión de distancia para evitar Z-Fighting y superposición extrema
            bool isTooClose = false;
            for (int j = 0; j < maxDecals; j++)
            {
                if (decalPool[j].activeInHierarchy)
                {
                    if (Vector3.Distance(pos, decalPool[j].transform.position) < minDistance)
                    {
                        isTooClose = true;
                        break;
                    }
                }
            }

            if (isTooClose) continue;

            Vector3 normal = collisionEvents[i].normal;
            Quaternion rot = Quaternion.LookRotation(-normal);

            // Rotación aleatoria en Z para que la mancha no se vea repetitiva
            rot *= Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            GameObject currentDecal = decalPool[poolIndex];

            currentDecal.transform.position = pos;
            currentDecal.transform.rotation = rot;
            currentDecal.SetActive(true);

            DecalFader fader = currentDecal.GetComponent<DecalFader>();
            if (fader != null) fader.ResetFade();

            poolIndex++;
            if (poolIndex >= maxDecals) poolIndex = 0;
        }
    }
}