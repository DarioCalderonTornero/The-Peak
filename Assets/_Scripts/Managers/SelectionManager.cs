using System;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    public event Action<ClimberMovement> OnClimberSelected;
    public event Action OnClimberDeselected;

    private ClimberMovement currentSelectedClimber;
    public ClimberMovement CurrentSelected => currentSelectedClimber;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void HandleClimberClicked(ClimberMovement clicked)
    {
        if (currentSelectedClimber == clicked)
            Deselect();
        else
            SelectClimber(clicked);
    }

    private void SelectClimber(ClimberMovement climber)
    {
        if (currentSelectedClimber != null)
            currentSelectedClimber.SetSelected(false);

        currentSelectedClimber = climber;
        currentSelectedClimber.SetSelected(true);
        OnClimberSelected?.Invoke(currentSelectedClimber);
    }

    public void Deselect() => DeselectCurrent();

    private void DeselectCurrent()
    {
        if (currentSelectedClimber == null) return;
        currentSelectedClimber.SetSelected(false);
        currentSelectedClimber = null;
        OnClimberDeselected?.Invoke();
        ClimberListUI.Instance?.OnWorldClimberDeselected();
    }

    public void ForceDeselect() => DeselectCurrent();
}