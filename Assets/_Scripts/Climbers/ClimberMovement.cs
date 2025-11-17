using UnityEngine;
using UnityEngine.AI;

public class ClimberMovement : MonoBehaviour
{
    [Header("NavMesh")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Campamentos / Grafo")]
    [Tooltip("Se buscará automáticamente en la escena si está vacío.")]
    [SerializeField] private CampGraphBuilder campGraph;

    [Header("Objetivo (Cima)")]
    [Tooltip("Si está vacío, buscará un objeto llamado 'FinalDestination' en la escena.")]
    [SerializeField] private Transform summit;

    [Header("Configuración llegada")]
    [Tooltip("Margen adicional para considerar que ha llegado al campamento/destino.")]
    [SerializeField] private float reachedThreshold = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // Estado de turnos
    private bool isActiveThisTurn = false;

    // Estado del grafo
    private CampGraphBuilder.CampNode currentNode;   // campamento lógico donde está ahora
    private CampGraphBuilder.CampNode targetNode;    // campamento al que quiere ir este turno
    private CampGraphBuilder.CampNode lastNode;      // campamento anterior, para evitar ping-pong

    // Estado general
    private bool reachedSummit = false;
    private bool isGoingToFirstCamp = true;          // al principio va al campamento más cercano

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
        // 1) Buscar la cima si no se ha asignado
        if (summit == null)
        {
            GameObject targetObj = GameObject.Find("FinalDestination");
            if (targetObj != null)
            {
                summit = targetObj.transform;
            }
            else
            {
                Debug.LogWarning("[ClimberMovement] No se encontró un objeto llamado 'FinalDestination' en la escena (opcional).");
            }
        }

        // 2) Buscar automáticamente el CampGraphBuilder si no se ha asignado nada
        if (campGraph == null)
        {
            campGraph = Object.FindFirstObjectByType<CampGraphBuilder>();
            if (campGraph == null)
            {
                Debug.LogError("[ClimberMovement] No se encontró ningún CampGraphBuilder en la escena.");
            }
        }
    }

    private void Update()
    {
        if (!isActiveThisTurn || agent == null || campGraph == null || reachedSummit)
            return;

        // Comprobar si ha llegado al campamento objetivo
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

        // PRIMER TURNO ? ir al campamento más cercano desde su posición actual
        if (isGoingToFirstCamp)
        {
            MoveToClosestCamp();
            return;
        }

        // RESTO DE TURNOS ? moverse entre campamentos del grafo
        ChooseNextCampAndMove();
    }

    private void HandleClimberTurnEnd()
    {
        StopMoving();
    }

    private void StopMoving()
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

        // Moverse físicamente al campamento más cercano
        targetNode = closest;
        isActiveThisTurn = true;
        agent.isStopped = false;
        agent.SetDestination(closest.position);

        if (debugLogs)
            Debug.Log($"{name} inicia el juego moviéndose al campamento más cercano: Camp {closest.id}");
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

        CampGraphBuilder.CampEdge bestEdge = null;
        float bestScore = float.NegativeInfinity;

        float currentDistToSummit = summit != null
            ? Vector3.Distance(currentNode.position, summit.position)
            : 0f;

        const float minApproachGain = 0.05f; // margen para considerar que nos acercamos "de verdad"

        foreach (var edge in currentNode.neighbors)
        {
            // Evitar volver directamente al campamento anterior si hay otras opciones
            if (edge.to == lastNode && currentNode.neighbors.Count > 1)
                continue;

            float pathLength = Mathf.Max(edge.pathLength, 0.01f); // evitar división por cero
            float score = 0f;

            if (summit != null)
            {
                float distFromNext = Vector3.Distance(edge.to.position, summit.position);
                float approach = currentDistToSummit - distFromNext; // positivo si nos acercamos

                // Si nos alejamos claramente, penalizamos fuerte
                if (approach < -minApproachGain)
                {
                    score = -9999f; // solo se elegirá si no queda nada mejor
                }
                else
                {
                    // Rentabilidad = cuánto nos acercamos por metro recorrido
                    float profitability = approach / pathLength;
                    score = profitability;
                }
            }
            else
            {
                // Si no hay cima, simplemente preferimos el camino más corto
                score = -pathLength; // más corto = score más alto (menos negativo)
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestEdge = edge;
            }
        }

        if (bestEdge == null)
        {
            if (debugLogs)
                Debug.LogWarning($"{name} no ha encontrado un siguiente campamento al que moverse.");
            return;
        }

        // Guardar el campamento objetivo y el anterior para evitar ping-pong
        lastNode = currentNode;
        targetNode = bestEdge.to;

        if (debugLogs)
        {
            Debug.Log($"{name} se mueve de camp {currentNode.id} a camp {targetNode.id}. " +
                      $"Score: {bestScore:F3}");
        }

        isActiveThisTurn = true;
        agent.isStopped = false;
        agent.SetDestination(targetNode.position);
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

        // Actualizar nodo lógico actual
        currentNode = targetNode;
        targetNode = null;
        isActiveThisTurn = false;
        agent.isStopped = true;

        // Si era el primer movimiento (desde fuera de los campamentos)
        if (isGoingToFirstCamp)
        {
            isGoingToFirstCamp = false; // ya tiene su primer campamento real
            lastNode = null;            // aún no hay campamento previo
        }

        if (debugLogs)
            Debug.Log($"{name} ha llegado al campamento {currentNode.id} (altura {currentNode.height}).");

        // Comprobar si estamos lo bastante cerca de la cima
        if (summit != null)
        {
            float distToSummit = Vector3.Distance(transform.position, summit.position);
            if (distToSummit <= agent.stoppingDistance + reachedThreshold)
            {
                HandleReachedGoal();
            }
        }
    }

    private void HandleReachedGoal()
    {
        reachedSummit = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver("Un escalador ha alcanzado la cima.");
        }

        Destroy(gameObject);
    }
}
