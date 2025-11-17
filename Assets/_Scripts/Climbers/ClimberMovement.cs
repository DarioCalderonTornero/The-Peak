using UnityEngine;
using UnityEngine.AI;

public class ClimberMovement : MonoBehaviour
{
    [Header("NavMesh")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Campamentos / Grafo")]
    [SerializeField] private CampGraphBuilder campGraph;

    [Header("Objetivo (Cima)")]
    [SerializeField] private Transform summit;

    [Header("Configuración llegada")]
    [SerializeField] private float reachedThreshold = 0.2f;

    [Header("Capabilities (equipamiento)")]
    [SerializeField] private ClimberCapabilities capabilities;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float baseCostPerMeter = 1f;
    [SerializeField] private float uphillExtraCostFactor = 2f;
    [SerializeField] private float minStaminaCost = 0.1f;

    [SerializeField] private float currentStamina;

    private Vector3 lastFramePosition;
    private float lastFrameHeight;

    private float originalSpeed;

    // Estado de turnos
    private bool isActiveThisTurn = false;

    // Estado del grafo
    private CampGraphBuilder.CampNode currentNode;
    private CampGraphBuilder.CampNode targetNode;
    private CampGraphBuilder.CampNode lastNode;

    // Estado general
    private bool reachedSummit = false;
    private bool isGoingToFirstCamp = true;
    
    // --- Modificadores de velocidad (zarzas, etc.) ---
    private float currentSpeedMultiplier = 1f;
    private int slowZoneCount = 0;


    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (capabilities == null)
            capabilities = GetComponent<ClimberCapabilities>();
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
        // Buscar la cima si no se ha asignado
        if (summit == null)
        {
            GameObject targetObj = GameObject.Find("FinalDestination");
            if (targetObj != null)
                summit = targetObj.transform;
        }

        // Buscar automáticamente el CampGraphBuilder si no se ha asignado nada
        if (campGraph == null)
        {
#if UNITY_2023_1_OR_NEWER
            campGraph = Object.FindFirstObjectByType<CampGraphBuilder>();
#else
            campGraph = FindObjectOfType<CampGraphBuilder>();
#endif
            if (campGraph == null)
            {
                Debug.LogError("[ClimberMovement] No se encontró ningún CampGraphBuilder en la escena.");
            }
        }

        currentStamina = maxStamina;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (agent != null)
            originalSpeed = agent.speed;
    }

    private void Update()
    {
        if (!isActiveThisTurn || agent == null || campGraph == null || reachedSummit)
            return;

        // --- Cálculo de gasto de estamina en movimiento frame a frame ---
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

        // Reducción de velocidad al quedarse sin estamina
        if (currentStamina <= 0f)
        {
            agent.speed = 0f;
        }
        else
        {
            agent.speed = originalSpeed * currentSpeedMultiplier;
        }

        // Comprobar llegada al campamento
        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
        {
            HandleReachedCamp();
        }
    }

    // -------------------------------------------------------
    //                LÓGICA DE TURNOS
    // -------------------------------------------------------

    private void HandleClimberTurnStart()
    {
        if (reachedSummit) return;

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
        if (agent != null)
            agent.isStopped = true;
    }

    // -------------------------------------------------------
    //    PRIMER MOVIMIENTO: IR AL CAMPAMENTO MÁS CERCANO
    // -------------------------------------------------------

    private void MoveToClosestCamp()
    {
        if (campGraph == null || campGraph.nodes == null || campGraph.nodes.Count == 0)
        {
            Debug.LogError("[ClimberMovement] No hay campamentos disponibles.");
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
            Debug.LogError("[ClimberMovement] No se ha podido encontrar ningún campamento cercano.");
            return;
        }

        targetNode = closest;
        isActiveThisTurn = true;
        agent.isStopped = false;
        agent.SetDestination(closest.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (debugLogs)
            Debug.Log($"{name} inicia el juego moviéndose al campamento más cercano: Camp {closest.id}");
    }

    // -------------------------------------------------------
    //    COSTE DE ESTAMINA POR EDGE
    // -------------------------------------------------------

    private float CalculateStaminaCost(CampGraphBuilder.CampEdge edge)
    {
        float length = Mathf.Max(edge.pathLength, 0.01f);
        float uphill = Mathf.Max(edge.heightDelta, 0f);
        float slope = uphill / length;

        float cost = length * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);
        return Mathf.Max(cost, minStaminaCost);
    }

    // -------------------------------------------------------
    //          MOVIMIENTOS NORMALES ENTRE CAMPAMENTOS
    // -------------------------------------------------------

    private void ChooseNextCampAndMove()
    {
        if (currentNode == null)
        {
            Debug.LogError("[ClimberMovement] currentNode es null al intentar elegir siguiente campamento.");
            return;
        }

        if (currentNode.neighbors == null || currentNode.neighbors.Count == 0)
        {
            if (debugLogs)
                Debug.LogWarning($"{name} está en un campamento sin vecinos (camp {currentNode.id}). No puede avanzar.");
            return;
        }

        CampGraphBuilder.CampEdge bestAffordable = null;
        float bestAffordableScore = float.NegativeInfinity;

        CampGraphBuilder.CampEdge bestUnaffordable = null;
        float bestUnaffordableScore = float.NegativeInfinity;

        bool hasSummit = (summit != null);
        float distNow = hasSummit
            ? Vector3.Distance(currentNode.position, summit.position)
            : 0f;

        const float minApproachGain = 0.05f;

        foreach (var edge in currentNode.neighbors)
        {
            // Evitar volver directamente al campamento anterior si hay otras opciones
            if (edge.to == lastNode && currentNode.neighbors.Count > 1)
                continue;

            // ⚠️ Comprobar obstáculos en este edge
            if (IsEdgeBlockedForThisClimber(edge))
            {
                if (debugLogs)
                {
                    Debug.Log($"{name} descarta edge {currentNode.id} -> {edge.to.id} " +
                              "porque tiene obstáculos que no puede manejar.");
                }
                continue;
            }

            float cost = CalculateStaminaCost(edge);

            float score;

            if (hasSummit)
            {
                float distNext = Vector3.Distance(edge.to.position, summit.position);
                float approach = distNow - distNext;

                score = (approach < -minApproachGain)
                    ? -9999f
                    : approach / Mathf.Max(cost, 0.01f);
            }
            else
            {
                // Si no hay cima, priorizamos caminos de menor coste
                score = -cost;
            }

            bool affordable = cost <= currentStamina;

            if (affordable)
            {
                if (score > bestAffordableScore)
                {
                    bestAffordableScore = score;
                    bestAffordable = edge;
                }
            }
            else
            {
                if (score > bestUnaffordableScore)
                {
                    bestUnaffordableScore = score;
                    bestUnaffordable = edge;
                }
            }
        }

        CampGraphBuilder.CampEdge chosen =
            bestAffordable != null ? bestAffordable : bestUnaffordable;

        if (chosen == null)
        {
            if (debugLogs)
                Debug.LogWarning($"{name} no ha encontrado un siguiente campamento al que moverse " +
                                 "(todos bloqueados, demasiado costosos o alejando demasiado de la cima).");
            return;
        }

        lastNode = currentNode;
        targetNode = chosen.to;

        isActiveThisTurn = true;
        agent.isStopped = false;
        agent.SetDestination(targetNode.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;

        if (debugLogs)
        {
            Debug.Log($"{name} se mueve de camp {lastNode.id} a camp {targetNode.id}. " +
                      $"Stamina actual: {currentStamina:F1}");
        }
    }

    /// <summary>
    /// Devuelve true si este edge está bloqueado para este escalador
    /// por tener obstáculos de tipos que NO puede manejar.
    /// </summary>
    private bool IsEdgeBlockedForThisClimber(CampGraphBuilder.CampEdge edge)
    {
        if (edge.blockingObstacles == null || edge.blockingObstacles.Count == 0)
            return false;

        // Si no hay capabilities, dejamos que pase (para prototipo).
        if (capabilities == null)
            return false;

        foreach (var marker in edge.blockingObstacles)
        {
            if (marker == null)
                continue;

            ObstacleType type = marker.obstacleType;

            // Si NO puede manejar este tipo, el edge se considera bloqueado para él.
            if (!capabilities.CanHandleObstacleType(type))
            {
                return true;
            }
        }

        return false;
    }

    // -------------------------------------------------------
    //            CUANDO LLEGA A UN CAMPAMENTO
    // -------------------------------------------------------

    private void HandleReachedCamp()
    {
        if (targetNode == null)
        {
            isActiveThisTurn = false;
            if (agent != null) agent.isStopped = true;
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

        // Recuperar estamina al llegar al campamento
        currentStamina = maxStamina;
        if (agent != null)
            agent.speed = originalSpeed;

        if (debugLogs)
            Debug.Log($"{name} ha llegado al campamento {currentNode.id}. Stamina restaurada a {currentStamina}.");

        if (campGraph != null && currentNode.id == campGraph.finalDestinationNodeId)
            HandleReachedGoal();
    }

    private void HandleReachedGoal()
    {
        if (reachedSummit)
            return;

        reachedSummit = true;
        isActiveThisTurn = false;

        if (agent != null)
            agent.isStopped = true;

        Debug.Log($"GAME OVER: {name} ha alcanzado la cima.");
        Destroy(gameObject);
    }

    public void EnterSlowZone(float factor)
    {
        slowZoneCount++;
        RecalculateSpeedMultiplier(factor);
    }

    public void ExitSlowZone(float factor)
    {
        slowZoneCount = Mathf.Max(0, slowZoneCount - 1);
        RecalculateSpeedMultiplier(factor);
    }

    private void RecalculateSpeedMultiplier(float factor)
    {
        if (slowZoneCount <= 0)
            currentSpeedMultiplier = 1f;
        else
            currentSpeedMultiplier = factor;   // 0.7f → 30% más lento
    }
}
