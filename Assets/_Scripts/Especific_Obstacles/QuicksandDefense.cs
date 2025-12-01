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
        // 🔹 Pasamos por el sistema de equipamiento
        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmuneToMud = false;

        if (loadout != null)
        {
            // Lanza OnEncounterObstacle en todos los equipos
            loadout.TryHandleObstacle(ObstacleType.QuickSand);

            // Preguntamos si alguno puede manejar el lodo (tiene equipamiento anti-lodo)
            isImmuneToMud = loadout.CanHandleObstacle(ObstacleType.QuickSand);
        }

        // Si TIENE equipamiento que maneja el lodo → no aplicamos efecto
        if (isImmuneToMud)
        {
            // Opcional: debug
            // Debug.Log("[LodoDefense] Climber inmune al lodo.");
            return;
        }

        // 2) Si no tiene equipo (o no puede manejar QuickSand), aplicamos la lógica normal
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // Si aún no hay nadie absorbiéndose → empezamos con este
        if (absorbedClimber == null)
        {
            absorbedClimber = climber;
            hasRecordedInitialY = false;

            absorbedAgent = climber.GetComponent<NavMeshAgent>();
            if (absorbedAgent != null)
            {
                // No desactivamos el agent, solo lo “congelamos”
                absorbedAgent.isStopped = true;
                absorbedAgent.updatePosition = false;
                absorbedAgent.updateRotation = false;
            }

            // Este escalador se considera "done" a efectos de turno
            absorbedClimber.SetExternallyDoneThisTurn(true);
            return;
        }

        // Si ya había uno dentro y entra OTRO escalador → rescate
        if (climber != absorbedClimber)
        {
            RescueClimber();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // No dejamos que escape una vez “pillado”:
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

        // Registrar la altura inicial UNA sola vez
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

    private void RescueClimber()
    {
        if (absorbedClimber != null)
        {
            Transform t = absorbedClimber.transform;

            // 1) Lo sacamos un poco hacia arriba en el centro de la arena
            Vector3 safePos = transform.position + Vector3.up * 0.3f;
            t.position = safePos;

            // 2) Reactivamos el control normal del NavMeshAgent
            if (absorbedAgent == null)
                absorbedAgent = absorbedClimber.GetComponent<NavMeshAgent>();

            if (absorbedAgent != null)
            {
                absorbedAgent.updatePosition = true;
                absorbedAgent.updateRotation = true;
                absorbedAgent.isStopped = false;
            }

            // Volvemos a dejar que su lógica de turnos sea normal
            absorbedClimber.SetExternallyDoneThisTurn(false);

            // 3) Mandarlo al campamento más cercano
            CampGraphBuilder.CampNode nearest = GetNearestCamp(safePos);
            if (nearest != null)
            {
                absorbedClimber.ForceMoveToCampNode(nearest);
            }
            else
            {
                Debug.LogWarning("[QuicksandDefense] No se encontró campamento cercano al rescatar al escalador.");
            }
        }

        // 4) Las arenas desaparecen tras el rescate
        Destroy(gameObject);
    }

    private CampGraphBuilder.CampNode GetNearestCamp(Vector3 pos)
    {
#if UNITY_6000_0_OR_NEWER
        CampGraphBuilder graph = FindFirstObjectByType<CampGraphBuilder>();
#else
        CampGraphBuilder graph = FindObjectOfType<CampGraphBuilder>();
#endif
        if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
            return null;

        CampGraphBuilder.CampNode best = null;
        float bestDist = Mathf.Infinity;

        foreach (var node in graph.nodes)
        {
            float d = Vector3.Distance(pos, node.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = node;
            }
        }

        return best;
    }
}
