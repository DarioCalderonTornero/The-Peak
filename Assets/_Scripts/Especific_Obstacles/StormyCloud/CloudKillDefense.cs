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

    private class CloudVictimData
    {
        public bool isBeingKilled;
    }

    private Dictionary<ClimberMovement, CloudVictimData> victims =
        new Dictionary<ClimberMovement, CloudVictimData>();

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

        // ── Congelar al escalador inmediatamente nada más entrar ──────────────
        climber.FreezeInPlace();
        climber.SuppressStaminaDeath();

        var data = new CloudVictimData();
        victims[climber] = data;

        // Snapshot de posición en el momento de la captura
        var deathInfo = new GameManager.DeathInfo
        {
            climber = climber,
            position = climber.transform.position,
            cause = DeathCause.StormyCloud
        };

        // Pedimos slot. Cuando llegue nuestro turno, arranca todo el proceso.
        DeathCinematicManager.Instance.RequestSpectacleSlot(() =>
        {
            OnSpectacleSlotGranted(climber, data, deathInfo);
        });
    }

    /// <summary>
    /// El sistema está libre. Arrancamos la secuencia completa:
    /// cámara → muerte notificada → caminar al centro → rayo → explosión.
    /// </summary>
    private void OnSpectacleSlotGranted(ClimberMovement climber, CloudVictimData data,
        GameManager.DeathInfo deathInfo)
    {
        if (climber == null || hasKilled)
        {
            // Escalador ya destruido o nube ya usada: no podemos continuar.
            // El slot se libera automáticamente porque ProcessDeathQueue
            // no recibirá ningún NotifyReadyToProcess y al terminar el
            // bucle vacío llamará a TryStartNextSpectacle.
            // Para evitar el bloqueo, usamos un pequeño truco: empezamos
            // la secuencia pero la cortocircuitamos inmediatamente.
            return;
        }

        data.isBeingKilled = true;

        // 1. Mover cámara al escalador e iniciar estado cinemático AHORA
        DeathCinematicManager.Instance.BeginSpectacleCinematic(climber);

        // 2. Notificar muerte al GameManager (puntos, eventos)
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

        // Esperamos mientras camina
        yield return new WaitForSeconds(delayBeforeDeath);

        if (climber == null) yield break;

        // Congelamos para el impacto del rayo
        climber.FreezeInPlace();

        // VFX del rayo
        if (killVFX != null)
            killVFX.SetActive(true);

        if (Temporal_Sound_Music.Instance != null)
            Temporal_Sound_Music.Instance.Play2DSound(VFXSound, 1.0f);

        // Pequeña pausa para el impacto
        yield return new WaitForSeconds(0.5f);

        if (climber != null)
        {
            deathInfo.position = climber.transform.position;

            // Encola la fase final (explosión) en ProcessDeathQueue
            if (DeathCinematicManager.Instance != null)
                DeathCinematicManager.Instance.NotifyReadyToProcess(deathInfo);

            // El slot se libera dentro de ProcessDeathQueue tras la explosión completa
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