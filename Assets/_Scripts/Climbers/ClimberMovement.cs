using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ClimberMovement : MonoBehaviour
{
    public static ClimberMovement Instance { get; private set; }

    public event Action<bool> OnTentStateChanged;

    [Header("Referencias")]
    [SerializeField] public NavMeshAgent agent;
    [SerializeField] private CampGraphBuilder campGraph;
    [SerializeField] private Transform summit;
    [SerializeField] private ClimberLoadout loadout;

    [Header("Visualización de Ruta (Vistosa)")]
    [SerializeField] private LineRenderer pathLineRenderer;
    private Color pathColor;
    [SerializeField] private float animationSpeed = 2.0f;
    [SerializeField] private float textureTiling = 1.0f;
    private Material lineMaterialInstance;
    private bool isSelected = false;
    [SerializeField] private LayerMask mountainLayer;
    [SerializeField] private float raycastHeight = 8f;
    [SerializeField] private float floatOffset = 2f;
    [SerializeField] private int subdivisionsPerSegment = 6;

    [Header("Llegada / márgenes")]
    [SerializeField] private float reachedThreshold = 0.2f;

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainPerSecond = 2f;
    [SerializeField] private float slowDownSlopeFactor = 0.5f;

    [Header("Velocidad / efectos externos")]
    [SerializeField] private float externalSpeedMultiplier = 1f;

    [Header("Memoria de campamentos")]
    [SerializeField] private float revisitPenaltyPerVisit = 5f;
    [SerializeField] private int maxVisitsToDie = 3;

    [Header("Penalización por alejarse de la cima")]
    [SerializeField] private float backtrackTolerance = 2f;
    [SerializeField] private float backtrackPenaltyPerMeter = 5f;

    [Header("Estrategia IA")]
    [SerializeField] private float stepConversionFactor = 20f;
    [SerializeField] private float noiseRange = 50f;

    [Header("Configuración de Acampada")]
    [SerializeField] private GameObject tentPrefab;
    [SerializeField] private GameObject arrivalFXPrefab;
    [SerializeField] private float tentYOffset = 0.75f;
    [SerializeField] private GameObject climberVisual;
    private bool isInsideTent = false;

    private static float _globalNextMoveTime = 0f;

    [SerializeField] private float currentStamina;
    private Vector3 lastFramePosition;
    private float lastFrameHeight;
    private float originalSpeed;

    private CampGraphBuilder.CampNode currentNode;
    private CampGraphBuilder.CampNode plannedTargetNode;
    private CampGraphBuilder.CampNode targetNode;

    private bool isActiveThisTurn = false;
    private bool isAtCamp = false;
    private bool isGoingToFirstCamp = true;
    private bool reachedSummit = false;
    private bool hasStartedThisTurn = false;
    private bool waitingFirstTurn = true;

    private Dictionary<int, int> nodeVisitCount = new Dictionary<int, int>();
    private bool externallyForcedDone = false;

    public bool IsAtCamp => isAtCamp;
    public bool IsInsideTent => isInsideTent;
    public float GetAltitude() => transform.position.y;
    public bool IsOutOfStamina => currentStamina <= 0f;
    public bool IsDoneThisTurn => externallyForcedDone || isAtCamp || reachedSummit || IsOutOfStamina;

    public CampGraphBuilder.CampNode CurrentNode => currentNode;

    public bool isEating = false;
    public Vector3 originalDestination;
    [HideInInspector] public bool hasEatenThisTurn = false;

    private Coroutine temporaryStopRoutine;
    private float _resumeTime = -1f;
    private float _cachedMultiplierBeforeStop = 1f;
    private bool pointsAddedThisTurn = false;

    [Header("Camera Targets")]
    [SerializeField] private Transform camLookAt;
    [SerializeField] private Transform inspectAnchor;

    public Transform CamLookAt => camLookAt != null ? camLookAt : transform;
    public Transform InspectAnchor => inspectAnchor != null ? inspectAnchor : transform;

    public event Action<float> OnStaminaChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (loadout == null) loadout = GetComponent<ClimberLoadout>();
        if (pathLineRenderer == null) pathLineRenderer = GetComponent<LineRenderer>();

        if (pathLineRenderer.material != null)
            lineMaterialInstance = pathLineRenderer.material;

        currentStamina = maxStamina;

        pathLineRenderer.positionCount = 0;
        pathLineRenderer.enabled = false;
        pathLineRenderer.textureMode = LineTextureMode.Tile;
    }

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart += HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd += HandleClimberTurnEnd;
        }

        if (ClimberRegistry.Instance != null)
            ClimberRegistry.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart -= HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd -= HandleClimberTurnEnd;
        }

        if (ClimberRegistry.Instance != null)
            ClimberRegistry.Instance.Unregister(this);
    }

    private void Start()
    {
        if (summit == null)
        {
            GameObject summitGO = GameObject.Find("FinalDestination");
            if (summitGO != null) summit = summitGO.transform;
        }

        if (campGraph == null) campGraph = FindObjectOfType<CampGraphBuilder>();

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            originalSpeed = agent.speed;
            agent.isStopped = true;
        }

        isAtCamp = false;
        reachedSummit = false;
        StartCoroutine(StartLateCheck());
    }

    private IEnumerator StartLateCheck()
    {
        yield return new WaitForEndOfFrame();

        SyncPathColorWithHelmet();

        if (TurnManager.Instance != null)
        {
            if (TurnManager.Instance.IsPlayerTurn())
                PlanNextMove();
            else if (TurnManager.Instance.IsClimberTurn())
            {
                PlanNextMove();
                HandleClimberTurnStart();
            }
        }
    }

    // ─── Stamina ─────────────────────────────────────────────────────────────

    private void NotifyStaminaChanged()
    {
        float normalized = maxStamina > 0f ? currentStamina / maxStamina : 0f;
        OnStaminaChanged?.Invoke(normalized);
    }

    // ─── Ruta visual ─────────────────────────────────────────────────────────

    private void SyncPathColorWithHelmet()
    {
        if (loadout == null || pathLineRenderer == null) return;

        pathColor = loadout.GetHelmetColor();
        pathLineRenderer.startColor = Color.white;
        pathLineRenderer.endColor = Color.white;

        if (lineMaterialInstance != null)
        {
            if (lineMaterialInstance.HasProperty("_ColorDentro"))
                lineMaterialInstance.SetColor("_ColorDentro", pathColor);
            else if (lineMaterialInstance.HasProperty("_Color"))
                lineMaterialInstance.color = pathColor;
            else if (lineMaterialInstance.HasProperty("_BaseColor"))
                lineMaterialInstance.SetColor("_BaseColor", pathColor);
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        pathLineRenderer.enabled = isSelected;
        if (isSelected) UpdatePathVisualization();
    }

    private void UpdatePathVisualization()
    {
        if (plannedTargetNode == null) { pathLineRenderer.positionCount = 0; return; }

        NavMeshPath path = new NavMeshPath();
        NavMesh.SamplePosition(transform.position, out NavMeshHit hitStart, 5f, NavMesh.AllAreas);
        NavMesh.SamplePosition(plannedTargetNode.position, out NavMeshHit hitEnd, 5f, NavMesh.AllAreas);
        NavMesh.CalculatePath(hitStart.position, hitEnd.position, NavMesh.AllAreas, path);

        if (path.status != NavMeshPathStatus.PathComplete) { pathLineRenderer.positionCount = 0; return; }

        List<Vector3> finalPoints = new List<Vector3>();
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 a = path.corners[i];
            Vector3 b = path.corners[i + 1];
            for (int j = 0; j <= subdivisionsPerSegment; j++)
            {
                float t = j / (float)subdivisionsPerSegment;
                Vector3 samplePoint = Vector3.Lerp(a, b, t);
                Ray ray = new Ray(samplePoint + Vector3.up * raycastHeight, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, raycastHeight * 2f, mountainLayer))
                    finalPoints.Add(hit.point + hit.normal * floatOffset);
                else
                    finalPoints.Add(samplePoint + Vector3.up * floatOffset);
            }
        }

        pathLineRenderer.positionCount = finalPoints.Count;
        pathLineRenderer.SetPositions(finalPoints.ToArray());
    }

    private void AnimateLine()
    {
        if (lineMaterialInstance != null)
        {
            float textureOffset = Time.time * -animationSpeed;
        }
    }

    public void RecalculateIntention()
    {
        if (reachedSummit || externallyForcedDone) return;
        PlanNextMove();
        if (isSelected) UpdatePathVisualization();
    }

    // ─── Muerte ───────────────────────────────────────────────────────────────

    private void TriggerDeath(DeathCause cause)
    {
        if (pointsAddedThisTurn) return;

        PointsManager.Instance.AddPoints(5);
        ClimberDeathPointsManager.Instance.AddClimberDeathPoints();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyClimberDied(new GameManager.DeathInfo
            {
                climber = this,
                position = transform.position,
                cause = cause
            });
        }

        isActiveThisTurn = false;
        isAtCamp = false;
        pointsAddedThisTurn = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (currentStamina <= 0f && !pointsAddedThisTurn)
        {
            TriggerDeath(DeathCause.Stamina);
            return;
        }

        if (isSelected && pathLineRenderer.enabled) AnimateLine();
        if (!isActiveThisTurn || reachedSummit || agent == null) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Vector3 currentPos = transform.position;
        float frameDistance = Vector3.Distance(currentPos, lastFramePosition);
        float frameHeight = currentPos.y;
        float heightDelta = frameHeight - lastFrameHeight;

        // --- LÓGICA DE VELOCIDAD POR PENDIENTE ---
        float currentSlopeMultiplier = 1f;
        if (frameDistance > 0.001f)
        {
            
            float uphill = Mathf.Max(heightDelta, 0f);
            float slope = uphill / frameDistance;

            
            currentSlopeMultiplier = Mathf.Clamp(1f - (slope * slowDownSlopeFactor), 0.2f, 1f);
        }

        agent.speed = originalSpeed * currentSlopeMultiplier * externalSpeedMultiplier;

       
        if (agent.velocity.magnitude > 0.1f)
        {
            currentStamina -= staminaDrainPerSecond * Time.deltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
            NotifyStaminaChanged();
        }

       
        lastFramePosition = currentPos;
        lastFrameHeight = frameHeight;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
            HandleReachedCamp();
    }

    // ─── Turnos ───────────────────────────────────────────────────────────────

    private void HandlePlayerTurnStart()
    {
        if (reachedSummit || externallyForcedDone) return;

        // Si acaba de terminar su primer turno de espera, limpiamos isAtCamp
        if (!waitingFirstTurn && isAtCamp && currentNode == null)
            isAtCamp = false;

        pointsAddedThisTurn = false;
        PlanNextMove();
        if (isSelected) UpdatePathVisualization();
    }

    private void PlanNextMove()
    {
        if (campGraph == null) campGraph = FindObjectOfType<CampGraphBuilder>();
        if (campGraph != null) campGraph.RecalculateObstaclesOnEdges();

        if (isGoingToFirstCamp || currentNode == null)
        {
            plannedTargetNode = FindClosestCampNode();
            return;
        }

        plannedTargetNode = CalculateBestNeighborNode();
    }

    private void HandleClimberTurnStart()
    {
        // Primer turno — espera sin moverse
        if (waitingFirstTurn)
        {
            waitingFirstTurn = false;
            isAtCamp = true;
            return;
        }

        if (reachedSummit || agent == null || externallyForcedDone) return;

        hasStartedThisTurn = true;
        hasEatenThisTurn = false;

        float now = Time.time;
        if (_globalNextMoveTime < now) _globalNextMoveTime = now;
        float delay = _globalNextMoveTime - now;
        _globalNextMoveTime += 0.75f;

        StartCoroutine(ExecuteTurnDecisionWithDelay(delay));
    }

    private IEnumerator ExecuteTurnDecisionWithDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (this == null || externallyForcedDone || reachedSummit) yield break;

        if (isInsideTent) ExitTent();

        PlanNextMove();
        if (isSelected) UpdatePathVisualization();

        if (plannedTargetNode != null)
            MoveToNode(plannedTargetNode);
        else
        {
            isAtCamp = true;
            isActiveThisTurn = false;
        }
    }

    private void MoveToNode(CampGraphBuilder.CampNode node)
    {
        targetNode = node;
        isActiveThisTurn = true;
        isAtCamp = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetNode.position);
            originalDestination = targetNode.position;
        }

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    private CampGraphBuilder.CampNode FindClosestCampNode()
    {
        if (campGraph == null) campGraph = FindObjectOfType<CampGraphBuilder>();
        if (campGraph == null || campGraph.nodes == null) return null;

        float bestDist = float.PositiveInfinity;
        CampGraphBuilder.CampNode closest = null;

        foreach (var node in campGraph.nodes)
        {
            float d = Vector3.Distance(transform.position, node.position);
            if (d < bestDist) { bestDist = d; closest = node; }
        }

        return closest;
    }

    private CampGraphBuilder.CampNode CalculateBestNeighborNode()
    {
        if (currentNode != null &&
            nodeVisitCount.ContainsKey(currentNode.id) &&
            nodeVisitCount[currentNode.id] >= maxVisitsToDie)
        {
            TriggerDeath(DeathCause.Stamina);
            return null;
        }

        if (currentNode == null || currentNode.neighbors == null || currentNode.neighbors.Count == 0)
            return null;

        CampGraphBuilder.CampEdge bestAffordable = null;
        float bestAffordableScore = float.PositiveInfinity;
        CampGraphBuilder.CampEdge bestUnaffordable = null;
        float bestUnaffordableScore = float.PositiveInfinity;

        foreach (var edge in currentNode.neighbors)
        {
            bool hasRealObstacle = edge.hasObstacle && edge.obstacleType != ObstacleType.None;
            bool canPassObstacle = !hasRealObstacle || (loadout != null && loadout.CanHandleObstacle(edge.obstacleType));

            if (hasRealObstacle && !canPassObstacle) continue;

            float effectiveWeight = edge.weight;
            int myVisits = nodeVisitCount.ContainsKey(edge.to.id) ? nodeVisitCount[edge.to.id] : 0;
            effectiveWeight += myVisits * revisitPenaltyPerVisit;

            if (summit != null && currentNode != null && edge.to != null)
            {
                float distNow = Vector3.Distance(currentNode.position, summit.position);
                float distNext = Vector3.Distance(edge.to.position, summit.position);
                float approach = distNow - distNext;
                if (approach < -backtrackTolerance)
                    effectiveWeight += (-approach - backtrackTolerance) * backtrackPenaltyPerMeter;
            }

            effectiveWeight = Mathf.Max(effectiveWeight, 0.01f);
            float perceivedWeight = Mathf.Max(effectiveWeight + UnityEngine.Random.Range(-noiseRange, noiseRange), 0.1f);
            int steps = edge.to != null ? edge.to.stepsToSummit : 999;
            float finalScore = perceivedWeight + (steps * stepConversionFactor);

            if (CalculateStaminaCost(edge) <= currentStamina)
            {
                if (finalScore < bestAffordableScore)
                {
                    bestAffordableScore = finalScore;
                    bestAffordable = edge;
                }
            }
            else
            {
                float penaltyScore = finalScore + 10000f;
                if (penaltyScore < bestUnaffordableScore)
                {
                    bestUnaffordableScore = penaltyScore;
                    bestUnaffordable = edge;
                }
            }
        }

        return (bestAffordable != null ? bestAffordable : bestUnaffordable)?.to;
    }

    private void HandleReachedCamp()
    {
        if (targetNode != null) { currentNode = targetNode; targetNode = null; }

        isActiveThisTurn = false;
        isAtCamp = true;

        if (agent != null) agent.isStopped = true;

        if (currentNode != null)
        {
            if (!nodeVisitCount.ContainsKey(currentNode.id)) nodeVisitCount[currentNode.id] = 0;
            nodeVisitCount[currentNode.id]++;
            StartCoroutine(EnterTentSequence(currentNode));
        }

        isGoingToFirstCamp = false;

        currentStamina = maxStamina;
        NotifyStaminaChanged();

        if (agent != null) agent.speed = originalSpeed;

        if (campGraph != null && currentNode != null && currentNode.id == campGraph.finalDestinationNodeId)
            HandleReachedGoal();
    }

    private IEnumerator EnterTentSequence(CampGraphBuilder.CampNode node)
    {
        if (arrivalFXPrefab != null)
            Instantiate(arrivalFXPrefab, node.position + Vector3.up * 0.2f, Quaternion.identity);

        yield return new WaitForSeconds(2f);

        node.occupantsCount++;

        if (!node.presentClimbers.Contains(this))
            node.presentClimbers.Add(this);

        if (tentPrefab != null && !node.HasTent && node.id != campGraph.finalDestinationNodeId)
        {
            Vector3 tentPos = node.position + Vector3.up * tentYOffset;
            node.instantiatedTent = Instantiate(tentPrefab, tentPos, Quaternion.identity);
        }

        isInsideTent = true;
        OnTentStateChanged?.Invoke(true);

        if (climberVisual != null) climberVisual.SetActive(false);
        if (pathLineRenderer != null) pathLineRenderer.enabled = false;

        yield return null;
    }

    private void ExitTent()
    {
        isInsideTent = false;
        OnTentStateChanged?.Invoke(false);

        isAtCamp = false;

        if (climberVisual != null) climberVisual.SetActive(true);

        if (currentNode != null)
        {
            currentNode.occupantsCount--;

            if (currentNode.presentClimbers.Contains(this))
                currentNode.presentClimbers.Remove(this);

            if (currentNode.occupantsCount <= 0)
            {
                currentNode.occupantsCount = 0;
                if (currentNode.instantiatedTent != null)
                {
                    Destroy(currentNode.instantiatedTent);
                    currentNode.instantiatedTent = null;
                }
            }
        }

        if (isSelected && pathLineRenderer != null)
        {
            pathLineRenderer.enabled = true;
            UpdatePathVisualization();
        }
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;
        isActiveThisTurn = false;
        isAtCamp = false;

        if (agent != null) agent.isStopped = true;

        GameOverManager.Instance.SetGameOverCamera();
        ShowFinalStats.Instance.Show();
    }

    private void HandleClimberTurnEnd()
    {
        isActiveThisTurn = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    public void SuppressStaminaDeath() => pointsAddedThisTurn = true;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private float CalculateStaminaCost(CampGraphBuilder.CampEdge edge)
    {
        if (edge == null) return 0f;

        float slope = Mathf.Max(edge.heightDelta, 0f) / edge.pathLength;
        float estimatedSpeed = originalSpeed * Mathf.Clamp(1f - (slope * slowDownSlopeFactor), 0.2f, 1f);

       
        float estimatedTime = edge.pathLength / estimatedSpeed;

        
        return estimatedTime * staminaDrainPerSecond;
    }

    // ─── API pública ─────────────────────────────────────────────────────────

    public void SetExternalSpeedMultiplier(float multiplier)
        => externalSpeedMultiplier = Mathf.Max(0f, multiplier);

    public float GetCurrentStamina() => currentStamina;

    public void SetCurrentStamina(float value)
    {
        currentStamina = Mathf.Clamp(value, 0f, maxStamina);
        NotifyStaminaChanged();
    }

    public void FreezeInPlace()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        isActiveThisTurn = false;
        SetExternalSpeedMultiplier(0f);
    }

    public void MoveToWorldPosition(Vector3 targetPos)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);
        }

        isActiveThisTurn = false;
    }

    public float GetMaxStamina() => maxStamina;

    public void AddMaxStamina(float amount)
    {
        maxStamina += amount;
        currentStamina = maxStamina;
        NotifyStaminaChanged();
    }

    public void ForceMoveToCampNode(CampGraphBuilder.CampNode node)
    {
        if (agent == null || node == null) return;

        isGoingToFirstCamp = false;
        isAtCamp = false;
        isActiveThisTurn = true;
        reachedSummit = false;
        targetNode = node;
        plannedTargetNode = node;

        agent.isStopped = false;
        agent.SetDestination(node.position);
        originalDestination = node.position;
    }

    public void SetExternallyDoneThisTurn(bool value)
    {
        externallyForcedDone = value;
        if (value && agent != null) agent.isStopped = true;
    }

    public void StopForSeconds(float duration)
    {
        if (!gameObject.activeInHierarchy) return;

        if (temporaryStopRoutine == null)
            _cachedMultiplierBeforeStop = externalSpeedMultiplier;

        _resumeTime = Mathf.Max(_resumeTime, Time.time + duration);

        if (temporaryStopRoutine == null)
            temporaryStopRoutine = StartCoroutine(StopLoop());
    }

    private IEnumerator StopLoop()
    {
        SetExternalSpeedMultiplier(0f);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;

        while (Time.time < _resumeTime) yield return null;

        SetExternalSpeedMultiplier(_cachedMultiplierBeforeStop);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;

        temporaryStopRoutine = null;
        _resumeTime = -1f;
    }
}