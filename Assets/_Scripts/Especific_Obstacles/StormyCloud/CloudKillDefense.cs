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

        var data = new CloudVictimData();
        victims[climber] = data;
        StartCoroutine(KillAfterDelay(climber, data));
    }

    private IEnumerator KillAfterDelay(ClimberMovement climber, CloudVictimData data)
    {
        if (climber == null) yield break;

        data.isBeingKilled = true;
        climber.FreezeInPlace();
        climber.SuppressStaminaDeath();

        // Construimos el deathInfo una sola vez y lo reutilizamos
        var deathInfo = new GameManager.DeathInfo
        {
            climber = climber,
            position = climber.transform.position,
            cause = DeathCause.StormyCloud
        };

        // Notificamos inmediatamente → HandleClimberDeath ve StormyCloud y solo mueve la cámara
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyClimberDied(deathInfo);
            ClimberDeathPointsManager.Instance?.AddClimberDeathPoints();
            PointsManager.Instance?.AddPoints(15);
        }

        yield return new WaitForSeconds(delayBeforeDeath);

        if (climber == null) yield break;

        if (killVFX != null)
            killVFX.SetActive(true);

        yield return new WaitForSeconds(0.5f);

        if (climber != null)
        {
            // Actualizamos posición al punto actual igual que hace el Geyser
            deathInfo.position = climber.transform.position;

            if (DeathCinematicManager.Instance != null)
                DeathCinematicManager.Instance.NotifyReadyToProcess(deathInfo);
        }

        hasKilled = true;

        yield return new WaitForSeconds(1.5f);

        if (visualChild != null)
            yield return StartCoroutine(AnimatePop(visualChild, originalVisualScale, Vector3.zero));

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