using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private LayerMask climberLayer;
    [SerializeField] private LayerMask tentLayer;

    public event Action<ClimberMovement> OnClimberSelected;
    public event Action OnClimberDeselected;
    public event Action<ClimberMovement> OnClimberInspectRequested;

    private ClimberMovement currentSelectedClimber;
    public ClimberMovement CurrentSelected => currentSelectedClimber;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnClimberClickRoute += (_, __) => HandleTapSelection();
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsPointerOverTent()
    {
        if (Camera.main == null) return false;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, Mathf.Infinity, tentLayer);
    }

    private ClimberMovement RaycastClimberUnderMouse()
    {
        if (Camera.main == null) return null;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, climberLayer))
            return hit.collider.GetComponentInParent<ClimberMovement>();
        return null;
    }

    private void HandleTapSelection()
    {
        if (IsPointerOverUI()) return;
        if (IsPointerOverTent()) return;

        ClimberMovement clicked = RaycastClimberUnderMouse();
        if (clicked == null) return;
        if (clicked.IsInsideTent) return;

        if (currentSelectedClimber == clicked)
            DeselectCurrent();
        else
            SelectClimber(clicked, showRoute: true);
    }

    private void SelectClimber(ClimberMovement newClimber, bool showRoute)
    {
        if (currentSelectedClimber != null)
            currentSelectedClimber.SetSelected(false);

        currentSelectedClimber = newClimber;

        if (showRoute)
            currentSelectedClimber.SetSelected(true);

        OnClimberSelected?.Invoke(currentSelectedClimber);
    }

    private void DeselectCurrent()
    {
        if (currentSelectedClimber == null) return;
        currentSelectedClimber.SetSelected(false);
        currentSelectedClimber = null;
        OnClimberDeselected?.Invoke();
    }

    public void ForceDeselect()
    {
        DeselectCurrent();
    }
}