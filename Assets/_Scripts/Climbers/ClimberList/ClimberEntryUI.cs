using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ClimberEntryUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image colorIndicator;
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private Image equipmentIcon;
    [SerializeField] private Image stateBadgeIcon;
    [SerializeField] private Image clickedIcon;
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

    [Header("Configuración selección")]
    [SerializeField] private Color selectedBorderColor = new Color(1f, 1f, 1f, 1f);

    [Header("Altitud")]
    [SerializeField] private TMPro.TextMeshProUGUI altitudeText;
    [SerializeField] private float worldYMin = 0f;
    [SerializeField] private float worldYMax = 50f;

    [Header("Animación entrada")]
    [SerializeField] private float spawnDuration = 0.3f;

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
            loadout.OnEquipmentInitialized += Loadout_OnEquipmentInitialized;
        }

        // Stamina inicial
        float normalized = climber.GetCurrentStamina() / Mathf.Max(1f, climber.GetMaxStamina());
        UpdateStamina(normalized);

        // Estado inicial
        UpdateStateBadge();

        // Urgencia desactivada por defecto
        //SetUrgent(false);

        // Eventos
        climber.OnStaminaChanged += UpdateStamina;
        climber.OnTentStateChanged += OnTentStateChanged;

        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);

        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, spawnDuration).SetEase(Ease.OutBack);
    }

    // ─── Updates desde eventos ────────────────────────────────────────────────

    private void Loadout_OnEquipmentInitialized(EquipmentDefinitionSO equipmentDefinition)
    {
        if (equipmentDefinition == null) return;
        if (equipmentIcon != null)
            equipmentIcon.sprite = equipmentDefinition.climberIcon;
    }

    private void Update()
    {
        if (climber == null || altitudeText == null) return;
        float t = Mathf.InverseLerp(worldYMin, worldYMax, climber.transform.position.y);
        int meters = Mathf.RoundToInt(t * 1000);
        altitudeText.text = $"{meters}m";
    }

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
            staminaBarFill.fillAmount = normalized;
    }

    private Color GetStaminaColor(float normalized)
    {
        if (normalized > 0.6f)
            return Color.Lerp(staminaMidColor, staminaHighColor, (normalized - 0.6f) / 0.4f);
        if (normalized > 0.3f)
            return Color.Lerp(staminaLowColor, staminaMidColor, (normalized - 0.3f) / 0.3f);
        return staminaLowColor;
    }

    // ─── Refresh manual ───────────────────────────────────────────────────────

    public void RefreshState()
    {
        UpdateStateBadge();
    }

    private void UpdateStateBadge()
    {
        if (stateBadgeIcon == null || climber == null) return;
        stateBadgeIcon.sprite = climber.IsInsideTent ? iconInTent : iconMoving;
    }

    // ─── Urgencia ─────────────────────────────────────────────────────────────

    /*
    public void SetUrgent(bool urgent)
    {
        if (isUrgent == urgent) return;
        isUrgent = urgent;

        if (urgencyBorder != null)
            urgencyBorder.color = urgent ? urgencyColor : urgencyInactiveColor;
    }
    */

    // ─── Selección ────────────────────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        if (clickedIcon == null) return;

        //float backgroundSelectedScaleMultiplier = 1.5f;

        if (selected)
        {
            background.color = selectedBorderColor;
            //background.rectTransform.localScale = Vector3.one * backgroundSelectedScaleMultiplier;
        }
        else
            background.color = Color.white;
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
        transform.DOKill();

        if (climber != null)
        {
            climber.OnStaminaChanged -= UpdateStamina;
            climber.OnTentStateChanged -= OnTentStateChanged;
        }

        if (loadout != null)
        {
            loadout.OnHelmetColorChanged -= OnHelmetColorChanged;
            loadout.OnEquipmentInitialized -= Loadout_OnEquipmentInitialized;
        }

        if (selectButton != null)
            selectButton.onClick.RemoveAllListeners();
    }
}