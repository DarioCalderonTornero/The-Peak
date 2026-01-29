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

    //Trigger de entrada del jugador al area de cualquier obstaculo. Si puede con el (CanHandleBy), se llama a OnHandleBy (ya ha podido con el).
    protected virtual void OnTriggerEnter(Collider collider)
    {
        var loadout = collider.GetComponent<ClimberLoadout>();
        if (loadout == null)
            return;

        loadout.TryHandleObstacle(obstacleType);

        if (loadout.CanHandleObstacle(obstacleType))
        {
            OnHandleBy(loadout);
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout == null)
            return;

        loadout.TryHandleObstacleExit(obstacleType);
    }

    protected virtual void OnHandleBy(ClimberLoadout loadout)
    {

    }
}
