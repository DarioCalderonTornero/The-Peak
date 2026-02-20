using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public event EventHandler OnSettingsClose;

    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectsSlider;
    [SerializeField] private Slider cameraSpeedSlider;
    [SerializeField] private Slider cameraPanSlider;  

    [Header("Volume Labels")]
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private TextMeshProUGUI effectsVolumeText;
    [SerializeField] private TextMeshProUGUI cameraSpeedText;
    [SerializeField] private TextMeshProUGUI cameraPanText; 

    [Header("Buttons")]
    [SerializeField] private Button backToNormalGamepauseButton;
    [SerializeField] private Button volumeSettingsButton;
    [SerializeField] private Button generalSettingsButton;

    [Header("References")]
    [SerializeField] private FreeCameraMovement freeCameraController;

    [Header("Panels")]
    [SerializeField] private GameObject volumeSettingsPanel;
    [SerializeField] private GameObject cameraSettingsPanel;

    private void Awake()
    {
        backToNormalGamepauseButton.onClick.AddListener(() =>
        {
            HideSettings();
            OnSettingsClose?.Invoke(this, EventArgs.Empty);
        });

        volumeSettingsButton.onClick.AddListener(() =>
        {
            ShowVolumeSettings();
        });

        generalSettingsButton.onClick.AddListener(() =>
        {
            ShowGeneralSettings();
        });

        ShowMainButtons();
    }

    public void ShowMainButtons()
    {
        // Mostrar solo los botones principales
        backToNormalGamepauseButton.gameObject.SetActive(true);
        volumeSettingsButton.gameObject.SetActive(true);
        generalSettingsButton.gameObject.SetActive(true);

        // Ocultar sliders y textos
        musicSlider.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);
        musicVolumeText.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);
        cameraSpeedSlider.gameObject.SetActive(false);
        cameraPanSlider.gameObject.SetActive(false);
        cameraSpeedText.gameObject.SetActive(false);
        cameraPanText.gameObject.SetActive(false);
    }

    public void ShowGeneralSettings()
    {
        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        if (volumeSettingsPanel != null)
            volumeSettingsPanel.SetActive(false);
        if (cameraSettingsPanel != null)
            cameraSettingsPanel.SetActive(true);

        cameraSpeedSlider.gameObject.SetActive(true);
        cameraPanSlider.gameObject.SetActive(true);
        cameraSpeedText.gameObject.SetActive(true);
        cameraPanText.gameObject.SetActive(true);

        musicSlider.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);
        musicVolumeText.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);

        cameraSpeedSlider.onValueChanged.AddListener(value =>
        {
            freeCameraController.SetCameraSpeed(value);
            PlayerPrefs.SetFloat("CameraSpeed", value);
        });

        cameraPanSlider.onValueChanged.AddListener(value =>
        {
            freeCameraController.SetCameraPan(value);
            PlayerPrefs.SetFloat("CameraPan", value);
        });


        cameraSpeedSlider.SetValueWithoutNotify(freeCameraController.GetCameraSpeed());
        cameraPanSlider.SetValueWithoutNotify(freeCameraController.GetCameraPan());

    }

    public void ShowVolumeSettings()
    {
        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        //ShowVolumeSettingsPanel();

        musicSlider.gameObject.SetActive(true);
        effectsSlider.gameObject.SetActive(true);
        musicVolumeText.gameObject.SetActive(true);
        effectsVolumeText.gameObject.SetActive(true);

        musicSlider.SetValueWithoutNotify
       (
           PlayerPrefs.GetFloat("MusicVolume", 1.0f)
       );

        effectsSlider.SetValueWithoutNotify
        (
            PlayerPrefs.GetFloat("EffectsVolume", 1.0f)
        );

        musicSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetMusicVolume);
        effectsSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetEffectsVolume);
    }

    private void HideSettings()
    {
        gameObject.SetActive(false);

        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();

        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();
    }

    private void Update()
    {
        musicVolumeText.text = "Music Volume: " + Temporal_Sound_Music.Instance.GetMusicVolume().ToString("F1");
        effectsVolumeText.text = "Effects Volume: " + Temporal_Sound_Music.Instance.GetSoundVolume().ToString("F1");

        cameraSpeedText.text = "Camera Speed: " + freeCameraController.GetCameraSpeed().ToString("F0");
        cameraPanText.text = "Camera Pan: " + freeCameraController.GetCameraPan().ToString("F1");
    }
}