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
    [SerializeField] private RectTransform newCardPreviewSlot; // Panel dentro del panel de reemplazo donde aparecerá la nueva carta
    private GameObject previewCardInstance;

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

            // Desactivamos interacción para que solo sea visual
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

        // Reemplazamos la carta
        cardSlotsUI.ReplaceCardAt(slotIndex, newCardData);

        EndReplacement();
    }

    private void CancelReplacement()
    {
        if (!isReplacementActive) return;

        EndReplacement();
    }

    private void EndReplacement()
    {
        isReplacementActive = false;
        replacementPanel.SetActive(false);

        // Deshabilitar selección en todas las cartas
        foreach (var card in cardSlotsUI.GetAllCards())
        {
            card.DisableReplacementSelection();
        }

        if (previewCardInstance != null)
            Destroy(previewCardInstance);
    }
}
