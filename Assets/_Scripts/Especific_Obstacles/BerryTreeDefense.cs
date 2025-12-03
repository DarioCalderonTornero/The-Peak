using System.Collections;
using System.Collections.Generic;
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
    public int turnsToGrow = 2;
    public int turnsToRespawn = 2;

    private int growCounter = 0;

    [Header("Detección")]
    public float detectionRadius = 5f;
    public LayerMask climberLayer;

    [Header("Probabilidades")]
    [Range(0f, 1f)] public float eatProbability = 0.15f;

    [Header("Punto al que van a comer")]
    public Transform eatPoint;

    public Vector3 lastDestination;

    private void OnEnable()
    {
        TurnManager.Instance.OnClimberTurnEnd += OnClimberTurnEnd;
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd -= OnClimberTurnEnd;
    }

    private void Start()
    {
        if (goodBerry != null) goodBerry.SetActive(false);
        if (badBerry != null) badBerry.SetActive(false);
    }

    private void OnClimberTurnEnd()
    {
        if (!goodBerryAlive && !badBerryAlive)
        {
            growCounter++;
            if (growCounter >= turnsToGrow)
            {
                SpawnBothBerries();
            }
        }
    }

    private void SpawnBothBerries()
    {
        growCounter = 0;

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

        Debug.Log("🌱 Han aparecido la baya buena y la mala.");
    }

    private void Update()
    {
        if (!TurnManager.Instance.IsClimberTurn())
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
            if (climber.isEating || climber.hasEatenThisTurn) continue;

            // Solo intentamos comer con la probabilidad definida
            if (Random.value <= eatProbability)
            {
                // Elegimos la baya disponible más cercana
                GameObject berryToEat = null;
                if (goodBerryAlive) berryToEat = goodBerry;
                else if (badBerryAlive) berryToEat = badBerry;

                if (berryToEat == null) continue;

                climber.hasEatenThisTurn = true; // marca que comerá solo 1
                SendClimberToEat(climber, berryToEat);
            }
        }
    }

    private void SendClimberToEat(ClimberMovement climber, GameObject berry)
    {
        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh) return;

        // Guardamos el destino original para volver
        climber.originalDestination = agent.destination;

        climber.isEating = true;

        agent.SetDestination(berry.transform.position);
        agent.isStopped = false;

        StartCoroutine(WaitForArrival(climber, berry));
    }

    private IEnumerator WaitForArrival(ClimberMovement climber, GameObject berry)
    {
        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            climber.isEating = false;
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
        if (berry == goodBerry) EatGoodBerry(climber);
        else if (berry == badBerry) EatBadBerry(climber);

        climber.isEating = false;
    }

    private void EatGoodBerry(ClimberMovement climber)
    {
        goodBerry.SetActive(false);
        goodBerryAlive = false;

        climber.AddMaxStamina(200f);
        climber.isEating = false;

        // Restauramos el destino original
        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(climber.originalDestination);
            agent.isStopped = false;
        }

        // Activamos el escalador para que Update() siga moviéndolo
        climber.SetExternallyDoneThisTurn(false);

        StartCoroutine(RespawnBerryAfterTurns(true));

        Debug.Log("✅ Baya BUENA comida: +200 MaxStamina y sigue su camino.");
    }

    private void EatBadBerry(ClimberMovement climber)
    {
        badBerry.SetActive(false);
        badBerryAlive = false;

        Destroy(climber.gameObject);

        StartCoroutine(RespawnBerryAfterTurns(false));
        Debug.Log("💀 Baya MALA comida: escalador destruido");
    }

    // Corrutina independiente para cada baya
    private IEnumerator RespawnBerryAfterTurns(bool good)
    {
        int counter = 0;

        void OnTurn()
        {
            counter++;
        }

        TurnManager.Instance.OnClimberTurnEnd += OnTurn;

        while (counter < turnsToRespawn)
            yield return null;

        TurnManager.Instance.OnClimberTurnEnd -= OnTurn;

        if (good && goodBerry != null)
        {
            goodBerry.SetActive(true);
            goodBerryAlive = true;
        }
        else if (!good && badBerry != null)
        {
            badBerry.SetActive(true);
            badBerryAlive = true;
        }

        Debug.Log("🔁 Una baya ha vuelto a crecer.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.25f);
        Gizmos.DrawSphere(transform.position, detectionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}

