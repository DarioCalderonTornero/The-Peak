using UnityEngine;

public class BrambleDefense : BaseDefense
{
    public override void Initialize()
    {
        base.Initialize();
        // Aquí podrías meter lógica futura, pero ahora mismo no hace falta nada.
    }

    [SerializeField] private float slowFactor = 0.7f; // 30% más lento

    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber != null)
            climber.EnterSlowZone(slowFactor);
    }

    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber != null)
            climber.ExitSlowZone(slowFactor);
    }
}
