using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class LodoDefense : BaseDefense
{
    [Header("Efecto de lodo")]
    [SerializeField] private float slowFactor = 0.2f;            // 80% más lento (20% speed)
    [SerializeField] private float staminaMultiplier = 5f;       // x5 gasto
    [SerializeField] private float deathThresholdPercent = 0.3f; // muere si < 30%

    [Header("Animación de aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    [Header("Hundimiento visual")]
    [SerializeField] private float idleSinkSpeed = 0.1f;         // qué rápido se hunde mientras camina
    [SerializeField] private float maxVisualSinkDepth = 0.5f;    // profundidad máxima visual (offset hacia abajo)
    [SerializeField] private float restoreSpeed = 1.5f;          // velocidad para volver a la altura original
    [SerializeField] private float deathAbsorbSpeed = 4f;        // absorción rápida al morir
    [SerializeField] private float deathExtraSinkDepth = 0.5f;   // cuánto extra se hunde en la muerte

    private Vector3 originalLocalScale;
    private Coroutine spawnRoutine;

    private class MudClimberData
    {
        public float lastStamina;
        public bool inside;

        // Para hundimiento visual
        public NavMeshAgent agent;
        public float initialBaseOffset;
        public bool hasInitialOffset;

        public bool isDying;                  // si está en animación de muerte
        public Coroutine restoreRoutine;      // corrutina de “subir” al salir del lodo
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

            // Preguntamos si alguno puede manejar el lodo (tiene equipamiento anti-lodo)
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

        if (!tracked.TryGetValue(climber, out MudClimberData data))
        {
            data = new MudClimberData();
            tracked[climber] = data;
        }

        data.inside = true;
        data.lastStamina = climber.GetCurrentStamina();
        data.isDying = false;

        // Guardar NavMeshAgent y su offset original (para hundimiento visual)
        if (data.agent == null)
            data.agent = climber.GetComponent<NavMeshAgent>();

        if (data.agent != null && !data.hasInitialOffset)
        {
            data.initialBaseOffset = data.agent.baseOffset;
            data.hasInitialOffset = true;
        }

        // Si estaba restaurando altura de una salida anterior, cortamos esa corrutina
        if (data.restoreRoutine != null)
        {
            StopCoroutine(data.restoreRoutine);
            data.restoreRoutine = null;
        }
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

        if (!tracked.TryGetValue(climber, out MudClimberData data))
        {
            // Por si acaso, reset de velocidad
            climber.SetExternalSpeedMultiplier(1f);
            return;
        }

        data.inside = false;

        // Si está en animación de muerte, no hacemos nada: la muerte se encargará de todo
        if (!data.isDying)
        {
            // Restablecemos velocidad de movimiento
            climber.SetExternalSpeedMultiplier(1f);

            // Lanzamos corrutina para volver a la altura original (baseOffset)
            if (data.agent != null && data.hasInitialOffset)
            {
                if (data.restoreRoutine != null)
                    StopCoroutine(data.restoreRoutine);

                data.restoreRoutine = StartCoroutine(RestoreBaseOffset(climber, data));
            }
            else
            {
                // Si no hay agent, lo sacamos moviendo la posición Y del transform
                if (data.restoreRoutine != null)
                    StopCoroutine(data.restoreRoutine);

                data.restoreRoutine = StartCoroutine(RestoreTransformHeight(climber, data));
            }
        }
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

            if (!tracked.TryGetValue(climber, out MudClimberData data))
                continue;

            // Si ya está en animación de muerte, aquí no hacemos nada
            if (data.isDying)
                continue;

            // Si no está dentro, tampoco aplicamos gasto ni hundimiento (se encarga la corrutina de salida)
            if (!data.inside)
                continue;

            float prev = data.lastStamina;
            float current = climber.GetCurrentStamina();
            float max = climber.GetMaxStamina();

            // 🔹 Gasto de stamina multiplicado
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

            // 🔹 Hundimiento visual suave mientras camina por el lodo
            ApplyIdleSink(data);

            // 🔹 Comprobamos muerte por stamina
            if (max > 0f && current <= max * deathThresholdPercent)
            {
                // Empezamos animación rápida de absorción y muerte
                data.isDying = true;
                StartCoroutine(QuickAbsorbAndKill(climber, data));
            }
        }
    }

    // Hundimiento lento mientras está en el lodo
    private void ApplyIdleSink(MudClimberData data)
    {
        if (data.agent != null && data.hasInitialOffset)
        {
            float targetOffset = data.initialBaseOffset - maxVisualSinkDepth;
            data.agent.baseOffset = Mathf.MoveTowards(
                data.agent.baseOffset,
                targetOffset,
                idleSinkSpeed * Time.deltaTime
            );
        }
        // Si quisieras un fallback sin NavMeshAgent, aquí podrías tocar transform.position.y
    }

    // Animación rápida de absorción al morir
    private IEnumerator QuickAbsorbAndKill(ClimberMovement climber, MudClimberData data)
    {
        NavMeshAgent agent = data.agent != null ? data.agent : climber.GetComponent<NavMeshAgent>();

        if (agent != null && data.hasInitialOffset)
        {
            float targetOffset = data.initialBaseOffset - (maxVisualSinkDepth + deathExtraSinkDepth);

            while (agent != null &&
                   Mathf.Abs(agent.baseOffset - targetOffset) > 0.01f)
            {
                agent.baseOffset = Mathf.MoveTowards(
                    agent.baseOffset,
                    targetOffset,
                    deathAbsorbSpeed * Time.deltaTime
                );
                yield return null;
            }
        }
        else
        {
            // Fallback: hundimos el transform en Y
            Transform t = climber.transform;
            float startY = t.position.y;
            float targetY = startY - (maxVisualSinkDepth + deathExtraSinkDepth);

            while (climber != null &&
                   Mathf.Abs(t.position.y - targetY) > 0.01f)
            {
                Vector3 pos = t.position;
                pos.y = Mathf.MoveTowards(pos.y, targetY, deathAbsorbSpeed * Time.deltaTime);
                t.position = pos;
                yield return null;
            }
        }

        if (climber != null)
        {
            Destroy(climber.gameObject);
        }

        // El lodo desaparece tras absorber al escalador
        Destroy(gameObject);
    }

    // Corrutina para volver suavemente al offset original al salir del lodo (con NavMeshAgent)
    private IEnumerator RestoreBaseOffset(ClimberMovement climber, MudClimberData data)
    {
        NavMeshAgent agent = data.agent;
        if (agent == null || !data.hasInitialOffset)
        {
            tracked.Remove(climber);
            yield break;
        }

        while (agent != null &&
               Mathf.Abs(agent.baseOffset - data.initialBaseOffset) > 0.01f)
        {
            agent.baseOffset = Mathf.MoveTowards(
                agent.baseOffset,
                data.initialBaseOffset,
                restoreSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (agent != null)
            agent.baseOffset = data.initialBaseOffset;

        // Una vez restaurado, ya no necesitamos seguir trackeando
        tracked.Remove(climber);
        data.restoreRoutine = null;
    }

    // Fallback si NO hay NavMeshAgent: restaurar transform.position.y
    private IEnumerator RestoreTransformHeight(ClimberMovement climber, MudClimberData data)
    {
        if (climber == null)
        {
            tracked.Remove(climber);
            yield break;
        }

        Transform t = climber.transform;
        float targetY = t.position.y; // usamos la altura actual como “buena”

        // Aquí podrías guardar una altura original diferente si la tuvieses.
        // Para no liarnos, lo dejamos simple.

        while (climber != null &&
               Mathf.Abs(t.position.y - targetY) > 0.01f)
        {
            Vector3 pos = t.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY, restoreSpeed * Time.deltaTime);
            t.position = pos;
            yield return null;
        }

        tracked.Remove(climber);
        data.restoreRoutine = null;
    }
}
