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
    [Tooltip("La cámara virtual dedicada a las muertes.")]
    [SerializeField] private CinemachineCamera deathCamera;

    [Header("Configuración de Posicionamiento")]
    [Tooltip("Capa de la montaña para detectar la normal del suelo.")]
    [SerializeField] private LayerMask mountainLayer;
    [Tooltip("Distancia a la que se colocará la cámara desde la cara del escalador.")]
    [SerializeField] private float cameraDistance = 3f;
    [Tooltip("Altura extra para que la cámara mire un poco desde arriba.")]
    [SerializeField] private float heightOffset = 1.5f;
    [Tooltip("Desplazamiento lateral. Positivo = Derecha, Negativo = Izquierda.")]
    [SerializeField] private float sideOffset = 3f;

    [Header("Tiempos Cinemáticos")]
    [Tooltip("Tiempo de espera para que la cámara llegue al escalador antes de ejecutar la muerte.")]
    [SerializeField] private float blendInTime = 2.0f;
    [Tooltip("Tiempo de gracia extra después de la animación de muerte antes de volver.")]
    [SerializeField] private float delayAfterAnim = 0.5f;
    [Tooltip("Tiempo de espera desde que la cámara llega a la muerte hasta que salen las bandas negras")]
    [SerializeField] private float delayAfterBlendCameras = 0.0f;

    [Header("Ajustes de Control")]
    [Tooltip("Si se desactiva, no habrá cinemática y el escalador morirá instantáneamente.")]
    [SerializeField] private bool useDeathCinematics = true;

    [Header("Opciones de Salto")]
    [Tooltip("Tecla para saltar la cinemática de muerte.")]

    private Coroutine processQueueCoroutine;
    private GameManager.DeathInfo currentDeathInfo;

    // La cola de muertes pendientes
    private Queue<GameManager.DeathInfo> deathQueue = new Queue<GameManager.DeathInfo>();
    private bool isPlayingCinematic = false;

    // Objeto invisible que usaremos para posicionar la cámara
    private Transform cameraAnchor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Crear el ancla dinámico en tiempo de ejecución (para no ensuciar la escena)
        cameraAnchor = new GameObject("DeathCameraAnchor").transform;
        cameraAnchor.SetParent(transform);

        // Asegurarnos de que la cámara de muerte empieza apagada (prioridad 0)
        if (deathCamera != null) deathCamera.Priority = 0;
    }

    private void Start()
    {
        // Nos suscribimos al evento de muerte del GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnClimberDead += HandleClimberDeath;
        }
        else
        {
            Debug.LogWarning("[DeathCinematicManager] No se encontró el GameManager en la escena.");
        }

        InputManager.Instance.OnSkipCinematic += InputManager_OnSkipCinematic;
    }

    private void InputManager_OnSkipCinematic(object sender, System.EventArgs e)
    {
        if (isPlayingCinematic)
        {
            SkipCinematic();
        }   
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnClimberDead -= HandleClimberDeath;
        }
    }


    private void SkipCinematic()
    {
        // 1. Abortamos la corrutina de los tiempos inmediatamente
        if (processQueueCoroutine != null)
        {
            StopCoroutine(processQueueCoroutine);
            processQueueCoroutine = null;
        }

        // 2. Destruimos al escalador actual que estaba en pantalla. 
        // CORRECCIÓN: Al ser un struct, solo comprobamos si el escalador es nulo.
        if (currentDeathInfo.climber != null)
        {
            Destroy(currentDeathInfo.climber.gameObject);
        }

        // 3. Vaciamos la cola por si murieron varios a la vez
        // y los destruimos al instante para no tener que ver sus cinemáticas tampoco.
        while (deathQueue.Count > 0)
        {
            var pendingDeath = deathQueue.Dequeue();
            if (pendingDeath.climber != null) Destroy(pendingDeath.climber.gameObject);
        }

        // 4. Detenemos temblores de cámara si los hubiera
        if (CameraShake.Instance != null) CameraShake.Instance.StopShake();

        // 5. Devolvemos la cámara principal devolviendo la prioridad de la cámara de muerte a 0
        deathCamera.Priority = 0;

        // 6. Quitamos las bandas negras inmediatamente
        if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();

        // 7. Encendemos el UI principal del juego
        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);

        // 8. Devolvemos el control al jugador
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;

        Debug.Log("[DeathCinematicManager] Cinemática saltada por el jugador.");

        OnCinematicFinished?.Invoke(this, EventArgs.Empty);

    }

    private void HandleClimberDeath(GameManager.DeathInfo deathInfo)
    {
        if (!useDeathCinematics)
        {
            if (deathInfo.climber != null)
            {
                Destroy(deathInfo.climber.gameObject);
            }
            return;
        }

        // --- Si el sistema está activado, procedemos como antes ---
        if (deathInfo.climber != null)
        {
            deathInfo.climber.SetExternalSpeedMultiplier(0f);
            var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        deathQueue.Enqueue(deathInfo);

        if (!isPlayingCinematic)
        {
            processQueueCoroutine = StartCoroutine(ProcessDeathQueue());
        }
    }

    private IEnumerator ProcessDeathQueue()
    {
        isPlayingCinematic = true;
        GameManager.Instance.SetState(GameManager.GameState.Cinematic);

        // 1. Apagamos la UI de golpe para limpiar la pantalla durante el vuelo
        UIManager.Instance.HideAll();

        bool barsAreShown = false;

        while (deathQueue.Count > 0)
        {
            currentDeathInfo = deathQueue.Dequeue();
            if (currentDeathInfo.climber == null) continue;

            CalculateCameraAnchorPosition(currentDeathInfo.climber.transform);

            deathCamera.Follow = cameraAnchor;
            deathCamera.LookAt = currentDeathInfo.climber.transform;
            deathCamera.Priority = 100;

            // --- EL VUELO DE LA CÁMARA (Sincronizado con las bandas) ---

            float barsAnimationTime = 0.0f; 
            float waitBeforeBars = Mathf.Max(0f, blendInTime - barsAnimationTime);
            float waitAfterBars = blendInTime - waitBeforeBars;

            // 1. Esperamos la mayor parte del viaje de la cámara
            yield return new WaitForSeconds(waitBeforeBars);

            if (currentDeathInfo.climber == null)
            {
                Debug.LogWarning("[DeathCinematicManager] ¡Un script externo ha destruido al escalador antes de tiempo!");
                if (deathQueue.Count <= 1) deathCamera.Priority = 0;
                continue;
            }

            // 2. ¡AHORA SACAMOS LAS BANDAS! (Mientras la cámara da sus últimos metros de vuelo)
            if (!barsAreShown)
            {
                if (CinematicBars.Instance != null) CinematicBars.Instance.ShowBars();
                barsAreShown = true;
            }

            // 3. Esperamos el tiempo que le queda a la cámara para llegar a su destino
            yield return new WaitForSeconds(waitAfterBars + delayAfterBlendCameras);

            // --- 3. REPRODUCIR EFECTOS ---
            DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeathInfo.cause);
            float animDuration = 1.0f;
            float audioDuration = 0f;

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ShakeDeathCamera(3f, 10f, 0.2f);
            }

            if (config != null)
            {
                if (config.deathAudioClip != null)
                {
                    audioDuration = config.deathAudioClip.length;
                    Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);
                }

                // Reproducir Animación
                if (config.deathAnimationClip != null)
                {
                    Animator anim = currentDeathInfo.climber.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.Play(config.deathAnimationClip.name);
                        animDuration = config.deathAnimationClip.length;
                    }
                }
            }

            // --- NUEVO: ESPERAR DE FORMA INTELIGENTE ---
            float timeToWait = Mathf.Max(animDuration, audioDuration);

            // Esperamos ese tiempo + el tiempo de gracia
            yield return new WaitForSeconds(timeToWait + delayAfterAnim);

            // --- 4. RESOLUCIÓN ---

            if (config != null && config.climberExplodeGameObject != null)
            {
                Instantiate(config.climberExplodeGameObject, currentDeathInfo.climber.transform.position, currentDeathInfo.climber.transform.rotation);
            }
                Destroy(currentDeathInfo.climber.gameObject);
            Debug.Log($"[DeathCinematicManager] Escalador {currentDeathInfo.climber.name} destruido después de la cinemática.");

            // Bajamos la prioridad de la cámara para que vuelva a la vista general si es la última muerte
            if (deathQueue.Count == 0)
            {
                deathCamera.Priority = 0;
                // Esperamos a que la cámara vuelva antes de devolver el control
                yield return new WaitForSeconds(blendInTime);
            }
        }

        if (CinematicBars.Instance != null) CinematicBars.Instance.HideBars();

        UIManager.Instance.ShowMultiple(UICanvasType.Alex, UICanvasType.Dario);

        // Devolvemos el control al jugador
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;

        OnCinematicFinished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Calcula una posición segura para la cámara evaluando colisiones laterales.
    /// </summary>
    private void CalculateCameraAnchorPosition(Transform climberTransform)
    {
        // 1. Definimos desde dónde mira la cámara (la cabeza/pecho del escalador)
        Vector3 headPosition = climberTransform.position + (Vector3.up * 1.5f);
        Vector3 baseOrigin = climberTransform.position + (Vector3.up * 0.5f);

        // 2. Obtenemos la normal del suelo para saber hacia dónde es "afuera" de la montaña
        Vector3 outwardsDirection = Vector3.back; // Por defecto hacia atrás
        if (Physics.Raycast(baseOrigin, Vector3.down, out RaycastHit groundHit, 5f, mountainLayer))
        {
            outwardsDirection = groundHit.normal.normalized;
        }

        Vector3[] sideDirectionsToTry = new Vector3[]
        {
            climberTransform.right,
            -climberTransform.right,
            Vector3.zero
        };

        Vector3 finalSafePos = climberTransform.position;

        // 4. Probamos cada dirección una por una
        foreach (Vector3 sideDir in sideDirectionsToTry)
        {
            // Calculamos el punto teórico donde nos gustaría poner la cámara
            Vector3 targetPos = climberTransform.position
                              + (outwardsDirection * cameraDistance)
                              + (sideDir * sideOffset)
                              + (Vector3.up * heightOffset);

            // Vector y distancia desde la cabeza hasta ese punto teórico
            Vector3 directionToTarget = targetPos - headPosition;
            float distanceToTarget = directionToTarget.magnitude;

            if (!Physics.SphereCast(headPosition, 0.5f, directionToTarget.normalized, out RaycastHit wallHit, distanceToTarget, mountainLayer))
            {
                finalSafePos = targetPos;
                break;
            }
            else
            {
                // Hemos chocado con una pared. 
                finalSafePos = wallHit.point + (wallHit.normal * 0.5f);
            }
        }

        // 5. Asignamos la posición ganadora al ancla
        cameraAnchor.position = finalSafePos;
    }

    //SETTERS
    public void IsPlayingCinematic() => useDeathCinematics = !useDeathCinematics; 
}