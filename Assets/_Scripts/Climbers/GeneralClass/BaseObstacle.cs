using UnityEngine;

public class BaseObstacle : MonoBehaviour
{
    [Header("Obstacle")]
    public ObstacleType obstacleType = ObstacleType.None;

    protected virtual void OnTriggerEnter(Collider other)
    {
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout == null)
            return;

        Debug.Log($"[Obstacle] {gameObject.name} encountered by climber. Type: {obstacleType}");

        loadout.TryHandleObstacle(obstacleType);
    }
}
