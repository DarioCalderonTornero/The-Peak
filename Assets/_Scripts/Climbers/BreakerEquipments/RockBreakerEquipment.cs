using Unity.VisualScripting;
using UnityEngine;

public class RockBreakerEquipment : EquipmentInstance
{
    private ClimberMovement climberMovement;

    private float maxTime = 3f;

    private void Awake()
    {
        //climberMovement = <ClimberMovement>();
    }

    public override void Initialize(string name)
    {
        base.Initialize(name);
        Debug.Log("<color=cyan>[RockBreakerEquipment]</color> Ready to break rocks!");
    }

    public override void OnEncounterObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"<color=yellow>[RockBreakerEquipment]</color> Encountered obstacle: {obstacleType}");

        if (obstacleType == ObstacleType.Rock)
        {
            Debug.Log("RockEncounterWithAxe");
            OnCounterSuccess(obstacleType);
        }
        else
        {
            Debug.Log("RockEncounterNonAxe");
            OnCounterFail(obstacleType);
        }
    }

    public override void OnCounterSuccess(ObstacleType type)
    {
        base.OnCounterSuccess(type);

        if (ownerClimber == null)
            return;

        ownerClimber.StopForSeconds(3);


        Debug.Log($"<color=green>[RockBreakerEquipment]</color> Successfully countered {type}! (Would break it here)");
    }

    public override void OnCounterFail(ObstacleType type)
    {
        Debug.Log($"<color=red>[RockBreakerEquipment]</color> Cannot counter {type}. (Would block climber here)");
    } 

    /// <summary>
    /// A nivel de grafo, este equipo permite usar aristas con ROCK.
    /// </summary>
    public override bool CanHandleObstacle(ObstacleType obstacleType)
    {
        return obstacleType == ObstacleType.Rock;
    }
}
