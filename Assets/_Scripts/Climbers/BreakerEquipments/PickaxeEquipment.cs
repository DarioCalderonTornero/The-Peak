using UnityEngine;
using UnityEngine.UI;

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
        ownerLoadout.StartEquipmentSequence(2f, obstacleType: type);
        OwnerLoadout.TriggerAnimation("Geyser");

        var geyser = ownerLoadout.GetComponentInParent<GeyserDefense>();
        if (geyser == null)
        {
            var collider = ownerLoadout.GetComponent<Collider>();
            Collider[] hits = Physics.OverlapSphere(ownerLoadout.transform.position, 2f);
            foreach (var hit in hits)
            {
                geyser = hit.GetComponent<GeyserDefense>();
                if (geyser != null) break;
            }
        }

        if (geyser != null)
            geyser.ActivateCooldown();
    }

    public override bool CanHandleObstacle(ObstacleType obstacleType)
    {
        return obstacleType == ObstacleType.Rock || obstacleType == ObstacleType.Geyser;
    }
}