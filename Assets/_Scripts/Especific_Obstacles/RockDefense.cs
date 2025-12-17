using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

public class RockDefense : BaseDefense
{
    // [Header("NavMesh Blocking")]
    // [SerializeField] private NavMeshObstacle obstacle;
    // [SerializeField] private bool configureObstacleAtRuntime = true;

    [Header("Spawn VFX")]
    [SerializeField] private VisualEffect spawnVfxPrefab; // tu VFX prefab
    [SerializeField] private float spawnVfxDuration = 1f;

    public override void Initialize()
    {
        base.Initialize();

        PlaySpawnVfx();
    }

    private void PlaySpawnVfx()
    {
        if (spawnVfxPrefab == null)
            return;

        // Instanciar VFX en la posición de la roca
        VisualEffect vfx = Instantiate(
            spawnVfxPrefab,
            transform.position,
            transform.rotation,
            null // sin padre, o puedes usar transform si quieres que se mueva con la roca
        );

        // Destruirlo tras 1 segundo
        Destroy(vfx.gameObject, spawnVfxDuration);
    }

    /* private void SetupNavMeshObstacle()
    {
        if (obstacle == null)
            obstacle = GetComponent<NavMeshObstacle>();

        // Si no hay, lo añadimos
        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; // o Cylinder, según tu modelo
        }

        if (configureObstacleAtRuntime)
        {
            obstacle.carving = false;
            obstacle.carveOnlyStationary = false;
            // El tamaño lo controlas desde el inspector con el componente.
        }
    } */
}
