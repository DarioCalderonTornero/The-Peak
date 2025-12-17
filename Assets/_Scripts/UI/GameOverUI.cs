using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private Button retryButton;

    private void Awake()
    {
        retryButton.onClick.AddListener(() =>
        {
            SceneLoader.LoadScene(SceneLoader.Scene.DarioScene);
        });
    }

    private void Start()
    {
        retryButton.gameObject.SetActive(false);
        GameOverManager.Instance.OnGameOver += GameOverManager_OnGameOver;
    }

    private void GameOverManager_OnGameOver(object sender, System.EventArgs e)
    {
        StartCoroutine(SetRetryButtonActive());

    }

    private IEnumerator SetRetryButtonActive()
    {
        yield return new WaitForSeconds(5f);
        retryButton.gameObject.SetActive(true);
    }
}
