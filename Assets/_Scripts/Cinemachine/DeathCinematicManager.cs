using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine; // Importante para Cinemachine 3
using UnityEngine;
using UnityEngine.AI;

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
        // 1. Pausamos la lógica del escalador para que no se mueva (si sigue vivo por físicas)
        if (deathInfo.climber != null)
        {
            deathInfo.climber.SetExternalSpeedMultiplier(0f);
            var agent = deathInfo.climber.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }

        // 2. Metemos la muerte en la cola
        deathQueue.Enqueue(deathInfo);

        // 3. Si no estamos ya reproduciendo una cinemática, la iniciamos
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

            // --- 3. REPRODUCIR EFECTOS ---
            DeathEffectConfigSO config = GameManager.Instance.GetDeathEffectConfig(currentDeath.cause);
            float animDuration = 1.0f; // Duración por defecto si no hay configuración

            if (config != null)
            {
                // Reproducir Sonido
                if (config.deathAudioClip != null)
                {
                    Temporal_Sound_Music.Instance.PlaySound(config.deathAudioClip, 1f);
                }

                // Reproducir Animación
                if (config.deathAnimationClip != null)
                {
                    Animator anim = currentDeath.climber.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        // Le decimos al Animator que reproduzca EXACTAMENTE el nombre del clip de tu Scriptable Object
                        anim.Play(config.deathAnimationClip.name);

                        // Guardamos lo que dura para que la cámara espere
                        animDuration = config.deathAnimationClip.length;
                    }
                }
            }

            // Esperamos lo que dure la animación + el tiempo de gracia
            yield return new WaitForSeconds(animDuration + delayAfterAnim);

            // --- 4. RESOLUCIÓN ---
            // AHORA SÍ, destruimos al escalador
            Destroy(currentDeath.climber.gameObject);

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
    private void CalculateCameraAnchorPosition(Transform climberTransform)
    {
        Vector3 origin = climberTransform.position + Vector3.up * 0.5f;
        Vector3 safePos;

        // Lanzamos un raycast hacia abajo para detectar la pendiente exacta
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, mountainLayer))
        {
            // 1. Hacia afuera de la montaña
            Vector3 outwardsDirection = hit.normal.normalized;

            // 2. Hacia un lado (usamos la derecha del escalador)
            Vector3 sideDirection = climberTransform.right;

            // 3. Calculamos la posición final sumando todo
            safePos = climberTransform.position
                      + (outwardsDirection * cameraDistance)
                      + (sideDirection * sideOffset); // <-- APLICAMOS EL LADO

            safePos.y += heightOffset;
        }
        else
        {
            // Fallback si el raycast falla
            safePos = climberTransform.position
                      + (Vector3.back * cameraDistance)
                      + (climberTransform.right * sideOffset)
                      + (Vector3.up * heightOffset);
        }

        cameraAnchor.position = safePos;
    }
}