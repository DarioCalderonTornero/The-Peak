using UnityEngine;
using UnityEngine.AI;

public class RockDefense : BaseDefense
{
    [Header("NavMesh Blocking (opcional)")]
    [SerializeField] private NavMeshObstacle obstacle;

    [Tooltip("Si está a true, configurará el NavMeshObstacle en runtime.")]
    [SerializeField] private bool configureObstacleAtRuntime = true;

    [Tooltip("Si está a true, desactivará completamente el NavMeshObstacle para que NO afecte al NavMesh.")]
    [SerializeField] private bool disableNavMeshObstacle = true;

    public override void Initialize()
    {
        base.Initialize();
        SetupNavMeshObstacle();
    }

    private void SetupNavMeshObstacle()
    {
        // Intentar coger el componente que ya tenga el prefab
        if (obstacle == null)
            obstacle = GetComponent<NavMeshObstacle>();

        // Si no hay NavMeshObstacle en el prefab y no lo necesitamos, simplemente salimos
        if (obstacle == null)
            return;

        // Opción fuerte: desactivar completamente el NavMeshObstacle
        if (disableNavMeshObstacle)
        {
            obstacle.enabled = false;
            return;
        }

        // Si no queremos deshabilitarlo, al menos nos aseguramos de que NO carva el NavMesh
        if (configureObstacleAtRuntime)
        {
            obstacle.carving = false;              // ← clave: que NO haga agujero en el NavMesh
            obstacle.carveOnlyStationary = true;
            // El tamaño/forma lo sigues controlando desde el inspector si decides usarlo para otra cosa.
        }
    }
}
