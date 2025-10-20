using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "TowerDefense/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public Sprite icon;
    public GameObject defensePrefab;
    public int cost;
}
