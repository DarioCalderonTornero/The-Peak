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

    [Header("Volume Labels")]
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private TextMeshProUGUI effectsVolumeText;

    [Header("Buttons")]
    [SerializeField] private Button backToNormalGamepauseButton;


    private void Awake()
    {
        backToNormalGamepauseButton.onClick.AddListener(() =>
        {
            HideSettings();
            OnSettingsClose?.Invoke(this, EventArgs.Empty);
        });
    }

    private void HideSettings()
    {
        gameObject.SetActive(false);
    }

    private void Start()
    {
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


    private void Update()
    {
        musicVolumeText.text = ("Music Volume: ") + Temporal_Sound_Music.Instance.GetMusicVolume().ToString("F1");
        effectsVolumeText.text = ("Effects Volume: ") + Temporal_Sound_Music.Instance.GetSoundVolume().ToString("F1");
    }
}
