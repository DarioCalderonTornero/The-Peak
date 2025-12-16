using System.Collections;
using UnityEngine;

public class HideUIManager : MonoBehaviour
{
    private void Start()
    {
        GameManager.Instance.OnGamePaused += GameManager_OnGamePaused;
        GameManager.Instance.OnGameUnPaused += GameManager_OnGameUnPaused;

        if (ClimberMovement.Instance != null)
        {
            ClimberMovement.Instance.OnReachedGoal += ClimberMovement_OnReachedGoal;
        }
        else
        {
            StartCoroutine(WaitForClimberMovement());
        }
    }

    private IEnumerator WaitForClimberMovement()
    {
        yield return new WaitUntil(() => ClimberMovement.Instance != null);
        ClimberMovement.Instance.OnReachedGoal += ClimberMovement_OnReachedGoal;
    }

    private void ClimberMovement_OnReachedGoal(object sender, System.EventArgs e)
    {
        HideThisCanvas();
    }

    private void GameManager_OnGameUnPaused(object sender, System.EventArgs e)
    {
        ShowThisCanvas();
    }

    private void GameManager_OnGamePaused(object sender, System.EventArgs e)
    {
        HideThisCanvas();
    }

    private void HideThisCanvas()
    {
        gameObject.SetActive(false);
    }

    private void ShowThisCanvas()
    {
        gameObject.SetActive(true); 
    }

    /*
    public void HideAllCanvas()
    {
        if (uiManagerSO.mainCanvas != null && uiManagerSO.alexCanvas != null)
        {
            uiManagerSO.mainCanvas.gameObject.SetActive(false);
            uiManagerSO.alexCanvas.gameObject.SetActive(false);
        }
    }

    public void ShowAllCanvas()
    {
        if (uiManagerSO.mainCanvas != null && uiManagerSO.alexCanvas != null)
        {
            uiManagerSO.mainCanvas.gameObject.SetActive(true);
            uiManagerSO.alexCanvas.gameObject.SetActive(true);
        }
    }
    */

    private void OnDestroy()
    {
        GameManager.Instance.OnGamePaused -= GameManager_OnGamePaused;
        GameManager.Instance.OnGameUnPaused -= GameManager_OnGameUnPaused;
    }
}
