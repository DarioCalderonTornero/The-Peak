using Unity.Cinemachine;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [SerializeField] private CinemachineBasicMultiChannelPerlin noise;

    private float shakeDuration;
    private float shakeTimer;

    private void Update()
    {
        if (shakeTimer == 0)
            return;

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0)
            {
                StopShake();
            }
        }
    }

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

    public void SetCurrentStateCameraShake(float amplitude, float frequency, float time)
    {
        noise.AmplitudeGain = amplitude;
        noise.FrequencyGain = frequency;
        shakeDuration = time;
        shakeTimer = time;
        
    }

    public void StopShake()
    {
        SetCurrentStateCameraShake(0f, 0f, 0f);
    }
}
