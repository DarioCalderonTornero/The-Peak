using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SettingsUI : MonoBehaviour
{
    public event EventHandler OnSettingsClose;

    [Header("Panel")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI titleText;

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

    [Header("Animación")]
    [SerializeField] private float panelSpawnDuration = 0.3f;
    [SerializeField] private float buttonSpawnDuration = 0.25f;
    [SerializeField] private float buttonSpawnDelay = 0.1f;

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

    private void OnEnable()
    {
        if (panel != null)
        {
            panel.localScale = Vector3.zero;
            panel.DOScale(Vector3.one, panelSpawnDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
    }

    public void ShowMainButtons()
    {
        SetTitle("SETTINGS");

        backToNormalGamepauseButton.gameObject.SetActive(true);

        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(false);
        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(false);

        musicSlider.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);
        musicVolumeText.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);

        cameraSpeedSlider.gameObject.SetActive(false);
        cameraPanSlider.gameObject.SetActive(false);
        cameraSpeedText.gameObject.SetActive(false);
        cameraPanText.gameObject.SetActive(false);

        // Desactivar antes de animar para resetear escala
        generalSettingsButton.gameObject.SetActive(false);
        volumeSettingsButton.gameObject.SetActive(false);

        AnimateButton(generalSettingsButton, 0, panelSpawnDuration);
        AnimateButton(volumeSettingsButton, 1, panelSpawnDuration);
    }

    public void ShowGeneralSettings()
    {
        SetTitle("CAMERA");

        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(false);
        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(true);

        cameraSpeedSlider.gameObject.SetActive(true);
        cameraPanSlider.gameObject.SetActive(true);
        cameraSpeedText.gameObject.SetActive(true);
        cameraPanText.gameObject.SetActive(true);

        musicSlider.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);
        musicVolumeText.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);

        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();

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

        cameraSpeedSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("CameraSpeedMultiplier", 1f));
        cameraPanSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("CameraPanMultiplier", 1f));
    }

    public void ShowVolumeSettings()
    {
        SetTitle("VOLUME");

        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(false);
        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(true);

        musicSlider.gameObject.SetActive(true);
        effectsSlider.gameObject.SetActive(true);
        musicVolumeText.gameObject.SetActive(true);
        effectsVolumeText.gameObject.SetActive(true);

        musicSlider.onValueChanged.RemoveAllListeners();
        effectsSlider.onValueChanged.RemoveAllListeners();

        musicSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetMusicVolume);
        effectsSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetEffectsVolume);

        musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1.0f));
        effectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("EffectsVolume", 1.0f));
    }

    private void HideSettings()
    {
        musicSlider.onValueChanged.RemoveAllListeners();
        effectsSlider.onValueChanged.RemoveAllListeners();
        cameraSpeedSlider.onValueChanged.RemoveAllListeners();
        cameraPanSlider.onValueChanged.RemoveAllListeners();

        gameObject.SetActive(false);
    }

    private void SetTitle(string title)
    {
        if (titleText != null)
            titleText.text = title;
    }

    private void AnimateButton(Button btn, int index, float delayOffset = 0f)
    {
        btn.gameObject.SetActive(true);
        btn.transform.localScale = Vector3.zero;
        btn.transform.DOKill();
        btn.transform
            .DOScale(Vector3.one, buttonSpawnDuration)
            .SetEase(Ease.OutBack)
            .SetDelay(delayOffset + index * buttonSpawnDelay)
            .SetUpdate(true);
    }

    private void Update()
    {
        if (cameraSettingsPanel != null && cameraSettingsPanel.activeSelf)
        {
            cameraSpeedText.text = "Camera Speed: " + cameraSpeedSlider.value.ToString("F1");
            cameraPanText.text = "Camera Pan: " + cameraPanSlider.value.ToString("F1");
        }
    }
}