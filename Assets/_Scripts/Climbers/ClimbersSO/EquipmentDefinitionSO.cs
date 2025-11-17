using UnityEngine;

[CreateAssetMenu(menuName = "Climbers/EquipmentDefinitionSO")]
public class EquipmentDefinitionSO : ScriptableObject
{
    [Header("Basic Info")]
    public string equipmentName;

    [Tooltip("Nombre EXACTO de la clase que implementa el comportamiento. Ej: 'RockBreakerEquipment'.")]
    public string logicClassName;

    [Header("Obstacle Capability")]
    [Tooltip("Tipo de obstáculo que este equipo puede contrarrestar.")]
    public ObstacleType supportedType = ObstacleType.None;
}
