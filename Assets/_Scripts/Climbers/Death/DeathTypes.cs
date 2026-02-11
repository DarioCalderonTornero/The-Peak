using System;
using UnityEngine;

public enum DeathCause
{
    Unknown,
    BadBerry,
    Quicksand,
    Fall,
    Cold
}

[Serializable]
public struct DeathInfo
{
    public ClimberMovement climber;
    public Vector3 position;
    public DeathCause cause;

    public DeathInfo(ClimberMovement climber, Vector3 position, DeathCause cause)
    {
        this.climber = climber;
        this.position = position;
        this.cause = cause;
    }
}
