using UnityEngine;

public class SnowshoesEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.Snow)
            OnCounterSuccess(obstacleType);
        else
            OnCounterFail(obstacleType);
    }

    public override void OnCounterSuccess(ObstacleType type)
    {
        base.OnCounterSuccess(type);
        if (ownerLoadout == null) return;
        ownerLoadout.StartEquipmentSequence(2f);
    }

    public override bool CanHandleObstacle(ObstacleType obstacleType)
    {
        return obstacleType == ObstacleType.Snow;
    }
}