using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private LayerMask climberLayer;
 

    private ClimberMovement currentSelectedClimber;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    private void Update()
    {
        // Solo detectamos Clic Izquierdo (0)
        if (Input.GetMouseButtonDown(0))
        {
            HandleSelection();
        }
    }

    private void HandleSelection()
    {
        // 1. Si clicamos UI (botones), no hacemos nada
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;


        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Lanzamos rayo SOLO buscando la capa "Climber"
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, climberLayer))
        {
            ClimberMovement clickedClimber = hit.collider.GetComponent<ClimberMovement>();

            // Buscamos en padres por si el collider está en un hijo
            if (clickedClimber == null)
                clickedClimber = hit.collider.GetComponentInParent<ClimberMovement>();

            if (clickedClimber != null)
            {
                // CASO A: Hemos clicado el MISMO que ya teníamos -> Lo quitamos (Toggle)
                if (currentSelectedClimber == clickedClimber)
                {
                    DeselectCurrent();
                }
                // CASO B: Hemos clicado uno NUEVO -> Cambiamos
                else
                {
                    SelectClimber(clickedClimber);
                }
            }
        }

    }
    private void SelectClimber(ClimberMovement newClimber)
    {
        // Deseleccionamos el anterior si existía
        if (currentSelectedClimber != null)
        {
            currentSelectedClimber.SetSelected(false);
        }

        // Seleccionamos el nuevo
        currentSelectedClimber = newClimber;
        currentSelectedClimber.SetSelected(true);

        Debug.Log($"[SelectionManager] Seleccionado: {newClimber.name}");
    }

    private void DeselectCurrent()
    {
        if (currentSelectedClimber != null)
        {
            currentSelectedClimber.SetSelected(false);
            currentSelectedClimber = null;
            Debug.Log("[SelectionManager] Deseleccionado.");
        }
    }
}