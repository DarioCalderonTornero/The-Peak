using Unity.Cinemachine;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Referencias de Ruido (Noise)")]
    [Tooltip("El componente Noise de la cámara principal")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin mainCameraNoise;
    [Tooltip("El componente Noise de la cámara de cinemáticas de muerte")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin deathCameraNoise;

    private float shakeTimer;
    private CinemachineBasicMultiChannelPerlin currentActiveNoise;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StopShake();
    }

    private void Update()
    {
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0)
            {
                StopShake();
            }
        }
    }

    // --- MÉTODOS PÚBLICOS PARA LLAMAR DESDE OTROS SCRIPTS ---

    public void ShakeMainCamera(float amplitude, float frequency, float time)
    {
        ApplyShake(mainCameraNoise, amplitude, frequency, time);
    }

    public void ShakeDeathCamera(float amplitude, float frequency, float time)
    {
        ApplyShake(deathCameraNoise, amplitude, frequency, time);
    }


    private void ApplyShake(CinemachineBasicMultiChannelPerlin targetNoise, float amplitude, float frequency, float time)
    {
        if (targetNoise == null) return;

        StopShake();

        currentActiveNoise = targetNoise;
        currentActiveNoise.AmplitudeGain = amplitude;
        currentActiveNoise.FrequencyGain = frequency;
        shakeTimer = time;
    }

    public void StopShake()
    {
        if (currentActiveNoise != null)
        {
            currentActiveNoise.AmplitudeGain = 0f;
            currentActiveNoise.FrequencyGain = 0f;
            currentActiveNoise = null;
        }
        else
        {
            // Por seguridad al inicio, apagamos las dos
            if (mainCameraNoise != null) { mainCameraNoise.AmplitudeGain = 0f; mainCameraNoise.FrequencyGain = 0f; }
            if (deathCameraNoise != null) { deathCameraNoise.AmplitudeGain = 0f; deathCameraNoise.FrequencyGain = 0f; }
        }

        shakeTimer = 0f;
    }
}