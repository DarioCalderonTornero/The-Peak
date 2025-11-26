using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "TowerDefense/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public Sprite icon;
    public GameObject defensePrefab;
    public int cost;

    public bool hasFixedPlacement = false;
    public Vector3 fixedPosition;

    public Material previewMaterial;

    [Header("Soporte completo")]
    public bool requireFullSupport = false;

    // Extensión de la base para el chequeo (en X/Z). Piensa en medio tamaño del objeto.
    public Vector2 supportCheckExtents = new Vector2(0.5f, 0.5f);

    // Distancia máxima hacia la montaña para considerar que está apoyado (tolerancia).
    public float supportRayDistance = 0.5f;

    // Altura desde donde sale el cuadrado de raycasts
    public float supportYOffset = 0.1f;

    [Header("Colisión entre defensas")]
    // Mitad del tamaño del cubo para comprobar si hay otra defensa cerca
    public Vector3 placementCheckExtents = new Vector3(0.5f, 0.5f, 0.5f);
}
