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
    [SerializeField] private float blendInTime = 2.0f;
    [SerializeField] private float delayAfterExplosion = 1.5f;
    [SerializeField] private float delayAfterBlendCameras = 0.0f;

    [Header("Slow Motion")]
    [SerializeField] private float slowMotionScale = 0.2f;
    [SerializeField] private float slowMotionDuration = 0.5f;

    [Header("Rotación Visual de la Muerte")]
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

    // LinkedList en vez de Queue para poder insertar al frente
    private readonly LinkedList<GameManager.DeathInfo> deathQueue = new LinkedList<GameManager.DeathInfo>();
    private bool isPlayingCinematic = false;

    // Escalador concreto al que hemos pre-enfocado la cámara (Geyser/StormyCloud)
    // y que aún no ha llamado a NotifyReadyToProcess. null si no hay ninguno.
    private ClimberMovement preFocusedClimber = null;

    private Transform cameraAnchor;
    private bool animationComplete = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cameraAnchor = new GameObject("DeathCameraAnchor").transform;
        cameraAnchor.SetParent(transform);
        if (deathCamera != null) deathCamera.Priority = 0;
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
        if (isPlayingCinematic) SkipCinematic();
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
            var pending = deathQueue.First.Value;
            deathQueue.RemoveFirst();
            if (pending.climber != null)
                Destroy(pending.climber.gameObject);
        }

        if (CameraShake.Instance != null) CameraShake.Instance.StopShake();
        if (deathCamera != null) deathCamera.Priority = 0;
        if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;
        preFocusedClimber = null;

        Debug.Log("[DeathCinematicManager] Cinemática saltada por el jugador.");

        OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);
        OnCinematicFinished?.Invoke(this, EventArgs.Empty);
    }

    // ─── Handle ──────────────────────────────────────────────────────────────

    private void HandleClimberDeath(GameManager.DeathInfo deathInfo)
    {
        if (!useDeathCinematics)
        {
            if (deathInfo.climber != null) Destroy(deathInfo.climber.gameObject);
            return;
        }

        if (deathInfo.cause == DeathCause.Geyser || deathInfo.cause == DeathCause.StormyCloud)
        {
            if (deathInfo.climber != null)
            {
                if (deathInfo.cause == DeathCause.Geyser)
                {
                    deathInfo.climber.SetExternalSpeedMultiplier(0f);
                    var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
                    if (agent != null) agent.enabled = false;
                }
            }

            // Intentamos pre-enfocar. Si el sistema está ocupado, el pre-focus
            // se ignora silenciosamente y este escalador irá por la cola normal
            // cuando llegue su NotifyReadyToProcess.
            FocusCameraOnClimber(deathInfo.climber);
            return;
        }

        if (deathInfo.climber != null)
        {
            deathInfo.climber.SetExternalSpeedMultiplier(0f);
            var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        // Muerte normal: añadir al final de la cola
        deathQueue.AddLast(deathInfo);

        if (!isPlayingCinematic)
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
    }

    // ─── Foco de cámara para Geyser y StormyCloud ────────────────────────────

    public void FocusCameraOnClimber(ClimberMovement climber)
    {
        if (climber == null) return;

        // Solo pre-enfocamos si el sistema está completamente libre.
        // Si hay una cinemática reproduciéndose, muertes en cola, o ya hay
        // otro escalador pre-enfocado, este escalador esperará su turno en
        // la cola cuando llegue su NotifyReadyToProcess.
        if (isPlayingCinematic || deathQueue.Count > 0 || preFocusedClimber != null)
        {
            Debug.Log($"[DeathCinematicManager] Pre-focus de {climber.name} ignorado: sistema ocupado. Irá por cola normal.");
            return;
        }

        CalculateCameraAnchorPosition(climber.transform,
            geyserCameraDistance, geyserHeightOffset, geyserSideOffset);

        deathCamera.Follow = cameraAnchor;
        deathCamera.LookAt = climber.transform;
        deathCamera.Priority = 100;

        GameManager.Instance.SetState(GameManager.GameState.Cinematic);
        UIManager.Instance.HideAll();

        if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();

        isPlayingCinematic = true;
        preFocusedClimber = climber;
    }

    // ─── NotifyReadyToProcess ────────────────────────────────────────────────

    public void NotifyReadyToProcess(GameManager.DeathInfo deathInfo)
    {
        // ¿Este escalador concreto es el que tenía la cámara reservada?
        bool wasPreFocused = (deathInfo.climber != null
                              && deathInfo.climber == preFocusedClimber);

        if (wasPreFocused)
        {
            // La cámara ya está en posición para él → debe salir PRIMERO.
            deathQueue.AddFirst(deathInfo);
            preFocusedClimber = null;
        }
        else
        {
            // No tenía pre-focus (sistema estaba ocupado cuando él entró, o
            // perdió su pre-focus). Entra al FINAL como cualquier otra muerte,
            // respetando FIFO.
            deathQueue.AddLast(deathInfo);
        }

        if (processQueueCoroutine == null)
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
            currentDeathInfo = deathQueue.First.Value;
            deathQueue.RemoveFirst();

            if (currentDeathInfo.climber == null)
                continue;

            // ¿La cámara ya está siguiendo a este escalador? (venía de pre-focus)
            bool cameraAlreadyOnThisClimber =
                (deathCamera.LookAt == currentDeathInfo.climber.transform);

            if (!cameraAlreadyOnThisClimber)
            {
                // Preparar cámara desde cero, eligiendo parámetros según la causa
                bool isSpectacular = currentDeathInfo.cause == DeathCause.Geyser
                                  || currentDeathInfo.cause == DeathCause.StormyCloud;

                if (isSpectacular)
                {
                    CalculateCameraAnchorPosition(currentDeathInfo.climber.transform,
                        geyserCameraDistance, geyserHeightOffset, geyserSideOffset);
                }
                else
                {
                    CalculateCameraAnchorPosition(currentDeathInfo.climber.transform);
                }

                deathCamera.Follow = cameraAnchor;
                deathCamera.LookAt = currentDeathInfo.climber.transform;
                deathCamera.Priority = 100;

                float waitBeforeBars = Mathf.Max(0f, blendInTime);
                yield return new WaitForSeconds(waitBeforeBars);

                if (currentDeathInfo.climber == null)
                {
                    Debug.LogWarning("[DeathCinematicManager] Escalador destruido antes de tiempo.");
                    if (deathQueue.Count == 0) deathCamera.Priority = 0;
                    continue;
                }

                if (!barsAreShown)
                {
                    if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                    barsAreShown = true;
                }

                yield return new WaitForSeconds(delayAfterBlendCameras);
            }
            else
            {
                // Cámara ya en posición por pre-focus → solo re-asegurar LookAt
                deathCamera.LookAt = currentDeathInfo.climber.transform;

                if (!barsAreShown)
                {
                    if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                    barsAreShown = true;
                }
            }

            if (CameraShake.Instance != null)
                CameraShake.Instance.ShakeDeathCamera(3f, 10f, 0.2f);

            DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeathInfo.cause);

            Vector3 deathPos = currentDeathInfo.climber.transform.position;
            Quaternion deathRot = GetDeathVisualRotation(currentDeathInfo.climber.transform);

            var loadout = currentDeathInfo.climber.GetComponent<ClimberLoadout>();
            Color helmetColor = loadout != null ? loadout.GetHelmetColor() : Color.white;

            Destroy(currentDeathInfo.climber.gameObject);

            animationComplete = false;

            if (config != null && config.deathVisualPrefab != null)
            {
                GameObject visual = Instantiate(config.deathVisualPrefab, deathPos, deathRot);
                DeathAnimation deathAnim = visual.GetComponent<DeathAnimation>();

                if (deathAnim != null)
                {
                    void HandleDeathMoment()
                    {
                        if (slowMotionCoroutine != null) StopCoroutine(slowMotionCoroutine);
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
                    if (config.deathAudioClip != null)
                        Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);
                    if (config.deathVFX_Effect != null)
                        Instantiate(config.deathVFX_Effect, deathPos, deathRot);
                    animationComplete = true;
                }
            }
            else
            {
                if (slowMotionCoroutine != null) StopCoroutine(slowMotionCoroutine);
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
            yield return new WaitForSeconds(delayAfterExplosion);

            OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);

            if (deathQueue.Count == 0)
            {
                deathCamera.Priority = 0;
                yield return new WaitForSeconds(blendInTime);
            }
        }

        if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;
        preFocusedClimber = null;
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
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;

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