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

    // --- NUEVO: Variable para morir si se repite mucho ---
    [Tooltip("Número de veces que puede visitar un mismo campamento antes de morir por cansancio mental/aburrimiento.")]
    [SerializeField] private int maxVisitsToDie = 3;
    // ----------------------------------------------------

    [Header("Penalización por alejarse de la cima")]
    [Tooltip("Metros de alejamiento de la cima permitidos sin penalización.")]
    [SerializeField] private float backtrackTolerance = 2f;

    [Tooltip("Peso extra por cada metro de alejamiento de la cima más allá de la tolerancia.")]
    [SerializeField] private float backtrackPenaltyPerMeter = 5f;

    [Header("Estrategia IA")]
    [Tooltip("Factor de Conversión (K): Cuánta estamina (peso) vale 1 paso. Recomendado: 15-25.")]
    [SerializeField] private float stepConversionFactor = 20f;

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
    public Vector3 originalDestination;
    [HideInInspector] public bool hasEatenThisTurn = false;

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

        if (currentStamina <= 0f)
        {
            isActiveThisTurn = false;
            isAtCamp = false;
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

    // ================= TURNOS =================

    private void HandleClimberTurnStart()
    {
        if (reachedSummit || agent == null)
            return;

        if (externallyForcedDone)
            return;

        hasStartedThisTurn = true;
        hasEatenThisTurn = false;

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

    // --------------------------------------------------------------------------------------
    // ESTE ES EL MÉTODO MODIFICADO SEGÚN TUS INSTRUCCIONES
    // --------------------------------------------------------------------------------------
    private void ChooseNextCampAndMove()
    {
        // 1. CHEQUEO DE MUERTE POR REPETICIÓN
        // Si ya hemos estado en este nodo muchas veces, morimos.
        if (currentNode != null && nodeVisitCount.ContainsKey(currentNode.id))
        {
            if (nodeVisitCount[currentNode.id] >= maxVisitsToDie)
            {
                Destroy(gameObject);
                return;
            }
        }

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
        float bestAffordableScore = float.PositiveInfinity;

        CampGraphBuilder.CampEdge bestUnaffordable = null;
        float bestUnaffordableScore = float.PositiveInfinity;

        foreach (var edge in currentNode.neighbors)
        {
            // --- CAMBIO: ELIMINAMOS EL BLOQUEO ---
            // Ya no hacemos 'continue' si hay obstáculo. Siempre se evalúa el camino.
            // Simplemente comprobamos si tenemos la herramienta para saber si descontamos peso o no.
            bool hasTool = !edge.hasObstacle || (loadout != null && loadout.CanHandleObstacle(edge.obstacleType));

            // --- CÁLCULO DEL PESO ---
            float effectiveWeight = edge.weight;

            // Si hay obstáculo y TENEMOS herramienta, restamos la penalización del grafo.
            // Si NO tenemos herramienta, no restamos nada (se queda el peso alto del grafo), pero PODEMOS PASAR.
            if (edge.hasObstacle && hasTool && campGraph != null)
            {
                float obstaclesWeight = campGraph.obstaclePenalty * edge.obstacleCount;
                effectiveWeight -= obstaclesWeight;
            }

            // Memoria propia
            int myVisits = nodeVisitCount.ContainsKey(edge.to.id) ? nodeVisitCount[edge.to.id] : 0;
            effectiveWeight += myVisits * revisitPenaltyPerVisit;

            // Backtracking
            if (summit != null && currentNode != null && edge.to != null)
            {
                float distNow = Vector3.Distance(currentNode.position, summit.position);
                float distNext = Vector3.Distance(edge.to.position, summit.position);
                float approach = distNow - distNext;

                if (approach < -backtrackTolerance)
                {
                    float backtrackAmount = -approach - backtrackTolerance;
                    float backtrackPenalty = backtrackAmount * backtrackPenaltyPerMeter;
                    effectiveWeight += backtrackPenalty;
                }
            }

            effectiveWeight = Mathf.Max(effectiveWeight, 0.01f);

            // Heurística con Pasos
            int steps = (edge.to != null) ? edge.to.stepsToSummit : 999;
            float finalScore = effectiveWeight + (steps * stepConversionFactor);

            // Estamina
            float staminaCost = CalculateStaminaCost(edge);
            bool affordable = staminaCost <= currentStamina;

            if (affordable)
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

        CampGraphBuilder.CampEdge chosen = bestAffordable != null ? bestAffordable : bestUnaffordable;

        if (chosen == null)
        {
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
    // --------------------------------------------------------------------------------------

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

    private void HandleReachedCamp()
    {
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

        if (currentNode != null)
        {
            if (!nodeVisitCount.ContainsKey(currentNode.id))
                nodeVisitCount[currentNode.id] = 0;

            nodeVisitCount[currentNode.id]++;
        }

        if (isGoingToFirstCamp)
        {
            isGoingToFirstCamp = false;
        }

        currentStamina = maxStamina;
        if (agent != null)
            agent.speed = originalSpeed;

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

        Destroy(gameObject);
    }

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

        isGoingToFirstCamp = false;
        isAtCamp = false;
        isActiveThisTurn = true;
        reachedSummit = false;

        targetNode = node;

        agent.isStopped = false;
        agent.SetDestination(node.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    public void SetExternallyDoneThisTurn(bool value)
    {
        externallyForcedDone = value;

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