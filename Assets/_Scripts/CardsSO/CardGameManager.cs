using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardGameManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CardInventoryUI inventoryUI;
    [SerializeField] private CardSlotsUI slotsUI;
    [SerializeField] private Button startButton;

    [Header("Configuración")]
    [SerializeField] private int maxSelectedCards = 8;
    [SerializeField] private int slotCount = 3;

    private List<CardData> selectedCards = new List<CardData>();

    private void Start()
    {
        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.onClick.AddListener(StartMatch);
        }

        inventoryUI.OnCardSelected += HandleCardSelected;
        inventoryUI.OnCardDeselected += HandleCardDeselected;

        // Mostrar el inventario de inicio
        inventoryUI.ShowInventory();
    }

    private void HandleCardSelected(CardData card)
    {
        if (selectedCards.Contains(card)) return;
        if (selectedCards.Count >= maxSelectedCards) return;

        selectedCards.Add(card);
        CheckStartButton();
    }

    private void HandleCardDeselected(CardData card)
    {
        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
            CheckStartButton();
        }
    }

    private void CheckStartButton()
    {
        if (startButton != null)
            startButton.interactable = (selectedCards.Count == maxSelectedCards);
    }

    private void StartMatch()
    {
        inventoryUI.HideInventory();

        slotsUI.ClearCards();

        for (int i = 0; i < slotCount; i++)
        {
            CardData randomCard = selectedCards[Random.Range(0, selectedCards.Count)];
            slotsUI.AddCard(randomCard);
        }

        foreach (var dragCard in slotsUI.GetAllCards())
        {
            dragCard.OnCardUsed += HandleCardUsed;
        }
    }

    private void HandleCardUsed(DragCardUI usedCard)
    {
        int index = slotsUI.GetCardIndex(usedCard);
        if (index == -1) return;

        CardData newCard = selectedCards[Random.Range(0, selectedCards.Count)];
        slotsUI.ReplaceCardAt(index, newCard);

        var newUI = slotsUI.GetAllCards()[index];
        newUI.OnCardUsed += HandleCardUsed;
    }
}
