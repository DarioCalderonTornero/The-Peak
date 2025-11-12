using TMPro;
using UnityEngine;

public class TurnsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerTurnText;
    [SerializeField] private TextMeshProUGUI climberTurnText;

    private void Start()
    {
        HidePlayerTurn();
        HideIATurn();

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
            TurnManager.Instance.OnPlayerTurnEnd += HandlePlayerTurnEnd;
            TurnManager.Instance.OnClimberTurnStart += HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd += HandleClimberTurnEnd;
        }
        else
        {
            Debug.LogWarning("[TurnsUI] TurnManager not found!");
        }
    }

    private void HandlePlayerTurnStart()
    {
        ShowPlayerTurn();
        HideIATurn();
    }

    private void HandlePlayerTurnEnd()
    {
        HidePlayerTurn();
    }

    private void HandleClimberTurnStart()
    {
        ShowIATurn();
        HidePlayerTurn();
    }

    private void HandleClimberTurnEnd()
    {
        HideIATurn();
    }

    private void ShowPlayerTurn() => playerTurnText.gameObject.SetActive(true);
    private void HidePlayerTurn() => playerTurnText.gameObject.SetActive(false);
    private void ShowIATurn() => climberTurnText.gameObject.SetActive(true);
    private void HideIATurn() => climberTurnText.gameObject.SetActive(false);

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
            TurnManager.Instance.OnPlayerTurnEnd -= HandlePlayerTurnEnd;
            TurnManager.Instance.OnClimberTurnStart -= HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd -= HandleClimberTurnEnd;
        }
    }
}
