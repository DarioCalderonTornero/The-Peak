using UnityEngine;
using UnityEngine.AI;

public class ClimberConfig : MonoBehaviour
{
    [Header("Archetype")]
    public ClimberArchetypeSO archetype;

    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private ClimberLoadout loadout;

    // Runtime stats (for future use)
    private float currentHealth;
    private float resolve;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (loadout == null)
            loadout = GetComponent<ClimberLoadout>();

        InitializeFromArchetype();
    }

    /// <summary>
    /// Llamable desde fuera (SpawnManager) para aplicar un arquetipo concreto a este escalador.
    /// </summary>
    public void ApplyArchetype(ClimberArchetypeSO newArchetype)
    {
        archetype = newArchetype;
        InitializeFromArchetype();
    }

    private void InitializeFromArchetype()
    {
        if (archetype == null)
        {
            Debug.LogWarning("[ClimberConfig] No archetype assigned to climber.");
            return;
        }

        // Base stats
        currentHealth = archetype.maxHealth;
        resolve = archetype.resolve;

        // Apply movement stats
        if (agent != null)
        {
            agent.speed = archetype.baseSpeed;
            Debug.Log($"[ClimberConfig] NavMeshAgent speed set to {agent.speed} from archetype {archetype.archetypeName}.");
        }

        // Initialize equipment loadout
        if (loadout != null)
        {
            loadout.InitializeLoadout(archetype);
        }
        else
        {
            Debug.LogWarning("[ClimberConfig] No ClimberLoadout found on this climber.");
        }

        int equipmentCount = archetype.startingEquipment != null ? archetype.startingEquipment.Length : 0;
        Debug.Log($"[ClimberConfig] Climber initialized. HP: {currentHealth}, Resolve: {resolve}, Equipment: {equipmentCount}.");
    }
}
