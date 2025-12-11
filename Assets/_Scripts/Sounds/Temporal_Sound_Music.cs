using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Temporal_Sound_Music : MonoBehaviour
{
    public static Temporal_Sound_Music Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;

    private float masterVolume = 1.0f;
    private float effectsVolume = 1.0f;
    private float musicVolume = 1.0f;

    [SerializeField] private AudioClip[] musicPlayList;

    private int currentTrackIndex;

    //  ARREGLO BUG #7: Object Pooling para AudioSources
    private Queue<GameObject> sfxPool = new Queue<GameObject>();
    private const int INITIAL_POOL_SIZE = 10;

    private void Awake()
    {
        //  ARREGLO BUG #3: Singleton seguro
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolume();
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            CreateNewPoolObject();
        }
    }

    private GameObject CreateNewPoolObject()
    {
        GameObject go = new GameObject("Pooled_SFX");
        go.transform.SetParent(transform);
        go.AddComponent<AudioSource>();
        go.SetActive(false);
        sfxPool.Enqueue(go);
        return go;
    }

    private void Start()
    {
        //  ARREGLO BUG #8: Chequeo de nulos y longitud antes de acceder al array
        if (musicPlayList != null && musicPlayList.Length > 0)
        {
            currentTrackIndex = 0;
            PlayMusic(musicPlayList[currentTrackIndex]);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (musicSource != null && !musicSource.isPlaying && musicPlayList != null && musicPlayList.Length > 0)
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
        // PlayClipAtPoint crea un objeto y lo destruye, pero para sonidos 2D/Globales es "aceptable" si no son muchos.
        // Lo ideal sería usar también el pool aquí, pero PlayClipAtPoint es estático.
        // Lo dejaremos así para no complicar en exceso, el crítico es Play3DSound.
        Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, camPos, volume * masterVolume * effectsVolume);
    }

    public void PlayMusic(AudioClip musicClip, float volume = 0.05f)
    {
        if (musicClip == null || musicSource == null) return;
        if (musicSource.clip == musicClip) return;

        musicSource.clip = musicClip;
        musicSource.volume = volume * masterVolume * musicVolume;
        musicSource.loop = false;
        musicSource.Play();
    }

    public void StopMusic() { if (musicSource) musicSource.Stop(); }
    public void PauseMusic() { if (musicSource) musicSource.Pause(); }
    public void UnPauseMusic() { if (musicSource) musicSource.UnPause(); }

    public void Play3DSound(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        if (clip == null) return;

        //  USAMOS EL POOL
        GameObject audioObj = null;
        if (sfxPool.Count > 0) audioObj = sfxPool.Dequeue();
        else audioObj = CreateNewPoolObject(); // Expandir si es necesario

        audioObj.SetActive(true);
        audioObj.transform.position = position;

        AudioSource aSource = audioObj.GetComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume * masterVolume * effectsVolume;
        aSource.spatialBlend = 1.0f;
        aSource.rolloffMode = AudioRolloffMode.Logarithmic;
        aSource.maxDistance = 15f;
        aSource.Play();

        // En lugar de Destroy, iniciamos corrutina para devolver al pool
        StartCoroutine(ReturnToPool(audioObj, clip.length));
    }

    private IEnumerator ReturnToPool(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        obj.SetActive(false);
        sfxPool.Enqueue(obj);
    }

    public void SetMasterVolume(float volume) { masterVolume = volume; UpdateVolumes(); SaveVolume(); }
    public void SetEffectsVolume(float volume) { effectsVolume = volume; UpdateVolumes(); SaveVolume(); }
    public void SetMusicVolume(float volume) { musicVolume = volume; UpdateVolumes(); SaveVolume(); }

    private void UpdateVolumes()
    {
        if (musicSource) musicSource.volume = masterVolume * musicVolume;
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
}