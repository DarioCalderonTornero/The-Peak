using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FameLevelPrototype : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI counterText;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Button decreaseButton;

    [Header("Configuración")]
    [SerializeField] private int currentValue = 1;
    [SerializeField] private int maxValue = 10;
    [SerializeField] private int minValue = 1;

    [Header("Sistema de reemplazo")]
    [SerializeField] private CardReplacementController cardReplacementController;

    private void Start()
    {
        // Inicializa UI
        UpdateCounterText();

        // Suscribir botones
        if (increaseButton != null)
            increaseButton.onClick.AddListener(IncreaseCounter);

        if (decreaseButton != null)
            decreaseButton.onClick.AddListener(DecreaseCounter);
    }

    private void IncreaseCounter()
    {
        if (currentValue < maxValue)
        {
            currentValue++;
            UpdateCounterText();

            if (currentValue >= maxValue)
                TriggerReplacementMode();
        }
    }

    private void DecreaseCounter()
    {
        if (currentValue > minValue)
        {
            currentValue--;
            UpdateCounterText();
        }
    }

    private void UpdateCounterText()
    {
        if (counterText != null)
            counterText.text = currentValue.ToString();
    }

    private void TriggerReplacementMode()
    {
        if (cardReplacementController != null)
        {
            Debug.Log("Contador llegó a 10  activando modo reemplazo...");
            cardReplacementController.StartReplacement();
        }
        else
        {
            Debug.LogWarning("No se asignó un CardReplacementController al contador.");
        }
    }
}
