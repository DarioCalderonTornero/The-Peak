using UnityEngine;

[CreateAssetMenu(fileName = "ClimberArchetype", menuName = "Climbers/New Archetype")]
public class ClimberArchetypeSO : ScriptableObject
{
    [Header("General Info")]
    public string archetypeName;
    public Sprite icon;

    [Header("Stats Base")]
    public float maxHealth = 100f;
    public float baseSpeed = 3.5f;
    public float resolve = 1f; //Energy

    [Header("Default equipment")]
    public EquipmentDefinitionSO[] startingEquipment;
}
