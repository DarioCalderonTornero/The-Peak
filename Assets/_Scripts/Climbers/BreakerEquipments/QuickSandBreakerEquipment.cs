using UnityEngine;

public class QuickSandBreakerEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
        Debug.Log("<color=cyan>[QuickSandBreakerEquipment]</color> Ready to negate quicksand!");
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"<color=yellow>[QuickSandBreakerEquipment]</color> Encountered obstacle: {obstacleType}");

        if (obstacleType == ObstacleType.Snow)
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
        Debug.Log($"<color=green>[QuickSandBreakerEquipment]</color> Successfully handled {type}! (Quicksand has no effect on this climber)");
        // Aquí podrías lanzar VFX/SFX específicos del equipo si quieres
    }

    public override void OnCounterFail(ObstacleType type)
    {
        Debug.Log($"<color=red>[QuickSandBreakerEquipment]</color> Cannot counter {type}. (Quicksand or other obstacle will affect normally)");
    }

    public override void OnExitObstacle(ObstacleType type)
    {
        Debug.Log("<color=magenta>[QuickSandBreakerEquipment]</color> Exit obstacle.");
    }

    /// <summary>
    /// A nivel de grafo, este equipo permite usar aristas con QUICK SAND.
    /// </summary>
    public override bool CanHandleObstacle(ObstacleType obstacleType)
    {
        return obstacleType == ObstacleType.Snow;
    }
}
