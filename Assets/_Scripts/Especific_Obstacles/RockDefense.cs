using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

public class RockDefense : BaseDefense
{
    [Header("NavMesh Blocking")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    [Header("Spawn VFX")]
    [SerializeField] private VisualEffect spawnVfxPrefab;
    [SerializeField] private float spawnVfxDuration = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SetupNavMeshObstacle();
        PlaySpawnVfx();
    }

    private void SetupNavMeshObstacle()
    {
        if (navMeshObstacle == null)
            navMeshObstacle = GetComponent<NavMeshObstacle>();
        if (navMeshObstacle == null)
            navMeshObstacle = gameObject.AddComponent<NavMeshObstacle>();

        navMeshObstacle.shape = NavMeshObstacleShape.Box;
        navMeshObstacle.carving = true;
        navMeshObstacle.carveOnlyStationary = true;
    }

    private void PlaySpawnVfx()
    {
        if (spawnVfxPrefab == null) return;
        VisualEffect vfx = Instantiate(spawnVfxPrefab, transform.position, transform.rotation, null);
        Destroy(vfx.gameObject, spawnVfxDuration);
    }
}