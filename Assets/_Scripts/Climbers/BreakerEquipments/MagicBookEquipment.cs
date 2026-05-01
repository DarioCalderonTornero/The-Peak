using UnityEngine;

public class MagicBookEquipment : EquipmentInstance
{
    public override void Initialize(string name)
    {
        base.Initialize(name);
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.BerryTree || obstacleType == ObstacleType.Bramble)
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
        return obstacleType == ObstacleType.BerryTree || obstacleType == ObstacleType.Bramble;
    }
}