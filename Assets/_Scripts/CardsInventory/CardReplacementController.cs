using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CardReplacementController : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private CardSlotsUI cardSlotsUI;
    [SerializeField] private GameObject replacementPanel; // Panel que se activa en modo reemplazo
    [SerializeField] private TextMeshProUGUI replacementText;
    [SerializeField] private Button cancelButton;

    [Header("Datos de reemplazo")]
    [SerializeField] private CardData newCardData; // La carta que se ofrece para reemplazo

    private bool isReplacementActive = false;

    [Header("Vista previa de la nueva carta")]
    [SerializeField] private RectTransform newCardPreviewSlot; // Panel dentro del panel de reemplazo donde aparecer� la nueva carta
    private GameObject previewCardInstance;

    [SerializeField] private CardInventoryUI cardInventoryUI;

    private void Start()
    {
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelReplacement);

        replacementPanel.SetActive(false);
    }

    private void ShowNewCardPreview()
    {
        if (previewCardInstance != null)
            Destroy(previewCardInstance);

        if (newCardPreviewSlot == null || newCardData == null || cardSlotsUI == null || cardSlotsUI.cardPrefab == null)
            return;

        previewCardInstance = Instantiate(cardSlotsUI.cardPrefab, newCardPreviewSlot);
        DragCardUI dragCard = previewCardInstance.GetComponent<DragCardUI>();

        if (dragCard != null)
        {
            dragCard.cardData = newCardData;
            dragCard.mainCamera = Camera.main;

            // Desactivamos interacci�n para que solo sea visual
            dragCard.GetComponent<CanvasGroup>().blocksRaycasts = false;
            dragCard.DisableReplacementSelection();
        }
    }
    /// <summary>
    /// Activa el modo reemplazo.
    /// </summary>
    public void StartReplacement()
    {
        if (isReplacementActive || cardSlotsUI == null || newCardData == null)
            return;

        isReplacementActive = true;
        replacementPanel.SetActive(true);

        if (replacementText != null)
            replacementText.text = "Elige por qué carta reemplazar:";

        // Hacer que las cartas sean seleccionables para reemplazo
        foreach (var card in cardSlotsUI.GetAllCards())
        {
            card.EnableReplacementSelection(OnCardSelected);
        }

        ShowNewCardPreview();
    }

    private void OnCardSelected(DragCardUI oldCard)
    {
        if (!isReplacementActive)
            return;

        int slotIndex = cardSlotsUI.GetCardIndex(oldCard);
        if (slotIndex < 0)
            return;

        // 🔹 Guarda los datos de la carta antigua antes de reemplazarla
        CardData oldCardData = oldCard.cardData;

        // 🔹 Reemplaza visual y lógicamente la carta en ese slot
        cardSlotsUI.ReplaceCardAt(slotIndex, newCardData);

        // 🔹 Mueve la carta vieja al inventario
        if (cardInventoryUI != null && oldCardData != null)
        {
            Debug.Log($"Carta {oldCardData.cardName} movida al inventario tras el reemplazo");
            cardInventoryUI.AddCard(oldCardData);
        }

        EndReplacement();
    }

    private void CancelReplacement()
    {
        if (!isReplacementActive) return;

        // Guarda la nueva carta en el inventario
        if (cardInventoryUI != null && newCardData != null)
        {
            Debug.Log($" Carta {newCardData.cardName} guardada en inventario");
            cardInventoryUI.AddCard(newCardData);
        }

        EndReplacement();
    }

    private void EndReplacement()
    {
        isReplacementActive = false;
        replacementPanel.SetActive(false);

        // Deshabilitar selecci�n en todas las cartas
        foreach (var card in cardSlotsUI.GetAllCards())
        {
            card.DisableReplacementSelection();
        }

        if (previewCardInstance != null)
            Destroy(previewCardInstance);
    }
}
