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
            SceneLoader.LoadScene(SceneLoader.Scene.MenuScene);
        });
    }

    private void Start()
    {
        Hide();

        InputManager.Instance.OnGamePause += InputManager_OnGamePause;
    }

    private void InputManager_OnGamePause(object sender, System.EventArgs e)
    {
        TogglePauseMenu();
    }

    private void TogglePauseMenu()
    {
        if (!isGamePaused)
        {
            Show();
            GameManager.Instance.PauseGame();
            //HideUIManager.Instance.HideAllCanvas();
        }

        else if (isGamePaused)
        {
            Hide();
            GameManager.Instance.UnPauseGame();
            //HideUIManager.Instance.ShowAllCanvas();
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
