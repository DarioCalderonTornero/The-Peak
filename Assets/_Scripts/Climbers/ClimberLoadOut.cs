using System;
using System.Collections.Generic;
using UnityEngine;

public class ClimberLoadout : MonoBehaviour
{
    [Header("Assigned at runtime")]
    private List<EquipmentInstance> equippedItems = new List<EquipmentInstance>();

    private ClimberArchetypeSO archetype;

    /// <summary>
    /// Initialize the loadout based on the archetype.
    /// This is called by the Climber main controller.
    /// </summary>
    public void InitializeLoadout(ClimberArchetypeSO data)
    {
        archetype = data;
        equippedItems.Clear();

        if (archetype == null || archetype.startingEquipment == null)
        {
            Debug.LogWarning("[ClimberLoadout] Archetype or equipment list is null.");
            return;
        }

        foreach (var eq in archetype.startingEquipment)
        {
            if (eq == null)
            {
                Debug.LogWarning("[ClimberLoadout] Null equipment in archetype.");
                continue;
            }

            var instance = CreateEquipmentInstance(eq);

            if (instance != null)
            {
                instance.Initialize(eq.equipmentName);
                equippedItems.Add(instance);

                Debug.Log($"[ClimberLoadout] Equipped: {eq.equipmentName}");
            }
        }

        Debug.Log($"[ClimberLoadout] Loadout initialized with {equippedItems.Count} items.");
    }

    /// <summary>
    /// Creates the EquipmentInstance using reflection and the logicClassName.
    /// </summary>
    private EquipmentInstance CreateEquipmentInstance(EquipmentDefinitionSO definition)
    {
        if (definition == null)
            return null;

        if (string.IsNullOrWhiteSpace(definition.logicClassName))
        {
            Debug.LogWarning($"[ClimberLoadout] No logicClassName for equipment {definition.equipmentName}");
            return null;
        }

        string typeName = definition.logicClassName;
        Type type = Type.GetType(typeName);

        // Buscar en todas las assemblies si no lo encuentra directo
        if (type == null)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                type = asm.GetType(typeName);
                if (type != null)
                    break;
            }
        }

        if (type == null)
        {
            Debug.LogError($"[ClimberLoadout] Cannot find class: {definition.logicClassName}. " +
                           $"Asegúrate de que el nombre coincide exactamente y de que la clase no está en un namespace.");
            return null;
        }

        var instance = Activator.CreateInstance(type) as EquipmentInstance;

        if (instance == null)
        {
            Debug.LogError($"[ClimberLoadout] {definition.logicClassName} is not an EquipmentInstance.");
        }
        else
        {
            Debug.Log($"[ClimberLoadout] Created equipment instance of type {type.FullName}");
        }

        return instance;
    }

    /// <summary>
    /// Called when the climber hits an obstacle trigger in el mundo.
    /// </summary>
    public void TryHandleObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"[ClimberLoadout] Climber encountered obstacle: {obstacleType}");

        foreach (var eq in equippedItems)
        {
            eq?.OnEncounterObstacle(obstacleType);
        }
    }

    /// <summary>
    /// A nivel de grafo / pathfinding, pregunta si este escalador
    /// puede manejar un obstáculo de cierto tipo en una arista.
    /// </summary>
    public bool CanHandleObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.None)
            return true;

        foreach (var eq in equippedItems)
        {
            if (eq != null && eq.CanHandleObstacle(obstacleType))
            {
                Debug.Log($"[ClimberLoadout] {eq.GetType().Name} CAN handle obstacle {obstacleType}");
                return true;
            }
        }

        Debug.Log($"[ClimberLoadout] No equipment can handle obstacle {obstacleType}");
        return false;
    }
}
