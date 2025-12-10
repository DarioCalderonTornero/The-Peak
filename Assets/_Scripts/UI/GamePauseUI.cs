using UnityEngine;
using UnityEngine.UI;

public class GamePauseUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToMenuButton;

    private bool isGamePaused = false;

    private void Awake()
    {
        resumeButton.onClick.AddListener(() =>
        {
            TogglePauseMenu();
        });

        backToMenuButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1.0f;
            SceneLoader.LoadScene(SceneLoader.Scene.MenuScene);
        });

        Hide();
    }

    public void TogglePauseMenu()
    {
        if (!isGamePaused)
        {
            Show();
            GameManager.Instance.PauseGame();
        }
        else
        {
            Hide();
            GameManager.Instance.UnPauseGame();
        }

        isGamePaused = !isGamePaused;
    }

    private void Show()
    {
        backgroundImage.gameObject.SetActive(true);
        resumeButton.gameObject.SetActive(true);
        settingsButton.gameObject.SetActive(true);
        backToMenuButton.gameObject.SetActive(true);
    }

    private void Hide()
    {
        backgroundImage.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(false);
        settingsButton.gameObject.SetActive(false);
        backToMenuButton.gameObject.SetActive(false);
    }
}
