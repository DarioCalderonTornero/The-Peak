using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GamePauseUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button cinematicModeToggleButton;
    [SerializeField] private AudioClip stopGameAudioClip;
    [SerializeField] private float volume = 1f;
    [SerializeField] private SettingsUI settingsUI;

    [Header("Animación botones")]
    [SerializeField] private float buttonSpawnDuration = 0.25f;
    [SerializeField] private float buttonSpawnDelay = 0.1f;

    private bool isGamePaused = false;

    private void Awake()
    {
        resumeButton.onClick.AddListener(() => TogglePauseMenu());

        settingsButton.onClick.AddListener(() =>
        {
            HidePauseButtons(false);
            settingsUI.gameObject.SetActive(true);
            settingsUI.ShowMainButtons();
        });

        backToMenuButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1.0f;
            SceneLoader.LoadScene(SceneLoader.Scene.MainMenuScenee);
        });

        cinematicModeToggleButton.onClick.AddListener(() =>
        {
            DeathCinematicManager.Instance.IsPlayingCinematic();
        });

        HideAll();
    }

    private void Start()
    {
        if (settingsUI != null)
            settingsUI.OnSettingsClose += SettingsUI_OnSettingsClose;
    }

    private void OnDestroy()
    {
        if (settingsUI != null)
            settingsUI.OnSettingsClose -= SettingsUI_OnSettingsClose;
    }

    private void SettingsUI_OnSettingsClose(object sender, System.EventArgs e)
    {
        ShowPauseButtons();
    }

    public void TogglePauseMenu()
    {
        if (!isGamePaused)
        {
            ShowPauseButtons();
            Temporal_Sound_Music.Instance.PlaySound(stopGameAudioClip, volume);
            GameManager.Instance.PauseGame();
        }
        else
        {
            HideAll();
            GameManager.Instance.UnPauseGame();
            Temporal_Sound_Music.Instance.PlaySound(stopGameAudioClip, volume);
        }

        isGamePaused = !isGamePaused;
    }

    private void ShowPauseButtons()
    {
        backgroundImage.gameObject.SetActive(true);

        if (settingsUI != null)
            settingsUI.gameObject.SetActive(false);

        Button[] buttons = { resumeButton, settingsButton, backToMenuButton, cinematicModeToggleButton };

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            btn.gameObject.SetActive(false);
            btn.transform.DOKill();
            btn.transform.localScale = Vector3.zero;
            btn.gameObject.SetActive(true);
            btn.transform
                .DOScale(Vector3.one, buttonSpawnDuration)
                .SetEase(Ease.OutBack)
                .SetDelay(i * buttonSpawnDelay)
                .SetUpdate(true);
        }
    }

    private void HidePauseButtons(bool hideBackground)
    {
        if (hideBackground)
            backgroundImage.gameObject.SetActive(false);

        resumeButton.gameObject.SetActive(false);
        settingsButton.gameObject.SetActive(false);
        backToMenuButton.gameObject.SetActive(false);
        cinematicModeToggleButton.gameObject.SetActive(false);
    }

    private void HideAll()
    {
        HidePauseButtons(true);

        if (settingsUI != null)
            settingsUI.gameObject.SetActive(false);
    }
}