using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class BrambleDefense : BaseDefense
{
    [Header("Ralentización")]
    [SerializeField] private float slowFactor = 0.7f;

    [Header("Stamina extra en zarzas")]
    [SerializeField] private float staminaMultiplier = 2f;

    [Header("Aparición")]
    [SerializeField] private float spawnDuration = 0.25f;
    [SerializeField] private float spawnAnimSpeed = 1f;

    [Header("Vibración cuando hay escaladores dentro")]
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private float shakeSpeed = 25f; // (reservado)
    [SerializeField] private float shakeRadiusAroundClimber = 1.2f;

    [Header("Visual de zarzas")]
    [SerializeField] private GameObject bramblePrefab;
    [SerializeField] private int minClumps = 4;
    [SerializeField] private int maxClumps = 8;
    [SerializeField] private Vector2 randomScaleRange = new Vector2(0.8f, 1.4f);

    [Header("Escala aleatoria")]
    [SerializeField] private float globalScaleMultiplier = 0.5f;
    [SerializeField] private float zScaleMultiplier = 2f;

    [Header("Bramble Sound")]
    [SerializeField] private AudioSource brambleAudioSource;
    [SerializeField] private AudioClip brambleClip;

    [Header("Preview / Seed")]
    [SerializeField] private bool isPreview = false;
    [SerializeField] private int layoutSeed = 0; // el seed que define cuántas/dónde
    [SerializeField] private Material previewMaterialOverride; // material transparente (del CardData)

    private int insideCount = 0;
    private Collider areaCollider;
    private Coroutine spawnRoutine;

    // Escaladores dentro del área de la zarza
    private readonly List<ClimberMovement> climbersInside = new List<ClimberMovement>();

    private class BrambleClimberData
    {
        public float lastStamina;
        public bool inside;
    }

    private readonly Dictionary<ClimberMovement, BrambleClimberData> staminaTracked =
        new Dictionary<ClimberMovement, BrambleClimberData>();

    private class BrambleInstance
    {
        public Transform transform;
        public Vector3 targetScale;
        public Quaternion startRotation;
        public Quaternion endRotation;
        public Vector3 originalLocalPos;
    }

    private readonly List<BrambleInstance> spawnedInstances = new List<BrambleInstance>();

    [Header("Spawn VFX")]
    [SerializeField] private VisualEffect spawnVfxPrefab;
    [SerializeField] private float spawnVfxDuration = 1f;

    private void Awake()
    {
        areaCollider = GetComponent<Collider>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
        else
            Debug.LogWarning("[BrambleDefense] No hay Collider en el objeto de zarzas, el área no funcionará.");

        // Si es preview y ya tenemos seed, generamos al despertar
        if (isPreview && bramblePrefab != null && layoutSeed != 0)
        {
            GenerateLayout(layoutSeed, previewMaterialOverride, animate: false);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        // Defensa REAL: si no hay seed asignado, generamos uno
        if (layoutSeed == 0)
            layoutSeed = Random.Range(int.MinValue, int.MaxValue);

        PlaySpawnVfx();

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround(layoutSeed, previewMat: null, animate: true));
    }

    private void PlaySpawnVfx()
    {
        if (spawnVfxPrefab == null)
            return;

        VisualEffect vfx = Instantiate(
            spawnVfxPrefab,
            transform.position,
            transform.rotation,
            null // sin padre
        );

        Destroy(vfx.gameObject, spawnVfxDuration);
    }

    // ---------- API para Preview / Runtime ----------

    public void SetupPreview(int seed, Material previewMat)
    {
        isPreview = true;
        layoutSeed = seed;
        previewMaterialOverride = previewMat;

        // Generamos instantáneo (sin pop) para que se vea claro
        GenerateLayout(layoutSeed, previewMaterialOverride, animate: false);
    }

    public void SetupRuntimeFromPreviewSeed(int seed)
    {
        isPreview = false;
        layoutSeed = seed;
    }

    // ---------- Generación ----------

    private IEnumerator SpawnFromGround(int seed, Material previewMat, bool animate)
    {
        ClearSpawnedInstances();

        if (bramblePrefab == null)
        {
            Debug.LogWarning("[BrambleDefense] No hay bramblePrefab asignado.");
            yield break;
        }

        // Creamos layout (instancias en escala 0)
        BuildLayout(seed, previewMat);

        // Preview: normalmente no animamos ni hacemos pop
        if (!animate)
        {
            foreach (var inst in spawnedInstances)
            {
                if (inst.transform == null) continue;
                inst.transform.localScale = inst.targetScale;
                inst.transform.localRotation = inst.endRotation;  // 🔹 LOCAL
                inst.originalLocalPos = inst.transform.localPosition;
            }
            spawnRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime * Mathf.Max(0.01f, spawnAnimSpeed);
            float t = Mathf.Clamp01(elapsed / spawnDuration);
            float eased = t * t * (3f - 2f * t);

            foreach (var inst in spawnedInstances)
            {
                if (inst.transform == null) continue;
                inst.transform.localScale = inst.targetScale * eased;           // 🔹 LOCAL
                inst.transform.localRotation = Quaternion.Slerp(inst.startRotation, inst.endRotation, eased);  // 🔹 LOCAL
            }

            yield return null;
        }

        foreach (var inst in spawnedInstances)
        {
            if (inst.transform == null) continue;
            inst.transform.localScale = inst.targetScale;
            inst.transform.localRotation = inst.endRotation;                 // 🔹 LOCAL
            inst.originalLocalPos = inst.transform.localPosition;
        }

        spawnRoutine = null;
    }

    private void GenerateLayout(int seed, Material previewMat, bool animate)
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround(seed, previewMat, animate));
    }

    private void BuildLayout(int seed, Material previewMat)
    {
        spawnedInstances.Clear();

        var rng = new System.Random(seed);

        int count = RandRangeInt(rng, minClumps, maxClumps + 1);

        BoxCollider box = areaCollider as BoxCollider;
        if (box == null)
        {
            Debug.LogWarning("[BrambleDefense] El collider no es BoxCollider, se usará el punto central.");
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 localPos = Vector3.zero;

            if (box != null)
            {
                Vector3 halfSize = box.size * 0.5f;

                float randX = RandRangeFloat(rng, -halfSize.x, halfSize.x);
                float randZ = RandRangeFloat(rng, -halfSize.z, halfSize.z);

                // 🔹 Y = 0 en el espacio local del collider (suelo local)
                localPos = new Vector3(randX, 0f, randZ) + box.center;
            }

            // 🔹 Instanciar directamente como hijo con posición LOCAL
            GameObject instance = Instantiate(bramblePrefab, transform);
            instance.transform.localPosition = localPos;

            // Rotación inicial / final SOLO en eje Y local + "tumba"
            float randomY = RandRangeFloat(rng, 0f, 360f);

            Quaternion startRot = Quaternion.Euler(-90f, randomY, 0f);
            Quaternion endRot = Quaternion.Euler(90f, randomY, 0f);

            instance.transform.localRotation = startRot;  // 🔹 LOCAL

            // Escala determinista
            Vector3 baseScale = instance.transform.localScale;
            float randomScale = RandRangeFloat(rng, randomScaleRange.x, randomScaleRange.y) * globalScaleMultiplier;

            Vector3 targetScale = new Vector3(
                baseScale.x * randomScale,
                baseScale.y * randomScale,
                baseScale.z * randomScale * zScaleMultiplier
            );

            instance.transform.localScale = Vector3.zero;

            if (previewMat != null)
                ApplyMaterialToRenderers(instance, previewMat);

            spawnedInstances.Add(new BrambleInstance
            {
                transform = instance.transform,
                targetScale = targetScale,
                startRotation = startRot,
                endRotation = endRot,
                originalLocalPos = localPos  // 🔹 Guardar la posición local calculada
            });
        }
    }

    private static int RandRangeInt(System.Random rng, int minInclusive, int maxExclusive)
        => rng.Next(minInclusive, maxExclusive);

    private static float RandRangeFloat(System.Random rng, float min, float max)
        => (float)(min + (max - min) * rng.NextDouble());

    private static void ApplyMaterialToRenderers(GameObject root, Material mat)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
        }
    }

    private void ClearSpawnedInstances()
    {
        for (int i = 0; i < spawnedInstances.Count; i++)
        {
            if (spawnedInstances[i] != null && spawnedInstances[i].transform != null)
                Destroy(spawnedInstances[i].transform.gameObject);
        }

        spawnedInstances.Clear();
    }

    // ---------- Gameplay (solo si NO es preview) ----------

    private void Update()
    {
        if (isPreview) return;
        if (spawnedInstances.Count == 0) return;

        // Limpieza de lista visual (ya la tienes)
        for (int i = climbersInside.Count - 1; i >= 0; i--)
        {
            if (climbersInside[i] == null)
                climbersInside.RemoveAt(i);
        }

        // ✅ FIX: si se destruyen dentro, no hay OnTriggerExit -> aquí forzamos el Stop
        if (climbersInside.Count == 0)
        {
            insideCount = 0;
            if (brambleAudioSource != null && brambleAudioSource.isPlaying)
                brambleAudioSource.Stop();
        }
        else
        {
            if (brambleAudioSource != null && !brambleAudioSource.isPlaying)
                brambleAudioSource.Play();
        }

        // --- SHAKE VISUAL (tal cual lo tienes) ---
        // ...

        // --- NUEVO: stamina extra + posible muerte por zarza ---
        if (staminaTracked.Count == 0)
            return;

        var keys = new List<ClimberMovement>(staminaTracked.Keys);

        foreach (var climber in keys)
        {
            if (climber == null)
            {
                staminaTracked.Remove(climber);
                continue;
            }

            if (!staminaTracked.TryGetValue(climber, out BrambleClimberData data))
                continue;

            if (!data.inside)
                continue;

            float prev = data.lastStamina;
            float current = climber.GetCurrentStamina();

            // Gasto base que ha ocurrido este frame (por caminar, habilidades, etc.)
            float delta = Mathf.Max(0f, prev - current);

            if (delta > 0f && staminaMultiplier > 1f)
            {
                // Igual que en el lodo: compensamos el slow para que el coste total
                // sea delta * staminaMultiplier aunque vaya más lento.
                float effectiveFactor = staminaMultiplier;

                if (slowFactor > 0f)
                    effectiveFactor = staminaMultiplier / slowFactor;

                float extra = delta * (effectiveFactor - 1f);
                float newStamina = Mathf.Max(0f, current - extra);

                climber.SetCurrentStamina(newStamina);
                current = newStamina;
            }

            data.lastStamina = current;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (isPreview) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmuneToBramble = false;

        insideCount++;

        if (brambleAudioSource != null && !brambleAudioSource.isPlaying)
            brambleAudioSource.Play();

        if (loadout != null)
        {
            loadout.TryHandleObstacle(ObstacleType.Bramble);
            isImmuneToBramble = loadout.CanHandleObstacle(ObstacleType.Bramble);
        }

        if (isImmuneToBramble)
            return;

        climber.SetExternalSpeedMultiplier(slowFactor);

        if (!climbersInside.Contains(climber))
            climbersInside.Add(climber);

        // 🔹 NUEVO: registrar stamina mientras esté dentro
        if (!staminaTracked.TryGetValue(climber, out BrambleClimberData data))
        {
            data = new BrambleClimberData();
            staminaTracked[climber] = data;
        }

        data.inside = true;
        data.lastStamina = climber.GetCurrentStamina();
    }

    private void OnTriggerExit(Collider other)
    {
        if (isPreview) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        insideCount = Mathf.Max(0, insideCount - 1);
        if (insideCount == 0 && brambleAudioSource != null)
            brambleAudioSource.Stop();

        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null)
            loadout.TryHandleObstacleExit(ObstacleType.Bramble);

        climber.SetExternalSpeedMultiplier(1f);
        climbersInside.Remove(climber);

        // 🔹 NUEVO: dejar de trackear stamina
        if (staminaTracked.TryGetValue(climber, out BrambleClimberData data))
        {
            data.inside = false;
            // si quieres, también puedes hacer: staminaTracked.Remove(climber);
        }
    }
}
