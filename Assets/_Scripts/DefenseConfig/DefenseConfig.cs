using UnityEngine;

[CreateAssetMenu(fileName = "DefenseConfig", menuName = "TowerDefense/DefenseConfig")]
public class DefenseConfig : ScriptableObject
{
    public static DefenseConfig Instance { get; private set; }

    [Header("Shovel Hover")]
    public Material shovelHoverMaterial;

    private void OnEnable()
    {
        Instance = this;
    }
}