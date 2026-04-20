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

        volumeSettingsButton.onClick.AddListener(ShowVolumeSettings);
        generalSettingsButton.onClick.AddListener(ShowGeneralSettings);

        ShowMainButtons();
    }

    public void ShowMainButtons()
    {
        // 1. Mostrar solo los botones principales
        backToNormalGamepauseButton.gameObject.SetActive(true);
        volumeSettingsButton.gameObject.SetActive(true);
        generalSettingsButton.gameObject.SetActive(true);

        // 2. MUY IMPORTANTE: Apagar los paneles de los sub-menús
        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(false);
        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(false);

        // 3. Ocultar sliders y textos para asegurar limpieza
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

        // Gestionar Paneles
        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(false);
        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(true);

        // Activar UI de Cámara
        cameraSpeedSlider.gameObject.SetActive(true);
        cameraPanSlider.gameObject.SetActive(true);
        cameraSpeedText.gameObject.SetActive(true);
        cameraPanText.gameObject.SetActive(true);

        // Desactivar UI de Música
        musicSlider.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);
        musicVolumeText.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);

        // Limpiar listeners anteriores para evitar que se multipliquen
        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();

        // Asignar nuevos listeners
        cameraSpeedSlider.onValueChanged.AddListener(value =>
        {
            freeCameraController.SetCameraSpeedMultiplier(value);
            PlayerPrefs.SetFloat("CameraSpeedMultiplier", value);
        });

        cameraPanSlider.onValueChanged.AddListener(value =>
        {
            freeCameraController.SetCameraPanMultiplier(value);
            PlayerPrefs.SetFloat("CameraPanMultiplier", value);
        });

        // Setear slider en el multiplicador guardado (o 1 si no existe)
        float savedSpeedMult = PlayerPrefs.GetFloat("CameraSpeedMultiplier", 1f);
        float savedPanMult = PlayerPrefs.GetFloat("CameraPanMultiplier", 1f);

        cameraSpeedSlider.SetValueWithoutNotify(savedSpeedMult);
        cameraPanSlider.SetValueWithoutNotify(savedPanMult);
    }

    public void ShowVolumeSettings()
    {
        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        // CORRECCIÓN: Gestionar Paneles correctamente
        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(false);
        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(true);

        // Activar UI de Música
        musicSlider.gameObject.SetActive(true);
        effectsSlider.gameObject.SetActive(true);
        musicVolumeText.gameObject.SetActive(true);
        effectsVolumeText.gameObject.SetActive(true);

        // Limpiar listeners anteriores para evitar que se multipliquen
        musicSlider.onValueChanged.RemoveAllListeners();
        effectsSlider.onValueChanged.RemoveAllListeners();

        // Asignar nuevos listeners
        musicSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetMusicVolume);
        effectsSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetEffectsVolume);

        // Setear valores actuales sin disparar los eventos
        musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1.0f));
        effectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("EffectsVolume", 1.0f));
    }

    private void HideSettings()
    {
        // Limpiamos de forma segura todos los listeners antes de apagar
        musicSlider.onValueChanged.RemoveAllListeners();
        effectsSlider.onValueChanged.RemoveAllListeners();
        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();

        gameObject.SetActive(false);
    }

    private void Update()
    {
        cameraSpeedText.text = "Camera Speed: " + cameraSpeedSlider.value.ToString("F1");
        cameraPanText.text = "Camera Pan: " + cameraPanSlider.value.ToString("F1");
    }
}