using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BerryTreeDefense : BaseDefense
{
    [Header("Bayas (visuales)")]
    public GameObject goodBerry;
    public GameObject badBerry;

    private bool goodBerryAlive = false;
    private bool badBerryAlive = false;

    [Header("Turnos")]
    public int turnsToFirstGrow = 2;
    public int turnsToRespawn = 2;

    private int initialGrowCounter = 0;
    private bool initialGrowDone = false;

    [Header("Detección")]
    public float detectionRadius = 2f;
    public LayerMask climberLayer;

    [Header("Comer")]
    public float eatStopDuration = 1f;

    // Evita que el mismo escalador dispare comer 20 veces
    private readonly Dictionary<ClimberMovement, Coroutine> eatingRoutines = new();

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd += HandleGlobalClimberTurnEnd;
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd -= HandleGlobalClimberTurnEnd;
    }

    private void Start()
    {
        if (goodBerry != null) goodBerry.SetActive(false);
        if (badBerry != null) badBerry.SetActive(false);

        goodBerryAlive = false;
        badBerryAlive = false;

        initialGrowCounter = 0;
        initialGrowDone = false;
    }

    private void HandleGlobalClimberTurnEnd()
    {
        if (initialGrowDone) return;

        initialGrowCounter++;
        if (initialGrowCounter >= turnsToFirstGrow)
        {
            SpawnBothBerriesFirstTime();
            initialGrowDone = true;
        }
    }

    private void SpawnBothBerriesFirstTime()
    {
        if (goodBerry != null) { goodBerry.SetActive(true); goodBerryAlive = true; }
        if (badBerry != null) { badBerry.SetActive(true); badBerryAlive = true; }

        Debug.Log("🌱 Primer crecimiento: aparecen las dos bayas.");
    }

    private void Update()
    {
        if (TurnManager.Instance == null || !TurnManager.Instance.IsClimberTurn())
            return;

        if (!goodBerryAlive && !badBerryAlive)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, climberLayer);

        foreach (var hit in hits)
        {
            var climber = hit.GetComponent<ClimberMovement>();
            if (climber == null) continue;

            // Si ya está en proceso o ya comió este turno, nada
            if (climber.hasEatenThisTurn) continue;
            if (climber.isEating) continue;
            if (eatingRoutines.ContainsKey(climber)) continue;

            // Arrancamos la secuencia (PAUSA -> COME)
            Coroutine c = StartCoroutine(EatSequence(climber));
            eatingRoutines[climber] = c;

            // Opcional: solo enganchar a 1 por frame
            break;
        }
    }

    private IEnumerator EatSequence(ClimberMovement climber)
    {
        if (climber == null)
        {
            yield break;
        }

        // Marcamos estado "en proceso"
        climber.isEating = true;

        // 1) Primero se para 1s
        climber.StopForSeconds(eatStopDuration);

        float tEnd = Time.time + eatStopDuration;
        while (Time.time < tEnd)
        {
            if (climber == null)
                yield break;
            yield return null;
        }

        // 2) Después de la pausa, elegimos la baya disponible en ese momento
        GameObject berry = ChooseBerryToEat();
        if (berry == null)
        {
            // No había bayas al final de la pausa
            if (climber != null) climber.isEating = false;
            eatingRoutines.Remove(climber);
            yield break;
        }

        // 3) Ahora sí: "se la come"
        ResolveBerry(climber, berry);

        // Marcamos que ya comió este turno (DESPUÉS de la pausa)
        if (climber != null)
            climber.hasEatenThisTurn = true;

        if (climber != null)
            climber.isEating = false;

        eatingRoutines.Remove(climber);
    }

    private GameObject ChooseBerryToEat()
    {
        if (goodBerryAlive && !badBerryAlive) return goodBerry;
        if (!goodBerryAlive && badBerryAlive) return badBerry;
        if (goodBerryAlive && badBerryAlive) return (Random.value < 0.5f) ? goodBerry : badBerry;
        return null;
    }

    private void ResolveBerry(ClimberMovement climber, GameObject berry)
    {
        if (climber == null || berry == null) return;

        if (berry == goodBerry) EatGoodBerry(climber);
        else if (berry == badBerry) EatBadBerry(climber);
    }

    private void EatGoodBerry(ClimberMovement climber)
    {
        if (goodBerry != null) goodBerry.SetActive(false);
        goodBerryAlive = false;

        climber.AddMaxStamina(200f);

        StartCoroutine(RespawnBerryAfterTurns(true));
        Debug.Log("✅ Baya BUENA comida (+200 MaxStamina).");
    }

    private void EatBadBerry(ClimberMovement climber)
    {
        if (badBerry != null) badBerry.SetActive(false);
        badBerryAlive = false;

        Destroy(climber.gameObject);

        StartCoroutine(RespawnBerryAfterTurns(false));
        Debug.Log("💀 Baya MALA comida (escalador muere).");
    }

    private IEnumerator RespawnBerryAfterTurns(bool good)
    {
        int counter = 0;
        if (TurnManager.Instance == null) yield break;

        void OnTurnEnd() => counter++;
        TurnManager.Instance.OnClimberTurnEnd += OnTurnEnd;

        while (counter < turnsToRespawn)
            yield return null;

        TurnManager.Instance.OnClimberTurnEnd -= OnTurnEnd;

        if (good && goodBerry != null) { goodBerry.SetActive(true); goodBerryAlive = true; }
        else if (!good && badBerry != null) { badBerry.SetActive(true); badBerryAlive = true; }

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
