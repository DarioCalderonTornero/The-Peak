using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentDefinition", menuName = "Climbers/Equipment Definition")]
public class EquipmentDefinitionSO : ScriptableObject
{
    [Header("General Info")]
    public string equipmentName;
    public Sprite icon;

    [Header("Logic Class (equipment behavior)")]
    public string logicClassName;

    public Color color;

    [Header("Viñeta UI")]
    [Tooltip("Icono que representa este tipo de escalador en la lista de viñetas")]
    public Sprite climberIcon;
}