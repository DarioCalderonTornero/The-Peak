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

    [Header("Límite de rocas")]
    private static int maxRocks = 2;
    private static System.Collections.Generic.Queue<RockDefense> activeRocks =
        new System.Collections.Generic.Queue<RockDefense>();

    public override void Initialize()
    {
        base.Initialize();

        // Si ya hay el máximo de rocas, destruir la más antigua
        if (activeRocks.Count >= maxRocks)
        {
            RockDefense oldest = activeRocks.Dequeue();
            if (oldest != null)
                Destroy(oldest.gameObject);
        }

        activeRocks.Enqueue(this);
        PlaySpawnVfx();
    }

    private void OnDestroy()
    {
        // Limpiar referencias nulas de la cola
        var temp = new System.Collections.Generic.Queue<RockDefense>();
        while (activeRocks.Count > 0)
        {
            RockDefense rock = activeRocks.Dequeue();
            if (rock != null && rock != this)
                temp.Enqueue(rock);
        }
        activeRocks = temp;
    }

    private void OnTriggerEnter(Collider other)
    {
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout == null) return;

        if (loadout.CanHandleObstacle(ObstacleType.Rock)) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        ForceAlternativeRoute(climber);
    }

    private void ForceAlternativeRoute(ClimberMovement climber)
    {
        CampGraphBuilder graph = FindObjectOfType<CampGraphBuilder>();
        if (graph == null || graph.nodes == null) return;

        CampGraphBuilder.CampNode rockNode = null;
        float minDist = float.PositiveInfinity;
        foreach (var node in graph.nodes)
        {
            float d = Vector3.Distance(node.position, transform.position);
            if (d < minDist) { minDist = d; rockNode = node; }
        }
        if (rockNode != null)
            climber.AddBlockedNode(rockNode.id);

        CampGraphBuilder.CampNode bestNode = null;
        float bestDist = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();

        foreach (var node in graph.nodes)
        {
            if (climber.CurrentNode != null && node.id == climber.CurrentNode.id)
                continue;
            if (climber.IsNodeBlocked(node.id))
                continue;
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
            if (dist < bestDist) { bestDist = dist; bestNode = node; }
        }

        if (bestNode != null)
            climber.ForceMoveToCampNode(bestNode);
        else if (climber.CurrentNode != null)
        {
            climber.ClearBlockedNodes();
            climber.ForceMoveToCampNode(climber.CurrentNode);
        }
        else
            climber.SetCurrentStamina(0f);
    }

    private void PlaySpawnVfx()
    {
        if (spawnVfxPrefab == null) return;
        VisualEffect vfx = Instantiate(spawnVfxPrefab, transform.position, transform.rotation, null);
        Destroy(vfx.gameObject, spawnVfxDuration);
    }
}