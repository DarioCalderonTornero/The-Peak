// ClimberLoadout.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class ClimberLoadout : MonoBehaviour
{
    [Header("Arquetipo del escalador (configuración de equipamiento)")]
    [SerializeField] private ClimberArchetypeSO archetype;

    [Header("Equipamiento asignado en runtime (solo lectura)")]
    private readonly List<EquipmentInstance> equippedItems = new List<EquipmentInstance>();

    [Header("DEBUG – Equipo visible en Inspector (no tocar)")]
    [SerializeField] private string[] debugEquippedItemNames;

    [SerializeField] private Renderer helmetRenderer;
    public IReadOnlyList<EquipmentInstance> EquippedItems => equippedItems;

    [Header("Climber Getters")]
    [SerializeField] private AudioClip pickaxeAudioClip;
    [SerializeField] private AudioClip rockDestroyAudioClip;

    [Header("Initialization")]
    [Tooltip("Si está activo, el loadout se inicializa automáticamente en Awake usando el archetype (modo normal). " +
             "Si vas a forzar equipo desde el SpawnManager, puedes dejarlo activo: la clase detecta si ya fue inicializada.")]
    [SerializeField] private bool autoInitializeOnAwake = true;

    private bool _isInitialized = false;

    private void Awake()
    {
        if (helmetRenderer == null)
        {
            Debug.LogWarning("No helmet renderer");
        }

        if (autoInitializeOnAwake)
        {
            // Importante: solo inicializa si nadie lo ha forzado antes (por pooling, etc.)
            InitializeRandomLoadoutIfNeeded();
        }
    }

    /// <summary>
    /// Inicializa el loadout de forma random según el archetype, pero solo si aún no está inicializado.
    /// </summary>
    public void InitializeRandomLoadoutIfNeeded()
    {
        if (_isInitialized) return;
        InitializeRandomLoadout_Internal();
        _isInitialized = true;
    }

    /// <summary>
    /// Fuerza que el escalador lleve EXACTAMENTE 1 equipo concreto (sin random),
    /// y marca el loadout como inicializado.
    /// </summary>
    public void InitializeForcedSingleEquipment(EquipmentDefinitionSO forcedEquipment)
    {
        equippedItems.Clear();

        if (forcedEquipment == null)
        {
            UpdateDebugNames();
            _isInitialized = true;
            return;
        }

        var instance = CreateEquipmentInstance(forcedEquipment);
        if (instance != null)
        {
            instance.SetupOwner(this);
            instance.Initialize(forcedEquipment.equipmentName);
            equippedItems.Add(instance);
            ChangeClimberColorBasedOnEquipment(forcedEquipment);
        }

        UpdateDebugNames();
        _isInitialized = true;
    }

    /// <summary>
    /// Permite re-inicializar si algún día haces pooling.
    /// </summary>
    public void ResetLoadoutState()
    {
        _isInitialized = false;
        equippedItems.Clear();
        UpdateDebugNames();
    }

    // ----------------- Internals -----------------

    private void InitializeRandomLoadout_Internal()
    {
        equippedItems.Clear();

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
                instance.SetupOwner(this);
                instance.Initialize(chosenDef.equipmentName);
                equippedItems.Add(instance);
                ChangeClimberColorBasedOnEquipment(chosenDef);
            }
        }

        UpdateDebugNames();
    }

    private void ChangeClimberColorBasedOnEquipment(EquipmentDefinitionSO equipment)
    {
        if (helmetRenderer != null && equipment != null)
        {
            helmetRenderer.material.color = equipment.color;
        }
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

    public void TryHandleObstacleExit(ObstacleType obstacleType)
    {
        foreach (var eq in equippedItems)
            eq?.OnExitObstacle(obstacleType);
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

    // GETTERS
    public void PickAxeSound()
    {
        Temporal_Sound_Music.Instance.PlaySound(pickaxeAudioClip, 1.0f);
    }

    public void RockDestroySound()
    {
        Temporal_Sound_Music.Instance.PlaySound(rockDestroyAudioClip, 0.25f);
    }
}
