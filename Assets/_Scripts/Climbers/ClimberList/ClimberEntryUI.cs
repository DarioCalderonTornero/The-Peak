using UnityEngine;
using UnityEngine.UI;

public class ClimberEntryUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image colorIndicator;
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private Image equipmentIcon;
    [SerializeField] private Image stateBadgeIcon;
    [SerializeField] private Image urgencyBorder;
    [SerializeField] private Image background;
    [SerializeField] private Button selectButton;

    [Header("Configuración stamina")]
    [SerializeField] private Color staminaHighColor = new Color(0.24f, 0.81f, 0.45f);
    [SerializeField] private Color staminaMidColor = new Color(0.88f, 0.72f, 0.0f);
    [SerializeField] private Color staminaLowColor = new Color(0.88f, 0.25f, 0.25f);

    [Header("Configuración estado")]
    [SerializeField] private Sprite iconMoving;
    [SerializeField] private Sprite iconInTent;

    [Header("Configuración urgencia")]
    [SerializeField] private Color urgencyColor = new Color(0.88f, 0.24f, 0.24f, 1f);
    [SerializeField] private Color urgencyInactiveColor = new Color(1f, 1f, 1f, 0f);

    private ClimberMovement climber;
    private ClimberLoadout loadout;
    private bool isUrgent = false;

    // ─── Setup ───────────────────────────────────────────────────────────────

    public void Setup(ClimberMovement climberMovement)
    {
        climber = climberMovement;
        if (climber == null) return;

        loadout = climber.GetComponent<ClimberLoadout>();

        if (loadout != null)
        {
            Color helmetColor = loadout.GetHelmetColor();
            helmetColor.a = 1f;
            if (colorIndicator != null)
                colorIndicator.color = helmetColor;

            loadout.OnHelmetColorChanged += OnHelmetColorChanged;

            // Icono del counter
            RefreshEquipmentIcon();
        }

        // Stamina inicial
        float normalized = climber.GetCurrentStamina() / Mathf.Max(1f, climber.GetMaxStamina());
        UpdateStamina(normalized);

        // Estado inicial
        UpdateStateBadge();

        // Urgencia desactivada por defecto
        SetUrgent(false);

        // Eventos
        climber.OnStaminaChanged += UpdateStamina;
        climber.OnTentStateChanged += OnTentStateChanged;

        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);
    }

    // ─── Updates desde eventos ────────────────────────────────────────────────

    private void OnHelmetColorChanged(Color color)
    {
        if (colorIndicator != null)
        {
            color.a = 1.0f;
            colorIndicator.color = color;
        }
    }

    private void OnTentStateChanged(bool inTent)
    {
        if (stateBadgeIcon == null) return;
        stateBadgeIcon.sprite = inTent ? iconInTent : iconMoving;
    }

    private void UpdateStamina(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        if (staminaBarFill != null)
        {
            staminaBarFill.fillAmount = normalized;
            //staminaBarFill.color = GetStaminaColor(normalized);
        }
    }

    private Color GetStaminaColor(float normalized)
    {
        if (normalized > 0.6f)
            return Color.Lerp(staminaMidColor, staminaHighColor, (normalized - 0.6f) / 0.4f);
        if (normalized > 0.3f)
            return Color.Lerp(staminaLowColor, staminaMidColor, (normalized - 0.3f) / 0.3f);
        return staminaLowColor;
    }

    // ─── Refresh manual (llamado por ClimberListUI cada turno) ───────────────

    public void RefreshState()
    {
        UpdateStateBadge();
    }

    private void UpdateStateBadge()
    {
        if (stateBadgeIcon == null || climber == null) return;

        bool inTent = climber.IsInsideTent;
        stateBadgeIcon.sprite = inTent ? iconInTent : iconMoving;
    }

    private void RefreshEquipmentIcon()
    {
        if (equipmentIcon == null || loadout == null) return;

        Sprite icon = loadout.GetFirstEquipmentIcon();
        equipmentIcon.sprite = icon;
        equipmentIcon.enabled = icon != null;
    }

    // ─── Urgencia ─────────────────────────────────────────────────────────────

    public void SetUrgent(bool urgent)
    {
        if (isUrgent == urgent) return;
        isUrgent = urgent;

        if (urgencyBorder != null)
            urgencyBorder.color = urgent ? urgencyColor : urgencyInactiveColor;
    }

    // ─── Selección ────────────────────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        // El highlight visual de selección lo gestiona ClimberListUI desde fuera
        // Aquí solo guardamos estado si hiciera falta en el futuro
    }

    private void OnSelectClicked()
    {
        if (climber == null) return;
        ClimberListUI.Instance?.OnEntryClicked(climber);
    }

    // ─── Getter ───────────────────────────────────────────────────────────────

    public ClimberMovement GetClimber() => climber;

    // ─── Cleanup ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (climber != null)
            climber.OnStaminaChanged -= UpdateStamina;

        if (loadout != null)
            loadout.OnHelmetColorChanged -= OnHelmetColorChanged;

        if (selectButton != null)
            selectButton.onClick.RemoveAllListeners();

        if (climber != null)
            climber.OnTentStateChanged -= OnTentStateChanged;
    }
}