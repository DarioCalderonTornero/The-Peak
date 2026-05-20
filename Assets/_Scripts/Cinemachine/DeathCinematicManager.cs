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

    private readonly LinkedList<GameManager.DeathInfo> deathQueue = new LinkedList<GameManager.DeathInfo>();
    private readonly Queue<Action> spectacleQueue = new Queue<Action>();

    private bool isPlayingCinematic = false;
    private bool spectacleInProgress = false;

    private Transform spectacleCinematicTarget = null;

    private Transform cameraAnchor;
    private bool animationComplete = false;
    private bool skipRequested = false;
    public bool UseDeathCinematics => useDeathCinematics;

    // ─── Nuevo: controla si la cámara está activa para esta muerte ───────────
    private bool cameraActiveForCurrentDeath = true;

    public bool SkipRequested => skipRequested;

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
            Debug.LogWarning("[DeathCinematicManager] No se encontró el GameManager.");

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
            skipRequested = true;
    }

    // ─── Handle ──────────────────────────────────────────────────────────────

    private void HandleClimberDeath(GameManager.DeathInfo deathInfo)
    {
        // CAMBIO: Contar inmediatamente al entrar al sistema de cinemáticas normales
        if (deathInfo.climber != null && ClimberDeathPointsManager.Instance != null)
        {
            ClimberDeathPointsManager.Instance.AddClimberDeathPoints(deathInfo.climber);
        }

        if (!useDeathCinematics)
        {
            if (deathInfo.climber != null) Destroy(deathInfo.climber.gameObject);
            PointsManager.Instance?.AddPoints(5);
            return;
        }

        if (deathInfo.cause == DeathCause.Geyser || deathInfo.cause == DeathCause.StormyCloud)
            return;

        if (deathInfo.climber != null)
        {
            deathInfo.climber.SetExternalSpeedMultiplier(0f);
            var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        deathQueue.AddLast(deathInfo);

        if (!isPlayingCinematic)
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
    }

    // ─── API pública para Geyser y StormyCloud ───────────────────────────────

    public void RequestSpectacleSlot(Action onGreenLight)
    {
        spectacleQueue.Enqueue(onGreenLight);
        TryStartNextSpectacle();
    }

    public void ForceReleaseSpectacleSlot()
    {
        spectacleInProgress = false;
        spectacleCinematicTarget = null;
        TryStartNextSpectacle();

        if (!isPlayingCinematic && !spectacleInProgress)
        {
            if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();
            UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
            GameManager.Instance.SetState(GameManager.GameState.Playing);
            OnCinematicFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    public void BeginSpectacleCinematic(ClimberMovement climber)
    {
        if (climber == null) return;

        CalculateCameraAnchorPosition(climber.transform,
            geyserCameraDistance, geyserHeightOffset, geyserSideOffset);

        deathCamera.Follow = cameraAnchor;
        deathCamera.LookAt = climber.transform;
        deathCamera.Priority = 100;

        spectacleCinematicTarget = climber.transform;

        GameManager.Instance.SetState(GameManager.GameState.Cinematic);
        UIManager.Instance.HideAll();
        if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();

        isPlayingCinematic = true;
    }

    public void NotifyReadyToProcess(GameManager.DeathInfo deathInfo)
    {
        deathQueue.AddFirst(deathInfo);

        if (processQueueCoroutine == null)
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
    }

    public void ForceEndSpectacle(GameManager.DeathInfo deathInfo)
    {
        skipRequested = false;
        spectacleInProgress = false;
        spectacleCinematicTarget = null;
        isPlayingCinematic = false;

        deathCamera.Priority = 0;

        if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();
        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);

        OnOwnClimberCinematicFinished?.Invoke(deathInfo);
        OnCinematicFinished?.Invoke(this, EventArgs.Empty);

        TryStartNextSpectacle();
    }

    private void TryStartNextSpectacle()
    {
        if (spectacleInProgress) return;
        if (isPlayingCinematic) return;
        if (spectacleQueue.Count == 0) return;

        spectacleInProgress = true;
        var callback = spectacleQueue.Dequeue();
        callback?.Invoke();
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

            // Al empezar cada muerte, la cámara está activa por defecto
            cameraActiveForCurrentDeath = true;
            skipRequested = false;

            bool isSpectacular = currentDeathInfo.cause == DeathCause.Geyser
                               || currentDeathInfo.cause == DeathCause.StormyCloud;

            bool cameraAlreadySet = spectacleCinematicTarget != null
                                 && currentDeathInfo.climber != null
                                 && spectacleCinematicTarget == currentDeathInfo.climber.transform;

            spectacleCinematicTarget = null;

            // ─── Posicionamiento de cámara ────────────────────────────────
            if (!cameraAlreadySet && cameraActiveForCurrentDeath)
            {
                if (isSpectacular)
                    CalculateCameraAnchorPosition(currentDeathInfo.climber.transform,
                        geyserCameraDistance, geyserHeightOffset, geyserSideOffset);
                else
                    CalculateCameraAnchorPosition(currentDeathInfo.climber.transform);

                deathCamera.Follow = cameraAnchor;
                deathCamera.LookAt = currentDeathInfo.climber.transform;
                deathCamera.Priority = 100;

                if (!isSpectacular)
                {
                    // Blend in — salimos si se skipea
                    float elapsed = 0f;
                    while (elapsed < blendInTime && !skipRequested)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

                    if (skipRequested)
                    {
                        // Desactivar cámara pero continuar la muerte
                        DeactivateCameraKeepDeath();
                        // Si hay más muertes en cola, pasar a la siguiente
                        if (deathQueue.Count > 0)
                        {
                            FinishCurrentDeath();
                            continue;
                        }
                    }

                    if (currentDeathInfo.climber == null)
                    {
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
                    if (!barsAreShown)
                    {
                        if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                        barsAreShown = true;
                    }
                }
            }
            else if (!cameraAlreadySet)
            {
                if (!barsAreShown)
                {
                    if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                    barsAreShown = true;
                }
            }
            else
            {
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

            if (currentDeathInfo.cause == DeathCause.Snow)
                deathPos += Vector3.up * 0.5f;

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

            // Esperar animación — si se skipea, desactivar cámara pero dejar que termine
            while (!animationComplete)
            {
                if (skipRequested && cameraActiveForCurrentDeath)
                {
                    DeactivateCameraKeepDeath();

                    // Si hay más muertes, pasar a la siguiente sin esperar
                    if (deathQueue.Count > 0)
                    {
                        FinishCurrentDeath();
                        break;
                    }
                }
                yield return null;
            }

            if (!animationComplete)
                continue;

            // Delay post explosión — si se skipea, saltar directamente
            float waitElapsed = 0f;
            while (waitElapsed < delayAfterExplosion)
            {
                if (skipRequested && cameraActiveForCurrentDeath)
                {
                    DeactivateCameraKeepDeath();
                    if (deathQueue.Count > 0)
                    {
                        FinishCurrentDeath();
                        break;
                    }
                }
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            FinishCurrentDeath();

            if (isSpectacular)
                spectacleInProgress = false;

            // Si hay más muertes y la cámara ya fue desactivada,
            // la siguiente muerte arranca con cámara activa de nuevo
            if (deathQueue.Count > 0 && !cameraActiveForCurrentDeath)
                skipRequested = false;
        }

        // Cola vacía — limpiar todo
        isPlayingCinematic = false;
        TryStartNextSpectacle();

        if (!spectacleInProgress)
        {
            if (spectacleQueue.Count == 0)
                deathCamera.Priority = 0;

            // Solo ahora se ocultan las barras
            if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();
            UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
            GameManager.Instance.SetState(GameManager.GameState.Playing);
            OnCinematicFinished?.Invoke(this, EventArgs.Empty);
        }

        processQueueCoroutine = null;
    }

    // ─── Desactiva cámara pero deja la muerte ocurrir ────────────────────────

    private void DeactivateCameraKeepDeath()
    {
        cameraActiveForCurrentDeath = false;
        skipRequested = false;

        // Bajar prioridad → Cinemachine transiciona sola de vuelta
        deathCamera.Priority = 0;

        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        if (CameraShake.Instance != null)
            CameraShake.Instance.StopShake();
    }

    private void FinishCurrentDeath()
    {
        OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);
        skipRequested = false;
    }

    // ─── Skip ─────────────────────────────────────────────────────────────────

    private void HandleSkipCleanup(bool isSpectacular)
    {
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

        if (CameraShake.Instance != null) CameraShake.Instance.StopShake();

        if (spectacleQueue.Count == 0 && !spectacleInProgress)
            deathCamera.Priority = 0;

        spectacleCinematicTarget = null;

        OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);

        if (isSpectacular)
            spectacleInProgress = false;

        skipRequested = false;
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

    public bool IsProcessingDeaths()
    {
        return isPlayingCinematic || spectacleInProgress ||
               deathQueue.Count > 0 || spectacleQueue.Count > 0;
    }
}