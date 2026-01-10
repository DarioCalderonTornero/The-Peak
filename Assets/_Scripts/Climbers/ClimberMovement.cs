using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ClimberMovement : MonoBehaviour
{
    public static ClimberMovement Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CampGraphBuilder campGraph;
    [SerializeField] private Transform summit;
    [SerializeField] private ClimberLoadout loadout;

    [Header("Visualización de Ruta (Vistosa)")]
    [SerializeField] private LineRenderer pathLineRenderer;
    [SerializeField] private Color pathColor = new Color(1f, 0.5f, 0f); // Naranja brillante
    [SerializeField] private float lineHeightOffset = 0.5f; // Altura sobre el suelo para que no se oculte
    [SerializeField] private float animationSpeed = 2.0f; // Velocidad de las "hormigas"
    [SerializeField] private float textureTiling = 1.0f; // Repetición de la textura
    private Material lineMaterialInstance; // Para animar sin afectar a otros
    private bool isSelected = false;

    // ... (El resto de tus headers siguen igual) ...
    [Header("Llegada / márgenes")]
    [SerializeField] private float reachedThreshold = 0.2f;

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float baseCostPerMeter = 1f;
    [SerializeField] private float uphillExtraCostFactor = 2f;
    [SerializeField] private float minStaminaCost = 0.1f;

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

    // --- Coordinación escalonada ---
    private static float _globalNextMoveTime = 0f;

    // Estado
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

    private Dictionary<int, int> nodeVisitCount = new Dictionary<int, int>();
    private bool externallyForcedDone = false;

    // Getters
    public bool IsAtCamp => isAtCamp;
    public bool IsOutOfStamina => currentStamina <= 0f;
    public bool IsDoneThisTurn => externallyForcedDone || isAtCamp || reachedSummit || IsOutOfStamina;
    public bool isEating = false;
    public Vector3 originalDestination;
    [HideInInspector] public bool hasEatenThisTurn = false;

    // Equipment related
    private Coroutine temporaryStopRoutine;
    private float _resumeTime = -1f;
    private float _cachedMultiplierBeforeStop = 1f;
    private bool pointsAddedThisTurn = false;

    [Header("Camera Targets")]
    [SerializeField] private Transform camLookAt;
    [SerializeField] private Transform inspectAnchor;

    public Transform CamLookAt => camLookAt != null ? camLookAt : transform;
    public Transform InspectAnchor => inspectAnchor != null ? inspectAnchor : transform;


    private void Awake()
    {
        if (Instance == null) Instance = this; // Ojo con el singleton en múltiples agentes

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (loadout == null) loadout = GetComponent<ClimberLoadout>();

        // CONFIGURACIÓN VISUAL INICIAL
        if (pathLineRenderer == null) pathLineRenderer = GetComponent<LineRenderer>();

        // Creamos una instancia del material para poder animarlo individualmente
        if (pathLineRenderer.material != null)
        {
            lineMaterialInstance = pathLineRenderer.material; // Esto crea una copia automática
        }

        // Configuramos colores y anchura inicial
        pathLineRenderer.startColor = pathColor;
        pathLineRenderer.endColor = new Color(pathColor.r, pathColor.g, pathColor.b, 0.1f); // Fade out al final
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.enabled = false;

        // Aseguramos que use modo Tile para que la animación funcione
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
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
            TurnManager.Instance.OnClimberTurnStart -= HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd -= HandleClimberTurnEnd;
        }
    }

    private void Start()
    {
        if (summit == null)
        {
            GameObject summitGO = GameObject.Find("FinalDestination");
            if (summitGO != null) summit = summitGO.transform;
        }

        if (campGraph == null) campGraph = FindObjectOfType<CampGraphBuilder>();

        currentStamina = maxStamina;
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
        if (TurnManager.Instance != null)
        {
            if (TurnManager.Instance.IsPlayerTurn()) PlanNextMove();
            else if (TurnManager.Instance.IsClimberTurn()) { PlanNextMove(); HandleClimberTurnStart(); }
        }
    }

    // ================= SELECCIÓN & VISUALIZACIÓN =================

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        pathLineRenderer.enabled = isSelected;

        if (isSelected)
        {
            UpdatePathVisualization();
        }
    }

    private void UpdatePathVisualization()
    {
        if (plannedTargetNode == null)
        {
            pathLineRenderer.positionCount = 0;
            return;
        }

        NavMeshPath path = new NavMeshPath();
        Vector3 startPos = transform.position;
        Vector3 targetPos = plannedTargetNode.position;

        // 1. Intentamos encontrar el punto válido en el NavMesh más cercano al inicio y al final.
        // Esto corrige errores si el campamento está un poco hundido o el escalador flotando.
        NavMeshHit hitStart, hitEnd;

        // Buscamos en un radio de 5 metros (5.0f) el punto de navegación más cercano
        bool startValid = NavMesh.SamplePosition(startPos, out hitStart, 5.0f, NavMesh.AllAreas);
        bool endValid = NavMesh.SamplePosition(targetPos, out hitEnd, 5.0f, NavMesh.AllAreas);

        bool pathFound = false;

        if (startValid && endValid)
        {
            // Calculamos la ruta entre esos dos puntos válidos
            pathFound = NavMesh.CalculatePath(hitStart.position, hitEnd.position, NavMesh.AllAreas, path);
        }
        else
        {
            // Si falla SamplePosition, intentamos con las posiciones originales por si acaso
            pathFound = NavMesh.CalculatePath(startPos, targetPos, NavMesh.AllAreas, path);
        }

        // 2. Si encontramos ruta y es válida (o parcial, que significa "lo más cerca posible")
        if (pathFound && path.status != NavMeshPathStatus.PathInvalid)
        {
            pathLineRenderer.positionCount = path.corners.Length;

            Vector3[] elevatedCorners = new Vector3[path.corners.Length];
            for (int i = 0; i < path.corners.Length; i++)
            {
                // Elevamos cada esquina un poco (lineHeightOffset) para que la línea no atraviese la tierra
                elevatedCorners[i] = path.corners[i] + Vector3.up * lineHeightOffset;
            }

            pathLineRenderer.SetPositions(elevatedCorners);
        }
        else
        {
            // DEBUG: Esto te dirá en la consola por qué sale la línea recta
            Debug.LogWarning($"[Climber] Ruta fallida. StartValid: {startValid}, EndValid: {endValid}, PathStatus: {path.status}");

            // Fallback: Línea recta (solo para que sepas que intentó ir ahí)
            pathLineRenderer.positionCount = 2;
            pathLineRenderer.SetPosition(0, startPos + Vector3.up * lineHeightOffset);
            pathLineRenderer.SetPosition(1, targetPos + Vector3.up * lineHeightOffset);
        }
    }

    // Método para animar la línea en cada frame
    private void AnimateLine()
    {
        if (lineMaterialInstance != null)
        {
            // Movemos la textura en el eje X (Offset) basándonos en el tiempo
            // Esto crea el efecto de "flechas caminando" hacia el destino
            float textureOffset = Time.time * -animationSpeed;
            lineMaterialInstance.mainTextureOffset = new Vector2(textureOffset, 0);

            // Ajustamos el tiling según la longitud (opcional, si TextureMode es Tile ya lo hace Unity)
            // lineMaterialInstance.mainTextureScale = new Vector2(textureTiling, 1);
        }
    }

    public void RecalculateIntention()
    {
        if (reachedSummit || externallyForcedDone) return;
        PlanNextMove();
        if (isSelected) UpdatePathVisualization();
    }

    // ================= UPDATE Y LÓGICA =================

    private void Update()
    {
        // 1. Animación de la línea (Solo si está seleccionado para ahorrar recursos)
        if (isSelected && pathLineRenderer.enabled)
        {
            AnimateLine();
        }

        // 2. Lógica de movimiento normal
        if (!isActiveThisTurn || reachedSummit || agent == null) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Vector3 currentPos = transform.position;
        float frameDistance = Vector3.Distance(currentPos, lastFramePosition);
        float frameHeight = currentPos.y;
        float heightDelta = frameHeight - lastFrameHeight;

        if (frameDistance > 0.0001f)
        {
            float uphill = Mathf.Max(heightDelta, 0f);
            float slope = uphill / frameDistance;
            float frameCost = frameDistance * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);
            frameCost = Mathf.Max(frameCost, minStaminaCost * Time.deltaTime);

            currentStamina = Mathf.Max(0f, currentStamina - frameCost);

            lastFramePosition = currentPos;
            lastFrameHeight = frameHeight;
        }

        if (currentStamina <= 0f && !pointsAddedThisTurn)
        {
            PointsManager.Instance.AddPoints(10);
            isActiveThisTurn = false;
            isAtCamp = false;
            pointsAddedThisTurn = true;
            Destroy(gameObject);
            return;
        }

        agent.speed = originalSpeed * externalSpeedMultiplier;

        if (!agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
            {
                HandleReachedCamp();
            }
        }
    }

    // ================= FASE 1: PENSAR =================

    private void HandlePlayerTurnStart()
    {
        if (reachedSummit || externallyForcedDone) return;
        pointsAddedThisTurn = false;
        PlanNextMove();
        if (isSelected) UpdatePathVisualization();
    }

    private void PlanNextMove()
    {
        if (isGoingToFirstCamp || currentNode == null)
        {
            plannedTargetNode = FindClosestCampNode();
            return;
        }
        plannedTargetNode = CalculateBestNeighborNode();
    }

    // ================= FASE 2: ACTUAR =================

    private void HandleClimberTurnStart()
    {
        if (reachedSummit || agent == null || externallyForcedDone) return;

        hasStartedThisTurn = true;
        hasEatenThisTurn = false;

        float now = Time.time;
        float delay = 0f;
        if (_globalNextMoveTime < now) _globalNextMoveTime = now;
        delay = _globalNextMoveTime - now;
        _globalNextMoveTime += 0.75f;

        StartCoroutine(ExecuteTurnDecisionWithDelay(delay));
    }

    private IEnumerator ExecuteTurnDecisionWithDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (this == null || externallyForcedDone || reachedSummit) yield break;

        if (plannedTargetNode != null)
        {
            MoveToNode(plannedTargetNode);
        }
        else
        {
            PlanNextMove();
            if (plannedTargetNode != null) MoveToNode(plannedTargetNode);
            else { isAtCamp = true; isActiveThisTurn = false; }
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

    // ================= HEURÍSTICA & UTILS =================
    // (Mantengo estos métodos colapsados porque no han cambiado, pero deben estar en el script)

    private CampGraphBuilder.CampNode FindClosestCampNode()
    {
        if (campGraph == null) campGraph = FindObjectOfType<CampGraphBuilder>();
        if (campGraph == null || campGraph.nodes == null) return null;
        float bestDist = float.PositiveInfinity;
        CampGraphBuilder.CampNode closest = null;
        Vector3 pos = transform.position;
        foreach (var node in campGraph.nodes)
        {
            float d = Vector3.Distance(pos, node.position);
            if (d < bestDist) { bestDist = d; closest = node; }
        }
        return closest;
    }

    private CampGraphBuilder.CampNode CalculateBestNeighborNode()
    {
        if (currentNode != null && nodeVisitCount.ContainsKey(currentNode.id) && nodeVisitCount[currentNode.id] >= maxVisitsToDie)
        {
            Destroy(gameObject); return null;
        }
        if (currentNode == null || currentNode.neighbors == null || currentNode.neighbors.Count == 0) return null;

        CampGraphBuilder.CampEdge bestAffordable = null;
        float bestAffordableScore = float.PositiveInfinity;
        CampGraphBuilder.CampEdge bestUnaffordable = null;
        float bestUnaffordableScore = float.PositiveInfinity;

        if (campGraph != null) campGraph.RecalculateObstaclesOnEdges();

        foreach (var edge in currentNode.neighbors)
        {
            bool canPassObstacle = true;
            if (edge.hasObstacle && edge.obstacleType != ObstacleType.None)
                canPassObstacle = (loadout != null) && loadout.CanHandleObstacle(edge.obstacleType);

            float effectiveWeight = edge.weight;
            if (edge.hasObstacle && canPassObstacle && campGraph != null)
                effectiveWeight -= (campGraph.obstaclePenalty * edge.obstacleCount);

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
            float randomNoise = UnityEngine.Random.Range(-noiseRange, noiseRange);
            float perceivedWeight = Mathf.Max(effectiveWeight + randomNoise, 0.1f);
            int steps = (edge.to != null) ? edge.to.stepsToSummit : 999;
            float finalScore = perceivedWeight + (steps * stepConversionFactor);

            if (CalculateStaminaCost(edge) <= currentStamina)
            {
                if (finalScore < bestAffordableScore) { bestAffordableScore = finalScore; bestAffordable = edge; }
            }
            else
            {
                float penaltyScore = finalScore + 10000f;
                if (penaltyScore < bestUnaffordableScore) { bestUnaffordableScore = penaltyScore; bestUnaffordable = edge; }
            }
        }
        return (bestAffordable != null ? bestAffordable : bestUnaffordable)?.to;
    }

    private void HandleReachedCamp()
    {
        if (targetNode != null) { currentNode = targetNode; targetNode = null; }
        isActiveThisTurn = false;
        isAtCamp = true;
        if (agent != null) { agent.isStopped = true; lastFramePosition = transform.position; lastFrameHeight = transform.position.y; }
        if (currentNode != null)
        {
            if (!nodeVisitCount.ContainsKey(currentNode.id)) nodeVisitCount[currentNode.id] = 0;
            nodeVisitCount[currentNode.id]++;
        }
        isGoingToFirstCamp = false;
        currentStamina = maxStamina;
        if (agent != null) agent.speed = originalSpeed;
        if (campGraph != null && currentNode != null && currentNode.id == campGraph.finalDestinationNodeId) HandleReachedGoal();
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;
        isActiveThisTurn = false;
        isAtCamp = false;
        if (agent != null) agent.isStopped = true;
        GameOverManager.Instance.SetGameOverCamera();
        Debug.Log("Cima alcanzada");
    }

    private void HandleClimberTurnEnd()
    {
        isActiveThisTurn = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    private float CalculateStaminaCost(CampGraphBuilder.CampEdge edge)
    {
        if (edge == null) return 0f;
        float distance = edge.pathLength;
        if (distance <= 0.01f) return minStaminaCost;
        float climb = Mathf.Max(edge.heightDelta, 0f);
        return Mathf.Max(distance * baseCostPerMeter * (1f + (climb / distance) * uphillExtraCostFactor), minStaminaCost);
    }

    public void SetExternalSpeedMultiplier(float multiplier) => externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    public float GetCurrentStamina() => currentStamina;
    public void SetCurrentStamina(float value) => currentStamina = Mathf.Clamp(value, 0f, maxStamina);
    public float GetMaxStamina() => maxStamina;
    public void ForceMoveToCampNode(CampGraphBuilder.CampNode node)
    {
        if (agent == null || node == null) return;
        isGoingToFirstCamp = false; isAtCamp = false; isActiveThisTurn = true; reachedSummit = false;
        targetNode = node; plannedTargetNode = node;
        agent.isStopped = false; agent.SetDestination(node.position); originalDestination = node.position;
        lastFramePosition = transform.position; lastFrameHeight = transform.position.y;
    }
    public void SetExternallyDoneThisTurn(bool value) { externallyForcedDone = value; if (value && agent != null) agent.isStopped = true; }
    public void AddMaxStamina(float amount) { maxStamina += amount; currentStamina = maxStamina; }
    public void StopForSeconds(float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        if (temporaryStopRoutine == null) _cachedMultiplierBeforeStop = externalSpeedMultiplier;
        _resumeTime = Mathf.Max(_resumeTime, Time.time + duration);
        if (temporaryStopRoutine == null) temporaryStopRoutine = StartCoroutine(StopLoop());
    }
    private IEnumerator StopLoop()
    {
        SetExternalSpeedMultiplier(0f);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        while (Time.time < _resumeTime) yield return null;
        SetExternalSpeedMultiplier(_cachedMultiplierBeforeStop);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        temporaryStopRoutine = null; _resumeTime = -1f;
    }
}