using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine; // Importante para Cinemachine 3
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.Rendering.STP;

public class DeathCinematicManager : MonoBehaviour
{
    public static DeathCinematicManager Instance { get; private set; }

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
    [SerializeField] private float blendInTime = 1.0f;
    [Tooltip("Tiempo de gracia extra después de la animación de muerte antes de volver.")]
    [SerializeField] private float delayAfterAnim = 1.0f;

    [Header("Ajustes de Control")]
    [Tooltip("Si se desactiva, no habrá cinemática y el escalador morirá instantáneamente.")]
    [SerializeField] private bool useDeathCinematics = true;

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
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnClimberDead -= HandleClimberDeath;
        }
    }

    private void HandleClimberDeath(GameManager.DeathInfo deathInfo)
    {
        if (!useDeathCinematics)
        {
            //DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeath.cause);
            if (deathInfo.climber != null)
            {
                //Temporal_Sound_Music.Instance.Play2DSound(config.deathAudioClip, 1f);
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
            StartCoroutine(ProcessDeathQueue());
        }
    }

    private IEnumerator ProcessDeathQueue()
    {
        isPlayingCinematic = true;

        // Cambiamos el estado del juego para bloquear input y pausar turnos (Paso 3)
        GameManager.Instance.SetState(GameManager.GameState.Cinematic);

        while (deathQueue.Count > 0)
        {
            GameManager.DeathInfo currentDeath = deathQueue.Dequeue();

            // Si el escalador ya fue destruido por un error o bug externo, lo saltamos
            if (currentDeath.climber == null) continue;

            // --- 1. CÁLCULO DE POSICIÓN DINÁMICA ---
            CalculateCameraAnchorPosition(currentDeath.climber.transform);

            // --- 2. ENCUADRE DE CÁMARA ---
            deathCamera.Follow = cameraAnchor;
            deathCamera.LookAt = currentDeath.climber.transform;

            // Subimos la prioridad para que Cinemachine haga el Blend hacia aquí
            deathCamera.Priority = 100;

            // Esperamos a que la cámara llegue (Blend Time)
            yield return new WaitForSeconds(blendInTime);

            if (currentDeath.climber == null)
            {
                Debug.LogWarning("[DeathCinematicManager] ¡Un script externo ha destruido al escalador antes de tiempo! Ignorando efectos...");

                if (deathQueue.Count <= 1) deathCamera.Priority = 0;
                continue; // Saltamos a la siguiente muerte para que no crashee
            }

            // --- 3. REPRODUCIR EFECTOS ---
            DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeath.cause);
            float animDuration = 1.0f; 
            float audioDuration = 0f;  

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
                    Animator anim = currentDeath.climber.GetComponentInChildren<Animator>();
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
            Destroy(currentDeath.climber.gameObject);
            Debug.Log($"[DeathCinematicManager] Escalador {currentDeath.climber.name} destruido después de la cinemática.");

            // Bajamos la prioridad de la cámara para que vuelva a la vista general si es la última muerte
            // Si hay más en la cola, simplemente saltará a la siguiente
            if (deathQueue.Count == 0)
            {
                deathCamera.Priority = 0;
                // Esperamos a que la cámara vuelva antes de devolver el control
                yield return new WaitForSeconds(blendInTime);
            }
        }

        // Devolvemos el control al jugador
        GameManager.Instance.SetState(GameManager.GameState.Playing);
        isPlayingCinematic = false;
    }

    /// <summary>
    /// Calcula una posición segura para la cámara basada en la normal de la montaña.
    /// </summary>
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

        // 3. ¡LA MAGIA! Creamos una lista de direcciones a probar (Derecha -> Izquierda -> Centro)
        Vector3[] sideDirectionsToTry = new Vector3[]
        {
            climberTransform.right,  // Plan A: A la derecha
            -climberTransform.right, // Plan B: A la izquierda
            Vector3.zero             // Plan C: Justo enfrente (sin desplazamiento lateral)
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

            // Lanzamos una "esfera gorda" (SphereCast) para ver si el camino está libre de rocas
            // Usamos radio 0.5f para simular el volumen físico de la cámara
            if (!Physics.SphereCast(headPosition, 0.5f, directionToTarget.normalized, out RaycastHit wallHit, distanceToTarget, mountainLayer))
            {
                // ¡Vía libre! No hemos chocado con nada. Este sitio es perfecto.
                finalSafePos = targetPos;
                break; // Salimos del bucle, ya hemos encontrado nuestro sitio
            }
            else
            {
                // Hemos chocado con una pared. 
                // Guardamos una posición de "emergencia" pegada a la pared (por si todos los lados fallan)
                finalSafePos = wallHit.point + (wallHit.normal * 0.5f);
            }
        }

        // 5. Asignamos la posición ganadora al ancla
        cameraAnchor.position = finalSafePos;
    }
}