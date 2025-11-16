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
        if (string.IsNullOrWhiteSpace(definition.logicClassName))
        {
            Debug.LogWarning($"[ClimberLoadout] No logicClassName for equipment {definition.equipmentName}");
            return null;
        }

        Type type = Type.GetType(definition.logicClassName);

        if (type == null)
        {
            Debug.LogError($"[ClimberLoadout] Cannot find class: {definition.logicClassName}");
            return null;
        }

        var instance = Activator.CreateInstance(type) as EquipmentInstance;

        if (instance == null)
        {
            Debug.LogError($"[ClimberLoadout] {definition.logicClassName} is not an EquipmentInstance.");
        }

        return instance;
    }

    /// <summary>
    /// Called when the climber hits an obstacle.
    /// </summary>
    public void TryHandleObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"[ClimberLoadout] Climber encountered obstacle: {obstacleType}");

        foreach (var eq in equippedItems)
        {
            eq.OnEncounterObstacle(obstacleType);
        }
    }
}
