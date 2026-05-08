using UnityEngine;

public class CameraWindAudio : MonoBehaviour
{
    // =========================================================
    //  CONFIG
    // =========================================================

    [Header("Audio")]
    [SerializeField] private AudioSource windAudioSource;

    [Header("Volume")]
    // Volumen máximo que alcanza el viento a velocidad máxima (antes de aplicar settings)
    [SerializeField] private float maxVolume = 1f;
    // Qué tan rápido sube/baja el volumen al empezar o parar de moverse
    [SerializeField] private float volumeFadeSpeed = 3f;

    [Header("Pitch")]
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.4f;
    // Qué tan rápido cambia el pitch al acelerar o decelerar
    [SerializeField] private float pitchFadeSpeed = 2f;

    [Header("Velocity Detection")]
    // Velocidad a partir de la cual el viento suena al máximo
    // Ajusta según tu flySpeed (por defecto 25). Con 15 el viento
    // ya suena fuerte antes de llegar a la velocidad máxima.
    [SerializeField] private float maxReferenceSpeed = 15f;

    // =========================================================
    //  PRIVATE
    // =========================================================

    private Vector3 lastPosition;
    private float currentVolumeT;  // 0..1 suavizado actual
    private float currentPitchT;   // 0..1 suavizado actual

    // =========================================================
    //  UNITY
    // =========================================================

    private void Start()
    {
        lastPosition = transform.position;

        if (windAudioSource == null)
        {
            Debug.LogError("CameraWindAudio: No hay AudioSource asignado.");
            enabled = false;
            return;
        }

        windAudioSource.loop = true;
        windAudioSource.volume = 0f;
        windAudioSource.pitch = minPitch;

        if (!windAudioSource.isPlaying)
            windAudioSource.Play();
    }

    private void Update()
    {
        // Bloqueamos el viento durante estados que pausan la cámara
        if (GameManager.Instance != null &&
           (GameManager.Instance.CurrentState == GameManager.GameState.Cinematic ||
            GameManager.Instance.CurrentState == GameManager.GameState.GamePause ||
            GameManager.Instance.CurrentState == GameManager.GameState.GameOver ||
            GameManager.Instance.CurrentState == GameManager.GameState.Initializing))
        {
            SetWindSilent();
            return;
        }

        // Actualizamos lastPosition siempre, incluso en pausa,
        // para que al reanudar no haya un spike de velocidad falso
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        // Protegemos la división por cero cuando Time.deltaTime es 0
        float speed = Time.deltaTime > 0.0001f ? distanceMoved / Time.deltaTime : 0f;

        // Normalizamos la velocidad a un valor 0..1
        float targetT = Mathf.Clamp01(speed / maxReferenceSpeed);

        // Suavizamos volumen y pitch por separado para que puedan
        // tener velocidades de transición distintas
        currentVolumeT = Mathf.Lerp(currentVolumeT, targetT, Time.deltaTime * volumeFadeSpeed);
        currentPitchT = Mathf.Lerp(currentPitchT, targetT, Time.deltaTime * pitchFadeSpeed);

        // Aplicamos volumen respetando masterVolume y effectsVolume de settings
        float settingsMultiplier = GetSettingsMultiplier();
        windAudioSource.volume = currentVolumeT * maxVolume * settingsMultiplier;

        // Aplicamos pitch
        windAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, currentPitchT);
    }

    // =========================================================
    //  HELPERS
    // =========================================================

    private void SetWindSilent()
    {
        currentVolumeT = Mathf.Lerp(currentVolumeT, 0f, Time.deltaTime * volumeFadeSpeed);
        currentPitchT = Mathf.Lerp(currentPitchT, 0f, Time.deltaTime * pitchFadeSpeed);

        float settingsMultiplier = GetSettingsMultiplier();
        windAudioSource.volume = currentVolumeT * maxVolume * settingsMultiplier;
        windAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, currentPitchT);
    }

    private float GetSettingsMultiplier()
    {
        if (Temporal_Sound_Music.Instance == null) return 1f;

        return Temporal_Sound_Music.Instance.GetMasterVolume() *
               Temporal_Sound_Music.Instance.GetSoundVolume();
    }
}
