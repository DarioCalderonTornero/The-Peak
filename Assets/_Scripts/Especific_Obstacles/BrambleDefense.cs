using System.Collections.Generic;
using UnityEngine;

public class BrambleDefense : BaseDefense
{
    [Header("Ralentización")]
    [SerializeField] private float slowFactor = 0.6f;

    [Header("Stamina extra en zarzas")]
    [SerializeField] private float staminaMultiplier = 1.05f;

    [Header("Bramble Sound")]
    [SerializeField] private AudioSource brambleAudioSource;

    private int insideCount = 0;

    private class BrambleClimberData
    {
        public float lastStamina;
        public bool inside;
        public bool wasImmune;
    }

    private readonly Dictionary<ClimberMovement, BrambleClimberData> staminaTracked =
        new Dictionary<ClimberMovement, BrambleClimberData>();

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
        else
            Debug.LogWarning("[BrambleDefense] No hay Collider en el objeto de zarzas, el área no funcionará.");
    }

    public override void Initialize()
    {
        base.Initialize();
    }

    private void Update()
    {
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
                float extra = delta * (staminaMultiplier - 1f);
                float newStamina = Mathf.Max(0f, current - extra);

                climber.SetCurrentStamina(newStamina);
                current = newStamina;

                if (current <= 0f)
                {
                    GameManager.Instance?.NotifyClimberDied(new GameManager.DeathInfo
                    {
                        climber = climber,
                        position = climber.transform.position,
                        cause = DeathCause.Bramble
                    });
                    ClimberDeathPointsManager.Instance?.AddClimberDeathPoints();
                    PointsManager.Instance?.AddPoints(5);

                    climber.SuppressStaminaDeath();
                    staminaTracked.Remove(climber);
                    continue;
                }
            }

            data.lastStamina = current;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
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
    }

    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        insideCount = Mathf.Max(0, insideCount - 1);

        if (insideCount == 0 && brambleAudioSource != null)
            brambleAudioSource.Stop();

        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null)
            loadout.TryHandleObstacleExit(ObstacleType.Bramble);

        if (staminaTracked.TryGetValue(climber, out BrambleClimberData data))
        {
            if (!data.wasImmune)
                climber.SetExternalSpeedMultiplier(0.3f);

            data.inside = false;
        }
        else
        {
            climber.SetExternalSpeedMultiplier(0.3f);
        }
    }
}