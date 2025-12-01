using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class QuicksandDefense : BaseDefense
{
    [Header("Absorción")]
    [SerializeField] private float sinkSpeed = 1.2f;        // Velocidad a la que baja el escalador
    [SerializeField] private float maxSinkDepth = 1.5f;     // Cuánto debe hundirse para morir

    [Header("Centrado del escalador")]
    [SerializeField] private float centerLerpSpeed = 6f;    // Qué rápido se desplaza al centro (XZ)

    [Header("Animación de aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    private Vector3 originalLocalScale;
    private Coroutine spawnRoutine;

    // Sólo un escalador a la vez
    private ClimberMovement absorbedClimber = null;
    private NavMeshAgent absorbedAgent = null;

    private float initialY = 0f;
    private bool hasRecordedInitialY = false;

    private Collider triggerCol;

    private void Awake()
    {
        // Guardamos escala original del prefab
        originalLocalScale = transform.localScale;

        // Aseguramos trigger
        triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
            triggerCol.isTrigger = true;
    }

    public override void Initialize()
    {
        base.Initialize();

        // Animación de aparecer SOLO en la instancia colocada,
        // no en el preview (el preview no llama a Initialize).
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    private IEnumerator SpawnFromGround()
    {
        // Arranca desde escala 0
        transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            // smoothstep
            float eased = t * t * (3f - 2f * t);
            transform.localScale = originalLocalScale * eased;

            yield return null;
        }

        transform.localScale = originalLocalScale;
        spawnRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // CASO 1: aún no hay nadie atrapado → este pasa a ser el atrapado
        if (absorbedClimber == null)
        {
            absorbedClimber = climber;
            hasRecordedInitialY = false; // lo pillamos en Update la primera vez

            // Desactivar el NavMeshAgent para que no intente moverse
            absorbedAgent = climber.GetComponent<NavMeshAgent>();
            if (absorbedAgent != null)
            {
                absorbedAgent.enabled = false;
            }

            return;
        }

        // CASO 2: ya hay alguien atrapado y entra OTRO escalador → lo rescata
        if (climber != absorbedClimber)
        {
            RescueClimber();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // No dejamos que escape una vez “pillado” de forma normal:
        // sólo se puede salir mediante rescate o muerte.
    }

    private void Update()
    {
        if (absorbedClimber == null)
            return;

        // Si el escalador ha sido destruido por otra cosa
        if (absorbedClimber.gameObject == null)
        {
            absorbedClimber = null;
            absorbedAgent = null;
            return;
        }

        Transform t = absorbedClimber.transform;
        Vector3 pos = t.position;

        // 1) Moverlo SUAVEMENTE al centro de las arenas (solo en XZ)
        Vector3 targetXZ = new Vector3(transform.position.x, pos.y, transform.position.z);
        pos = Vector3.Lerp(pos, targetXZ, Time.deltaTime * centerLerpSpeed);
        t.position = pos;

        // Registrar la altura inicial UNA sola vez,
        // cuando ya está empezando a quedar “anclado”.
        if (!hasRecordedInitialY)
        {
            initialY = t.position.y;
            hasRecordedInitialY = true;
        }

        // 2) Solo hundimos durante el turno de escaladores
        if (TurnManager.Instance == null || !TurnManager.Instance.IsClimberTurn())
            return;

        AbsorbStep();
    }

    private void AbsorbStep()
    {
        if (absorbedClimber == null)
            return;

        Transform t = absorbedClimber.transform;
        Vector3 pos = t.position;

        // Hundir verticalmente
        pos.y -= sinkSpeed * Time.deltaTime;
        t.position = pos;

        float sunkAmount = initialY - pos.y;

        if (sunkAmount >= maxSinkDepth)
        {
            KillClimber();
        }
    }

    private void KillClimber()
    {
        if (absorbedClimber != null)
        {
            Destroy(absorbedClimber.gameObject);
        }

        // Las arenas desaparecen tras “comerse” al escalador
        Destroy(gameObject);
    }

    /// <summary>
    /// Un segundo escalador entra en el trigger y rescata al que se hundía.
    /// El atrapado sale de las arenas y las arenas desaparecen.
    /// </summary>
    private void RescueClimber()
    {
        if (absorbedClimber != null)
        {
            // Recolocamos al escalador en un punto seguro, justo encima del centro de las arenas
            Transform t = absorbedClimber.transform;
            Vector3 safePos = transform.position + Vector3.up * 0.2f;
            t.position = safePos;

            // Reactivamos su NavMeshAgent
            if (absorbedAgent == null)
                absorbedAgent = absorbedClimber.GetComponent<NavMeshAgent>();

            if (absorbedAgent != null)
            {
                absorbedAgent.enabled = true;
                absorbedAgent.isStopped = false;
            }

            // Por si luego queremos usar flags de inmovilización, aquí se podrían resetear.
        }

        // Las arenas desaparecen tras el rescate
        Destroy(gameObject);
    }
}
