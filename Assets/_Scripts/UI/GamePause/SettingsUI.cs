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
    [SerializeField] private FreeCameraController freeCameraController;
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
            //ShowGeneralSettings();
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

    public void ShowVolumeSettings()
    {
        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        musicSlider.gameObject.SetActive(true);
        effectsSlider.gameObject.SetActive(true);
        musicVolumeText.gameObject.SetActive(true);
        effectsVolumeText.gameObject.SetActive(true);
    }

    /*
    private void ShowGeneralSettings()
    {
        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);
        cameraSpeedSlider.gameObject.SetActive(true);
        cameraPanSlider.gameObject.SetActive(true);
        cameraSpeedText.gameObject.SetActive(true);
        cameraPanText.gameObject.SetActive(true);

        cameraSpeedSlider.value = freeCameraController.GetCameraSpeed();
        cameraPanSlider.value = freeCameraController.GetCameraPan();

        cameraSpeedSlider.onValueChanged.AddListener(value =>
        {
            freeCameraController.SetCameraSpeed(value);
            cameraSpeedText.text = $"Camera Speed: {value:F1}";
        });

        cameraPanSlider.onValueChanged.AddListener(value =>
        {
            freeCameraController.SetCameraPan(value);
            cameraPanText.text = $"Camera Pan: {value:F1}";
        });
    }
    */

    private void HideSettings()
    {
        gameObject.SetActive(false);

        // Limpiar listeners para evitar duplicados
        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();
    }

    private void Update()
    {
        // Actualizar dinámicamente los textos de volumen
        musicVolumeText.text = "Music Volume: " + Temporal_Sound_Music.Instance.GetMusicVolume().ToString("F1");
        effectsVolumeText.text = "Effects Volume: " + Temporal_Sound_Music.Instance.GetSoundVolume().ToString("F1");
    }
}