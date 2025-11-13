using UnityEngine;
using UnityEngine.AI;

public class ClimberMovement : MonoBehaviour
{
    [Header("NavMesh")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Configuración")]
    [Tooltip("Margen adicional para considerar que ha llegado al destino.")]
    [SerializeField] private float reachedThreshold = 0.2f;

    private Transform finalDestination;
    private bool isActiveThisTurn = false;

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
        // Buscar SIEMPRE el destino por nombre
        GameObject targetObj = GameObject.Find("FinalDestination");
        if (targetObj != null)
        {
            finalDestination = targetObj.transform;
        }
        else
        {
            Debug.LogError("[ClimberMovement] No se encontró un objeto llamado 'FinalDestination' en la escena.");
        }

        // Respetar el estado del turno actual
        if (TurnManager.Instance != null && TurnManager.Instance.IsClimberTurn())
        {
            StartMoving();
        }
        else
        {
            StopMoving();
        }
    }

    private void Update()
    {
        if (!isActiveThisTurn || agent == null || finalDestination == null)
            return;

        // Recalcular path si se ha perdido (por obstáculos nuevos)
        if (!agent.hasPath && !agent.pathPending)
        {
            agent.SetDestination(finalDestination.position);
        }

        // Comprobar si ha llegado
        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + reachedThreshold)
        {
            HandleReachedGoal();
        }
    }

    private void StartMoving()
    {
        if (agent == null || finalDestination == null)
            return;

        isActiveThisTurn = true;
        agent.isStopped = false;
        agent.SetDestination(finalDestination.position);
    }

    private void StopMoving()
    {
        isActiveThisTurn = false;
        if (agent != null)
            agent.isStopped = true;
    }

    private void HandleClimberTurnStart()
    {
        StartMoving();
    }

    private void HandleClimberTurnEnd()
    {
        StopMoving();
    }

    private void HandleReachedGoal()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver("Un escalador ha alcanzado la cima.");
        }

        Destroy(gameObject);
    }
}
