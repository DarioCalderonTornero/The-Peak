using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private LayerMask climberLayer;

    public event Action<ClimberMovement> OnClimberSelected;
    public event Action OnClimberDeselected;

    /// <summary>
    /// HOLD: pedir inspección (cámara + stats). IMPORTANTE: NO activa ruta.
    /// </summary>
    public event Action<ClimberMovement> OnClimberInspectRequested;

    private ClimberMovement currentSelectedClimber;
    public ClimberMovement CurrentSelected => currentSelectedClimber;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (InputManager.Instance != null)
        {
            // TAP -> ruta (selección)
            InputManager.Instance.OnClimberClickRoute += (_, __) => HandleTapSelection();

            // HOLD -> inspección (sin ruta)
            InputManager.Instance.OnClickCameraClimber += (_, __) => HandleHoldInspect();
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private ClimberMovement RaycastClimberUnderMouse()
    {
        if (Camera.main == null) return null;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, climberLayer))
        {
            return hit.collider.GetComponentInParent<ClimberMovement>();
        }
        return null;
    }

    // ---------------- TAP (ruta) ----------------

    private void HandleTapSelection()
    {
        if (IsPointerOverUI()) return;

        ClimberMovement clicked = RaycastClimberUnderMouse();
        if (clicked == null) return;

        // Toggle selección
        if (currentSelectedClimber == clicked)
        {
            DeselectCurrent();
        }
        else
        {
            SelectClimber(clicked, showRoute: true);
        }
    }

    // ---------------- HOLD (inspección) ----------------

    private void HandleHoldInspect()
    {
        if (IsPointerOverUI()) return;

        // En inspección, priorizamos lo que está bajo el ratón.
        // Si no hay nada, usamos el seleccionado actual.
        ClimberMovement hovered = RaycastClimberUnderMouse();
        ClimberMovement target = hovered != null ? hovered : currentSelectedClimber;
        if (target == null) return;

        // REQUISITO: Hold NO debe activar ruta.
        // Por tanto NO llamamos a SetSelected(true) aquí.
        // Solo pedimos inspección del escalador objetivo.
        OnClimberInspectRequested?.Invoke(target);
    }

    private void SelectClimber(ClimberMovement newClimber, bool showRoute)
    {
        if (currentSelectedClimber != null)
            currentSelectedClimber.SetSelected(false);

        currentSelectedClimber = newClimber;

        if (showRoute)
            currentSelectedClimber.SetSelected(true);

        OnClimberSelected?.Invoke(currentSelectedClimber);
        Debug.Log($"[SelectionManager] Seleccionado (ruta): {newClimber.name}");
    }

    private void DeselectCurrent()
    {
        if (currentSelectedClimber == null) return;

        currentSelectedClimber.SetSelected(false);
        currentSelectedClimber = null;

        OnClimberDeselected?.Invoke();
        Debug.Log("[SelectionManager] Deseleccionado.");
    }

    // Llamable desde UI / ESC si quieres salir de inspección y limpiar selección
    public void ForceDeselect()
    {
        DeselectCurrent();
    }
}
