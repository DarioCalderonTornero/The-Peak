using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class QuicksandDefense : BaseDefense
{
    [Header("Absorción por turnos")]
    [SerializeField] private int turnsToKill = 2;
    private int absorbedTurns = 0;

    [Header("Centrado del escalador")]
    [SerializeField] private float centerLerpSpeed = 6f;

    [Header("Rescate")]
    [SerializeField] private float rescueRadius = 1.5f;
    [SerializeField] private LayerMask climberLayer;

    [Header("Animación de aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    [Header("Hundimiento visual")]
    [SerializeField] private float sinkTargetY = 0.3f;
    [SerializeField] private float sinkDuration = 0.6f;

    [Header("Animación de forcejeo")]
    [SerializeField] private float struggleRotationAmount = 12f;
    [SerializeField] private float struggleBobAmount = 0.06f;
    [SerializeField] private float struggleCycleTime = 0.45f;

    private Coroutine spawnRoutine;
    private Coroutine struggleRoutine;
    private Vector3 spawnTargetLocalScale;

    private ClimberMovement absorbedClimber = null;
    private NavMeshAgent absorbedAgent = null;
    private GameObject absorbedVisual = null;
    private Collider absorbedCollider = null;
    private Animator absorbedAnimator = null;

    private float fixedRootY;

    private Vector3 visualOriginalLocalPos;
    private Quaternion visualOriginalLocalRot;

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
        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmune = loadout != null && loadout.CanHandleObstacle(ObstacleType.Snow);

        // Si es immune, HandleSnowObstacle gestiona el counter
        if (isImmune) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        if (absorbedClimber == null)
        {
            AbsorbClimber(climber);
            return;
        }

        if (climber != absorbedClimber)
            TryRescueWithClimber(climber);
    }

    private void AbsorbClimber(ClimberMovement climber)
    {
        absorbedClimber = climber;
        absorbedTurns = 0;

        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
            fixedRootY = hit.point.y;
        else
            fixedRootY = transform.position.y;

        absorbedVisual = climber.ClimberVisual;
        if (absorbedVisual != null)
        {
            visualOriginalLocalPos = absorbedVisual.transform.localPosition;
            visualOriginalLocalRot = absorbedVisual.transform.localRotation;
        }

        SubscribeToTurnEvents();

        absorbedAgent = climber.GetComponent<NavMeshAgent>();
        if (absorbedAgent != null)
        {
            absorbedAgent.isStopped = true;
            absorbedAgent.updatePosition = false;
            absorbedAgent.updateRotation = false;
        }

        absorbedClimber.SetExternallyDoneThisTurn(true);

        if (absorbedAgent != null)
            absorbedAgent.enabled = false;

        absorbedCollider = climber.GetComponent<Collider>();

        absorbedAnimator = climber.GetComponent<Animator>();
        if (absorbedAnimator != null) absorbedAnimator.enabled = false;

        if (absorbedVisual != null)
        {
            if (struggleRoutine != null) StopCoroutine(struggleRoutine);
            struggleRoutine = StartCoroutine(SinkAndStruggleRoutine());
        }
    }

    private IEnumerator SinkAndStruggleRoutine()
    {
        Vector3 startLocalPos = absorbedVisual.transform.localPosition;
        Vector3 sunkLocalPos = startLocalPos + Vector3.down * sinkTargetY;

        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            if (absorbedVisual == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sinkDuration);
            float eased = t * t * (3f - 2f * t);
            absorbedVisual.transform.localPosition = Vector3.Lerp(startLocalPos, sunkLocalPos, eased);
            yield return null;
        }

        if (absorbedVisual != null)
            absorbedVisual.transform.localPosition = sunkLocalPos;

        while (absorbedVisual != null && absorbedClimber != null)
        {
            float cycleElapsed = 0f;

            while (cycleElapsed < struggleCycleTime)
            {
                if (absorbedVisual == null) yield break;

                cycleElapsed += Time.deltaTime;
                float t = cycleElapsed / struggleCycleTime;

                float wave = Mathf.Sin(t * Mathf.PI * 2f);
                float rotX = wave * struggleRotationAmount * 0.6f;
                float rotZ = Mathf.Cos(t * Mathf.PI * 2f) * struggleRotationAmount;
                absorbedVisual.transform.localRotation = Quaternion.Euler(rotX, 0f, rotZ);

                float bobY = Mathf.Abs(wave) * struggleBobAmount;
                absorbedVisual.transform.localPosition = sunkLocalPos + Vector3.up * bobY;

                yield return null;
            }
        }
    }

    private void Update()
    {
        if (absorbedClimber == null) return;

        if (absorbedClimber.gameObject == null)
        {
            StopStruggle();
            absorbedClimber = null;
            absorbedAgent = null;
            UnsubscribeFromTurnEvents();
            return;
        }

        CheckRescueByRadius();
        if (absorbedClimber == null) return;

        Vector3 pos = absorbedClimber.transform.position;
        pos.y = Mathf.Lerp(pos.y, fixedRootY - sinkTargetY, Time.deltaTime * centerLerpSpeed);
        pos.x = Mathf.Lerp(pos.x, transform.position.x, Time.deltaTime * centerLerpSpeed);
        pos.z = Mathf.Lerp(pos.z, transform.position.z, Time.deltaTime * centerLerpSpeed);
        absorbedClimber.transform.position = pos;
    }

    private void HandleClimberTurnEnd()
    {
        if (absorbedClimber == null) return;

        absorbedTurns++;

        if (absorbedTurns >= turnsToKill)
            KillClimber();
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
        StopStruggle();

        if (absorbedClimber != null)
        {
            ClimberMovement climberRef = absorbedClimber;
            absorbedClimber = null;

            Vector3 deathPos = climberRef.transform.position;
            deathPos.y = fixedRootY - sinkTargetY;
            climberRef.transform.position = deathPos;

            var deathInfo = new GameManager.DeathInfo
            {
                climber = climberRef,
                position = deathPos,
                cause = DeathCause.Snow
            };

            if (GameManager.Instance != null)
                GameManager.Instance.NotifyClimberDied(deathInfo);

            if (PointsManager.Instance != null)
                PointsManager.Instance.AddPoints(10);

            if (DeathCinematicManager.Instance != null)
            {
                void OnThisDeath(GameManager.DeathInfo info)
                {
                    if (info.climber == climberRef)
                    {
                        DeathCinematicManager.Instance.OnOwnClimberCinematicFinished -= OnThisDeath;
                        if (this != null && gameObject != null)
                            Destroy(gameObject);
                    }
                }
                DeathCinematicManager.Instance.OnOwnClimberCinematicFinished += OnThisDeath;
            }
            else
            {
                Destroy(gameObject, 3f);
            }
        }

        absorbedAgent = null;
        absorbedVisual = null;
        absorbedCollider = null;
        absorbedAnimator = null;
    }

    private void RescueClimber()
    {
        UnsubscribeFromTurnEvents();
        StopStruggle();

        if (absorbedClimber != null)
        {
            if (absorbedVisual != null)
            {
                absorbedVisual.transform.localPosition = visualOriginalLocalPos;
                absorbedVisual.transform.localRotation = visualOriginalLocalRot;
            }

            if (absorbedAnimator != null) absorbedAnimator.enabled = true;

            Vector3 safePos = transform.position + Vector3.up * 0.3f;
            absorbedClimber.transform.position = safePos;

            if (absorbedAgent == null)
                absorbedAgent = absorbedClimber.GetComponent<NavMeshAgent>();

            if (absorbedAgent != null)
            {
                absorbedAgent.enabled = true;
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

    private void StopStruggle()
    {
        if (struggleRoutine != null)
        {
            StopCoroutine(struggleRoutine);
            struggleRoutine = null;
        }
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