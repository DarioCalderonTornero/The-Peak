using Unity.Cinemachine;
using UnityEngine;

public class CameraOrbitRightClick : MonoBehaviour
{
    private CinemachineInputAxisController axisController;

    private bool canMoveCamera;

    [Header("Wind SFX")]
    [SerializeField] private AudioSource windSource;

    [Tooltip("Volumen mínimo cuando hay rotación (muy bajo).")]
    [SerializeField, Range(0f, 1f)] private float windMinVolume = 0.03f;

    [Tooltip("Volumen máximo (mantén esto bajo).")]
    [SerializeField, Range(0f, 1f)] private float windMaxVolume = 0.12f;

    [Tooltip("A partir de qué velocidad de input empieza a sonar.")]
    [SerializeField] private float rotateThreshold = 0.02f;

    [Tooltip("Velocidad a la que ya consideramos 'rotación rápida' (para mapear a max volume).")]
    [SerializeField] private float speedForMax = 0.35f;

    [Tooltip("Suavizado del volumen (más alto = responde más rápido).")]
    [SerializeField] private float volumeSmooth = 12f;

    [Header("Optional Pitch")]
    [SerializeField] private bool affectPitch = true;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.10f;
    [SerializeField] private float pitchSmooth = 10f;

    private float targetVolume = 0f;
    private float smoothedSpeed = 0f;

    void Awake()
    {
        axisController = GetComponent<CinemachineInputAxisController>();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        if (windSource == null)
            windSource = GetComponent<AudioSource>();

        if (windSource != null)
        {
            windSource.loop = true;
            windSource.playOnAwake = false;
            windSource.spatialBlend = 0f;
            windSource.volume = 0f;
        }
    }

    private System.Collections.IEnumerator Start()
    {
        axisController.enabled = false;
        canMoveCamera = false;

        yield return new WaitUntil(() => CardGameManager.Instance != null);
        CardGameManager.Instance.OnInventoryHide += CardGameManager_OnInventoryHide;
    }

    private void CardGameManager_OnInventoryHide(object sender, System.EventArgs e)
    {
        canMoveCamera = true;
    }

    void Update()
    {
        if (!canMoveCamera)
        {
            axisController.enabled = false;
            SetWindTarget(0f, false);
            UpdateWind();
            return;
        }

        bool rightClickHeld = Input.GetMouseButton(1);
        axisController.enabled = rightClickHeld;

        // Velocidad de rotación (input "crudo")
        float lookX = Mathf.Abs(Input.GetAxis("Mouse X"));
        float lookY = Mathf.Abs(Input.GetAxis("Mouse Y"));
        float rawSpeed = lookX + lookY;

        // Suavizamos la velocidad para que no sea nervioso
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-volumeSmooth * Time.unscaledDeltaTime));

        bool rotating = rightClickHeld && smoothedSpeed > rotateThreshold;

        if (rotating)
        {
            // Map speed -> 0..1
            float t = Mathf.InverseLerp(rotateThreshold, speedForMax, smoothedSpeed);
            t = Mathf.Clamp01(t);

            // Curva suave para que la subida sea agradable
            float eased = t * t * (3f - 2f * t); // smoothstep

            float vol = Mathf.Lerp(windMinVolume, windMaxVolume, eased);
            SetWindTarget(vol, true);

            if (affectPitch && windSource != null)
            {
                float targetPitch = Mathf.Lerp(pitchMin, pitchMax, eased);
                windSource.pitch = Mathf.Lerp(windSource.pitch, targetPitch, 1f - Mathf.Exp(-pitchSmooth * Time.unscaledDeltaTime));
            }
        }
        else
        {
            SetWindTarget(0f, false);
        }

        UpdateWind();
    }

    private void SetWindTarget(float vol, bool ensurePlaying)
    {
        targetVolume = vol;

        if (windSource == null) return;

        if (ensurePlaying && !windSource.isPlaying)
            windSource.Play();
    }

    private void UpdateWind()
    {
        if (windSource == null) return;

        // Fade hacia targetVolume
        windSource.volume = Mathf.Lerp(windSource.volume, targetVolume, 1f - Mathf.Exp(-volumeSmooth * Time.unscaledDeltaTime));

        // Si ya estamos prácticamente a 0, pausamos (más limpio que Stop)
        if (targetVolume <= 0.0001f && windSource.volume <= 0.001f && windSource.isPlaying)
            windSource.Pause();

        // Si está pausado y hay volumen objetivo, reanuda
        if (targetVolume > 0.01f && !windSource.isPlaying)
            windSource.UnPause();
    }

    private void OnDestroy()
    {
        if (CardGameManager.Instance != null)
            CardGameManager.Instance.OnInventoryHide -= CardGameManager_OnInventoryHide;
    }
}
