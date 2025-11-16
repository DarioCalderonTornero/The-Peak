using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentDefinition", menuName = "Climbers/Equipment Definition")]
public class EquipmentDefinitionSO : ScriptableObject
{
    [Header("General Info")]
    public string equipmentName;
    public Sprite icon;

    [Header("Which obstacle does this counter?")]
    //public ObstacleType obstacleType;

    [Header("Logic Class (equipment behavior)")]
    public string logicClassName;
}
