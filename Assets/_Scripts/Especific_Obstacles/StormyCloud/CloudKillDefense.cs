using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudKillDefense : BaseDefense
{
    [Header("Kill Settings")]
    [SerializeField] private float killChance = 0.3f;
    [SerializeField] private float delayBeforeDeath = 2f;

    [Header("References")]
    [SerializeField] private Transform visualChild;
    [SerializeField] private Transform rainObject;
    [SerializeField] private Transform rayObject;
    [SerializeField] private GameObject killVFX;

    [Header("Pop Settings")]
    [SerializeField] private float popCloudDuration = 0.5f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    [SerializeField] private AudioClip VFXAudioClip;
    [SerializeField] private float audioDelay = 0f;
    [SerializeField] private AudioClip counterAirAudioClip;

    [Header("Cloud Disappear")]
    [SerializeField] private float cloudDisappearDelay = 2.5f;

    private bool hasKilled = false;
    private Vector3 originalVisualScale;
    private bool slotCancelled = false;

    private class CloudVictimData
    {
        public bool isBeingKilled;
    }

    private Dictionary<ClimberMovement, CloudVictimData> victims =
        new Dictionary<ClimberMovement, CloudVictimData>();

    private Coroutine keepFrozenRoutine;
    private Coroutine killSequenceRoutine;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        if (visualChild == null && transform.childCount > 0)
            visualChild = transform.GetChild(0);

        if (visualChild != null)
            originalVisualScale = visualChild.localScale;
    }

    [SerializeField] private float cloudHeight = 5f;
    [SerializeField] private Vector3 cloudOffset = new Vector3(0.5f, 0f, 0f);
    [SerializeField] private float RainHeight = 4f;

    private float placedYaw = 0f;

    public void SetPlacedYaw(float yaw)
    {
        placedYaw = yaw;
        var preview = GetComponent<CloudDefensePreview>();
        if (preview != null) preview.StopPreview();
    }

    private bool initialized = false;

    private void Start()
    {
        if (GetComponent<CloudDefensePreview>() != null) return;
        if (!initialized)
            Initialize();
    }

    private void Update()
    {
        if (visualChild != null)
            visualChild.rotation = Quaternion.Euler(-90f, placedYaw, 0f);

        if (rainObject != null)
            rainObject.rotation = Quaternion.Euler(0f, placedYaw, 0f);

        if (rayObject != null)
            rayObject.rotation = Quaternion.Euler(0f, placedYaw, 0f);
    }

    public void Initialize()
    {
        initialized = true;
        var preview = GetComponent<CloudDefensePreview>();
        if (preview != null) Destroy(preview);

        Vector3 rotatedOffset = Quaternion.Euler(0f, placedYaw, 0f) * cloudOffset;
        Vector3 cloudPosition = transform.position + Vector3.up * cloudHeight + rotatedOffset;

        if (visualChild != null)
        {
            // visualChild.SetParent(null);
            visualChild.position = cloudPosition;
            visualChild.rotation = Quaternion.Euler(-90f, placedYaw, 0f);
            StartCoroutine(AnimateAppear(visualChild));
        }

        if (rainObject != null)
        {
            rainObject.SetParent(null);
            rainObject.position = transform.position + Vector3.up * RainHeight;
            rainObject.rotation = Quaternion.Euler(0f, placedYaw, 0f);
        }

        if (rayObject != null)
        {
            rayObject.SetParent(null);
            rayObject.position = transform.position + Vector3.up * cloudHeight;
            rayObject.rotation = Quaternion.Euler(0f, placedYaw, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasKilled) return;
        if (victims.Count > 0) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null && loadout.CanHandleObstacle(ObstacleType.Cloud))
        {
            loadout.TryHandleObstacle(ObstacleType.Cloud);
            StartCoroutine(DelayedDisappear());
            return;
        }

        if (victims.ContainsKey(climber)) return;
        if (Random.value > killChance) return;

        climber.FreezeInPlace();
        climber.SuppressStaminaDeath();

        var data = new CloudVictimData();
        victims[climber] = data;
        slotCancelled = false;

        var deathInfo = new GameManager.DeathInfo
        {
            climber = climber,
            position = climber.transform.position,
            cause = DeathCause.StormyCloud
        };

        if (!DeathCinematicManager.Instance.UseDeathCinematics)
        {
            if (climber != null) Destroy(climber.gameObject);
            ClimberDeathPointsManager.Instance?.AddClimberDeathPoints();
            PointsManager.Instance?.AddPoints(5);
            StartCoroutine(DisappearAndDestroy());
            return;
        }

        if (keepFrozenRoutine != null) StopCoroutine(keepFrozenRoutine);
        keepFrozenRoutine = StartCoroutine(KeepFrozenWhileWaiting(climber));

        DeathCinematicManager.Instance.RequestSpectacleSlot(() =>
        {
            OnSpectacleSlotGranted(climber, data, deathInfo);
        });
    }

    private IEnumerator DelayedDisappear()
    {
        float maxAudioDistance = 30f;

        if (Temporal_Sound_Music.Instance != null && counterAirAudioClip != null)
        {
            Temporal_Sound_Music.Instance.Play3DSound(counterAirAudioClip, transform.position, 1f, 15f, maxAudioDistance);
        }
        yield return new WaitForSeconds(1f);
        StartCoroutine(DisappearAndDestroy());
    }

    private IEnumerator KeepFrozenWhileWaiting(ClimberMovement climber)
    {
        while (climber != null && !slotCancelled && !hasKilled)
        {
            if (DeathCinematicManager.Instance != null && DeathCinematicManager.Instance.SkipRequested)
            {
                HandleSkip(climber, new GameManager.DeathInfo
                {
                    climber = climber,
                    position = climber.transform.position,
                    cause = DeathCause.StormyCloud
                });
                yield break;
            }

            climber.FreezeInPlace();
            yield return new WaitForSeconds(0.1f);
        }
        keepFrozenRoutine = null;
    }

    private void OnSpectacleSlotGranted(ClimberMovement climber, CloudVictimData data,
        GameManager.DeathInfo deathInfo)
    {
        if (keepFrozenRoutine != null)
        {
            StopCoroutine(keepFrozenRoutine);
            keepFrozenRoutine = null;
        }

        if (climber == null || hasKilled || slotCancelled)
        {
            DeathCinematicManager.Instance.ForceReleaseSpectacleSlot();
            return;
        }

        data.isBeingKilled = true;

        DeathCinematicManager.Instance.BeginSpectacleCinematic(climber);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyClimberDied(deathInfo);
            ClimberDeathPointsManager.Instance?.AddClimberDeathPoints();
            PointsManager.Instance?.AddPoints(5);
        }

        if (killSequenceRoutine != null) StopCoroutine(killSequenceRoutine);
        killSequenceRoutine = StartCoroutine(KillSequence(climber, data, deathInfo));
    }

    private void HandleSkip(ClimberMovement climber, GameManager.DeathInfo deathInfo)
    {
        if (killSequenceRoutine != null) { StopCoroutine(killSequenceRoutine); killSequenceRoutine = null; }
        if (keepFrozenRoutine != null) { StopCoroutine(keepFrozenRoutine); keepFrozenRoutine = null; }

        hasKilled = true;

        if (climber != null)
            UnityEngine.Object.Destroy(climber.gameObject);

        DeathCinematicManager.Instance.ForceEndSpectacle(deathInfo);
        Destroy(gameObject);
    }

    private IEnumerator KillSequence(ClimberMovement climber, CloudVictimData data,
        GameManager.DeathInfo deathInfo)
    {
        if (climber == null) yield break;

        climber.MoveToWorldPosition(transform.position);

        float elapsed = 0f;
        while (elapsed < delayBeforeDeath)
        {
            if (DeathCinematicManager.Instance != null && DeathCinematicManager.Instance.SkipRequested)
            {
                HandleSkip(climber, deathInfo);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (climber == null) yield break;

        climber.FreezeInPlace();

        // Activar VFX
        if (killVFX != null)
            killVFX.SetActive(true);

        // Sonido con delay configurable respecto al VFX
        StartCoroutine(PlaySoundDelayed(audioDelay));

        float elapsed2 = 0f;
        while (elapsed2 < 0.5f)
        {
            if (DeathCinematicManager.Instance != null && DeathCinematicManager.Instance.SkipRequested)
            {
                HandleSkip(climber, deathInfo);
                yield break;
            }

            elapsed2 += Time.deltaTime;
            yield return null;
        }

        if (climber != null)
        {
            deathInfo.position = climber.transform.position;

            if (DeathCinematicManager.Instance != null)
                DeathCinematicManager.Instance.NotifyReadyToProcess(deathInfo);
        }

        hasKilled = true;

        // Esperar antes de desaparecer la nube
        yield return new WaitForSeconds(cloudDisappearDelay);

        isDisappearing = true;
        bool disappeared = false;
        StartCoroutine(AnimateDisappear(visualChild, () => disappeared = true));
        yield return new WaitUntil(() => disappeared);

        if (visualChild != null) Destroy(visualChild.gameObject);
        Destroy(gameObject);
        killSequenceRoutine = null;
    }

    private IEnumerator PlaySoundDelayed(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (Temporal_Sound_Music.Instance != null)
            Temporal_Sound_Music.Instance.Play2DSound(VFXAudioClip, 1.0f);
    }

    private IEnumerator AnimateAppear(Transform target)
    {
        if (target == null) yield break;

        var renderer = target.GetComponent<Renderer>();
        if (renderer == null) yield break;

        Material mat = renderer.material;

        float elapsed = 0f;
        while (elapsed < popCloudDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popCloudDuration);
            mat.SetFloat("_AlphaClip", Mathf.Lerp(1f, 0f, t));
            yield return null;
        }
        mat.SetFloat("_AlphaClip", 0f);
    }

    private IEnumerator AnimateDisappear(Transform target, System.Action onComplete)
    {
        if (target == null) { onComplete?.Invoke(); yield break; }

        var renderer = target.GetComponent<Renderer>();
        if (renderer == null) { onComplete?.Invoke(); yield break; }

        Material mat = renderer.material;

        float elapsed = 0f;
        while (elapsed < popCloudDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popCloudDuration);
            mat.SetFloat("_AlphaClip", Mathf.Lerp(0f, 1f, t));
            yield return null;
        }
        mat.SetFloat("_AlphaClip", 1f);
        onComplete?.Invoke();
    }

    public IEnumerator DisappearAndDestroy()
    {
        isDisappearing = true;
        bool done = false;
        StartCoroutine(AnimateDisappear(visualChild, () => done = true));
        yield return new WaitUntil(() => done);
        if (visualChild != null) Destroy(visualChild.gameObject);
        Destroy(gameObject);
    }

    private bool isDisappearing = false;

    private void OnDestroy()
    {
        if (!isDisappearing && visualChild != null)
            Destroy(visualChild.gameObject);
        if (rainObject != null)
            Destroy(rainObject.gameObject);
        if (rayObject != null)
            Destroy(rayObject.gameObject);
    }
}