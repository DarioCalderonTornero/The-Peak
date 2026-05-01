using UnityEngine;

public class PickaxeEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.Rock || obstacleType == ObstacleType.Geyser)
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
        return obstacleType == ObstacleType.Rock || obstacleType == ObstacleType.Geyser;
    }
}