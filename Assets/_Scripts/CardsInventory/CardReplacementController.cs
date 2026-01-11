using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardReplacementController : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private CardSlotsUI cardSlotsUI;
    [SerializeField] private GameObject replacementPanel;
    [SerializeField] private TextMeshProUGUI replacementText;
    [SerializeField] private Button cancelButton;

    [Header("Datos de reemplazo")]
    [SerializeField] private CardData newCardData;

    [Header("Vista previa de la nueva carta")]
    [SerializeField] private RectTransform newCardPreviewSlot;
    private GameObject previewCardInstance;

    [SerializeField] private CardInventoryUI cardInventoryUI;

    private bool isReplacementActive = false;

    private void Start()
    {
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelReplacement);

        if (replacementPanel != null)
            replacementPanel.SetActive(false);
    }

    private void ShowNewCardPreview()
    {
        if (previewCardInstance != null)
        {
            Destroy(previewCardInstance);
            previewCardInstance = null;
        }

        if (newCardPreviewSlot == null || newCardData == null || cardSlotsUI == null || cardSlotsUI.cardPrefab == null)
            return;

        previewCardInstance = Instantiate(cardSlotsUI.cardPrefab, newCardPreviewSlot);

        // Ajuste UI
        var rt = previewCardInstance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        // Configurar datos
        var dragCard = previewCardInstance.GetComponent<DragCardUI>();
        if (dragCard != null)
        {
            dragCard.cardData = newCardData;
            dragCard.mainCamera = Camera.main;

            // Si tu DragCardUI tiene SetupCardUI, lo llamamos
            // (si NO existe, quita esta línea)
            dragCard.SetupCardUI();

            // ✅ que sea solo visual
            dragCard.enabled = false;
        }

        // Evitar que capture clicks
        CanvasGroup cg = previewCardInstance.GetComponent<CanvasGroup>();
        if (cg == null) cg = previewCardInstance.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.alpha = 1f;
    }

    /// <summary>
    /// Activa el modo reemplazo.
    /// </summary>
    public void StartReplacement()
    {
        if (isReplacementActive || cardSlotsUI == null || newCardData == null)
            return;

        isReplacementActive = true;

        if (replacementPanel != null)
            replacementPanel.SetActive(true);

        if (replacementText != null)
            replacementText.text = "Elige por qué carta reemplazar:";

        // ✅ Añadimos "capturador" de click a cada carta real
        foreach (var card in cardSlotsUI.GetAllCards())
        {
            if (card == null) continue;

            var click = card.gameObject.GetComponent<ReplacementClickable>();
            if (click == null)
                click = card.gameObject.AddComponent<ReplacementClickable>();

            click.Init(this, card);
        }

        ShowNewCardPreview();
    }

    private void OnCardSelected(DragCardUI oldCard)
    {
        if (!isReplacementActive || oldCard == null)
            return;

        int slotIndex = cardSlotsUI.GetCardIndex(oldCard);
        if (slotIndex < 0)
            return;

        CardData oldCardData = oldCard.cardData;

        // Reemplaza la carta en ese slot
        cardSlotsUI.ReplaceCardAt(slotIndex, newCardData);

        // Mueve la carta vieja al inventario
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
            Debug.Log($"Carta {newCardData.cardName} guardada en inventario");
            cardInventoryUI.AddCard(newCardData);
        }

        EndReplacement();
    }

    private void EndReplacement()
    {
        isReplacementActive = false;

        if (replacementPanel != null)
            replacementPanel.SetActive(false);

        // ✅ Quitamos los "capturadores" de click
        foreach (var card in cardSlotsUI.GetAllCards())
        {
            if (card == null) continue;

            var click = card.gameObject.GetComponent<ReplacementClickable>();
            if (click != null) Destroy(click);
        }

        if (previewCardInstance != null)
        {
            Destroy(previewCardInstance);
            previewCardInstance = null;
        }
    }

    // -------------------------
    // Componente auxiliar
    // -------------------------
    private class ReplacementClickable : MonoBehaviour, IPointerClickHandler
    {
        private CardReplacementController controller;
        private DragCardUI card;

        public void Init(CardReplacementController controller, DragCardUI card)
        {
            this.controller = controller;
            this.card = card;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Solo click izquierdo
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (controller == null || card == null) return;

            controller.OnCardSelected(card);
        }
    }
}
