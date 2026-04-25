using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudKillDefense : BaseDefense
{
    [Header("Kill Settings")]
    [SerializeField] private float killChance = 0.3f;
    [SerializeField] private float delayBeforeDeath = 2f;

    [Header("References")]
    [Tooltip("Arrastra aquí el objeto hijo que contiene el modelo 3D de la nube")]
    [SerializeField] private Transform visualChild;
    [SerializeField] private GameObject killVFX;

    [Header("Pop Settings")]
    [SerializeField] private float popDuration = 0.5f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] private AudioClip VFXSound;

    private bool hasKilled = false;
    private Vector3 originalVisualScale;

    // FIX Bug 2: flag para cancelar si el escalador desaparece antes
    // de que le llegue el slot
    private bool slotCancelled = false;

    private class CloudVictimData
    {
        public bool isBeingKilled;
    }

    private Dictionary<ClimberMovement, CloudVictimData> victims =
        new Dictionary<ClimberMovement, CloudVictimData>();

    // FIX Bug 4: coroutine que mantiene al escalador congelado
    // mientras espera en la cola de espectáculos
    private Coroutine keepFrozenRoutine;

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

        // FIX Bug 4: mantenemos al escalador congelado entre turnos
        if (keepFrozenRoutine != null) StopCoroutine(keepFrozenRoutine);
        keepFrozenRoutine = StartCoroutine(KeepFrozenWhileWaiting(climber));

        DeathCinematicManager.Instance.RequestSpectacleSlot(() =>
        {
            OnSpectacleSlotGranted(climber, data, deathInfo);
        });
    }

    // FIX Bug 4: reaplica el congelado periódicamente mientras espera slot
    private IEnumerator KeepFrozenWhileWaiting(ClimberMovement climber)
    {
        while (climber != null && !slotCancelled && !hasKilled)
        {
            climber.FreezeInPlace();
            yield return new WaitForSeconds(0.1f);
        }
        keepFrozenRoutine = null;
    }

    private void OnSpectacleSlotGranted(ClimberMovement climber, CloudVictimData data,
        GameManager.DeathInfo deathInfo)
    {
        // Paramos el keep-frozen
        if (keepFrozenRoutine != null)
        {
            StopCoroutine(keepFrozenRoutine);
            keepFrozenRoutine = null;
        }

        // FIX Bug 2: si el escalador desapareció o la nube ya mató,
        // liberamos el slot para evitar el deadlock
        if (climber == null || hasKilled || slotCancelled)
        {
            DeathCinematicManager.Instance.ForceReleaseSpectacleSlot();
            return;
        }

        data.isBeingKilled = true;

        // 1. Mover cámara e iniciar estado cinemático
        DeathCinematicManager.Instance.BeginSpectacleCinematic(climber);

        // 2. Notificar muerte al GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyClimberDied(deathInfo);
            ClimberDeathPointsManager.Instance?.AddClimberDeathPoints();
            PointsManager.Instance?.AddPoints(5);
        }

        // 3. Arrancar secuencia espectacular
        StartCoroutine(KillSequence(climber, data, deathInfo));
    }

    private IEnumerator KillSequence(ClimberMovement climber, CloudVictimData data,
        GameManager.DeathInfo deathInfo)
    {
        if (climber == null) yield break;

        // El escalador camina al centro de la nube
        climber.MoveToWorldPosition(transform.position);

        yield return new WaitForSeconds(delayBeforeDeath);

        if (climber == null) yield break;

        climber.FreezeInPlace();

        if (killVFX != null)
            killVFX.SetActive(true);

        if (Temporal_Sound_Music.Instance != null)
            Temporal_Sound_Music.Instance.Play2DSound(VFXSound, 1.0f);

        yield return new WaitForSeconds(0.5f);

        if (climber != null)
        {
            deathInfo.position = climber.transform.position;

            if (DeathCinematicManager.Instance != null)
                DeathCinematicManager.Instance.NotifyReadyToProcess(deathInfo);
        }

        hasKilled = true;

        yield return new WaitForSeconds(1.5f);

        Destroy(gameObject);
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