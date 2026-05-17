using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClimberLoadout : MonoBehaviour
{
    [Header("Arquetipo del escalador (configuración de equipamiento)")]
    [SerializeField] private ClimberArchetypeSO archetype;

    [Header("Equipamiento asignado en runtime (solo lectura)")]
    private readonly List<EquipmentInstance> equippedItems = new List<EquipmentInstance>();
    private readonly List<EquipmentDefinitionSO> equippedDefinitions = new List<EquipmentDefinitionSO>();

    [Header("DEBUG – Equipo visible en Inspector (no tocar)")]
    [SerializeField] private string[] debugEquippedItemNames;

    [SerializeField] private Renderer helmetRenderer;
    [SerializeField] private Renderer bodyRenderer;
    public IReadOnlyList<EquipmentInstance> EquippedItems => equippedItems;

    [Header("Climber Getters")]
    [SerializeField] private AudioClip pickaxeAudioClip;
    [SerializeField] private AudioClip rockDestroyAudioClip;

    [Header("Initialization")]
    [SerializeField] private bool autoInitializeOnAwake = true;

    [SerializeField] private float counterDelay = 1f;

    public event Action<Color> OnHelmetColorChanged;
    public event Action<EquipmentDefinitionSO> OnEquipmentInitialized;

    public event Action OnCounterStarted;

    private bool _isInitialized = false;

    private ClimberMovement climberMovement;
    [SerializeField] private Animator climberAnimator;
    private ClimberEquipmentVisuals equipmentVisuals;

    private void Awake()
    {
        climberMovement = GetComponent<ClimberMovement>();
        //climberAnimator = GetComponentInChildren<Animator>();
        equipmentVisuals = GetComponent<ClimberEquipmentVisuals>();

        if (climberMovement == null)
            Debug.LogError($"[ClimberLoadout] Sin ClimberMovement en {gameObject.name}");

        if (climberAnimator == null)
            Debug.LogWarning($"[ClimberLoadout] Sin Animator en {gameObject.name}");

        if (helmetRenderer == null)
            Debug.LogWarning($"[ClimberLoadout] Sin helmet renderer en {gameObject.name}");

        if (autoInitializeOnAwake)
            InitializeRandomLoadoutIfNeeded();
    }

    // ─── Inicialización ───────────────────────────────────────────────────────

    public void InitializeRandomLoadoutIfNeeded()
    {
        if (_isInitialized) return;
        InitializeRandomLoadout_Internal();
        _isInitialized = true;
    }

    public void InitializeForcedSingleEquipment(EquipmentDefinitionSO forcedEquipment)
    {
        equippedItems.Clear();
        equippedDefinitions.Clear();

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
            equippedDefinitions.Add(forcedEquipment);
            ChangeClimberColorBasedOnEquipment(forcedEquipment);
        }

        UpdateDebugNames();
        _isInitialized = true;
    }

    public void ResetLoadoutState()
    {
        _isInitialized = false;
        equippedItems.Clear();
        equippedDefinitions.Clear();
        UpdateDebugNames();
    }

    // ─── Internals ────────────────────────────────────────────────────────────

    private void InitializeRandomLoadout_Internal()
    {
        equippedItems.Clear();
        equippedDefinitions.Clear();

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

            if (chosenDef == null) continue;

            var instance = CreateEquipmentInstance(chosenDef);
            if (instance != null)
            {
                instance.SetupOwner(this);
                instance.Initialize(chosenDef.equipmentName);
                equippedItems.Add(instance);
                equippedDefinitions.Add(chosenDef);
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
            OnHelmetColorChanged?.Invoke(equipment.color);
            OnEquipmentInitialized?.Invoke(equipment);
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
            debugEquippedItemNames[i] = equippedItems[i] != null
                ? equippedItems[i].GetType().Name
                : "NULL";
    }

    private EquipmentInstance CreateEquipmentInstance(EquipmentDefinitionSO definition)
    {
        if (definition == null) return null;
        if (string.IsNullOrWhiteSpace(definition.logicClassName)) return null;

        string typeName = definition.logicClassName;
        Type type = Type.GetType(typeName);

        if (type == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) break;
            }
        }

        if (type == null)
        {
            Debug.LogError($"[ClimberLoadout] Tipo '{typeName}' no encontrado. " +
                           $"Revisa 'logicClassName' en el SO '{definition.name}'.");
            return null;
        }

        return Activator.CreateInstance(type) as EquipmentInstance;
    }

    // ─── Obstacles ────────────────────────────────────────────────────────────

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
        if (obstacleType == ObstacleType.None) return true;

        foreach (var eq in equippedItems)
            if (eq != null && eq.CanHandleObstacle(obstacleType)) return true;

        return false;
    }

    // ─── Equipment Sequence ───────────────────────────────────────────────────

    /// <summary>
    /// Para al escalador, saca el equipo a la mano, espera y lo guarda.
    /// Llamado desde OnCounterSuccess de cada EquipmentInstance.
    /// </summary>
    public void StartEquipmentSequence(float duration, ObstacleType obstacleType = ObstacleType.None)
    {
        //if (!gameObject.activeInHierarchy) return;
        StartCoroutine(EquipmentSequenceRoutine(duration, obstacleType));
    }

    private IEnumerator EquipmentSequenceRoutine(float duration, ObstacleType obstacleType = ObstacleType.None)
    {
        // 1. Avisar UI inmediatamente
        OnCounterStarted?.Invoke();

        // 2. Parar al escalador inmediatamente
        RequestClimberStop(duration + counterDelay);

        // 3. Esperar antes de ejecutar la animación
        yield return new WaitForSeconds(counterDelay);

        // 4. Sacar equipo a la mano
        if (equipmentVisuals != null)
        {
            equipmentVisuals.MoveEquipmentToHand();
            equipmentVisuals.PlayHandEffect(obstacleType);
        }

        // 5. Esperar la duración
        yield return new WaitForSeconds(duration);

        // 6. Guardar equipo en la espalda
        if (equipmentVisuals != null)
            equipmentVisuals.MoveEquipmentToBack();
    }

    // ─── API pública / Getters ────────────────────────────────────────────────

    public Color GetHelmetColor()
    {
        if (helmetRenderer != null)
            return helmetRenderer.material.color;
        return Color.white;
    }

    public Color GetBodyColor()
    {
        if (bodyRenderer != null)
            return bodyRenderer.material.color;
        return Color.yellow;
    }

    public EquipmentDefinitionSO GetFirstEquipmentIcon()
    {
        if (equippedDefinitions == null || equippedDefinitions.Count == 0)
            return null;
        return equippedDefinitions[0];
    }

    public void RequestClimberStop(float duration)
        => climberMovement?.StopForSeconds(duration);

    public void TriggerAnimation(string triggerName)
        => climberAnimator?.SetTrigger(triggerName);

    public void PickAxeSound()
        => Temporal_Sound_Music.Instance.PlaySound(pickaxeAudioClip, 1.0f);

    public void RockDestroySound()
        => Temporal_Sound_Music.Instance.PlaySound(rockDestroyAudioClip, 0.25f);
}