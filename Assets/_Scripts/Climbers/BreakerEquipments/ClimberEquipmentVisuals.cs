using UnityEngine;

public class ClimberEquipmentVisuals : MonoBehaviour
{
    [Header("Espalda")]
    [SerializeField] private GameObject backPickaxe;
    [SerializeField] private GameObject backMagicBook;
    [SerializeField] private GameObject backPala;
    [SerializeField] private GameObject backCuerno;

    [Header("Mano")]
    [SerializeField] private GameObject handPickaxe;
    [SerializeField] private GameObject handMagicBook;
    [SerializeField] private GameObject handPala;
    [SerializeField] private GameObject handCuerno;

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
                activeBackObject = backPala;
                activeHandObject = handPala;
                break;
            case nameof(MudCloudEquipment):
                activeBackObject = backCuerno;
                activeHandObject = handCuerno;
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
        if (backPala != null) backPala.SetActive(false);
        if (backCuerno != null) backCuerno.SetActive(false);

        if (handPickaxe != null) handPickaxe.SetActive(false);
        if (handMagicBook != null) handMagicBook.SetActive(false);
        if (handPala != null) handPala.SetActive(false);
        if (handCuerno != null) handCuerno.SetActive(false);

        activeBackObject = null;
        activeHandObject = null;
    }
}