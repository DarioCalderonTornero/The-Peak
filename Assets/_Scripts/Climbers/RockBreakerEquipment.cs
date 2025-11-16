using UnityEngine;

public class RockBreakerEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
        Debug.Log("<color=cyan>[RockBreakerEquipment]</color> Ready to break rocks!");
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"<color=yellow>[RockBreakerEquipment]</color> Encountered obstacle: {obstacleType}");
    }

    public override void OnCounterSuccess(ObstacleType type)
    {
        Debug.Log($"<color=green>[RockBreakerEquipment]</color> Successfully countered {type}! (Would break it here)");
    }

    public override void OnCounterFail(ObstacleType type)
    {
        Debug.Log($"<color=red>[RockBreakerEquipment]</color> Cannot counter {type}. (Would block climber here)");
    }
}
