using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIAudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Play);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Play);
    }

    public void Play()
    {
        if (!button.interactable) return;
        if (clickClip == null) return;

        Temporal_Sound_Music.Instance.PlaySound(clickClip, volume);
    }
}
