using UnityEngine;

public class DestroyRockAfterTime : MonoBehaviour
{
    private int turnsLeft = 3;

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnEnd += TurnManager_OnPlayerTurnEnd;
        }
        
    }

    private void TurnManager_OnPlayerTurnEnd()
    {
        turnsLeft--;
        if (turnsLeft <= 0)
        {
            //Destroy(gameObject);
        }
    }
}
