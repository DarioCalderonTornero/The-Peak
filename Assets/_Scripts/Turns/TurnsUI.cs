using System.Collections;
using TMPro;
using UnityEngine;

public class TurnsUI : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI playerTurnText;
    [SerializeField] private TextMeshProUGUI climberTurnText;

    private void Start()
    {
        StartCoroutine(ShowOneSecond());
    }

    private IEnumerator ShowOneSecond()
    {
        Show();
        yield return new WaitForSeconds(1);
        Hide();
    }

    private void Show()
    {
        playerTurnText.gameObject.SetActive(true);
        climberTurnText.gameObject.SetActive(true);
    }

    private void Hide()
    {
        playerTurnText.gameObject.SetActive(false);
        climberTurnText.gameObject.SetActive(false);
    }
}
