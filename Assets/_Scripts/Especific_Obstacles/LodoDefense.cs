using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LodoDefense : BaseDefense
{
    [Header("Efecto de lodo")]
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

    private readonly Dictionary<ClimberMovement, MudClimberData> tracked =
        new Dictionary<ClimberMovement, MudClimberData>();

    private void Awake()
    {
        // Guardamos escala original del prefab
        originalLocalScale = transform.localScale;

        // Aseguramos trigger
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    public override void Initialize()
    {
        base.Initialize();

        // Animación de aparecer SOLO en la instancia colocada,
        // no en el preview (el preview no llama a Initialize).
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    private IEnumerator SpawnFromGround()
    {
        // Arranca desde escala 0
        transform.localScale = Vector3.zero;

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

    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // 🔹 Pasamos por el sistema de equipamiento
        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmuneToMud = false;

        if (loadout != null)
        {
            // Lanza OnEncounterObstacle en todos los equipos
            loadout.TryHandleObstacle(ObstacleType.Mud);

            // Preguntamos si alguno puede manejar el lodo (tiene LodoBreakerEquipment o similar)
            isImmuneToMud = loadout.CanHandleObstacle(ObstacleType.Mud);
        }

        // Si TIENE equipamiento que maneja el lodo → no aplicamos efecto
        if (isImmuneToMud)
        {
            // Opcional: debug
            // Debug.Log("[LodoDefense] Climber inmune al lodo.");
            return;
        }

        // Si NO es inmune → aplicamos el efecto de lodo como antes
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

    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // 🔹 Notificamos salida de obstáculo al equipamiento (por si quiere reaccionar)
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null)
        {
            loadout.TryHandleObstacleExit(ObstacleType.Mud);
        }

        // Restablecemos velocidad y dejamos de trackear al escalador
        climber.SetExternalSpeedMultiplier(1f);
        tracked.Remove(climber);
    }

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

            float delta = Mathf.Max(0f, prev - current);

            if (delta > 0f && staminaMultiplier > 1f)
            {
                // Queremos que el coste final sea = coste_normal * staminaMultiplier
                // pero delta ahora está “reducido” por el slowFactor.
                float effectiveFactor = staminaMultiplier;

                if (slowFactor > 0f)
                    effectiveFactor = staminaMultiplier / slowFactor;

                float extra = delta * (effectiveFactor - 1f);
                float newStamina = Mathf.Max(0f, current - extra);

                climber.SetCurrentStamina(newStamina);
                current = newStamina;
            }

            data.lastStamina = current;

            if (current <= max * deathThresholdPercent)
            {
                Destroy(climber.gameObject);
                Destroy(gameObject);
                break;
            }
        }
    }
}
