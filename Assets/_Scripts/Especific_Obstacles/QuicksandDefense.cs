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

    [Header("Rescate")]
    [SerializeField] private float rescueRadius = 1.5f;     // 👉 radio para que otro escalador pueda rescatar
    [SerializeField] private LayerMask climberLayer;        // 👉 capa de escaladores (para el overlap)

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
        originalLocalScale = transform.localScale;

        triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
            triggerCol.isTrigger = true;
    }

    public override void Initialize()
    {
        base.Initialize();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    private IEnumerator SpawnFromGround()
    {
        transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            float eased = t * t * (3f - 2f * t);
            transform.localScale = originalLocalScale * eased;

            yield return null;
        }

        transform.localScale = originalLocalScale;
        spawnRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 🔹 Equipamiento
        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmuneToQuickSand = false;

        if (loadout != null)
        {
            loadout.TryHandleObstacle(ObstacleType.QuickSand);
            isImmuneToQuickSand = loadout.CanHandleObstacle(ObstacleType.QuickSand);
        }

        if (isImmuneToQuickSand)
            return;

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

            absorbedClimber.SetExternallyDoneThisTurn(true);
            return;
        }

        // Si entra otro escalador y ya hay uno dentro → intentamos rescate (por si coincide)
        if (climber != absorbedClimber)
        {
            TryRescueWithClimber(climber);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // No dejamos que escape “normalmente”
    }

    private void Update()
    {
        if (absorbedClimber == null)
            return;

        if (absorbedClimber.gameObject == null)
        {
            absorbedClimber = null;
            absorbedAgent = null;
            return;
        }

        // 0) Comprobar rescate por proximidad (esto hace que el gizmo sea real)
        CheckRescueByRadius();

        // Si ya se rescató en este frame
        if (absorbedClimber == null)
            return;

        Transform t = absorbedClimber.transform;
        Vector3 pos = t.position;

        // 1) Moverlo SUAVEMENTE al centro (XZ)
        Vector3 targetXZ = new Vector3(transform.position.x, pos.y, transform.position.z);
        pos = Vector3.Lerp(pos, targetXZ, Time.deltaTime * centerLerpSpeed);
        t.position = pos;

        if (!hasRecordedInitialY)
        {
            initialY = t.position.y;
            hasRecordedInitialY = true;
        }

        // 2) Solo hundimos durante turno de escaladores
        if (TurnManager.Instance == null || !TurnManager.Instance.IsClimberTurn())
            return;

        AbsorbStep();
    }

    private void CheckRescueByRadius()
    {
        if (rescueRadius <= 0f) return;
        if (climberLayer.value == 0) return; // por si no lo asignas

        Collider[] hits = Physics.OverlapSphere(transform.position, rescueRadius, climberLayer);
        foreach (var hit in hits)
        {
            var c = hit.GetComponent<ClimberMovement>();
            if (c == null) continue;
            if (c == absorbedClimber) continue;

            // Si este escalador es inmune a quicksand, no lo usamos para rescatar
            var loadout = c.GetComponent<ClimberLoadout>();
            if (loadout != null && loadout.CanHandleObstacle(ObstacleType.QuickSand))
                continue;

            TryRescueWithClimber(c);
            break;
        }
    }

    private void TryRescueWithClimber(ClimberMovement rescuer)
    {
        if (absorbedClimber == null) return;
        if (rescuer == null) return;

        // Si está dentro del radio, rescata
        float d = Vector3.Distance(rescuer.transform.position, transform.position);
        if (d <= rescueRadius)
        {
            RescueClimber();
        }
    }

    private void AbsorbStep()
    {
        if (absorbedClimber == null)
            return;

        Transform t = absorbedClimber.transform;
        Vector3 pos = t.position;

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

        Destroy(gameObject);
    }

    private void RescueClimber()
    {
        if (absorbedClimber != null)
        {
            Transform t = absorbedClimber.transform;

            Vector3 safePos = transform.position + Vector3.up * 0.3f;
            t.position = safePos;

            if (absorbedAgent == null)
                absorbedAgent = absorbedClimber.GetComponent<NavMeshAgent>();

            if (absorbedAgent != null)
            {
                absorbedAgent.updatePosition = true;
                absorbedAgent.updateRotation = true;
                absorbedAgent.isStopped = false;
            }

            absorbedClimber.SetExternallyDoneThisTurn(false);

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

    private void OnDrawGizmosSelected()
    {
        // Área de rescate
        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.2f);
        Gizmos.DrawSphere(transform.position, rescueRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 1f);
        Gizmos.DrawWireSphere(transform.position, rescueRadius);
    }
}
