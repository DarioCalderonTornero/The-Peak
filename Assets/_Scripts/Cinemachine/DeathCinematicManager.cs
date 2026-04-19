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

    [Header("Ajustes de Control")]
    [SerializeField] private bool useDeathCinematics = true;

    private Coroutine processQueueCoroutine;
    private GameManager.DeathInfo currentDeathInfo;

    private Queue<GameManager.DeathInfo> deathQueue = new Queue<GameManager.DeathInfo>();
    private bool isPlayingCinematic = false;

    private Transform cameraAnchor;

    // Flag que se activa cuando DeathAnimation termina
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

        InputManager.Instance.OnSkipCinematic += InputManager_OnSkipCinematic;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnClimberDead -= HandleClimberDeath;
    }

    private void InputManager_OnSkipCinematic(object sender, System.EventArgs e)
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

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        animationComplete = true; // desbloquea cualquier WaitUntil pendiente

        if (currentDeathInfo.climber != null)
            Destroy(currentDeathInfo.climber.gameObject);

        while (deathQueue.Count > 0)
        {
            var pending = deathQueue.Dequeue();
            if (pending.climber != null) Destroy(pending.climber.gameObject);
        }

        if (CameraShake.Instance != null) CameraShake.Instance.StopShake();
        deathCamera.Priority = 0;
        if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;

        Debug.Log("[DeathCinematicManager] Cinemática saltada por el jugador.");
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
            if (currentDeathInfo.climber == null) continue;

            // 1. Posicionar cámara y volar hacia el escalador
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
                if (deathQueue.Count <= 1) deathCamera.Priority = 0;
                continue;
            }

            if (!barsAreShown)
            {
                if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                barsAreShown = true;
            }

            yield return new WaitForSeconds(waitAfterBars + delayAfterBlendCameras);

            // 2. Slow motion + camera shake al llegar
            if (CameraShake.Instance != null)
                CameraShake.Instance.ShakeDeathCamera(3f, 10f, 0.2f);

            yield return StartCoroutine(SlowMotionRoutine());

            // 3. Destruir escalador original e instanciar prefab visual
            DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeathInfo.cause);

            Vector3 deathPos = currentDeathInfo.climber.transform.position;
            Quaternion deathRot = currentDeathInfo.climber.transform.rotation;

            var loadout = currentDeathInfo.climber.GetComponent<ClimberLoadout>();
            Color helmetColor = loadout != null ? loadout.GetHelmetColor() : Color.white;

            Destroy(currentDeathInfo.climber.gameObject);

            // 4. Instanciar visual y esperar a que la animación termine
            animationComplete = false;

            if (config != null && config.deathVisualPrefab != null)
            {
                var visual = Instantiate(config.deathVisualPrefab, deathPos, deathRot);
                var deathAnim = visual.GetComponent<DeathAnimation>();

                if (deathAnim != null)
                {
                    // Suscribirse al evento de fin de animación
                    deathAnim.OnAnimationComplete += () => animationComplete = true;
                    deathAnim.PlayAnimation(currentDeathInfo.cause, helmetColor);
                }
                else
                {
                    // Si no hay DeathAnimation, continuamos igualmente
                    animationComplete = true;
                }
            }
            else
            {
                animationComplete = true;
            }

            // Esperamos a que la animación de código termine
            yield return new WaitUntil(() => animationComplete);

            // 5. Al terminar la animación: audio + VFX simultáneos
            // (la explosión la gestiona DeathClimberExplosion vía OnAnimationComplete)
            if (config != null)
            {
                if (config.deathAudioClip != null)
                    Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);

                if (config.deathVFX_Effect != null)
                    Instantiate(config.deathVFX_Effect, deathPos, deathRot);
            }

            // 6. Cámara se queda mirando el punto de muerte
            yield return new WaitForSeconds(delayAfterExplosion);

            // 7. Cámara vuelve
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
    }

    // ─── Posicionamiento de cámara ───────────────────────────────────────────

    private void CalculateCameraAnchorPosition(Transform climberTransform)
    {
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
                              + outwardsDirection * cameraDistance
                              + sideDir * sideOffset
                              + Vector3.up * heightOffset;

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

    // ─── Setters ─────────────────────────────────────────────────────────────

    public void IsPlayingCinematic() => useDeathCinematics = !useDeathCinematics;
}
