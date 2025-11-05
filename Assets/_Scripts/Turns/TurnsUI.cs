using System.Collections;
using TMPro;
using UnityEngine;

public class TurnsUI : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI playerTurnText;
    [SerializeField] private TextMeshProUGUI climberTurnText;

    private void Start()
    {
        //StartCoroutine(ShowOneSecondPlayerTurn());

        HidePlayerTurn();
        HideIATurn();

        TurnsStateMachine.Instance.OnPlayerStateTurn += TurnsStateMachine_OnPlayerStateTurn;
        TurnsStateMachine.Instance.OnClimberStateTurn += TurnsStateMachine_OnClimberStateTurn;
    }

    private void TurnsStateMachine_OnClimberStateTurn(object sender, System.EventArgs e)
    {
        ShowIATurn();
        HidePlayerTurn();
    }

    private void TurnsStateMachine_OnPlayerStateTurn(object sender, System.EventArgs e)
    {
        ShowPlayerTurn();
        HideIATurn();
    }

    private void ShowPlayerTurn()
    {
        playerTurnText.gameObject.SetActive(true);
    }

    private void HidePlayerTurn()
    {
        playerTurnText.gameObject.SetActive(false);
    }

    private void ShowIATurn()
    {
        climberTurnText.gameObject.SetActive(true);
    }

    private void HideIATurn()
    {
        climberTurnText.gameObject.SetActive(false);
    }
}
