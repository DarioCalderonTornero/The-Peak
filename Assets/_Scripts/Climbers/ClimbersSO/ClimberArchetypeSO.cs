using UnityEngine;

public enum ClimberRarity
{
    None,
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "ClimberArchetype", menuName = "NewClimberArchetype")]
public class ClimberArchetypeSO : ScriptableObject
{
    [Header("Climber Rarity")]
    public string archetypeName = "Default";
    public ClimberRarity rarity = ClimberRarity.None; 

    [Header("Equipment")]
    public EquipmentDefinitionSO[] equipmentPool;

    public int minRandomItems = 0;
    public int maxRandomItems = 1;
}
