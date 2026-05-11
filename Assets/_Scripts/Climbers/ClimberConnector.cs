using UnityEngine;
using System.Collections.Generic; 

public class ClimberConnector : MonoBehaviour
{
    
    private List<Renderer> myRenderers = new List<Renderer>();

    void Start()
    {
        
        Renderer[] allParts = GetComponentsInChildren<Renderer>();

        if (allParts.Length == 0)
        {
            Debug.LogError($"El escalador {name} no tiene ningún Renderer en sus hijos.");
            return;
        }

        ClimberLoadout climberLoadout = GetComponent<ClimberLoadout>();
        Color xRayColor = climberLoadout != null ? climberLoadout.GetHelmetColor() : Color.grey;
        
        if (MountainVision.Instance != null)
        {
            foreach (Renderer r in allParts)
            {
               
                if (r.sharedMaterials.Length < 2)
                {
                    Debug.LogWarning($"La parte '{r.name}' del escalador no tiene el Material de Rayos X (Element 1) asignado.");
                    
                    continue;
                }
               
                myRenderers.Add(r);
                MountainVision.Instance.RegisterClimber(r, xRayColor);
            }

            Debug.Log($"Escalador registrado con {myRenderers.Count} partes del cuerpo.");
        }
    }

    void OnDestroy()
    {
        if (MountainVision.Instance != null)
        {
            foreach (Renderer r in myRenderers)
            {
                if (r != null)
                {
                    MountainVision.Instance.UnregisterClimber(r);
                }
            }
        }
    }
}