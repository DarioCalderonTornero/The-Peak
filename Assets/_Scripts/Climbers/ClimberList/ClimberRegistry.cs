using System;
using System.Collections.Generic;
using UnityEngine;

public class ClimberRegistry : MonoBehaviour
{
    public static ClimberRegistry Instance { get; private set; }

    public event Action<ClimberMovement> OnClimberRegistered;
    public event Action<ClimberMovement> OnClimberUnregistered;

    private readonly List<ClimberMovement> climbers = new();
    public IReadOnlyList<ClimberMovement> Climbers => climbers;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(ClimberMovement climber)
    {
        if (climber == null || climbers.Contains(climber)) return;
        climbers.Add(climber);
        OnClimberRegistered?.Invoke(climber);
    }

    public void Unregister(ClimberMovement climber)
    {
        if (climber == null || !climbers.Remove(climber)) return;
        OnClimberUnregistered?.Invoke(climber);
    }
}