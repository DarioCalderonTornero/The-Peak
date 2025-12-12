using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections; // Necesario para IEnumerator
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

    [Tooltip("Si el escalador visita un mismo campamento esta cantidad de veces o más, muere.")]
    [SerializeField] private int maxVisitsToDie = 3;

    [Header("Penalización por alejarse de la cima")]
    [Tooltip("Metros de alejamiento de la cima permitidos sin penalización.")]
    [SerializeField] private float backtrackTolerance = 2f;

    [Tooltip("Peso extra por cada metro de alejamiento de la cima más allá de la tolerancia.")]
    [SerializeField] private float backtrackPenaltyPerMeter = 5f;

    [Header("Estrategia IA")]
    [Tooltip("Factor de Conversión (K): Cuánta estamina (peso) vale 1 paso. Recomendado: 15-25.")]
    [SerializeField] private float stepConversionFactor = 20f;

    [SerializeField] private float noiseRange = 50f;

    // --- NUEVO: Variable estática para coordinar la salida escalonada ---
    private static float _globalNextMoveTime = 0f;
    // ------------------------------------------------------------------

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

    // Memoria interna
    private Dictionary<int, int> nodeVisitCount = new Dictionary<int, int>();

    private bool externallyForcedDone = false;

    public bool IsAtCamp => isAtCamp;
    public bool IsOutOfStamina => currentStamina <= 0f;
    public bool IsDoneThisTurn => externallyForcedDone || isAtCamp || reachedSummit || IsOutOfStamina;

    public bool isEating = false;
    public Vector3 originalDestination;
    [HideInInspector] public bool hasEatenThisTurn = false;

    [Header("Equipment related")]
    private Coroutine temporaryStopRoutine;


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

        // --- MODIFICADO: Solución para que se muevan nada más empezar ---
        // Iniciamos una corutina que espera un frame. Esto asegura que el 
        // CampGraphBuilder haya terminado su Start() y el grafo exista.
        StartCoroutine(StartLateCheck());
    }

    private IEnumerator StartLateCheck()
    {
        // Esperamos al final del frame para asegurar que todos los Start() han corrido
        yield return new WaitForEndOfFrame();

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
        if (reachedSummit || agent == null) return;
        if (externallyForcedDone) return;

        hasStartedThisTurn = true;
        hasEatenThisTurn = false;

        if (campGraph != null)
            campGraph.RecalculateObstaclesOnEdges();

        // --- NUEVO: Lógica de salida escalonada (0.75s) ---
        float now = Time.time;
        float delay = 0f;

        // Si el "tiempo del siguiente" quedó en el pasado, lo traemos al presente
        if (_globalNextMoveTime < now)
        {
            _globalNextMoveTime = now;
        }

        // Mi retraso es la diferencia entre el turno global y ahora
        delay = _globalNextMoveTime - now;

        // Empujamos el turno global 0.75s hacia el futuro para el siguiente escalador
        _globalNextMoveTime += 0.75f;

        // Iniciamos la decisión con ese retraso
        StartCoroutine(ExecuteTurnDecisionWithDelay(delay));
    }

    // Corutina para esperar el turno de salida
    private IEnumerator ExecuteTurnDecisionWithDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // Verificamos que sigo vivo/activo tras la espera
        if (this == null || externallyForcedDone || reachedSummit)
            yield break;

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
            // Intentamos buscar el grafo de nuevo por si acaso
            campGraph = FindObjectOfType<CampGraphBuilder>();
            if (campGraph == null || campGraph.nodes == null || campGraph.nodes.Count == 0)
            {
                isAtCamp = true;
                isActiveThisTurn = false;
                return;
            }
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

        originalDestination = targetNode.position;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    private void ChooseNextCampAndMove()
    {
        // 1. COMPROBACIÓN DE MUERTE POR REPETICIÓN
        if (currentNode != null && nodeVisitCount.ContainsKey(currentNode.id))
        {
            if (nodeVisitCount[currentNode.id] >= maxVisitsToDie)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (currentNode == null || currentNode.neighbors == null || currentNode.neighbors.Count == 0)
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
            // 2. OBSTÁCULOS (No bloquean, solo se consultan para el peso)
            bool canPassObstacle = true;
            if (edge.hasObstacle && edge.obstacleType != ObstacleType.None)
            {
                canPassObstacle = (loadout != null) && loadout.CanHandleObstacle(edge.obstacleType);
            }

            // Cálculo de Peso Base
            float effectiveWeight = edge.weight;

            // Si tengo equipo, descuento el peso del obstáculo. Si no, me lo como.
            if (edge.hasObstacle && canPassObstacle && campGraph != null)
            {
                float obstaclesWeight = campGraph.obstaclePenalty * edge.obstacleCount;
                effectiveWeight -= obstaclesWeight;
            }

            // Memoria Propia
            int myVisits = nodeVisitCount.ContainsKey(edge.to.id) ? nodeVisitCount[edge.to.id] : 0;
            effectiveWeight += myVisits * revisitPenaltyPerVisit;

            // Penalización Backtrack
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

            // Aseguramos que el peso base sea positivo
            effectiveWeight = Mathf.Max(effectiveWeight, 0.01f);


            // Generamos el valor aleatorio (positivo o negativo)
            float randomNoise = UnityEngine.Random.Range(-noiseRange, noiseRange);

            // El peso percibido es la realidad + su confusión
            float perceivedWeight = effectiveWeight + randomNoise;

            // SEGURIDAD: Evitar que el peso sea 0 o negativo, lo cual rompería la IA
            perceivedWeight = Mathf.Max(perceivedWeight, 0.1f);

            // =================================================================

            // Heurística con Pasos
            // [CAMBIO]: Usamos 'perceivedWeight' en lugar de 'effectiveWeight' para el cálculo
            int steps = (edge.to != null) ? edge.to.stepsToSummit : 999;

            float finalScore = perceivedWeight + (steps * stepConversionFactor);

            // Estamina
            // [IMPORTANTE]: La estamina se calcula con la realidad física (edge), no con la percepción.
            // Así no intentan entrar en caminos que físicamente no pueden pagar.
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

        originalDestination = targetNode.position;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    private float CalculateStaminaCost(CampGraphBuilder.CampEdge edge)
    {
        if (edge == null) return 0f;
        float distance = edge.pathLength;
        if (distance <= 0.01f) return minStaminaCost;
        float climb = Mathf.Max(edge.heightDelta, 0f);
        float slope = climb / distance;
        float cost = distance * baseCostPerMeter * (1f + slope * uphillExtraCostFactor);
        return Mathf.Max(cost, minStaminaCost);
    }

    private void HandleReachedCamp()
    {
        if (targetNode != null) { currentNode = targetNode; targetNode = null; }

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
            if (!nodeVisitCount.ContainsKey(currentNode.id)) nodeVisitCount[currentNode.id] = 0;
            nodeVisitCount[currentNode.id]++;
        }

        if (isGoingToFirstCamp) isGoingToFirstCamp = false;
        currentStamina = maxStamina;
        if (agent != null) agent.speed = originalSpeed;

        if (campGraph != null && currentNode != null && currentNode.id == campGraph.finalDestinationNodeId)
        {
            HandleReachedGoal();
        }
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;
        isActiveThisTurn = false;
        isAtCamp = false;
        if (agent != null) agent.isStopped = true;
        Destroy(gameObject);
    }

    public void SetExternalSpeedMultiplier(float multiplier) { externalSpeedMultiplier = Mathf.Max(0f, multiplier); }
    public float GetCurrentStamina() => currentStamina;
    public void SetCurrentStamina(float value) => currentStamina = Mathf.Clamp(value, 0f, maxStamina);
    public float GetMaxStamina() => maxStamina;

    public void ForceMoveToCampNode(CampGraphBuilder.CampNode node)
    {
        if (agent == null || node == null) return;
        isGoingToFirstCamp = false;
        isAtCamp = false;
        isActiveThisTurn = true;
        reachedSummit = false;
        targetNode = node;
        agent.isStopped = false;
        agent.SetDestination(node.position);

        // 🔹 NUEVO:
        originalDestination = node.position;

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    public void SetExternallyDoneThisTurn(bool value)
    {
        externallyForcedDone = value;
        if (value && agent != null) agent.isStopped = true;
    }

    public void AddMaxStamina(float amount)
    {
        maxStamina += amount;
        currentStamina = maxStamina;
    }

    public void StopForSeconds(float duration)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (temporaryStopRoutine != null)
        {
            StopCoroutine(temporaryStopRoutine);
        }

        temporaryStopRoutine = StartCoroutine(StopForSecondsRoutine(duration));
    }

    private IEnumerator StopForSecondsRoutine(float duration)
    {
        float previousMultiplier = externalSpeedMultiplier;

        SetExternalSpeedMultiplier(0f);
        if (agent != null)
            agent.isStopped = true;

        yield return new WaitForSeconds(duration);

        SetExternalSpeedMultiplier(previousMultiplier);
        if (agent != null)
            agent.isStopped = false;

        temporaryStopRoutine = null;
    }
}