using UnityEngine;

public class ClimberEquipmentVisuals : MonoBehaviour
{
    [Header("Espalda")]
    [SerializeField] private GameObject backPickaxe;
    [SerializeField] private GameObject backMagicBook;
    [SerializeField] private GameObject backSnowshoes;
    [SerializeField] private GameObject backMudObject;

    [Header("Mano")]
    [SerializeField] private GameObject handPickaxe;
    [SerializeField] private GameObject handMagicBook;
    [SerializeField] private GameObject handSnowshoes;
    [SerializeField] private GameObject handMudObject;

    private ClimberLoadout loadout;

    private GameObject activeBackObject;
    private GameObject activeHandObject;

    private void Awake()
    {
        loadout = GetComponent<ClimberLoadout>();
    }

    private void OnEnable()
    {
        if (loadout != null)
        {
            loadout.OnEquipmentInitialized += HandleEquipmentInitialized;

            // Si el loadout ya estaba inicializado antes de que nos suscribiéramos
            // (caso del archetype random que se inicializa en Awake),
            // aplicamos el equipo actual directamente
            var current = loadout.GetFirstEquipmentIcon();
            if (current != null)
                HandleEquipmentInitialized(current);
        }
    }

    private void OnDisable()
    {
        if (loadout != null)
            loadout.OnEquipmentInitialized -= HandleEquipmentInitialized;
    }

    private void HandleEquipmentInitialized(EquipmentDefinitionSO equipment)
    {
        DeactivateAll();

        if (equipment == null || string.IsNullOrWhiteSpace(equipment.logicClassName))
            return;

        switch (equipment.logicClassName)
        {
            case nameof(PickaxeEquipment):
                activeBackObject = backPickaxe;
                activeHandObject = handPickaxe;
                break;
            case nameof(MagicBookEquipment):
                activeBackObject = backMagicBook;
                activeHandObject = handMagicBook;
                break;
            case nameof(SnowshoesEquipment):
                activeBackObject = backSnowshoes;
                activeHandObject = handSnowshoes;
                break;
            case nameof(MudCloudEquipment):
                activeBackObject = backMudObject;
                activeHandObject = handMudObject;
                break;
            default:
                activeBackObject = null;
                activeHandObject = null;
                break;
        }

        if (activeBackObject != null)
            activeBackObject.SetActive(true);
    }

    // ─── API pública para cuando el escalador usa el equipo ──────────────────

    /// <summary>
    /// Mueve el equipo de la espalda a la mano.
    /// Llamado cuando el escalador empieza la animación de uso.
    /// </summary>
    public void MoveEquipmentToHand()
    {
        if (activeBackObject != null)
            activeBackObject.SetActive(false);

        if (activeHandObject != null)
            activeHandObject.SetActive(true);
    }

    /// <summary>
    /// Devuelve el equipo de la mano a la espalda.
    /// Llamado cuando termina la animación de uso.
    /// </summary>
    public void MoveEquipmentToBack()
    {
        if (activeHandObject != null)
            activeHandObject.SetActive(false);

        if (activeBackObject != null)
            activeBackObject.SetActive(true);
    }

    /// <summary>
    /// Oculta todo el equipo útil si el escalador muere o entra en tienda.
    /// </summary>
    public void HideAll()
    {
        DeactivateAll();
    }

    private void DeactivateAll()
    {
        if (backPickaxe != null) backPickaxe.SetActive(false);
        if (backMagicBook != null) backMagicBook.SetActive(false);
        if (backSnowshoes != null) backSnowshoes.SetActive(false);
        if (backMudObject != null) backMudObject.SetActive(false);

        if (handPickaxe != null) handPickaxe.SetActive(false);
        if (handMagicBook != null) handMagicBook.SetActive(false);
        if (handSnowshoes != null) handSnowshoes.SetActive(false);
        if (handMudObject != null) handMudObject.SetActive(false);

        activeBackObject = null;
        activeHandObject = null;
    }
}