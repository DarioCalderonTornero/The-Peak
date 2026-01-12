using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class LodoDefense : BaseDefense
{
    [Header("Efecto de lodo")]
    [SerializeField] private float slowFactor = 0.2f;
    [SerializeField] private float staminaMultiplier = 5f;
    [SerializeField] private float deathThresholdPercent = 0.3f;

    [Header("Animación de aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    [Header("Hundimiento visual")]
    [SerializeField] private float idleSinkSpeed = 0.1f;
    [SerializeField] private float maxVisualSinkDepth = 0.5f;
    [SerializeField] private float restoreSpeed = 1.5f;
    [SerializeField] private float deathAbsorbSpeed = 4f;
    [SerializeField] private float deathExtraSinkDepth = 0.5f;

    private Coroutine spawnRoutine;
    private Vector3 spawnTargetLocalScale; // ✅ escala FINAL (ya escalada por placer)

    private class MudClimberData
    {
        public float lastStamina;
        public bool inside;

        public NavMeshAgent agent;
        public float initialBaseOffset;
        public bool hasInitialOffset;

        public bool isDying;
        public Coroutine restoreRoutine;
    }

    private readonly Dictionary<ClimberMovement, MudClimberData> tracked =
        new Dictionary<ClimberMovement, MudClimberData>();

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public override void Initialize()
    {
        // OJO: aquí ya NO vuelvas a leer transform.localScale, asume que
        // ApplyExternalScale ya ha puesto spawnTargetLocalScale.
        base.Initialize();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    private IEnumerator SpawnFromGround()
    {
        transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);
            float eased = t * t * (3f - 2f * t);

            transform.localScale = spawnTargetLocalScale * eased;
            yield return null;
        }

        transform.localScale = spawnTargetLocalScale;
        spawnRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmune = false;

        if (loadout != null)
        {
            loadout.TryHandleObstacle(ObstacleType.Mud);
            isImmune = loadout.CanHandleObstacle(ObstacleType.Mud);
        }

        if (isImmune)
            return;

        climber.SetExternalSpeedMultiplier(slowFactor);

        if (!tracked.TryGetValue(climber, out MudClimberData data))
        {
            data = new MudClimberData();
            tracked[climber] = data;
        }

        data.inside = true;
        data.lastStamina = climber.GetCurrentStamina();
        data.isDying = false;

        if (data.agent == null)
            data.agent = climber.GetComponent<NavMeshAgent>();

        if (data.agent != null && !data.hasInitialOffset)
        {
            data.initialBaseOffset = data.agent.baseOffset;
            data.hasInitialOffset = true;
        }

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

        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null)
            loadout.TryHandleObstacleExit(ObstacleType.Mud);

        if (!tracked.TryGetValue(climber, out MudClimberData data))
        {
            climber.SetExternalSpeedMultiplier(1f);
            return;
        }

        data.inside = false;

        if (!data.isDying)
        {
            climber.SetExternalSpeedMultiplier(1f);

            if (data.agent != null && data.hasInitialOffset)
            {
                if (data.restoreRoutine != null)
                    StopCoroutine(data.restoreRoutine);

                data.restoreRoutine = StartCoroutine(RestoreBaseOffset(climber, data));
            }
            else
            {
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

            if (data.isDying) continue;
            if (!data.inside) continue;

            float prev = data.lastStamina;
            float current = climber.GetCurrentStamina();
            float max = climber.GetMaxStamina();

            float delta = Mathf.Max(0f, prev - current);

            if (delta > 0f && staminaMultiplier > 1f)
            {
                float effectiveFactor = staminaMultiplier;
                if (slowFactor > 0f)
                    effectiveFactor = staminaMultiplier / slowFactor;

                float extra = delta * (effectiveFactor - 1f);
                float newStamina = Mathf.Max(0f, current - extra);

                climber.SetCurrentStamina(newStamina);
                current = newStamina;
            }

            data.lastStamina = current;

            ApplyIdleSink(data);

            if (max > 0f && current <= max * deathThresholdPercent)
            {
                data.isDying = true;
                StartCoroutine(QuickAbsorbAndKill(climber, data));
            }
        }
    }

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
    }

    private IEnumerator QuickAbsorbAndKill(ClimberMovement climber, MudClimberData data)
    {
        NavMeshAgent agent = data.agent != null ? data.agent : climber.GetComponent<NavMeshAgent>();

        if (agent != null && data.hasInitialOffset)
        {
            float targetOffset = data.initialBaseOffset - (maxVisualSinkDepth + deathExtraSinkDepth);

            while (agent != null && Mathf.Abs(agent.baseOffset - targetOffset) > 0.01f)
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
            Transform t = climber.transform;
            float startY = t.position.y;
            float targetY = startY - (maxVisualSinkDepth + deathExtraSinkDepth);

            while (climber != null && Mathf.Abs(t.position.y - targetY) > 0.01f)
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
            ClimberDeathPointsManager.Instance.AddClimberDeathPoints();
        }

        Destroy(gameObject);
    }

    private IEnumerator RestoreBaseOffset(ClimberMovement climber, MudClimberData data)
    {
        NavMeshAgent agent = data.agent;
        if (agent == null || !data.hasInitialOffset)
        {
            tracked.Remove(climber);
            yield break;
        }

        while (agent != null && Mathf.Abs(agent.baseOffset - data.initialBaseOffset) > 0.01f)
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

        tracked.Remove(climber);
        data.restoreRoutine = null;
    }

    private IEnumerator RestoreTransformHeight(ClimberMovement climber, MudClimberData data)
    {
        if (climber == null)
        {
            tracked.Remove(climber);
            yield break;
        }

        Transform t = climber.transform;
        float targetY = t.position.y;

        while (climber != null && Mathf.Abs(t.position.y - targetY) > 0.01f)
        {
            Vector3 pos = t.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY, restoreSpeed * Time.deltaTime);
            t.position = pos;
            yield return null;
        }

        tracked.Remove(climber);
        data.restoreRoutine = null;
    }

    public void ApplyExternalScale(Vector3 finalScale)
    {
        // Este será el tamaño “objetivo” para el lodo
        spawnTargetLocalScale = finalScale;
        transform.localScale = finalScale;
    }
}
