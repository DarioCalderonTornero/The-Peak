using UnityEngine;
using UnityEngine.UI;

public class ClimberEntryUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image colorIndicator;
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private Image equipmentIcon;
    [SerializeField] private Button selectButton;

    private ClimberMovement climber;

    public void Setup(ClimberMovement climberMovement)
    {
        climber = climberMovement;

        if (climber == null) return;

        // Color del casco
        ClimberLoadout loadout = climber.GetComponent<ClimberLoadout>();
        if (loadout != null)
        {
            if (colorIndicator != null)
                colorIndicator.color = loadout.GetHelmetColor();

            Sprite icon = loadout.GetFirstEquipmentIcon();
            if (equipmentIcon != null)
            {
                equipmentIcon.sprite = icon;
                equipmentIcon.enabled = icon != null;
            }
        }

        // Stamina inicial
        UpdateStamina(climber.GetCurrentStamina() / Mathf.Max(1f, climber.GetMaxStamina()));

        // Suscripción al evento de stamina
        climber.OnStaminaChanged += UpdateStamina;

        // Botón de selección (la cámara se conectará más adelante)
        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);
    }

    private void UpdateStamina(float normalized)
    {
        if (staminaBarFill != null)
            staminaBarFill.fillAmount = Mathf.Clamp01(normalized);
    }

    private void OnSelectClicked()
    {
        // ClimberInspectCamera se conectará en la siguiente fase
        Debug.Log($"[ClimberEntryUI] Click en escalador: {climber?.name}");
    }

    private void OnDestroy()
    {
        if (climber != null)
            climber.OnStaminaChanged -= UpdateStamina;

        if (selectButton != null)
            selectButton.onClick.RemoveAllListeners();
    }
}