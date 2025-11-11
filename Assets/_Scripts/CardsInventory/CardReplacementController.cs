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

        if (oldCard == null || oldCard.cardData == null)
            return;

        CardData oldCardData = oldCard.cardData;

        // NUEVO: Usar CardManager para reemplazar la carta
        if (CardManager.Instance != null)
        {
            bool success = CardManager.Instance.ReplaceCardInHand(oldCardData, newCardData);
            
            if (success)
            {
                // Mover la carta vieja al inventario
                if (cardInventoryUI != null)
                {
                    cardInventoryUI.AddCard(oldCardData);
                    Debug.Log($"[CardReplacementController] Carta {oldCardData.cardName} reemplazada por {newCardData.cardName}");
                }
                else
                {
                    Debug.LogWarning("[CardReplacementController] CardInventoryUI no encontrado para devolver carta vieja");
                }
            }
            else
            {
                Debug.LogError($"[CardReplacementController] No se pudo reemplazar {oldCardData.cardName}");
            }
        }
        else
        {
            Debug.LogError("[CardReplacementController] CardManager no encontrado!");
        }

        EndReplacement();
    }

private void CancelReplacement()
    {
        if (!isReplacementActive) return;

        // Guardar la nueva carta en el inventario (jugador rechaza el reemplazo)
        if (CardManager.Instance != null && newCardData != null)
        {
            // Añadir al inventario de cartas disponibles
            CardManager.Instance.AddCardToInventory(newCardData);
            
            // También mostrar en UI de inventario si está disponible
            if (cardInventoryUI != null)
            {
                cardInventoryUI.AddCard(newCardData);
            }
            
            Debug.Log($"[CardReplacementController] Carta {newCardData.cardName} guardada en inventario (reemplazo cancelado)");
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
