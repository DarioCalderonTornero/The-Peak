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
    [SerializeField] private float shakeSpeed = 25f;
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
    [SerializeField] private int layoutSeed = 0;
    [SerializeField] private Material previewMaterialOverride;

    private int insideCount = 0;
    private Collider areaCollider;
    private Coroutine spawnRoutine;

    private readonly List<ClimberMovement> climbersInside = new List<ClimberMovement>();

    private class BrambleClimberData
    {
        public float lastStamina;
        public bool inside;
        public bool wasImmune;
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

        if (isPreview && bramblePrefab != null && layoutSeed != 0)
            GenerateLayout(layoutSeed, previewMaterialOverride, animate: false);
    }

    private void Start()
    {
        DeathCinematicManager.Instance.OnCinematicFinished += DeathCinematicManager_OnCinematicFinished;
    }

    private void DeathCinematicManager_OnCinematicFinished(object sender, System.EventArgs e)
    {
        //throw new System.NotImplementedException();
    }

    public override void Initialize()
    {
        base.Initialize();

        if (layoutSeed == 0)
            layoutSeed = Random.Range(int.MinValue, int.MaxValue);

        PlaySpawnVfx();

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround(layoutSeed, previewMat: null, animate: true));
    }

    private void PlaySpawnVfx()
    {
        if (spawnVfxPrefab == null) return;
        VisualEffect vfx = Instantiate(spawnVfxPrefab, transform.position, transform.rotation, null);
        Destroy(vfx.gameObject, spawnVfxDuration);
    }

    // ---------- API para Preview / Runtime ----------

    public void SetupPreview(int seed, Material previewMat)
    {
        isPreview = true;
        layoutSeed = seed;
        previewMaterialOverride = previewMat;
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

        BuildLayout(seed, previewMat);

        if (!animate)
        {
            foreach (var inst in spawnedInstances)
            {
                if (inst.transform == null) continue;
                inst.transform.localScale = inst.targetScale;
                inst.transform.localRotation = inst.endRotation;
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
                inst.transform.localScale = inst.targetScale * eased;
                inst.transform.localRotation = Quaternion.Slerp(inst.startRotation, inst.endRotation, eased);
            }
            yield return null;
        }

        foreach (var inst in spawnedInstances)
        {
            if (inst.transform == null) continue;
            inst.transform.localScale = inst.targetScale;
            inst.transform.localRotation = inst.endRotation;
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
            Debug.LogWarning("[BrambleDefense] El collider no es BoxCollider, se usará el punto central.");

        for (int i = 0; i < count; i++)
        {
            Vector3 localPos = Vector3.zero;

            if (box != null)
            {
                Vector3 halfSize = box.size * 0.5f;
                float randX = RandRangeFloat(rng, -halfSize.x, halfSize.x);
                float randZ = RandRangeFloat(rng, -halfSize.z, halfSize.z);
                localPos = new Vector3(randX, 0f, randZ) + box.center;
            }

            GameObject instance = Instantiate(bramblePrefab, transform);
            instance.transform.localPosition = localPos;

            float randomY = RandRangeFloat(rng, 0f, 360f);
            Quaternion startRot = Quaternion.Euler(-90f, randomY, 0f);
            Quaternion endRot = Quaternion.Euler(90f, randomY, 0f);
            instance.transform.localRotation = startRot;

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
                originalLocalPos = localPos
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
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
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

    // ---------- Gameplay ----------

    private void Update()
    {
        if (isPreview) return;
        if (spawnedInstances.Count == 0) return;

        for (int i = climbersInside.Count - 1; i >= 0; i--)
        {
            if (climbersInside[i] == null)
                climbersInside.RemoveAt(i);
        }

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

        if (staminaTracked.Count == 0) return;

        var keys = new List<ClimberMovement>(staminaTracked.Keys);

        foreach (var climber in keys)
        {
            if (climber == null)
            {
                staminaTracked.Remove(climber);
                continue;
            }

            if (!staminaTracked.TryGetValue(climber, out BrambleClimberData data)) continue;
            if (!data.inside) continue;

            float prev = data.lastStamina;
            float current = climber.GetCurrentStamina();
            float delta = Mathf.Max(0f, prev - current);

            if (delta > 0f && staminaMultiplier > 1f)
            {
                float effectiveFactor = slowFactor > 0f
                    ? staminaMultiplier / slowFactor
                    : staminaMultiplier;

                float extra = delta * (effectiveFactor - 1f);
                float newStamina = Mathf.Max(0f, current - extra);

                climber.SetCurrentStamina(newStamina);
                current = newStamina;

                if (current <= 0f)
                {
                    // Nosotros gestionamos esta muerte: notificamos con causa Bramble
                    // y bloqueamos que ClimberMovement la dispare de nuevo como Stamina
                    GameManager.Instance?.NotifyClimberDied(new GameManager.DeathInfo
                    {
                        climber = climber,
                        position = climber.transform.position,
                        cause = DeathCause.Bramble
                    });
                    ClimberDeathPointsManager.Instance?.AddClimberDeathPoints();
                    PointsManager.Instance?.AddPoints(10);

                    climber.SuppressStaminaDeath();
                    staminaTracked.Remove(climber);
                    climbersInside.Remove(climber);
                    continue;
                }
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
        bool isImmune = loadout != null && loadout.CanHandleObstacle(ObstacleType.Bramble);

        insideCount++;

        if (brambleAudioSource != null && !brambleAudioSource.isPlaying)
            brambleAudioSource.Play();

        if (loadout != null)
            loadout.TryHandleObstacle(ObstacleType.Bramble);

        if (!staminaTracked.TryGetValue(climber, out BrambleClimberData data))
        {
            data = new BrambleClimberData();
            staminaTracked[climber] = data;
        }

        data.inside = true;
        data.wasImmune = isImmune;
        data.lastStamina = climber.GetCurrentStamina();

        if (isImmune) return;

        climber.SetExternalSpeedMultiplier(slowFactor);

        if (!climbersInside.Contains(climber))
            climbersInside.Add(climber);
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

        // Solo restauramos velocidad si fuimos nosotros quienes la redujimos
        if (staminaTracked.TryGetValue(climber, out BrambleClimberData data))
        {
            if (!data.wasImmune)
                climber.SetExternalSpeedMultiplier(1f);

            data.inside = false;
        }
        else
        {
            climber.SetExternalSpeedMultiplier(1f);
        }

        climbersInside.Remove(climber);
    }
}