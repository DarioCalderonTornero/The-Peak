using UnityEngine;

public class Temporal_Sound_Music : MonoBehaviour
{
    public static Temporal_Sound_Music Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;

    private float masterVolume = 1.0f;
    private float effectsVolume = 1.0f;
    private float musicVolume = 1.0f;

    [SerializeField] private AudioClip[] musicPlayList;

    private int currentTrackIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolume();
    }

    private void Start()
    {
        if (musicPlayList.Length == 0)
        {
            PlayMusic(musicPlayList[0]);
        }
    }

    private void Update()
    {
        if (!musicSource.isPlaying && musicPlayList.Length > 0)
        {
            PlayNextTrack();
        }
    }

    private void PlayNextTrack()
    {
        currentTrackIndex = (currentTrackIndex + 1) % musicPlayList.Length;
        PlayMusic(musicPlayList[currentTrackIndex]);
    }


    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        Vector3 camPos = Camera.main != null
            ? Camera.main.transform.position
            : Vector3.zero;

        AudioSource.PlayClipAtPoint(
            clip,
            camPos,
            volume * masterVolume * effectsVolume
        );
    }


    public void PlayMusic(AudioClip musicClip, float volume = 0.05f)
    {
        if (musicClip == null || musicSource.clip == musicClip) return;
        musicSource.clip = musicClip;
        musicSource.volume = volume * masterVolume * musicVolume;
        musicSource.loop = false;
        musicSource.Play();
    }

    public void StopMusic() => musicSource.Stop();
    public void PauseMusic() => musicSource.Pause();
    public void UnPauseMusic() => musicSource.UnPause();

    public void Play3DSound(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("3DSound");
        tempGO.transform.position = position;

        AudioSource aSource = tempGO.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume * masterVolume * effectsVolume;
        aSource.spatialBlend = 1.0f;
        aSource.rolloffMode = AudioRolloffMode.Logarithmic;
        aSource.maxDistance = 15f;
        aSource.Play();

        Destroy(tempGO, clip.length);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = volume;
        UpdateVolumes();
        SaveVolume();
    }

    public void SetEffectsVolume(float volume)
    {
        effectsVolume = volume;
        UpdateVolumes();
        SaveVolume();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        UpdateVolumes();
        SaveVolume();
    }

    private void UpdateVolumes()
    {
        musicSource.volume = masterVolume * musicVolume;
    }

    private void SaveVolume()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("EffectsVolume", effectsVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();
    }

    private void LoadVolume()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        effectsVolume = PlayerPrefs.GetFloat("EffectsVolume", 1.0f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
    }

    public float GetMasterVolume() => masterVolume;
    public float GetMusicVolume() => musicVolume;
    public float GetEffectsVolume() => effectsVolume;
}
