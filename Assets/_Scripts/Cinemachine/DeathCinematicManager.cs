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

    // ★ Entrada de la cola: guarda el DeathInfo y si está lista para procesar.
    // Geyser/StormyCloud se encolan como ready=false y se marcan true cuando
    // llaman a NotifyReadyToProcess. Las muertes normales entran ya ready=true.
    private class QueuedDeath
    {
        public GameManager.DeathInfo info;
        public bool ready;
    }

    private Coroutine processQueueCoroutine;
    private Coroutine slowMotionCoroutine;
    private GameManager.DeathInfo currentDeathInfo;

    // ★ La cola ahora guarda QueuedDeath, no DeathInfo directo.
    private readonly List<QueuedDeath> deathQueue = new List<QueuedDeath>();

    // ★ Dos flags separados en lugar de isPlayingCinematic a secas:
    //   - isProcessingQueue: la coroutine ProcessDeathQueue está corriendo
    //   - isCameraFocused:   hay una cámara de focus previa activa (Geyser/Cloud en fase previa)
    private bool isProcessingQueue = false;
    private bool isCameraFocused = false;

    private Transform cameraAnchor;
    private bool animationComplete = false;

    // ★ Propiedad pública para compatibilidad con otros scripts que pregunten.
    public bool IsCinematicActive => isProcessingQueue || isCameraFocused;

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
        if (IsCinematicActive)
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

        // ★ Vaciar la cola respetando la nueva estructura
        foreach (var q in deathQueue)
        {
            if (q.info.climber != null)
                Destroy(q.info.climber.gameObject);
        }
        deathQueue.Clear();

        if (CameraShake.Instance != null)
            CameraShake.Instance.StopShake();

        if (deathCamera != null)
            deathCamera.Priority = 0;

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);

        // ★ Reset de ambos flags
        isProcessingQueue = false;
        isCameraFocused = false;

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

        bool isFocusCause = deathInfo.cause == DeathCause.Geyser ||
                            deathInfo.cause == DeathCause.StormyCloud;

        if (deathInfo.climber != null)
        {
            // Geyser: detenemos al escalador, desactivamos agent (ya vuela por física).
            // StormyCloud: NO tocamos el agent, necesita caminar al centro.
            // Causa normal: paramos al escalador y desactivamos agent.
            if (deathInfo.cause == DeathCause.Geyser)
            {
                deathInfo.climber.SetExternalSpeedMultiplier(0f);
                var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
            }
            else if (!isFocusCause)
            {
                deathInfo.climber.SetExternalSpeedMultiplier(0f);
                var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
            }
        }

        // ★ Encolamos SIEMPRE, sin excepciones. Geyser/StormyCloud entran como not-ready.
        var queued = new QueuedDeath
        {
            info = deathInfo,
            ready = !isFocusCause
        };
        deathQueue.Add(queued);

        // ★ Si es Geyser/StormyCloud, activamos la cámara de focus previa.
        // Solo hay uno a la vez (confirmado), así que no hay conflicto de cámara.
        if (isFocusCause)
            FocusCameraOnClimber(deathInfo.climber);

        // ★ Arrancamos la coroutine de cola si no está corriendo
        if (!isProcessingQueue)
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

        isCameraFocused = true; // ★ flag separado
    }

    // ─── NotifyReadyToProcess ────────────────────────────────────────────────

    // ★ Ya NO encola. Solo busca la entrada correspondiente en la cola
    //    y la marca como ready. Así se respeta el orden cronológico.
    public void NotifyReadyToProcess(GameManager.DeathInfo deathInfo)
    {
        bool found = false;
        for (int i = 0; i < deathQueue.Count; i++)
        {
            // Comparamos por referencia al climber (más fiable que struct equality)
            if (deathQueue[i].info.climber == deathInfo.climber)
            {
                // Actualizamos la posición al punto final (Geyser la actualiza tras el vuelo)
                var q = deathQueue[i];
                q.info = deathInfo;
                q.ready = true;
                deathQueue[i] = q;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning("[DeathCinematicManager] NotifyReadyToProcess llamado para un climber que no está en cola. Encolando como fallback.");
            deathQueue.Add(new QueuedDeath { info = deathInfo, ready = true });
        }

        // ★ La cámara de focus deja de ser necesaria: la coroutine toma el control
        isCameraFocused = false;

        if (!isProcessingQueue)
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
    }

    // ─── Cola de muertes ─────────────────────────────────────────────────────

    private IEnumerator ProcessDeathQueue()
    {
        isProcessingQueue = true;
        GameManager.Instance.SetState(GameManager.GameState.Cinematic);
        UIManager.Instance.HideAll();

        bool barsAreShown = false;

        while (deathQueue.Count > 0)
        {
            // ★ Peek al frente. Si no está ready, esperamos a que NotifyReadyToProcess
            //    lo marque. No lo sacamos de la cola todavía.
            QueuedDeath front = deathQueue[0];

            if (!front.ready)
            {
                // Si el climber fue destruido antes de estar ready, lo descartamos
                if (front.info.climber == null)
                {
                    deathQueue.RemoveAt(0);
                    continue;
                }

                // Mientras no esté ready, cedemos control
                yield return null;
                continue;
            }

            // Ya está ready: lo sacamos y procesamos
            deathQueue.RemoveAt(0);
            currentDeathInfo = front.info;

            if (currentDeathInfo.climber == null)
                continue;

            bool isFocusCause = currentDeathInfo.cause == DeathCause.Geyser ||
                                currentDeathInfo.cause == DeathCause.StormyCloud;

            if (!isFocusCause)
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
                    if (deathQueue.Count == 0)
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
                // Geyser / StormyCloud: la cámara ya está en su sitio desde FocusCameraOnClimber.
                // Solo actualizamos el LookAt a la posición actual del climber.
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

            // 7. Si no queda nada ready en cola, bajamos prioridad de cámara.
            //    Si queda algo pero no está ready (p.ej. un Geyser en hold),
            //    mantenemos la cámara alta y esperamos en la próxima iteración del while.
            bool hasNextReady = deathQueue.Count > 0 && deathQueue[0].ready;
            if (!hasNextReady && deathQueue.Count == 0)
            {
                deathCamera.Priority = 0;
                yield return new WaitForSeconds(blendInTime);
            }
        }

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);

        isProcessingQueue = false;
        isCameraFocused = false;
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
