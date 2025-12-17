using UnityEngine;

public class HandleBramble : BaseObstacle
{
    protected override void OnHandleBy(ClimberLoadout loadout)
    {
        if (obstacleType != ObstacleType.Bramble)
            return;

        Destroy(gameObject, 2f);
    }
}
