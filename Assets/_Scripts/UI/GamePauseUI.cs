using UnityEngine;
using UnityEngine.UI;

public class GamePauseUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private GameObject climberInfo;

    [SerializeField] private AudioClip stopGameAudioClip;
    [SerializeField] private float volume;

    private bool isGamePaused = false;

    private bool hasPlayedSound = false;

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

    private void Start()
    {
    }

    public void TogglePauseMenu()
    {
        if (!hasPlayedSound)
        {
            hasPlayedSound = true;  
        }

        if (!isGamePaused)
        {
            Show();
            Temporal_Sound_Music.Instance.PlaySound(stopGameAudioClip, volume);
            GameManager.Instance.PauseGame();
        }
        else
        {
            Hide();
            GameManager.Instance.UnPauseGame();
            Temporal_Sound_Music.Instance.PlaySound(stopGameAudioClip, volume);
        }

        isGamePaused = !isGamePaused;
    }

    private void Show()
    {
        hasPlayedSound = false;

        backgroundImage.gameObject.SetActive(true);
        resumeButton.gameObject.SetActive(true);
        settingsButton.gameObject.SetActive(true);
        backToMenuButton.gameObject.SetActive(true);
        climberInfo.SetActive(true);
    }

    private void Hide()
    {
        hasPlayedSound = false;

        backgroundImage.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(false);
        settingsButton.gameObject.SetActive(false);
        backToMenuButton.gameObject.SetActive(false);
        climberInfo.SetActive(false);
    }
}
