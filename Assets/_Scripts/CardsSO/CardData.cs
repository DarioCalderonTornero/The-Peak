using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "NewCard", menuName = "TowerDefense/Card")]
public class CardData : ScriptableObject
{
    public string cardName;
    [TextArea] public string description;
    public Sprite icon;
    public GameObject defensePrefab;
    public int cost;
    
    [Header("Sprite extra")]
    public Sprite worldSprite;

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

    [Header("Configuración de Grilla")]
    // NUEVO: Tamaño en casillas (Ej: 1x1, 2x2, 3x1)
    public Vector2Int gridSize = new Vector2Int(1, 1);

    [Header("Decal Grid Offset")]
    public Vector2 decalOffset = Vector2.zero;

    // ═══════════════════════════════════════════════════════════════
    // NUEVO: DATOS DEL REVERSO DE LA CARTA
    // ═══════════════════════════════════════════════════════════════
    public enum CardType { Permanente, Temporal, Eventual }

    [Header("─── REVERSO DE LA CARTA ───")]
    public CardType cardType = CardType.Permanente;

    [Tooltip("Imagen de fondo del reverso (opcional - normalmente diferente del frente)")]
    public Sprite backBackground;

    [Tooltip("Ilustración o icono grande del reverso (opcional)")]
    public Sprite backIllustration;

    [Tooltip("Frase temática / lore que aparece en el reverso")]
    [TextArea(2, 4)]
    public string loreText;

    [Tooltip("Descripción extendida del reverso (más detallada que la del frente)")]
    [TextArea(2, 5)]
    public string extendedDescription;

    [Header("─── COUNTER (equipamiento que contrarresta) ───")]
    [Tooltip("Icono del equipamiento counter (pico, pantalón, etc.)")]
    public Sprite counterIcon;

    [Tooltip("Nombre del counter (ej: 'Pico', 'Pantalón')")]
    public string counterName;

    [Tooltip("Color o variante visual del counter")]
    public Sprite counterColorSprite;

    [Header("─── ICONO SLOT ÚLTIMO DECK ───")]
    [Tooltip("Icono pequeño que aparece en los slots del 'Último Deck'. " +
         "Si se deja vacío, se usará worldSprite como fallback.")]
    public Sprite deckSlotIcon;

    [Header("─── VÍDEO EXPLICATIVO ───")]
    [Tooltip("Vídeo explicativo que se muestra en el reverso de la carta.")]
    public VideoClip explanationVideo;

    [Tooltip("Texto corto que describe la función de la carta (para el panel de vídeo)")]
    public string functionText;
    
    [Tooltip("Descripción de cómo el counter neutraliza esta carta")]
    [TextArea(2, 3)]
    public string counterInfo;
}
