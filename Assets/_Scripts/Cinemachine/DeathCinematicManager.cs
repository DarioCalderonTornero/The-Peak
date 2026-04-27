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

    // FIX Bug 5: guardamos la referencia directa en vez de compararla
    // a través de deathCamera.LookAt, que puede devolver resultados
    // incorrectos si el objeto fue destruido entre frames.
    private Transform spectacleCinematicTarget = null;

    private Transform cameraAnchor;
    private bool animationComplete = false;
    private bool skipRequested = false;

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
        if (isPlayingCinematic) skipRequested = true;
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

    /// <summary>
    /// FIX Bug 2: llamado por la trampa cuando su escalador desapareció
    /// antes de que pudiera usar el slot. Evita el deadlock permanente.
    /// </summary>
    public void ForceReleaseSpectacleSlot()
    {
        spectacleInProgress = false;
        spectacleCinematicTarget = null;
        TryStartNextSpectacle();

        // Si no hay nada más en cola y no hay cinemática activa, restauramos
        if (!isPlayingCinematic && !spectacleInProgress)
        {
            if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();
            UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
            GameManager.Instance.SetState(GameManager.GameState.Playing);
            OnCinematicFinished?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// La trampa llama a esto al principio de su OnSpectacleSlotGranted.
    /// Mueve la cámara al escalador e inicia el estado cinemático.
    /// </summary>
    public void BeginSpectacleCinematic(ClimberMovement climber)
    {
        if (climber == null) return;

        CalculateCameraAnchorPosition(climber.transform,
            geyserCameraDistance, geyserHeightOffset, geyserSideOffset);

        deathCamera.Follow = cameraAnchor;
        deathCamera.LookAt = climber.transform;
        deathCamera.Priority = 100;

        // FIX Bug 5: guardamos referencia directa al transform
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

            skipRequested = false;

            bool isSpectacular = currentDeathInfo.cause == DeathCause.Geyser
                               || currentDeathInfo.cause == DeathCause.StormyCloud;

            // FIX Bug 5: comparamos contra nuestra referencia guardada,
            // no contra deathCamera.LookAt
            bool cameraAlreadySet = spectacleCinematicTarget != null
                                 && currentDeathInfo.climber != null
                                 && spectacleCinematicTarget == currentDeathInfo.climber.transform;

            // La referencia ya fue consumida, limpiamos
            spectacleCinematicTarget = null;

            if (!cameraAlreadySet)
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
                    float elapsed = 0f;
                    while (elapsed < blendInTime && !skipRequested)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

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
                    if (!barsAreShown)
                    {
                        if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                        barsAreShown = true;
                    }
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

            if (skipRequested)
            {
                HandleSkipCleanup(isSpectacular);
                continue;
            }

            // ── Shake + VFX + animación ──────────────────────────────────────
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

            while (!animationComplete && !skipRequested)
                yield return null;

            if (skipRequested)
            {
                HandleSkipCleanup(isSpectacular);
                continue;
            }

            float waitElapsed = 0f;
            while (waitElapsed < delayAfterExplosion && !skipRequested)
            {
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);

            if (isSpectacular)
                spectacleInProgress = false;

            if (skipRequested)
            {
                skipRequested = false;
                continue;
            }

            if (deathQueue.Count == 0)
            {
                // FIX Bug 1: siempre reseteamos Priority al salir del último elemento
                deathCamera.Priority = 0;
                float blendElapsed = 0f;
                while (blendElapsed < blendInTime && !skipRequested)
                {
                    blendElapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // ── Fin del bucle ────────────────────────────────────────────────────
        isPlayingCinematic = false;
        TryStartNextSpectacle();

        if (!spectacleInProgress)
        {
            if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();
            UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
            GameManager.Instance.SetState(GameManager.GameState.Playing);
            OnCinematicFinished?.Invoke(this, EventArgs.Empty);
        }

        processQueueCoroutine = null;
    }

    // ─── Skip de la muerte actual ─────────────────────────────────────────────

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

        // FIX Bug 3: destruimos el escalador si todavía existe
        // (puede estar vivo si el skip ocurrió durante el blend-in)
        if (currentDeathInfo.climber != null)
            Destroy(currentDeathInfo.climber.gameObject);

        if (CameraShake.Instance != null) CameraShake.Instance.StopShake();

        // FIX Bug 1: reseteamos la prioridad de la cámara siempre en skip
        deathCamera.Priority = 0;
        spectacleCinematicTarget = null;

        OnOwnClimberCinematicFinished?.Invoke(currentDeathInfo);

        if (isSpectacular)
            spectacleInProgress = false;

        skipRequested = false;

        Debug.Log("[DeathCinematicManager] Muerte saltada. Pasando a la siguiente.");
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