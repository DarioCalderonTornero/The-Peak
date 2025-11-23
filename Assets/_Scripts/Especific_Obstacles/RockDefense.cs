using UnityEngine;
using UnityEngine.AI;

public class RockDefense : BaseDefense
{
    [Header("NavMesh Blocking")]
    [SerializeField] private NavMeshObstacle obstacle;
    [SerializeField] private bool configureObstacleAtRuntime = true;

    public override void Initialize()
    {
        base.Initialize();
        SetupNavMeshObstacle();
    }

    private void SetupNavMeshObstacle()
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
    }
}
