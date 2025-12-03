using UnityEngine;
using UnityEngine.UI;

public class GamePauseUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button backToMenuButton;

    private bool isGamePaused;

    private void Awake()
    {
        resumeButton.onClick.AddListener(() =>
        {
            TogglePauseMenu();
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
        }

        else if (isGamePaused)
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
        backToMenuButton.gameObject.SetActive(true);    
    }

    private void Hide()
    {
        backgroundImage.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(false);
        backToMenuButton.gameObject.SetActive(false);
    }
}
