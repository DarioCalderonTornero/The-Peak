using UnityEngine;

public class LodoBreakerEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
        Debug.Log("<color=cyan>[LodoBreakerEquipment]</color> Ready to break rocks!");
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"<color=yellow>[LodoBreakerEquipment]</color> Encountered obstacle: {obstacleType}");

        if (obstacleType == ObstacleType.Mud)
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
        Debug.Log($"<color=green>[LodoBreakerEquipment]</color> Successfully countered {type}!");
    }

    public override void OnCounterFail(ObstacleType type)
    {
        Debug.Log($"<color=red>[LodoBreakerEquipment]</color> Cannot counter {type}.");
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
        return obstacleType == ObstacleType.Mud;
    }
}
