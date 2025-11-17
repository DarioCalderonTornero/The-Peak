using System;
using System.Collections.Generic;
using UnityEngine;

public class ClimberLoadout : MonoBehaviour
{
    [Header("Assigned at runtime")]
    private List<EquipmentInstance> equippedItems = new List<EquipmentInstance>();

    private ClimberArchetypeSO archetype;

    // Tipos de obstáculo soportados (derivados del equipo)
    private List<ObstacleType> supportedObstacleTypes = new List<ObstacleType>();

    private ClimberCapabilities capabilities;

    private void Awake()
    {
        capabilities = GetComponent<ClimberCapabilities>();
    }

    public void InitializeLoadout(ClimberArchetypeSO data)
    {
        archetype = data;
        equippedItems.Clear();
        supportedObstacleTypes.Clear();

        if (archetype == null || archetype.startingEquipment == null)
        {
            Debug.LogWarning("[ClimberLoadout] Archetype or equipment list is null.");
            SyncCapabilities();
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

                if (eq.supportedType != ObstacleType.None &&
                    !supportedObstacleTypes.Contains(eq.supportedType))
                {
                    supportedObstacleTypes.Add(eq.supportedType);
                }
            }
        }

        Debug.Log($"[ClimberLoadout] Loadout initialized with {equippedItems.Count} items. " +
                  $"Supported obstacle types: {string.Join(", ", supportedObstacleTypes)}");

        SyncCapabilities();
    }

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

    public void TryHandleObstacle(ObstacleType obstacleType)
    {
        Debug.Log($"[ClimberLoadout] Climber encountered obstacle: {obstacleType}");

        foreach (var eq in equippedItems)
        {
            eq.OnEncounterObstacle(obstacleType);
        }
    }

    public bool CanHandleObstacleType(ObstacleType type)
    {
        if (capabilities != null)
            return capabilities.CanHandleObstacleType(type);

        return supportedObstacleTypes.Contains(type);
    }

    private void SyncCapabilities()
    {
        if (capabilities != null)
            capabilities.SetCapabilitiesFromTypes(supportedObstacleTypes);
    }
}
