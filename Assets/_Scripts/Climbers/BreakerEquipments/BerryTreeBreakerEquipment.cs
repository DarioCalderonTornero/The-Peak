using UnityEngine;

public class BerryTreeBreakerEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
        Debug.Log("<color=cyan>[BrambleBreakerEquipment]</color> Ready to break rocks!");
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"<color=yellow>[BrambleBreakerEquipment]</color> Encountered obstacle: {obstacleType}");

        if (obstacleType == ObstacleType.BerryTree)
        {
            OnCounterSuccess(obstacleType);
        }
        else
        {
            OnCounterFail(obstacleType);
        }
    }

    public override void OnCounterSuccess(ObstacleType type)
    {
        Debug.Log($"<color=green>[BerryTreeBreakerEquipment]</color> Successfully countered {type}! (Would break it here)");
    }

    public override void OnCounterFail(ObstacleType type)
    {
        Debug.Log($"<color=red>[BerryTreeBreakerEquipment]</color> Cannot counter {type}. (Would block climber here)");
    }

    public override void OnExitObstacle(ObstacleType type)
    {
        Debug.Log("Exit Obstacle");
    }

    /// <summary>
    /// A nivel de grafo, este equipo permite usar aristas con ROCK.
    /// </summary>
    public override bool CanHandleObstacle(ObstacleType obstacleType)
    {
        return obstacleType == ObstacleType.BerryTree;
    }
}
