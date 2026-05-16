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
        SetTitle("OPCIONES");

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

        generalSettingsButton.gameObject.SetActive(false);
        volumeSettingsButton.gameObject.SetActive(false);

        AnimateElement(generalSettingsButton.gameObject, 0);
        AnimateElement(volumeSettingsButton.gameObject, 1);
    }

    public void ShowGeneralSettings()
    {
        SetTitle("CAMARA");

        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(false);
        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(true);

        // Ocultar volume
        musicSlider.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);
        musicVolumeText.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);

        // Listeners
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

        // Animar sliders y labels escalonados
        cameraSpeedText.gameObject.SetActive(false);
        cameraSpeedSlider.gameObject.SetActive(false);
        cameraPanText.gameObject.SetActive(false);
        cameraPanSlider.gameObject.SetActive(false);

        AnimateElement(cameraSpeedText.gameObject, 0);
        AnimateElement(cameraSpeedSlider.gameObject, 1);
        AnimateElement(cameraPanText.gameObject, 2);
        AnimateElement(cameraPanSlider.gameObject, 3);
    }

    public void ShowVolumeSettings()
    {
        SetTitle("SONIDO");

        volumeSettingsButton.gameObject.SetActive(false);
        generalSettingsButton.gameObject.SetActive(false);

        if (cameraSettingsPanel != null) cameraSettingsPanel.SetActive(false);
        if (volumeSettingsPanel != null) volumeSettingsPanel.SetActive(true);

        // Listeners
        musicSlider.onValueChanged.RemoveAllListeners();
        effectsSlider.onValueChanged.RemoveAllListeners();

        musicSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetMusicVolume);
        effectsSlider.onValueChanged.AddListener(Temporal_Sound_Music.Instance.SetEffectsVolume);

        musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MusicVolume", 1.0f));
        effectsSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("EffectsVolume", 1.0f));

        // Animar sliders y labels escalonados
        musicVolumeText.gameObject.SetActive(false);
        musicSlider.gameObject.SetActive(false);
        effectsVolumeText.gameObject.SetActive(false);
        effectsSlider.gameObject.SetActive(false);

        AnimateElement(musicVolumeText.gameObject, 0);
        AnimateElement(musicSlider.gameObject, 1);
        AnimateElement(effectsVolumeText.gameObject, 2);
        AnimateElement(effectsSlider.gameObject, 3);
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

    // Método genérico: funciona para botones, sliders, labels, cualquier GO
    private void AnimateElement(GameObject element, int index)
    {
        element.SetActive(true);
        element.transform.localScale = Vector3.zero;
        element.transform.DOKill();
        element.transform
            .DOScale(Vector3.one, buttonSpawnDuration)
            .SetEase(Ease.OutBack)
            .SetDelay(panelSpawnDuration + index * buttonSpawnDelay)
            .SetUpdate(true);
    }

    private void Update()
    {
        if (cameraSettingsPanel != null && cameraSettingsPanel.activeSelf)
        {
            cameraSpeedText.text = "Velocidad camara: " + cameraSpeedSlider.value.ToString("F1");
            cameraPanText.text = "Paneo camara: " + cameraPanSlider.value.ToString("F1");
        }

        if (volumeSettingsPanel != null && volumeSettingsPanel.activeSelf)
        {
            musicVolumeText.text = "Volumen musica: " + musicSlider.value.ToString("F1");
            effectsVolumeText.text = "Volumen efectos: " + effectsSlider.value.ToString("F1");
        }
    }
}