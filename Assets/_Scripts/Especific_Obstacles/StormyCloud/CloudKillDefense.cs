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
    [SerializeField] private GameObject killVFX;

    [Header("Pop Settings")]
    [SerializeField] private float popDuration = 0.5f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private AudioClip VFXSound;

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

    private void Start()
    {
        if (visualChild != null)
            StartCoroutine(AnimatePop(visualChild, Vector3.zero, originalVisualScale));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasKilled) return;
        if (victims.Count > 0) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;
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

        if (keepFrozenRoutine != null) StopCoroutine(keepFrozenRoutine);
        keepFrozenRoutine = StartCoroutine(KeepFrozenWhileWaiting(climber));

        DeathCinematicManager.Instance.RequestSpectacleSlot(() =>
        {
            OnSpectacleSlotGranted(climber, data, deathInfo);
        });
    }

    private IEnumerator KeepFrozenWhileWaiting(ClimberMovement climber)
    {
        while (climber != null && !slotCancelled && !hasKilled)
        {
            // Comprobar skip mientras esperamos el slot
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
            // Comprobar skip durante la secuencia
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

        if (killVFX != null)
            killVFX.SetActive(true);

        if (Temporal_Sound_Music.Instance != null)
            Temporal_Sound_Music.Instance.Play2DSound(VFXSound, 1.0f);

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

        yield return new WaitForSeconds(1.5f);

        Destroy(gameObject);
        killSequenceRoutine = null;
    }

    private IEnumerator AnimatePop(Transform target, Vector3 start, Vector3 end)
    {
        if (target == null) yield break;

        float elapsed = 0;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / popDuration;
            target.localScale = Vector3.Lerp(start, end, popCurve.Evaluate(percent));
            yield return null;
        }

        target.localScale = end;
    }
}