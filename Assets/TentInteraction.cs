using UnityEngine;
using System.Collections.Generic;

public class TentInteraction : MonoBehaviour
{
    /*private static TentInteraction _selectedTent; // La única tienda activa en el juego
    private List<ClimberMovement> occupants = new List<ClimberMovement>();

    public void RegisterClimber(ClimberMovement climber)
    {
        if (!occupants.Contains(climber)) occupants.Add(climber);
    }

    public void UnregisterClimber(ClimberMovement climber)
    {
        if (occupants.Contains(climber)) occupants.Remove(climber);
    }

    private void OnMouseDown()
    {
        // Si clicamos en una tienda distinta, "limpiamos" la anterior
        if (_selectedTent != null && _selectedTent != this)
        {
            _selectedTent.HideAllOccupantPaths();
        }

        _selectedTent = this;
        ShowAllOccupantPaths();
    }

    private void Update()
    {
        // Si esta es la tienda seleccionada y el jugador hace clic izquierdo...
        if (_selectedTent == this && Input.GetMouseButtonDown(0))
        {
            // Lanzamos un rayo para ver si el clic fue FUERA de la tienda
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform != this.transform)
                {
                    DeselectTent();
                }
            }
            else
            {
                // Clic al vacío (al cielo o fuera de colliders)
                DeselectTent();
            }
        }
    }

    private void ShowAllOccupantPaths()
    {
        foreach (var climber in occupants)
        {
            if (climber != null) climber.ShowPathFromTent(true);
        }
    }

    private void HideAllOccupantPaths()
    {
        foreach (var climber in occupants)
        {
            if (climber != null) climber.ShowPathFromTent(false);
        }
    }

    private void DeselectTent()
    {
        HideAllOccupantPaths();
        if (_selectedTent == this) _selectedTent = null;
    }

    private void OnDestroy()
    {
        // Limpieza de seguridad si la tienda se destruye mientras está seleccionada
        if (_selectedTent == this) _selectedTent = null;
    }*/
}