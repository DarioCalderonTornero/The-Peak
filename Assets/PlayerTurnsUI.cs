using TMPro;
using UnityEngine;

public class PlayerTurnsUI : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI currentTurns;

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnNumberChanged += UpdateTurnText;
            currentTurns.text = TurnManager.Instance.CurrentTurnNumber.ToString();
        }
        else
        {
            currentTurns.text = "0";
        }
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnNumberChanged -= UpdateTurnText;
    }

    private void UpdateTurnText(int turn)
    {
        currentTurns.text = turn.ToString();
    }
}