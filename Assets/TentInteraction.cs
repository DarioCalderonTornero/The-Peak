using System.Collections.Generic;
using UnityEngine;

public class TentInteraction : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CampGraphBuilder campGraph;

    [Header("Configuración de Clics")]
    [Tooltip("La capa o capas donde están las tiendas de campaña o los colliders invisibles de los campamentos.")]
    [SerializeField] private LayerMask clickableLayers;

    // Guardamos los escaladores que están seleccionados actualmente para poder deseleccionarlos después
    private List<ClimberMovement> currentlySelectedClimbers = new List<ClimberMovement>();

    private void Start()
    {
        // Si no se ha asignado manualmente, lo buscamos en la escena
        if (campGraph == null)
        {
            campGraph = FindObjectOfType<CampGraphBuilder>();
        }
    }

    private void Update()
    {
        // Detectar el clic izquierdo del ratón
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        // Lanzamos un rayo desde la posición del ratón en la pantalla
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, clickableLayers))
        {
            GameObject clickedObject = hit.collider.gameObject;
            CampGraphBuilder.CampNode clickedNode = GetNodeFromClickedObject(clickedObject);

            if (clickedNode != null && clickedNode.HasTent)
            {
                // Si hicimos clic en una tienda válida, mostramos las rutas
                ShowPathsForCamp(clickedNode);
            }
            else
            {
                // Si hicimos clic en algo de esa capa pero no es una tienda, limpiamos
                ClearSelectedPaths();
            }
        }
        else
        {
            // Si hicimos clic en el vacío, limpiamos las selecciones
            ClearSelectedPaths();
        }
    }

    private CampGraphBuilder.CampNode GetNodeFromClickedObject(GameObject clickedObject)
    {
        if (campGraph == null || campGraph.nodes == null) return null;

        foreach (var node in campGraph.nodes)
        {
            // Opción 1: Hicimos clic directamente en la tienda instanciada (o un hijo de ella)
            if (node.instantiatedTent != null &&
               (clickedObject == node.instantiatedTent || clickedObject.transform.IsChildOf(node.instantiatedTent.transform)))
            {
                return node;
            }

            // Opción 2: Hicimos clic en el collider invisible del campamento ("Camp_INVISIBLE")
            // Comparamos por proximidad al nodo, ya que se instancian en la misma posición
            if (clickedObject.name.Contains("Camp_INVISIBLE") &&
                Vector3.Distance(clickedObject.transform.position, node.position) < 0.1f)
            {
                return node;
            }
        }

        return null;
    }

    private void ShowPathsForCamp(CampGraphBuilder.CampNode node)
    {
        // Primero limpiamos cualquier ruta que estuviera dibujada previamente
        ClearSelectedPaths();

        // Activamos la ruta de todos los escaladores presentes en el campamento
        foreach (var climber in node.presentClimbers)
        {
            if (climber != null)
            {
                climber.SetSelected(true);
                currentlySelectedClimbers.Add(climber);
            }
        }
    }

    private void ClearSelectedPaths()
    {
        foreach (var climber in currentlySelectedClimbers)
        {
            if (climber != null)
            {
                // Deseleccionamos al escalador para apagar su LineRenderer
                climber.SetSelected(false);
            }
        }
        currentlySelectedClimbers.Clear();
    }
}