using System.Collections;
using UnityEngine;

public class HideUIManager : MonoBehaviour
{
    private void Start()
    {
        GameManager.Instance.OnGamePaused += GameManager_OnGamePaused;
        GameManager.Instance.OnGameUnPaused += GameManager_OnGameUnPaused;
        GameManager.Instance.OnTutorial += GameManager_OnTutorial;
        GameOverManager.Instance.OnGameOver += GameOverManager_OnGameOver;
    }

    private void GameManager_OnTutorial(object sender, System.EventArgs e)
    {
        HideThisCanvas();
    }

    private void GameOverManager_OnGameOver(object sender, System.EventArgs e)
    {
        HideThisCanvas();
    }

    private void GameManager_OnGameUnPaused(object sender, System.EventArgs e)
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Tutorial)
        {
            ShowThisCanvas();
        }
    }

    private void GameManager_OnGamePaused(object sender, System.EventArgs e)
    {
        HideThisCanvas();
        Debug.Log("Hide Canvas");
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
