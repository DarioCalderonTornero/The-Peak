using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardInventoryUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject inventoryPanel; // Panel con layout
    [SerializeField] private Button toggleButton;       // Botón para abrir/cerrar
    [SerializeField] private Transform cardContainer;   // Contenedor de cartas
    [SerializeField] private GameObject cardPrefab;     // Prefab visual de carta (usa el mismo que tus slots)

    private bool isVisible = false;
    private List<CardData> storedCards = new List<CardData>();

    private void Start()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleInventory);
    }

    public void AddCard(CardData card)
    {
        if (card == null) return;

        storedCards.Add(card);

        // Si el panel está visible, actualiza inmediatamente
        if (isVisible)
            RefreshInventory();
    }

    private void ToggleInventory()
    {
        isVisible = !isVisible;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(isVisible);

        if (isVisible)
            RefreshInventory();
    }

    private void RefreshInventory()
    {
        // Limpia las cartas viejas del contenedor
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        // Genera visualmente las cartas almacenadas
        foreach (var cardData in storedCards)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            DragCardUI cardUI = cardObj.GetComponent<DragCardUI>();

            if (cardUI != null)
            {
                cardUI.cardData = cardData;
                cardUI.mainCamera = Camera.main;
                cardUI.SetupCardUI();
                cardUI.UpdateInteractable();

                // Desactiva cualquier interacción (solo visual)
                var cg = cardUI.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = false;
            }
        }
    }
}
