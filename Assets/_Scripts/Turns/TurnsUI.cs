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

        // ACTUALIZADO: Ahora usa TurnManager en lugar de TurnsStateMachine
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart += TurnManager_OnPlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart += TurnManager_OnClimberTurnStart;
        }
        else
        {
            Debug.LogWarning("[TurnsUI] TurnManager not found!");
        }
    }

// ACTUALIZADO: Renombrado para TurnManager
    private void TurnManager_OnClimberTurnStart()
    {
        ShowIATurn();
        HidePlayerTurn();
    }

// ACTUALIZADO: Renombrado para TurnManager
    private void TurnManager_OnPlayerTurnStart()
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

    private void OnDestroy()
    {
        // Desuscribirse de eventos para evitar memory leaks
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= TurnManager_OnPlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart -= TurnManager_OnClimberTurnStart;
        }
    }
}
