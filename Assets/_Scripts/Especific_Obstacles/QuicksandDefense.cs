using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class QuicksandDefense : BaseDefense
{
    [Header("Absorción por turnos")]
    [SerializeField] private int turnsToKill = 2; // ✅ muere al terminar N turnos de escaladores
    private int absorbedTurns = 0;

    [Header("Centrado del escalador")]
    [SerializeField] private float centerLerpSpeed = 6f;

    [Header("Rescate")]
    [SerializeField] private float rescueRadius = 1.5f;
    [SerializeField] private LayerMask climberLayer;

    [Header("Animación de aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    private Coroutine spawnRoutine;
    private Vector3 spawnTargetLocalScale; // escala final

    // Sólo un escalador a la vez
    private ClimberMovement absorbedClimber = null;
    private NavMeshAgent absorbedAgent = null;

    private bool subscribedToTurns = false;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnDisable()
    {
        UnsubscribeFromTurnEvents();
    }

    public override void Initialize()
    {
        base.Initialize();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    public void ApplyExternalScale(Vector3 finalScale)
    {
        spawnTargetLocalScale = finalScale;
        transform.localScale = finalScale;
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

            transform.localScale = spawnTargetLocalScale * eased;
            yield return null;
        }

        transform.localScale = spawnTargetLocalScale;
        spawnRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Equipamiento
        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmune = false;

        if (loadout != null)
        {
            loadout.TryHandleObstacle(ObstacleType.Snow);
            isImmune = loadout.CanHandleObstacle(ObstacleType.Snow);
        }

        if (isImmune)
            return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // Si aún no hay nadie absorbiéndose → empezamos con este
        if (absorbedClimber == null)
        {
            absorbedClimber = climber;
            absorbedTurns = 0; // ✅ reset contador al caer

            SubscribeToTurnEvents();

            absorbedAgent = climber.GetComponent<NavMeshAgent>();
            if (absorbedAgent != null)
            {
                absorbedAgent.isStopped = true;
                absorbedAgent.updatePosition = false;
                absorbedAgent.updateRotation = false;
            }

            absorbedClimber.SetExternallyDoneThisTurn(true);
            return;
        }

        // Si entra otro escalador y ya hay uno dentro → intentamos rescate
        if (climber != absorbedClimber)
        {
            TryRescueWithClimber(climber);
        }
    }

    private void Update()
    {
        if (absorbedClimber == null)
            return;

        if (absorbedClimber.gameObject == null)
        {
            absorbedClimber = null;
            absorbedAgent = null;
            UnsubscribeFromTurnEvents();
            return;
        }

        // Rescate por proximidad
        CheckRescueByRadius();
        if (absorbedClimber == null) return;

        // Centrar XZ (solo visual/feedback)
        Transform t = absorbedClimber.transform;
        Vector3 pos = t.position;

        Vector3 targetXZ = new Vector3(transform.position.x, pos.y, transform.position.z);
        pos = Vector3.Lerp(pos, targetXZ, Time.deltaTime * centerLerpSpeed);
        t.position = pos;
    }

    // ✅ Turn-based kill: al terminar cada turno de escaladores
    private void HandleClimberTurnEnd()
    {
        if (absorbedClimber == null) return;

        absorbedTurns++;

        if (absorbedTurns >= turnsToKill)
        {
            KillClimber();
        }
    }

    private void SubscribeToTurnEvents()
    {
        if (subscribedToTurns) return;
        if (TurnManager.Instance == null) return;

        TurnManager.Instance.OnClimberTurnEnd += HandleClimberTurnEnd;
        subscribedToTurns = true;
    }

    private void UnsubscribeFromTurnEvents()
    {
        if (!subscribedToTurns) return;
        if (TurnManager.Instance == null) return;

        TurnManager.Instance.OnClimberTurnEnd -= HandleClimberTurnEnd;
        subscribedToTurns = false;
    }

    private void CheckRescueByRadius()
    {
        if (rescueRadius <= 0f) return;
        if (climberLayer.value == 0) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, rescueRadius, climberLayer);
        foreach (var hit in hits)
        {
            var c = hit.GetComponent<ClimberMovement>();
            if (c == null) continue;
            if (c == absorbedClimber) continue;

            var loadout = c.GetComponent<ClimberLoadout>();
            if (loadout != null && loadout.CanHandleObstacle(ObstacleType.Snow))
                continue;

            TryRescueWithClimber(c);
            break;
        }
    }

    private void TryRescueWithClimber(ClimberMovement rescuer)
    {
        if (absorbedClimber == null || rescuer == null) return;

        float d = Vector3.Distance(rescuer.transform.position, transform.position);
        if (d <= rescueRadius)
            RescueClimber();
    }

    private void KillClimber()
    {
        UnsubscribeFromTurnEvents();

        if (absorbedClimber != null)
        {
            Vector3 pos = absorbedClimber.transform.position;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyClimberDied(new GameManager.DeathInfo
                {
                    climber = absorbedClimber,
                    position = pos,
                    cause = GameManager.DeathCause.Quicksand
                });
            }

            Destroy(absorbedClimber.gameObject);
            ClimberDeathPointsManager.Instance.AddClimberDeathPoints();
            PointsManager.Instance.AddPoints(10);
        }

        Destroy(gameObject);

    }

    private void RescueClimber()
    {
        UnsubscribeFromTurnEvents();

        if (absorbedClimber != null)
        {
            Vector3 safePos = transform.position + Vector3.up * 0.3f;
            absorbedClimber.transform.position = safePos;

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
                absorbedClimber.ForceMoveToCampNode(nearest);
            else
                Debug.LogWarning("[QuicksandDefense] No se encontró campamento cercano al rescatar.");
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
        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.2f);
        Gizmos.DrawSphere(transform.position, rescueRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 1f);
        Gizmos.DrawWireSphere(transform.position, rescueRadius);
    }
}
