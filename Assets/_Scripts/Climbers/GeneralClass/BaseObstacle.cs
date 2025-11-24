using UnityEngine;

public class BaseObstacle : MonoBehaviour
{
    [Header("Obstacle")]
    public ObstacleType obstacleType = ObstacleType.None;

    private void Start()
    {
     
        CampGraphBuilder graph = FindObjectOfType<CampGraphBuilder>();
        if (graph != null && graph.nodes != null && graph.nodes.Count > 0)
        {
            graph.RecalculateObstaclesOnEdges();
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout == null)
            return;

        loadout.TryHandleObstacle(obstacleType);
    }
}
