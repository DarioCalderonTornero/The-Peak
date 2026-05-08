using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class Temporal_Sound_Music : MonoBehaviour
{
    public static Temporal_Sound_Music Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;

    [SerializeField] private AudioClip deathClimberClip;

    private float masterVolume = 1.0f;
    private float effectsVolume = 1.0f;
    private float musicVolume = 1.0f;

    [SerializeField] private AudioClip[] musicPlayList;

    private int currentTrackIndex;
    // ARREGLO BUG #7: Object Pooling para AudioSources
    private Queue<GameObject> sfxPool = new Queue<GameObject>();
    private const int INITIAL_POOL_SIZE = 10;

    [SerializeField] private float baseMusicVolume = 0.15f;

    private void Awake()
    {
        // ARREGLO BUG #3: Singleton seguro
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
        // ARREGLO BUG #8: Chequeo de nulos y longitud antes de acceder al array
        if (musicPlayList != null && musicPlayList.Length > 0)
        {
            currentTrackIndex = 0;
            PlayMusic(musicPlayList[currentTrackIndex]);
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnClimberDead += GameManager_OnClimberDead;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnClimberDead -= GameManager_OnClimberDead;
    }


    private void GameManager_OnClimberDead(GameManager.DeathInfo obj)
    {
        //Debug.Log("PlayDeathSound");
        //PlayDeathSound();
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

    public void PlayDeathSound()
    {
        PlaySound(deathClimberClip, 1f);
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, camPos, volume * masterVolume * effectsVolume);
    }

    public void PlayMusic(AudioClip musicClip, float volume = 0.15f)
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

        // USAMOS EL POOL
        GameObject audioObj = null;
        if (sfxPool.Count > 0) audioObj = sfxPool.Dequeue();
        else audioObj = CreateNewPoolObject(); // Expandir si es necesario

        audioObj.SetActive(true);
        audioObj.transform.position = position;

        AudioSource aSource = audioObj.GetComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume * masterVolume * effectsVolume;

        // --- CONFIGURACIÓN 3D ARREGLADA ---
        aSource.spatialBlend = 1.0f; // 100% 3D

        // Usamos Linear para que el volumen baje de forma suave y predecible
        aSource.rolloffMode = AudioRolloffMode.Linear;

        // Mientras la cámara esté a menos de 10 metros, se escuchará al 100% de volumen
        aSource.minDistance = 30;

        // El sonido desaparecerá por completo si la cámara se aleja a más de 50 metros
        aSource.maxDistance = 150;
        // ----------------------------------

        aSource.Play();

        // En lugar de Destroy, iniciamos corrutina para devolver al pool
        StartCoroutine(ReturnToPool(audioObj, clip.length));
    }

    public void Play2DSound(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;

        // Usamos el Pool igual que en el 3D
        GameObject audioObj = null;
        if (sfxPool.Count > 0) audioObj = sfxPool.Dequeue();
        else audioObj = CreateNewPoolObject();

        audioObj.SetActive(true);
        // ¡No importa la posición porque es 2D!

        AudioSource aSource = audioObj.GetComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume * masterVolume * effectsVolume;

        // --- ESTA ES LA MAGIA DEL 2D ---
        aSource.spatialBlend = 0f; // 0 = 100% 2D (Suena igual en todas partes)
        // -------------------------------

        aSource.Play();

        // Le damos 0.2 segundos extra de margen para que la "cola" del sonido no se corte
        StartCoroutine(ReturnToPool(audioObj, clip.length + 0.2f));
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
        if (musicSource) musicSource.volume = baseMusicVolume * masterVolume * musicVolume;
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

    public float GetMusicVolume()
    {
        return musicVolume;
    }

    public float GetSoundVolume()
    {
        return effectsVolume;
    }

    public float GetMasterVolume()
    {
        return masterVolume;
    }
}
