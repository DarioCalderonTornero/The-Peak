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

    // Mazo de cartas disponibles para sacar
    private List<CardData> availableDeck = new List<CardData>();

    private void Start()
    {
        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.onClick.AddListener(StartMatch);
        }

        inventoryUI.OnCardSelected += HandleCardSelected;
        inventoryUI.OnCardDeselected += HandleCardDeselected;

        inventoryUI.ShowInventory();
    }

    private void HandleCardSelected(CardData card)
    {
        if (!selectedCards.Contains(card) && selectedCards.Count < maxSelectedCards)
        {
            selectedCards.Add(card);
            CheckStartButton();
        }
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

        // Inicializar mazo de disponibles excluyendo cartas activas
        RefillAvailableDeck();

        // Llenar los slots
        for (int i = 0; i < slotCount; i++)
        {
            CardData card = DrawFromDeck();
            slotsUI.AddCard(card);
        }

        foreach (var dragCard in slotsUI.GetAllCards())
        {
            dragCard.OnCardUsed += HandleCardUsed;
        }
    }

    /// <summary>
    /// Rellena el mazo de disponibles excluyendo las cartas actualmente en slots.
    /// </summary>
    private void RefillAvailableDeck()
    {
        availableDeck = new List<CardData>(selectedCards);

        // Excluir cartas activas en slots
        foreach (var slotCard in slotsUI.GetAllCards())
        {
            availableDeck.Remove(slotCard.cardData);
        }

        ShuffleList(availableDeck);
    }

    /// <summary>
    /// Saca una carta del mazo disponible, recargando si se vacía.
    /// </summary>
    private CardData DrawFromDeck()
    {
        if (availableDeck.Count == 0)
        {
            RefillAvailableDeck();
        }

        CardData card = availableDeck[0];
        availableDeck.RemoveAt(0);
        return card;
    }

    private void HandleCardUsed(DragCardUI usedCard)
    {
        int index = slotsUI.GetCardIndex(usedCard);
        if (index == -1) return;

        // Reemplazar carta
        CardData newCard = DrawFromDeck();
        slotsUI.ReplaceCardAt(index, newCard);

        // Subscribir al nuevo dragCard
        var newUI = slotsUI.GetAllCards()[index];
        newUI.OnCardUsed += HandleCardUsed;
    }

    /// <summary>
    /// Mezcla una lista in-place
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
}
