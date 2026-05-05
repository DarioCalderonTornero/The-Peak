using UnityEngine;

public class TemporaryDefense : MonoBehaviour
{
    private int turnsRemaining;

    public void Initialize(int turns)
    {
        turnsRemaining = turns;

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd += OnClimberTurnEnd;
        else
            Debug.LogWarning("[TemporaryDefense] TurnManager no encontrado.");
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd -= OnClimberTurnEnd;
    }

    private void OnClimberTurnEnd()
    {
        turnsRemaining--;
        Debug.Log($"[TemporaryDefense] {gameObject.name} — turnos restantes: {turnsRemaining}");

        if (turnsRemaining <= 0)
            Destroy(gameObject);
    }
}