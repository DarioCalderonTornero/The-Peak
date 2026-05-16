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

    private void OnTriggerEnter(Collider other)
    {
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout == null) return;

        // Si tiene counter, HandleRockObstacle ya se encarga
        if (loadout.CanHandleObstacle(ObstacleType.Rock)) return;

        // Sin counter forzar ruta alternativa
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        ForceAlternativeRoute(climber);
    }

    private void ForceAlternativeRoute(ClimberMovement climber)
    {
        CampGraphBuilder graph = FindObjectOfType<CampGraphBuilder>();
        if (graph == null || graph.nodes == null) return;

        CampGraphBuilder.CampNode bestNode = null;
        float bestDist = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();

        foreach (var node in graph.nodes)
        {
            // Excluir nodo actual del escalador
            if (climber.CurrentNode != null && node.id == climber.CurrentNode.id)
                continue;

            // Excluir nodos cerca de la roca
            if (Vector3.Distance(node.position, transform.position) < 3f)
                continue;

            bool canReach = NavMesh.CalculatePath(
                climber.transform.position,
                node.position,
                NavMesh.AllAreas,
                path
            ) && path.status == NavMeshPathStatus.PathComplete;

            if (!canReach) continue;

            float dist = Vector3.Distance(climber.transform.position, node.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestNode = node;
            }
        }

        if (bestNode != null)
            climber.ForceMoveToCampNode(bestNode);
        else
            climber.SetCurrentStamina(0f);
    }

    public override void Initialize()
    {
        base.Initialize();
        PlaySpawnVfx();
    }

    private void PlaySpawnVfx()
    {
        if (spawnVfxPrefab == null) return;
        VisualEffect vfx = Instantiate(spawnVfxPrefab, transform.position, transform.rotation, null);
        Destroy(vfx.gameObject, spawnVfxDuration);
    }
}