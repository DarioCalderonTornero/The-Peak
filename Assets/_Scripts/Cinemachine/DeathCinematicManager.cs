using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

public class DeathCinematicManager : MonoBehaviour
{
    public static DeathCinematicManager Instance { get; private set; }

    public event EventHandler OnCinematicFinished;
    public event Action<GameManager.DeathInfo> OnOwnClimberCinematicFinished;

    [Header("Referencias de Cámara")]
    [SerializeField] private CinemachineCamera deathCamera;

    [Header("Configuración de Posicionamiento")]
    [SerializeField] private LayerMask mountainLayer;
    [SerializeField] private float cameraDistance = 3f;
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private float sideOffset = 3f;

    [Header("Tiempos Cinemáticos")]
    [Tooltip("Tiempo que tarda la cámara en llegar al escalador.")]
    [SerializeField] private float blendInTime = 2.0f;
    [Tooltip("Tiempo que la cámara se queda mirando después de la explosión.")]
    [SerializeField] private float delayAfterExplosion = 1.5f;
    [SerializeField] private float delayAfterBlendCameras = 0.0f;

    [Header("Slow Motion")]
    [SerializeField] private float slowMotionScale = 0.2f;
    [SerializeField] private float slowMotionDuration = 0.5f;

    [Header("Rotación Visual de la Muerte")]
    [Tooltip("Offset opcional para corregir la rotación del prefab visual de muerte.")]
    [SerializeField] private Vector3 deathVisualRotationOffset = Vector3.zero;

    [Header("Ajustes de Control")]
    [SerializeField] private bool useDeathCinematics = true;

    [Header("Geyser / StormyCloud Camera Override")]
    [SerializeField] private float geyserCameraDistance = 6f;
    [SerializeField] private float geyserHeightOffset = 3f;
    [SerializeField] private float geyserSideOffset = 5f;

    private Coroutine processQueueCoroutine;
    private Coroutine slowMotionCoroutine;
    private GameManager.DeathInfo currentDeathInfo;

    private readonly Queue<GameManager.DeathInfo> deathQueue = new Queue<GameManager.DeathInfo>();
    private bool isPlayingCinematic = false;

    private Transform cameraAnchor;
    private bool animationComplete = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        cameraAnchor = new GameObject("DeathCameraAnchor").transform;
        cameraAnchor.SetParent(transform);

        if (deathCamera != null)
            deathCamera.Priority = 0;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnClimberDead += HandleClimberDeath;
        else
            Debug.LogWarning("[DeathCinematicManager] No se encontró el GameManager en la escena.");

        if (InputManager.Instance != null)
            InputManager.Instance.OnSkipCinematic += InputManager_OnSkipCinematic;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnClimberDead -= HandleClimberDeath;

        if (InputManager.Instance != null)
            InputManager.Instance.OnSkipCinematic -= InputManager_OnSkipCinematic;
    }

    private void InputManager_OnSkipCinematic(object sender, EventArgs e)
    {
        if (isPlayingCinematic)
            SkipCinematic();
    }

    // ─── Skip ────────────────────────────────────────────────────────────────

    private void SkipCinematic()
    {
        if (processQueueCoroutine != null)
        {
            StopCoroutine(processQueueCoroutine);
            processQueueCoroutine = null;
        }

        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        animationComplete = true;

        if (currentDeathInfo.climber != null)
            Destroy(currentDeathInfo.climber.gameObject);

        while (deathQueue.Count > 0)
        {
            var pending = deathQueue.Dequeue();
            if (pending.climber != null)
                Destroy(pending.climber.gameObject);
        }

        if (CameraShake.Instance != null)
            CameraShake.Instance.StopShake();

        if (deathCamera != null)
            deathCamera.Priority = 0;

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;

        Debug.Log("[DeathCinematicManager] Cinemática saltada por el jugador.");

        OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);
        OnCinematicFinished?.Invoke(this, EventArgs.Empty);
    }

    // ─── Handle ──────────────────────────────────────────────────────────────

    private void HandleClimberDeath(GameManager.DeathInfo deathInfo)
    {
        if (!useDeathCinematics)
        {
            if (deathInfo.climber != null)
                Destroy(deathInfo.climber.gameObject);
            return;
        }

        // Geyser y StormyCloud: solo mover la cámara ahora.
        // NotifyReadyToProcess gestionará la cinemática completa después.
        if (deathInfo.cause == DeathCause.Geyser || deathInfo.cause == DeathCause.StormyCloud)
        {
            if (deathInfo.climber != null)
            {
                deathInfo.climber.SetExternalSpeedMultiplier(0f);
                var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
            }
            FocusCameraOnClimber(deathInfo.climber);
            return;
        }

        if (deathInfo.climber != null)
        {
            deathInfo.climber.SetExternalSpeedMultiplier(0f);
            var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        deathQueue.Enqueue(deathInfo);

        if (!isPlayingCinematic)
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
    }

    // ─── Foco de cámara para Geyser y StormyCloud ────────────────────────────

    public void FocusCameraOnClimber(ClimberMovement climber)
    {
        if (climber == null) return;

        CalculateCameraAnchorPosition(climber.transform,
            geyserCameraDistance,
            geyserHeightOffset,
            geyserSideOffset);

        deathCamera.Follow = cameraAnchor;
        deathCamera.LookAt = climber.transform;
        deathCamera.Priority = 100;

        GameManager.Instance.SetState(GameManager.GameState.Cinematic);
        UIManager.Instance.HideAll();

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.ShowBars();

        isPlayingCinematic = true;
    }

    // ─── NotifyReadyToProcess ────────────────────────────────────────────────

    public void NotifyReadyToProcess(GameManager.DeathInfo deathInfo)
    {
        deathQueue.Enqueue(deathInfo);

        if (!isPlayingCinematic || processQueueCoroutine == null)
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
    }

    // ─── Cola de muertes ─────────────────────────────────────────────────────

    private IEnumerator ProcessDeathQueue()
    {
        isPlayingCinematic = true;
        GameManager.Instance.SetState(GameManager.GameState.Cinematic);
        UIManager.Instance.HideAll();

        bool barsAreShown = false;

        while (deathQueue.Count > 0)
        {
            currentDeathInfo = deathQueue.Dequeue();

            if (currentDeathInfo.climber == null)
                continue;

            // Para Geyser y StormyCloud la cámara ya está en su sitio,
            // saltamos el reposicionamiento y el blendInTime
            if (currentDeathInfo.cause != DeathCause.Geyser &&
                currentDeathInfo.cause != DeathCause.StormyCloud)
            {
                CalculateCameraAnchorPosition(currentDeathInfo.climber.transform);
                deathCamera.Follow = cameraAnchor;
                deathCamera.LookAt = currentDeathInfo.climber.transform;
                deathCamera.Priority = 100;

                float barsAnimationTime = 0.0f;
                float waitBeforeBars = Mathf.Max(0f, blendInTime - barsAnimationTime);
                float waitAfterBars = blendInTime - waitBeforeBars;

                yield return new WaitForSeconds(waitBeforeBars);

                if (currentDeathInfo.climber == null)
                {
                    Debug.LogWarning("[DeathCinematicManager] Escalador destruido antes de tiempo.");
                    if (deathQueue.Count <= 1)
                        deathCamera.Priority = 0;
                    continue;
                }

                if (!barsAreShown)
                {
                    if (CinematicBars.Instance != null)
                        CinematicBars.Instance.ShowBars();
                    barsAreShown = true;
                }

                yield return new WaitForSeconds(waitAfterBars + delayAfterBlendCameras);
            }
            else
            {
                // Geyser / StormyCloud: actualizamos LookAt a la posición actual
                if (currentDeathInfo.climber != null)
                    deathCamera.LookAt = currentDeathInfo.climber.transform;

                if (!barsAreShown)
                {
                    if (CinematicBars.Instance != null)
                        CinematicBars.Instance.ShowBars();
                    barsAreShown = true;
                }
            }

            // 2. Shake al llegar
            if (CameraShake.Instance != null)
                CameraShake.Instance.ShakeDeathCamera(3f, 10f, 0.2f);

            // 3. Preparar datos de muerte ANTES de destruir
            DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeathInfo.cause);

            Vector3 deathPos = currentDeathInfo.climber.transform.position;
            Quaternion deathRot = GetDeathVisualRotation(currentDeathInfo.climber.transform);

            var loadout = currentDeathInfo.climber.GetComponent<ClimberLoadout>();
            Color helmetColor = loadout != null ? loadout.GetHelmetColor() : Color.white;

            // 4. Destruir escalador original
            Destroy(currentDeathInfo.climber.gameObject);

            // 5. Instanciar visual y esperar a que termine la animación
            animationComplete = false;

            if (config != null && config.deathVisualPrefab != null)
            {
                GameObject visual = Instantiate(config.deathVisualPrefab, deathPos, deathRot);
                DeathAnimation deathAnim = visual.GetComponent<DeathAnimation>();

                if (deathAnim != null)
                {
                    void HandleDeathMoment()
                    {
                        if (slowMotionCoroutine != null)
                            StopCoroutine(slowMotionCoroutine);

                        if (config.deathAudioClip != null)
                            Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);

                        if (config.deathVFX_Effect != null)
                            Instantiate(config.deathVFX_Effect, deathPos, deathRot);

                        animationComplete = true;
                        deathAnim.OnAnimationComplete -= HandleDeathMoment;
                    }

                    deathAnim.OnAnimationComplete += HandleDeathMoment;
                    deathAnim.PlayAnimation(currentDeathInfo.cause, helmetColor);
                }
                else
                {
                    if (slowMotionCoroutine != null)
                        StopCoroutine(slowMotionCoroutine);

                    if (config.deathAudioClip != null)
                        Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);

                    if (config.deathVFX_Effect != null)
                        Instantiate(config.deathVFX_Effect, deathPos, deathRot);

                    animationComplete = true;
                }
            }
            else
            {
                if (slowMotionCoroutine != null)
                    StopCoroutine(slowMotionCoroutine);

                slowMotionCoroutine = StartCoroutine(SlowMotionRoutine());

                if (config != null)
                {
                    if (config.deathAudioClip != null)
                        Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);

                    if (config.deathVFX_Effect != null)
                        Instantiate(config.deathVFX_Effect, deathPos, deathRot);
                }

                animationComplete = true;
            }

            yield return new WaitUntil(() => animationComplete);

            // 6. Cámara se queda mirando el punto de muerte
            yield return new WaitForSeconds(delayAfterExplosion);

            OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);

            // 7. Cámara vuelve
            if (deathQueue.Count == 0)
            {
                deathCamera.Priority = 0;
                yield return new WaitForSeconds(blendInTime);
            }
        }

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;
        processQueueCoroutine = null;

        OnCinematicFinished?.Invoke(this, EventArgs.Empty);
    }

    // ─── Slow Motion ─────────────────────────────────────────────────────────

    private IEnumerator SlowMotionRoutine()
    {
        float elapsed = 0f;

        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        while (elapsed < slowMotionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(slowMotionScale, 1f, elapsed / slowMotionDuration);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        slowMotionCoroutine = null;
    }

    // ─── Posicionamiento de cámara ───────────────────────────────────────────

    private void CalculateCameraAnchorPosition(Transform climberTransform,
        float? distanceOverride = null,
        float? heightOverride = null,
        float? sideOverride = null)
    {
        float distance = distanceOverride ?? cameraDistance;
        float height = heightOverride ?? heightOffset;
        float side = sideOverride ?? sideOffset;

        Vector3 headPosition = climberTransform.position + Vector3.up * 1.5f;
        Vector3 baseOrigin = climberTransform.position + Vector3.up * 0.5f;

        Vector3 outwardsDirection = Vector3.back;
        if (Physics.Raycast(baseOrigin, Vector3.down, out RaycastHit groundHit, 5f, mountainLayer))
            outwardsDirection = groundHit.normal.normalized;

        Vector3[] sideDirections = new Vector3[]
        {
            climberTransform.right,
            -climberTransform.right,
            Vector3.zero
        };

        Vector3 finalSafePos = climberTransform.position;

        foreach (Vector3 sideDir in sideDirections)
        {
            Vector3 targetPos = climberTransform.position
                              + outwardsDirection * distance
                              + sideDir * side
                              + Vector3.up * height;

            Vector3 dir = targetPos - headPosition;
            float dist = dir.magnitude;

            if (!Physics.SphereCast(headPosition, 0.5f, dir.normalized, out RaycastHit wallHit, dist, mountainLayer))
            {
                finalSafePos = targetPos;
                break;
            }
            else
            {
                finalSafePos = wallHit.point + wallHit.normal * 0.5f;
            }
        }

        cameraAnchor.position = finalSafePos;
    }

    // ─── Rotación del visual de muerte ───────────────────────────────────────

    private Quaternion GetDeathVisualRotation(Transform climberTransform)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(climberTransform.forward, Vector3.up).normalized;

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;

        Quaternion lookRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Quaternion offsetRotation = Quaternion.Euler(deathVisualRotationOffset);

        return lookRotation * offsetRotation;
    }

    // ─── Setters ─────────────────────────────────────────────────────────────

    public void IsPlayingCinematic()
    {
        useDeathCinematics = !useDeathCinematics;
    }
}