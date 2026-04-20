using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudKillDefense : BaseDefense
{
    [Header("Kill Settings")]
    [SerializeField] private float killChance = 0.3f;
    [SerializeField] private float delayBeforeDeath = 2f;

    private bool hasKilled = false;

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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasKilled) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        if (victims.ContainsKey(climber)) return;

        // 🎲 Tirada de probabilidad
        if (Random.value > killChance) return;

        var data = new CloudVictimData();
        victims[climber] = data;

        StartCoroutine(KillAfterDelay(climber, data));
    }

    private IEnumerator KillAfterDelay(ClimberMovement climber, CloudVictimData data)
    {
        if (climber == null) yield break;

        data.isBeingKilled = true;

        // 🧊 Lo dejamos quieto
        climber.SetExternalSpeedMultiplier(0f);

        yield return new WaitForSeconds(delayBeforeDeath);

        if (climber != null)
        {
            // 🔔 Notificar muerte (igual que haces en Lodo)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyClimberDied(new GameManager.DeathInfo
                {
                    climber = climber,
                    position = climber.transform.position,
                    // cause = DeathCause.Special // crea uno si quieres tipo Cloud
                });

                ClimberDeathPointsManager.Instance.AddClimberDeathPoints();
                PointsManager.Instance.AddPoints(15);
            }

            Destroy(climber.gameObject);
        }

        hasKilled = true;

        // 💨 La nube desaparece tras matar
        Destroy(gameObject);
    }
}