using UnityEngine;
using UnityEngine.AI;

public class ClimberMovement : MonoBehaviour
{
    public static ClimberMovement Instance { get; private set; }

    [Header("NavMesh")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Campamentos / Grafo")]
    [SerializeField] private CampGraphBuilder campGraph;

    [Header("Objetivo (Cima)")]
    [SerializeField] private Transform summit;

    [Header("Configuración llegada")]
    [SerializeField] private float reachedThreshold = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float baseCostPerMeter = 1f;
    [SerializeField] private float uphillExtraCostFactor = 2f;
    [SerializeField] private float minStaminaCost = 0.1f;

    [SerializeField] private float currentStamina;

    [Header("Equipamiento")]
    [SerializeField] private ClimberLoadout loadout;

    [Header("IA - Potencial hacia la cima")]
    [Tooltip("Cuánto influye la diferencia de potencial (distancia a la cima) en la decisión de camino.")]
    [SerializeField] private float potentialWeightFactor = 1f;

    private Vector3 lastFramePosition;
    private float lastFrameHeight;

    private float originalSpeed;

    private bool isActiveThisTurn = false;

    private CampGraphBuilder.CampNode currentNode;
    private CampGraphBuilder.CampNode targetNode;
    private CampGraphBuilder.CampNode lastNode;

    private bool reachedSummit = false;
    private bool isGoingToFirstCamp = true;

    private float externalSpeedMultiplier = 1f;

    private bool isAtCamp = false;
    private bool hasStartedThisTurn = false;

    public bool IsAtCamp => isAtCamp;
    public bool IsOutOfStamina => currentStamina <= 0f;
    public bool IsDoneThisTurn => hasStartedThisTurn && (IsAtCamp || IsOutOfStamina || reachedSummit);

    private void Awake()
    {
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
            GameObject targetObj = GameObject.Find("FinalDestination");
            if (targetObj != null)
                summit = targetObj.transform;
        }

        if (campGraph == null)
            campGraph = FindObjectOfType<CampGraphBuilder>();

        currentStamina = maxStamina;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        originalSpeed = agent.speed;

        isAtCamp = false;

        if (TurnManager.Instance != null && TurnManager.Instance.IsClimberTurn())
        {
            HandleClimberTurnStart();
        }
    }

    private void Update()
    {
        if (!isActiveThisTurn || agent == null || campGraph == null || reachedSummit)
            return;

        Vector3 currentPos = transform.position;
        float frameDistance = Vector3.Distance(currentPos, lastFramePosition);
        float heightDelta = currentPos.y - lastFrameHeight;

        if (frameDistance > 0f)
        {
            float uphill = Mathf.Max(heightDelta, 0f);
            float slope = uphill / frameDistance;

            float frameCost = frameDistance * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);
            frameCost = Mathf.Max(frameCost, 0f);

            currentStamina = Mathf.Max(0f, currentStamina - frameCost);
        }

        lastFramePosition = currentPos;
        lastFrameHeight = currentPos.y;

        if (currentStamina <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            agent.speed = originalSpeed * externalSpeedMultiplier;
        }

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
        {
            HandleReachedCamp();
        }
    }

    // ==============================
    // TURNOS
    // ==============================

    private void HandleClimberTurnStart()
    {
        if (reachedSummit) return;

        hasStartedThisTurn = true;

        // Actualizar rocas/aristas antes de elegir camino (también recalcula pesos y potenciales)
        if (campGraph != null)
            campGraph.RecalculateObstaclesOnEdges();

        if (isGoingToFirstCamp)
        {
            MoveToClosestCamp();
            return;
        }

        ChooseNextCampAndMove();
    }

    private void HandleClimberTurnEnd()
    {
        isActiveThisTurn = false;
        agent.isStopped = true;
    }

    // ==============================
    // MOVIMIENTO HACIA CAMPAMENTOS
    // ==============================

    private void MoveToClosestCamp()
    {
        if (campGraph == null || campGraph.nodes == null || campGraph.nodes.Count == 0)
        {
            isAtCamp = false;
            isActiveThisTurn = false;
            return;
        }

        CampGraphBuilder.CampNode closest = null;
        float bestDist = float.MaxValue;

        foreach (var node in campGraph.nodes)
        {
            float d = Vector3.SqrMagnitude(transform.position - node.position);
            if (d < bestDist)
            {
                bestDist = d;
                closest = node;
            }
        }

        if (closest == null)
        {
            isAtCamp = false;
            isActiveThisTurn = false;
            return;
        }

        targetNode = closest;
        isActiveThisTurn = true;
        isAtCamp = false;
        agent.isStopped = false;
        agent.SetDestination(closest.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    private float CalculateStaminaCost(CampGraphBuilder.CampEdge edge)
    {
        float length = Mathf.Max(edge.pathLength, 0.01f);
        float uphill = Mathf.Max(edge.heightDelta, 0f);
        float slope = uphill / length;

        float cost = length * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);
        return Mathf.Max(cost, minStaminaCost);
    }

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
            // Evitar ping-pong al nodo anterior si hay alternativas
            if (edge.to == lastNode && currentNode.neighbors.Count > 1)
                continue;

            bool canPassObstacle = true;

            // Si hay obstáculo y NO tengo equipo → descarto este camino
            if (edge.hasObstacle && edge.obstacleType != ObstacleType.None)
            {
                canPassObstacle = (loadout != null) && loadout.CanHandleObstacle(edge.obstacleType);

                if (!canPassObstacle)
                    continue;
            }

            // Peso base del grafo
            float effectiveWeight = edge.weight;

            // Si hay obstáculos y tengo equipo, resto el peso de esos obstáculos
            if (edge.hasObstacle && canPassObstacle && campGraph != null)
            {
                float obstaclesWeight = campGraph.obstaclePenalty * edge.obstacleCount;
                effectiveWeight -= obstaclesWeight;
            }

            // 🔵 Integrar potencial: moverse a nodos con potencial menor es más atractivo
            if (campGraph != null && currentNode != null)
            {
                float currentPot = currentNode.potential;
                float nextPot = edge.to.potential;

                if (!float.IsPositiveInfinity(currentPot) && !float.IsPositiveInfinity(nextPot))
                {
                    float deltaPot = nextPot - currentPot; // negativo = más cerca de la cima
                    effectiveWeight += deltaPot * potentialWeightFactor;
                }
            }

            // Evitar pesos negativos o cero
            effectiveWeight = Mathf.Max(effectiveWeight, 0.01f);

            // Coste de estamina de este camino
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

        lastNode = currentNode;
        targetNode = chosen.to;

        isActiveThisTurn = true;
        isAtCamp = false;
        agent.isStopped = false;
        agent.SetDestination(targetNode.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (debugLogs)
        {
            float currentPot = currentNode.potential;
            float nextPot = targetNode.potential;
            Debug.Log($"[ClimberMovement] Camino elegido {lastNode.id} -> {targetNode.id}, " +
                      $"pesoEdge={chosen.weight:F1}, potActual={currentPot:F1}, potNext={nextPot:F1}");
        }
    }

    // ==============================
    // LLEGADA A CAMPAMENTO / CIMA
    // ==============================

    private void HandleReachedCamp()
    {
        if (targetNode == null)
        {
            isActiveThisTurn = false;
            agent.isStopped = true;
            isAtCamp = true;
            return;
        }

        currentNode = targetNode;
        targetNode = null;
        isActiveThisTurn = false;
        agent.isStopped = true;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (isGoingToFirstCamp)
        {
            isGoingToFirstCamp = false;
            lastNode = null;
        }

        currentStamina = maxStamina;
        agent.speed = originalSpeed;
        isAtCamp = true;

        if (currentNode.id == campGraph.finalDestinationNodeId)
            HandleReachedGoal();
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;
        isActiveThisTurn = false;
        agent.isStopped = true;

        Destroy(gameObject);
    }

    // ==============================
    // EXTRAS
    // ==============================

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    public void SetCurrentStamina(float value)
    {
        currentStamina = Mathf.Clamp(value, 0f, maxStamina);
    }

    public float GetMaxStamina()
    {
        return maxStamina;
    }
}
