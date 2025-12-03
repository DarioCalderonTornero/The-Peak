using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

public class ClimberMovement : MonoBehaviour
{
    public static ClimberMovement Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CampGraphBuilder campGraph;
    [SerializeField] private Transform summit;
    [SerializeField] private ClimberLoadout loadout;

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
    [Tooltip("Penalización extra de peso por CADA visita previa a un campamento.")]
    [SerializeField] private float revisitPenaltyPerVisit = 5f;

    [Header("Penalización por alejarse de la cima")]
    [Tooltip("Metros de alejamiento de la cima permitidos sin penalización.")]
    [SerializeField] private float backtrackTolerance = 2f;

    [Tooltip("Peso extra por cada metro de alejamiento de la cima más allá de la tolerancia.")]
    [SerializeField] private float backtrackPenaltyPerMeter = 5f;

    // Estado de estamina
    [SerializeField] private float currentStamina;

    // Estado de movimiento
    private Vector3 lastFramePosition;
    private float lastFrameHeight;
    private float originalSpeed;

    // Estado del grafo
    private CampGraphBuilder.CampNode currentNode;
    private CampGraphBuilder.CampNode targetNode;

    // Estado de turnos
    private bool isActiveThisTurn = false;
    private bool isAtCamp = false;
    private bool isGoingToFirstCamp = true;
    private bool reachedSummit = false;
    private bool hasStartedThisTurn = false;

    // Memoria interna: cuántas veces he pasado por cada campamento
    // key = nodeId, value = visitas
    private Dictionary<int, int> nodeVisitCount = new Dictionary<int, int>();

    private bool externallyForcedDone = false;

    public bool IsAtCamp => isAtCamp;
    public bool IsOutOfStamina => currentStamina <= 0f;
    public bool IsDoneThisTurn => externallyForcedDone || isAtCamp || reachedSummit || IsOutOfStamina;

    public bool isEating = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (loadout == null)
            loadout = GetComponent<ClimberLoadout>();
    }

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnStart += HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd += HandleClimberTurnEnd;
        }
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnStart -= HandleClimberTurnStart;
            TurnManager.Instance.OnClimberTurnEnd -= HandleClimberTurnEnd;
        }
    }

    private void Start()
    {
        if (summit == null)
        {
            GameObject summitGO = GameObject.Find("FinalDestination");
            if (summitGO != null)
                summit = summitGO.transform;
        }

        if (campGraph == null)
            campGraph = FindObjectOfType<CampGraphBuilder>();

        currentStamina = maxStamina;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (agent != null)
        {
            originalSpeed = agent.speed;
            agent.isStopped = true;
        }

        isAtCamp = false;
        reachedSummit = false;

        // Si el turno de escaladores ya está en marcha cuando aparece este escalador
        if (TurnManager.Instance != null && TurnManager.Instance.IsClimberTurn())
        {
            HandleClimberTurnStart();
        }
    }

    private void Update()
    {
        if (!isActiveThisTurn || reachedSummit || agent == null)
            return;

        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        // 1) Cálculo de distancia recorrida y pendiente para gasto de estamina
        Vector3 currentPos = transform.position;
        float frameDistance = Vector3.Distance(currentPos, lastFramePosition);
        float frameHeight = currentPos.y;
        float heightDelta = frameHeight - lastFrameHeight;

        if (frameDistance > 0.0001f)
        {
            float uphill = Mathf.Max(heightDelta, 0f);
            float slope = uphill / frameDistance;

            float frameCost =
                frameDistance * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);

            frameCost = Mathf.Max(frameCost, minStaminaCost * Time.deltaTime);

            currentStamina = Mathf.Max(0f, currentStamina - frameCost);

            lastFramePosition = currentPos;
            lastFrameHeight = frameHeight;
        }

        // 2) Sin estamina → destruido (por ahora)
        if (currentStamina <= 0f)
        {
            isActiveThisTurn = false;
            isAtCamp = false;
            Destroy(gameObject);
            return;
        }

        // 3) Aplicar multiplicador de velocidad externo
        agent.speed = originalSpeed * externalSpeedMultiplier;

        // 4) Comprobar llegada al destino actual
        if (!agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
            {
                HandleReachedCamp();
            }
        }
    }

    // ================= TURNOS =================

    private void HandleClimberTurnStart()
    {
        if (reachedSummit || agent == null)
            return;

        // Si alguna defensa nos ha marcado como "done", este escalador no actúa este turno.
        if (externallyForcedDone)
            return;

        hasStartedThisTurn = true;

        // Recalcular obstáculos del grafo antes de decidir
        if (campGraph != null)
            campGraph.RecalculateObstaclesOnEdges();

        if (isGoingToFirstCamp || currentNode == null)
        {
            MoveToClosestCamp();
        }
        else
        {
            ChooseNextCampAndMove();
        }
    }

    private void HandleClimberTurnEnd()
    {
        isActiveThisTurn = false;
        if (agent != null)
            agent.isStopped = true;
    }

    // ============= LÓGICA DE MOVIMIENTO =============

    private void MoveToClosestCamp()
    {
        if (campGraph == null || campGraph.nodes == null || campGraph.nodes.Count == 0)
        {
            isAtCamp = true;
            isActiveThisTurn = false;
            return;
        }

        float bestDist = float.PositiveInfinity;
        CampGraphBuilder.CampNode closest = null;

        Vector3 pos = transform.position;

        foreach (var node in campGraph.nodes)
        {
            float d = Vector3.Distance(pos, node.position);
            if (d < bestDist)
            {
                bestDist = d;
                closest = node;
            }
        }

        if (closest == null)
        {
            isAtCamp = true;
            isActiveThisTurn = false;
            return;
        }

        targetNode = closest;
        isGoingToFirstCamp = true;

        isActiveThisTurn = true;
        isAtCamp = false;

        agent.isStopped = false;
        agent.SetDestination(targetNode.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    /// <summary>
    /// Elige el siguiente camino según:
    /// - Peso del grafo (edge.weight): más ligero = mejor.
    /// - Si tiene equipo para el obstáculo, se resta el peso de esos obstáculos.
    /// - Penalización extra por ir a campamentos muy visitados (memoria anti-bucles).
    /// - Penalización por alejarse demasiado de la cima (backtrack).
    /// - Se priorizan caminos que puede pagar con estamina; si no, el más ligero de los no asequibles.
    /// </summary>
    private void ChooseNextCampAndMove()
    {
        if (currentNode == null)
        {
            isAtCamp = true;
            isActiveThisTurn = false;
            return;
        }

        if (currentNode.neighbors == null || currentNode.neighbors.Count == 0)
        {
            isAtCamp = true;
            isActiveThisTurn = false;
            return;
        }

        CampGraphBuilder.CampEdge bestAffordable = null;
        float bestAffordableWeight = float.PositiveInfinity;

        CampGraphBuilder.CampEdge bestUnaffordable = null;
        float bestUnaffordableWeight = float.PositiveInfinity;

        foreach (var edge in currentNode.neighbors)
        {
            bool canPassObstacle = true;

            // Si hay obstáculo y NO tengo equipo → descarto el camino
            if (edge.hasObstacle && edge.obstacleType != ObstacleType.None)
            {
                canPassObstacle = (loadout != null) && loadout.CanHandleObstacle(edge.obstacleType);

                if (!canPassObstacle)
                    continue;
            }

            // 1) Peso base del grafo
            float effectiveWeight = edge.weight;

            // 2) Si hay obstáculos y tengo equipo, resto el peso de esos obstáculos
            if (edge.hasObstacle && canPassObstacle && campGraph != null)
            {
                float obstaclesWeight = campGraph.obstaclePenalty * edge.obstacleCount;
                effectiveWeight -= obstaclesWeight;
            }

            // 3) Penalización por campamentos visitados (memoria anti-bucles)
            if (edge.to != null && nodeVisitCount.TryGetValue(edge.to.id, out int visits) && visits > 0)
            {
                effectiveWeight += visits * revisitPenaltyPerVisit;
            }

            // 4) Penalización por alejarse de la cima (backtrack)
            if (summit != null && currentNode != null && edge.to != null)
            {
                float distNow = Vector3.Distance(currentNode.position, summit.position);
                float distNext = Vector3.Distance(edge.to.position, summit.position);
                float approach = distNow - distNext; // >0 = me acerco, <0 = me alejo

                if (approach < -backtrackTolerance)
                {
                    float backtrackAmount = -approach - backtrackTolerance; // cuánto me alejo de verdad
                    float backtrackPenalty = backtrackAmount * backtrackPenaltyPerMeter;
                    effectiveWeight += backtrackPenalty;
                }
            }

            // Evitar pesos negativos o 0
            effectiveWeight = Mathf.Max(effectiveWeight, 0.01f);

            // 5) Coste real de estamina de este camino
            float staminaCost = CalculateStaminaCost(edge);
            bool affordable = staminaCost <= currentStamina;

            if (affordable)
            {
                if (effectiveWeight < bestAffordableWeight)
                {
                    bestAffordableWeight = effectiveWeight;
                    bestAffordable = edge;
                }
            }
            else
            {
                if (effectiveWeight < bestUnaffordableWeight)
                {
                    bestUnaffordableWeight = effectiveWeight;
                    bestUnaffordable = edge;
                }
            }
        }

        // Preferimos el camino más ligero que pueda pagar con estamina;
        // si no hay ninguno asequible, cogemos el más ligero de los no asequibles.
        CampGraphBuilder.CampEdge chosen =
            bestAffordable != null ? bestAffordable : bestUnaffordable;

        if (chosen == null)
        {
            // No hay caminos válidos desde este campamento
            isAtCamp = true;
            isActiveThisTurn = false;
            return;
        }

        targetNode = chosen.to;

        isActiveThisTurn = true;
        isAtCamp = false;
        agent.isStopped = false;
        agent.SetDestination(targetNode.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    private float CalculateStaminaCost(CampGraphBuilder.CampEdge edge)
    {
        if (edge == null)
            return 0f;

        float distance = edge.pathLength;
        if (distance <= 0.01f)
            return minStaminaCost;

        float climb = Mathf.Max(edge.heightDelta, 0f);
        float slope = climb / distance;

        float cost = distance * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);
        cost = Mathf.Max(cost, minStaminaCost);
        return cost;
    }

    // ============= LLEGADA A CAMPAMENTO / CIMA =============

    private void HandleReachedCamp()
    {
        // Llegada a un punto intermedio (campamento)
        if (targetNode != null)
        {
            currentNode = targetNode;
            targetNode = null;
        }

        isActiveThisTurn = false;
        isAtCamp = true;

        if (agent != null)
        {
            agent.isStopped = true;
            lastFramePosition = transform.position;
            lastFrameHeight = transform.position.y;
        }

        // Registrar visita a este campamento (memoria interna)
        if (currentNode != null)
        {
            if (!nodeVisitCount.ContainsKey(currentNode.id))
                nodeVisitCount[currentNode.id] = 0;

            nodeVisitCount[currentNode.id]++;
        }

        // Si era el primer campamento, a partir de ahora trabajamos solo con el grafo
        if (isGoingToFirstCamp)
        {
            isGoingToFirstCamp = false;
        }

        // Recargar estamina al llegar a campamento
        currentStamina = maxStamina;
        if (agent != null)
            agent.speed = originalSpeed;

        // ¿Hemos llegado a la cima?
        if (campGraph != null &&
            currentNode != null &&
            currentNode.id == campGraph.finalDestinationNodeId)
        {
            HandleReachedGoal();
        }
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;
        isActiveThisTurn = false;
        isAtCamp = false;

        if (agent != null)
            agent.isStopped = true;

        // Aquí podrías notificar al GameManager / TurnManager

        Destroy(gameObject);
    }

    // ============= UTILIDADES =============

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public float GetCurrentStamina() => currentStamina;
    public void SetCurrentStamina(float value) => currentStamina = Mathf.Clamp(value, 0f, maxStamina);
    public float GetMaxStamina() => maxStamina;

    public void ForceMoveToCampNode(CampGraphBuilder.CampNode node)
    {
        if (agent == null || node == null)
            return;

        // Queremos que a partir de ahora funcione como si ya estuviera “en el grafo”
        isGoingToFirstCamp = false;
        isAtCamp = false;
        isActiveThisTurn = true;
        reachedSummit = false;

        targetNode = node;

        agent.isStopped = false;
        agent.SetDestination(node.position);

        // Reset de referencias para el cálculo de estamina
        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    public void SetExternallyDoneThisTurn(bool value)
    {
        externallyForcedDone = value;

        // Por seguridad, si lo marcamos como "done", paramos el agent
        if (value && agent != null)
        {
            agent.isStopped = true;
        }
    }

    public void AddMaxStamina(float amount)
    {
        maxStamina += amount;
        currentStamina = maxStamina;
    }
}
