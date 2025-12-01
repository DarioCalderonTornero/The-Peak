using System;
using System.Collections.Generic;
using UnityEngine;

public class ClimberLoadout : MonoBehaviour
{
    [Header("Arquetipo del escalador (configuración de equipamiento)")]
    [SerializeField] private ClimberArchetypeSO archetype;

    [Header("Equipamiento asignado en runtime (solo lectura)")]
    private List<EquipmentInstance> equippedItems = new();

    [Header("DEBUG – Equipo visible en Inspector (no tocar)")]
    [SerializeField] private string[] debugEquippedItemNames;

    public IReadOnlyList<EquipmentInstance> EquippedItems => equippedItems;

    private void Awake()
    {
        InitializeRandomLoadout();
    }

    private void InitializeRandomLoadout()
    {
        equippedItems.Clear();

        // Simplemente no equipa nada.
        if (archetype == null || archetype.equipmentPool == null || archetype.equipmentPool.Length == 0)
        {
            UpdateDebugNames();
            return;
        }

        var pool = new List<EquipmentDefinitionSO>(archetype.equipmentPool);
        int maxItems = Mathf.Clamp(archetype.maxRandomItems, 0, pool.Count);
        int minItems = Mathf.Clamp(archetype.minRandomItems, 0, maxItems);

        int itemsToEquip = UnityEngine.Random.Range(minItems, maxItems + 1);

        if (itemsToEquip == 0)
        {
            UpdateDebugNames();
            return;
        }

        for (int i = 0; i < itemsToEquip && pool.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            EquipmentDefinitionSO chosenDef = pool[index];
            pool.RemoveAt(index);

            if (chosenDef == null)
                continue;

            var instance = CreateEquipmentInstance(chosenDef);
            if (instance != null)
            {
                instance.Initialize(chosenDef.equipmentName);
                equippedItems.Add(instance);
            }
        }

        UpdateDebugNames();
    }

    private void UpdateDebugNames()
    {
        if (equippedItems == null || equippedItems.Count == 0)
        {
            debugEquippedItemNames = Array.Empty<string>();
            return;
        }

        debugEquippedItemNames = new string[equippedItems.Count];

        for (int i = 0; i < equippedItems.Count; i++)
        {
            debugEquippedItemNames[i] =
                equippedItems[i] != null ? equippedItems[i].GetType().Name : "NULL";
        }
    }

    private EquipmentInstance CreateEquipmentInstance(EquipmentDefinitionSO definition)
    {
        if (definition == null) return null;
        if (string.IsNullOrWhiteSpace(definition.logicClassName)) return null;

        string typeName = definition.logicClassName;
        Type type = Type.GetType(typeName);

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
            return null;

        return Activator.CreateInstance(type) as EquipmentInstance;
    }

    public void TryHandleObstacle(ObstacleType obstacleType)
    {
        foreach (var eq in equippedItems)
            eq?.OnEncounterObstacle(obstacleType);
    }

    public bool CanHandleObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.None)
            return true;

        foreach (var eq in equippedItems)
        {
            if (eq != null && eq.CanHandleObstacle(obstacleType))
                return true;
        }

        return false;
    }
}
