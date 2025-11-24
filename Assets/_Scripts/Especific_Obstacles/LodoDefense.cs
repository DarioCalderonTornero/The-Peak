using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LodoDefense : MonoBehaviour
{
    /* [Header("Efecto de lodo")]
    [SerializeField] private float slowFactor = 0.2f;            // 80% más lento (20% speed)
    [SerializeField] private float staminaMultiplier = 5f;       // x5 gasto
    [SerializeField] private float deathThresholdPercent = 0.3f; // muere si < 30%

    [Header("Animación de aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    private Vector3 originalLocalScale;

    private Coroutine spawnRoutine;

    private class MudClimberData
    {
        public float lastStamina;
        public bool inside;
    }

    // Escaladores dentro del lodo
    private readonly Dictionary<ClimberMovement, MudClimberData> tracked =
        new Dictionary<ClimberMovement, MudClimberData>();


    private void Awake()
    {
        // Escala original del prefab
        originalLocalScale = transform.localScale;

        // El lodo aparece desde escala 0
        transform.localScale = Vector3.zero;

        // Asegurar trigger
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        // Animación de aparición
        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    // -----------------------------
    //  ▶ ANIMACIÓN DE SPAWN
    // -----------------------------
    private IEnumerator SpawnFromGround()
    {
        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            // smoothstep
            float eased = t * t * (3f - 2f * t);

            transform.localScale = originalLocalScale * eased;
            yield return null;
        }

        transform.localScale = originalLocalScale;
        spawnRoutine = null;
    }


    // -----------------------------
    //  ▶ ENTRADA AL LODO
    // -----------------------------
    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // Aplicar lentitud
        climber.SetExternalSpeedMultiplier(slowFactor);

        float current = climber.GetCurrentStamina();

        if (!tracked.TryGetValue(climber, out MudClimberData data))
        {
            data = new MudClimberData();
            tracked[climber] = data;
        }

        data.inside = true;
        data.lastStamina = current;
    }

    // -----------------------------
    //  ▶ SALIDA DEL LODO
    // -----------------------------
    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // Recupera velocidad normal
        climber.SetExternalSpeedMultiplier(1f);

        tracked.Remove(climber);
    }


    // -----------------------------
    //  ▶ LÓGICA DE AGOTAMIENTO / MUERTE
    // -----------------------------
    private void LateUpdate()
    {
        if (tracked.Count == 0)
            return;

        var keys = new List<ClimberMovement>(tracked.Keys);

        foreach (var climber in keys)
        {
            if (climber == null)
            {
                tracked.Remove(climber);
                continue;
            }

            var data = tracked[climber];
            if (!data.inside) continue;

            float prev = data.lastStamina;
            float current = climber.GetCurrentStamina();
            float max = climber.GetMaxStamina();

            // Estamina perdida este frame sin el multiplicador
            float delta = Mathf.Max(0f, prev - current);

            if (delta > 0f && staminaMultiplier > 1f)
            {
                // Añadir el extra para llegar al x5
                float extra = delta * (staminaMultiplier - 1f);
                float newStamina = Mathf.Max(0f, current - extra);

                climber.SetCurrentStamina(newStamina);
                current = newStamina;
            }

            data.lastStamina = current;

            // ¿muere dentro del lodo?
            if (current <= max * deathThresholdPercent)
            {
                Destroy(climber.gameObject); // muere escalador
                Destroy(gameObject);         // muere lodo
                break;
            }
        }
    } */
}
