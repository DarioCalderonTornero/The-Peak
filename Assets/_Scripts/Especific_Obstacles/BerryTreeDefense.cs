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

        if (!goodBerryAlive && !badBerryAlive)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, climberLayer);

        foreach (Collider hit in hits)
        {
            ClimberMovement climber = hit.GetComponent<ClimberMovement>();
            if (climber == null) continue;
            if (climber.isEating) continue;

            if (Random.value <= eatProbability)
            {
                SendClimberToEat(climber);
            }
        }
    }

    private void SendClimberToEat(ClimberMovement climber)
    {
        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent == null) return;

        climber.isEating = true;

        Vector3 destination = eatPoint != null ? eatPoint.position : transform.position;
        agent.SetDestination(destination);

        StartCoroutine(WaitForArrival(climber));
    }

    private IEnumerator WaitForArrival(ClimberMovement climber)
    {
        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        while (true)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                ResolveBerry(climber);
                yield break;
            }
            yield return null;
        }
    }

    private void ResolveBerry(ClimberMovement climber)
    {
        climber.isEating = false;

        // Solo queda la buena
        if (goodBerryAlive && !badBerryAlive)
        {
            EatGoodBerry(climber);
            return;
        }

        // Solo queda la mala
        if (!goodBerryAlive && badBerryAlive)
        {
            EatBadBerry(climber);
            return;
        }

        // Están las dos → 50 / 50
        if (goodBerryAlive && badBerryAlive)
        {
            if (Random.value < 0.5f)
                EatGoodBerry(climber);
            else
                EatBadBerry(climber);
        }
    }

    private void EatGoodBerry(ClimberMovement climber)
    {
        goodBerry.SetActive(false);
        goodBerryAlive = false;

        climber.AddMaxStamina(200f);

        NavMeshAgent agent = climber.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.isStopped = false;

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

