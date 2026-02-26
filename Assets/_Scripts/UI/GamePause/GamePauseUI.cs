using UnityEngine;
using UnityEngine.UI;

public class GamePauseUI : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToMenuButton;

    [SerializeField] private AudioClip stopGameAudioClip;
    [SerializeField] private float volume = 1f;

    [SerializeField] private SettingsUI settingsUI;

    private bool isGamePaused = false;

    private void Awake()
    {
        resumeButton.onClick.AddListener(() =>
        {
            TogglePauseMenu();
        });

        settingsButton.onClick.AddListener(() =>
        {
            // Ocultamos solo los botones, pero dejamos el fondo gris
            HidePauseButtons(false);

            // Mostramos los ajustes
            settingsUI.ShowMainButtons();
            settingsUI.gameObject.SetActive(true);
        });

        backToMenuButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1.0f;
            SceneLoader.LoadScene(SceneLoader.Scene.MenuScene);
        });

        // Nos aseguramos de que todo esté apagado al iniciar
        HideAll();
    }

    private void Start()
    {
        // Nos suscribimos al evento de cierre de los ajustes
        if (settingsUI != null)
        {
            settingsUI.OnSettingsClose += SettingsUI_OnSettingsClose;
        }
    }

    private void OnDestroy()
    {
        // Buena práctica: desuscribirse de los eventos para evitar errores de memoria
        if (settingsUI != null)
        {
            settingsUI.OnSettingsClose -= SettingsUI_OnSettingsClose;
        }
    }

    private void SettingsUI_OnSettingsClose(object sender, System.EventArgs e)
    {
        // Cuando los ajustes se cierran, volvemos a mostrar el menú de pausa principal
        ShowPauseButtons();
    }

    public void TogglePauseMenu()
    {
        if (!isGamePaused)
        {
            // Pausar
            ShowPauseButtons();
            Temporal_Sound_Music.Instance.PlaySound(stopGameAudioClip, volume);
            GameManager.Instance.PauseGame();
        }
        else
        {
            // Despausar (Ocultamos TODO, incluyendo ajustes si estuvieran abiertos)
            HideAll();
            GameManager.Instance.UnPauseGame();
            Temporal_Sound_Music.Instance.PlaySound(stopGameAudioClip, volume);
        }

        isGamePaused = !isGamePaused;
    }

    // --- MÉTODOS DE CONTROL DE UI ---

    private void ShowPauseButtons()
    {
        backgroundImage.gameObject.SetActive(true);

        resumeButton.gameObject.SetActive(true);
        settingsButton.gameObject.SetActive(true);
        backToMenuButton.gameObject.SetActive(true);

        // MUY IMPORTANTE: Apagamos los ajustes para evitar solapamientos
        if (settingsUI != null)
        {
            settingsUI.gameObject.SetActive(false);
        }
    }

    private void HidePauseButtons(bool hideBackground)
    {
        if (hideBackground)
        {
            backgroundImage.gameObject.SetActive(false);
        }

        resumeButton.gameObject.SetActive(false);
        settingsButton.gameObject.SetActive(false);
        backToMenuButton.gameObject.SetActive(false);
    }

    private void HideAll()
    {
        // Oculta los botones y el fondo
        HidePauseButtons(true);

        // Oculta también el menú de ajustes por si estaba abierto
        if (settingsUI != null)
        {
            settingsUI.gameObject.SetActive(false);
        }
    }
}