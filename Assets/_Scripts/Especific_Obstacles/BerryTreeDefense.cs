using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BerryTreeDefense : BaseDefense
{
    [Header("Bayas (visuales, en posiciones distintas)")]
    public GameObject goodBerry;
    public GameObject badBerry;

    private bool goodBerryAlive = false;
    private bool badBerryAlive = false;

    [Header("Turnos")]
    [Tooltip("Turnos de escaladores para el primer crecimiento de ambas bayas.")]
    public int turnsToFirstGrow = 2;

    [Tooltip("Turnos de escaladores que tarda cada baya en volver a crecer tras ser comida.")]
    public int turnsToRespawn = 2;

    private int initialGrowCounter = 0;
    private bool initialGrowDone = false;

    [Header("Detección")]
    public float detectionRadius = 5f;
    public LayerMask climberLayer;

    [Header("Punto al que van a comer (opcional)")]
    public Transform eatPoint;

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnEnd += HandleGlobalClimberTurnEnd;
        }
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnEnd -= HandleGlobalClimberTurnEnd;
        }
    }

    private void Start()
    {
        // Siempre empezamos SIN bayas visibles
        if (goodBerry != null)
        {
            goodBerry.SetActive(false);
            goodBerryAlive = false;
        }

        if (badBerry != null)
        {
            badBerry.SetActive(false);
            badBerryAlive = false;
        }

        initialGrowCounter = 0;
        initialGrowDone = false;
    }

    // ================== Primer crecimiento de ambas bayas ==================

    private void HandleGlobalClimberTurnEnd()
    {
        if (initialGrowDone)
            return;

        initialGrowCounter++;

        if (initialGrowCounter >= turnsToFirstGrow)
        {
            SpawnBothBerriesFirstTime();
            initialGrowDone = true;
        }
    }

    private void SpawnBothBerriesFirstTime()
    {
        if (goodBerry != null)
        {
            goodBerry.SetActive(true);
            goodBerryAlive = true;
        }

        if (badBerry != null)
        {
            badBerry.SetActive(true);
            badBerryAlive = true;
        }

        Debug.Log("🌱 Primer crecimiento: han aparecido la baya buena y la mala.");
    }

    // ================== Lógica por frame ==================

    private void Update()
    {
        // Solo funcionan en turno de escaladores
        if (TurnManager.Instance == null || !TurnManager.Instance.IsClimberTurn())
            return;

        // Si no hay ninguna baya disponible, no hacemos nada
        if (!goodBerryAlive && !badBerryAlive)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, climberLayer);

        foreach (Collider hit in hits)
        {
            ClimberMovement climber = hit.GetComponent<ClimberMovement>();
            if (climber == null) continue;

            // Si el escalador ya está comiendo o ya ha comido este turno, lo saltamos
            if (climber.isEating || climber.hasEatenThisTurn)
                continue;

            GameObject berryToEat = ChooseBerryToEat();
            if (berryToEat == null)
                continue;

            climber.hasEatenThisTurn = true; // este turno solo come una vez

            SendClimberToEat(climber, berryToEat);
        }
    }

    private GameObject ChooseBerryToEat()
    {
        if (goodBerryAlive && !badBerryAlive)
            return goodBerry;

        if (!goodBerryAlive && badBerryAlive)
            return badBerry;

        if (goodBerryAlive && badBerryAlive)
        {
            // 50% de cada una
            return (Random.value < 0.5f) ? goodBerry : badBerry;
        }

        return null;
    }

    // ================== Enviar escalador a la baya ==================

    private void SendClimberToEat(ClimberMovement climber, GameObject berry)
    {
        if (climber == null || berry == null)
            return;

        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh)
            return;

        // YA NO tocamos climber.originalDestination: ese es el campamento que él ya tenía calculado.
        climber.isEating = true;

        Vector3 targetPos = berry.transform.position;
        if (eatPoint != null)
            targetPos = eatPoint.position;

        agent.isStopped = false;
        agent.SetDestination(targetPos);

        StartCoroutine(WaitForArrival(climber, berry));
    }

    private IEnumerator WaitForArrival(ClimberMovement climber, GameObject berry)
    {
        NavMeshAgent agent = climber != null ? climber.GetComponent<NavMeshAgent>() : null;

        if (climber == null || agent == null)
        {
            if (climber != null) climber.isEating = false;
            yield break;
        }

        while (climber != null && agent != null)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                ResolveBerry(climber, berry);
                yield break;
            }
            yield return null;
        }
    }

    private void ResolveBerry(ClimberMovement climber, GameObject berry)
    {
        if (berry == goodBerry)
            EatGoodBerry(climber);
        else if (berry == badBerry)
            EatBadBerry(climber);
        else
            climber.isEating = false;
    }

    // ================== Efectos de cada baya ==================

    private void EatGoodBerry(ClimberMovement climber)
    {
        if (goodBerry != null)
            goodBerry.SetActive(false);

        goodBerryAlive = false;

        // Efecto positivo
        climber.AddMaxStamina(200f);
        climber.isEating = false;

        // Restauramos el destino original para que siga su ruta
        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(climber.originalDestination);
        }

        // Respawn individual tras X turnos de escaladores
        StartCoroutine(RespawnBerryAfterTurns(true));

        Debug.Log("✅ Baya BUENA comida: +200 MaxStamina y sigue su camino.");
    }

    private void EatBadBerry(ClimberMovement climber)
    {
        if (badBerry != null)
            badBerry.SetActive(false);

        badBerryAlive = false;

        // Mata al escalador
        Destroy(climber.gameObject);

        // Respawn individual tras X turnos de escaladores
        StartCoroutine(RespawnBerryAfterTurns(false));
        Debug.Log("💀 Baya MALA comida: escalador destruido");
    }

    // Corrutina independiente para cada baya
    private IEnumerator RespawnBerryAfterTurns(bool good)
    {
        int counter = 0;

        if (TurnManager.Instance == null)
            yield break;

        void OnClimberTurnEnd()
        {
            counter++;
        }

        TurnManager.Instance.OnClimberTurnEnd += OnClimberTurnEnd;

        while (counter < turnsToRespawn)
            yield return null;

        TurnManager.Instance.OnClimberTurnEnd -= OnClimberTurnEnd;

        if (good)
        {
            if (goodBerry != null)
            {
                goodBerry.SetActive(true);
                goodBerryAlive = true;
            }
        }
        else
        {
            if (badBerry != null)
            {
                badBerry.SetActive(true);
                badBerryAlive = true;
            }
        }

        Debug.Log("🔁 Una baya ha vuelto a crecer.");
    }

    // ================== Gizmos ==================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.25f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
