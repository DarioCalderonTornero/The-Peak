using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrambleDefense : BaseDefense
{
    [Header("Ralentización")]
    [SerializeField] private float slowFactor = 0.7f; // 30% más lento

    [Header("Aparición")]
    [SerializeField] private float spawnDuration = 0.25f;
    [SerializeField] private float spawnAnimSpeed = 1f;     // velocidad de la animación (1 = normal)

    [Header("Vibración cuando hay escaladores dentro")]
    [SerializeField] private float shakeMagnitude = 0.05f;   // cuánto se mueve cada mata
    [SerializeField] private float shakeSpeed = 25f;         // (no afecta al random, pero lo dejamos por si luego lo usas)
    [SerializeField] private float shakeRadiusAroundClimber = 1.2f; // radio alrededor del escalador que activa zarzas

    [Header("Visual de zarzas")]
    [SerializeField] private GameObject bramblePrefab;       // 👉 tu nuevo modelo de zarza
    [SerializeField] private int minClumps = 4;              // mínimo número de zarzas
    [SerializeField] private int maxClumps = 8;              // máximo número de zarzas
    [SerializeField] private Vector2 randomScaleRange = new Vector2(0.8f, 1.4f);

    [Header("Escala aleatoria")]
    [SerializeField] private float globalScaleMultiplier = 0.5f; // hace todas las zarzas más pequeñas
    [SerializeField] private float zScaleMultiplier = 2f;        // Z será 2x X/Y

    private Vector3 originalLocalPosition;

    private Coroutine spawnRoutine;
    private Collider areaCollider;

    // Escaladores dentro del área de la zarza
    private readonly List<ClimberMovement> climbersInside = new List<ClimberMovement>();

    // Para animar cada instancia
    private class BrambleInstance
    {
        public Transform transform;
        public Vector3 targetScale;
        public Quaternion startRotation;
        public Quaternion endRotation;
        public Vector3 originalLocalPos;   // para el shake individual
    }

    private readonly List<BrambleInstance> spawnedInstances = new List<BrambleInstance>();

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;

        // Aseguramos que el collider sea trigger
        areaCollider = GetComponent<Collider>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
        else
            Debug.LogWarning("[BrambleDefense] No hay Collider en el objeto de zarzas, el área no funcionará.");
    }

    public override void Initialize()
    {
        base.Initialize();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    private IEnumerator SpawnFromGround()
    {
        // 1) Eliminar zarzas anteriores si por lo que sea se re-inicializa
        ClearSpawnedInstances();

        if (bramblePrefab == null)
        {
            Debug.LogWarning("[BrambleDefense] No hay bramblePrefab asignado.");
            yield break;
        }

        int count = Random.Range(minClumps, maxClumps + 1);
        spawnedInstances.Clear();

        // 2) Calcular posiciones dentro del área del collider
        BoxCollider box = areaCollider as BoxCollider;
        if (box == null)
        {
            Debug.LogWarning("[BrambleDefense] El collider no es BoxCollider, se usará el punto central.");
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 worldPos = transform.position;

            if (box != null)
            {
                Vector3 halfSize = box.size * 0.5f;

                float randX = Random.Range(-halfSize.x, halfSize.x);
                float randZ = Random.Range(-halfSize.z, halfSize.z);

                Vector3 localPos = new Vector3(randX, 0f, randZ) + box.center;
                worldPos = transform.TransformPoint(localPos);
            }

            GameObject instance = Instantiate(bramblePrefab, worldPos, Quaternion.identity, transform);

            // Rotación inicial: X = -90, Z aleatoria
            float randomZ = Random.Range(0f, 360f);
            Quaternion startRot = Quaternion.Euler(-90f, 0f, randomZ);
            Quaternion endRot = Quaternion.Euler(90f, 0f, randomZ);

            instance.transform.rotation = startRot;

            // Escala objetivo
            Vector3 baseScale = instance.transform.localScale;
            float randomScale = Random.Range(randomScaleRange.x, randomScaleRange.y) * globalScaleMultiplier;

            Vector3 targetScale = new Vector3(
                baseScale.x * randomScale,
                baseScale.y * randomScale,
                baseScale.z * randomScale * zScaleMultiplier
            );

            // Empezamos en escala 0 para el "pop"
            instance.transform.localScale = Vector3.zero;

            spawnedInstances.Add(new BrambleInstance
            {
                transform = instance.transform,
                targetScale = targetScale,
                startRotation = startRot,
                endRotation = endRot,
                originalLocalPos = instance.transform.localPosition
            });
        }

        // 3) Animación de aparición: escala + flip de -90º a 90º en X
        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime * Mathf.Max(0.01f, spawnAnimSpeed);
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            float eased = t * t * (3f - 2f * t); // smoothstep

            foreach (var inst in spawnedInstances)
            {
                if (inst.transform == null) continue;

                // Escala
                inst.transform.localScale = inst.targetScale * eased;

                // Rotación (flip rápido)
                inst.transform.rotation = Quaternion.Slerp(inst.startRotation, inst.endRotation, eased);
            }

            yield return null;
        }

        // Estado final
        foreach (var inst in spawnedInstances)
        {
            if (inst.transform == null) continue;
            inst.transform.localScale = inst.targetScale;
            inst.transform.rotation = inst.endRotation;
            inst.originalLocalPos = inst.transform.localPosition;
        }

        spawnRoutine = null;
    }

    private void ClearSpawnedInstances()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            Destroy(child.gameObject);
        }

        spawnedInstances.Clear();
    }

    private void Update()
    {
        if (spawnedInstances.Count == 0)
            return;

        // Limpiar climbers nulos (muertos)
        for (int i = climbersInside.Count - 1; i >= 0; i--)
        {
            if (climbersInside[i] == null)
                climbersInside.RemoveAt(i);
        }

        if (climbersInside.Count == 0)
        {
            // No hay escaladores dentro → todas vuelven a su posición original
            foreach (var inst in spawnedInstances)
            {
                if (inst.transform == null) continue;
                inst.transform.localPosition = Vector3.Lerp(
                    inst.transform.localPosition,
                    inst.originalLocalPos,
                    Time.deltaTime * 10f
                );
            }
            return;
        }

        float radiusSqr = shakeRadiusAroundClimber * shakeRadiusAroundClimber;

        // Por cada mata, miramos si está cerca de algún escalador
        foreach (var inst in spawnedInstances)
        {
            if (inst.transform == null) continue;

            bool shouldShake = false;

            foreach (var climber in climbersInside)
            {
                if (climber == null) continue;

                Vector3 bushPos = inst.transform.position;
                Vector3 climberPos = climber.transform.position;

                // Distancia en planta (XZ), ignorando Y
                Vector3 delta = bushPos - climberPos;
                delta.y = 0f;

                if (delta.sqrMagnitude <= radiusSqr)
                {
                    shouldShake = true;
                    break;
                }
            }

            if (shouldShake)
            {
                // Mismo patrón de shake que antes: random por frame
                Vector3 offset = Random.insideUnitSphere * shakeMagnitude;
                offset.y = 0f; // solo XZ
                inst.transform.localPosition = inst.originalLocalPos + offset;
            }
            else
            {
                // Si no está cerca de ningún escalador → vuelve a su sitio
                inst.transform.localPosition = Vector3.Lerp(
                    inst.transform.localPosition,
                    inst.originalLocalPos,
                    Time.deltaTime * 10f
                );
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmuneToBramble = false;

        if (loadout != null)
        {
            loadout.TryHandleObstacle(ObstacleType.Bramble);
            isImmuneToBramble = loadout.CanHandleObstacle(ObstacleType.Bramble);
        }

        if (isImmuneToBramble)
            return;

        climber.SetExternalSpeedMultiplier(slowFactor);
        Debug.Log($"[BrambleDefense] {other.name} ha entrado en zarza");

        if (!climbersInside.Contains(climber))
            climbersInside.Add(climber);
    }

    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null)
        {
            loadout.TryHandleObstacleExit(ObstacleType.Bramble);
        }

        climber.SetExternalSpeedMultiplier(1f);
        Debug.Log($"[BrambleDefense] {other.name} ha salido de zarza");

        climbersInside.Remove(climber);
        // Las matas se irán calmando solas en Update (al no haber escalador cerca)
    }
}
