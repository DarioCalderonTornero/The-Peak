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

    private bool isActiveThisTurn = false;

    private CampGraphBuilder.CampNode currentNode;
    private CampGraphBuilder.CampNode targetNode;
    private CampGraphBuilder.CampNode lastNode;

    private bool reachedSummit = false;
    private bool isGoingToFirstCamp = true;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
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

        // Reducción de velocidad al quedarse sin estamina
        if (currentStamina <= 0f)
        {
            agent.speed = 0f;
        }
        else
        {
            agent.speed = originalSpeed;
        }

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
        {
            HandleReachedCamp();
        }
    }

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
        agent.isStopped = true;
    }

    private void MoveToClosestCamp()
    {
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

        targetNode = closest;
        isActiveThisTurn = true;
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
        CampGraphBuilder.CampEdge bestAffordable = null;
        float bestAffordableScore = float.NegativeInfinity;

        CampGraphBuilder.CampEdge bestUnaffordable = null;
        float bestUnaffordableScore = float.NegativeInfinity;

        float distNow = Vector3.Distance(currentNode.position, summit.position);
        const float minApproachGain = 0.05f;

        foreach (var edge in currentNode.neighbors)
        {
            if (edge.to == lastNode && currentNode.neighbors.Count > 1)
                continue;

            float cost = CalculateStaminaCost(edge);

            float distNext = Vector3.Distance(edge.to.position, summit.position);
            float approach = distNow - distNext;

            float score = (approach < -minApproachGain)
                ? -9999f
                : approach / Mathf.Max(cost, 0.01f);

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

        lastNode = currentNode;
        targetNode = chosen.to;

        isActiveThisTurn = true;
        agent.isStopped = false;
        agent.SetDestination(targetNode.position);

        lastFramePosition = transform.position;
        lastFrameHeight = transform.position.y;
    }

    private void HandleReachedCamp()
    {
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

        if (currentNode.id == campGraph.finalDestinationNodeId)
            HandleReachedGoal();
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;
        isActiveThisTurn = false;
        agent.isStopped = true;

        Debug.Log($"GAME OVER: {name} ha alcanzado la cima.");
        Destroy(gameObject);
    }
}
