using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardGameManager : MonoBehaviour
{
    public static CardGameManager Instance { get; private set; }

    public event EventHandler OnInventoryHide;

    [Header("Referencias")]
    [SerializeField] private CardInventoryUI inventoryUI;
    [SerializeField] private CardSlotsUI slotsUI;
    [SerializeField] private Button startButton;

    [Header("Configuración")]
    [SerializeField] private int maxSelectedCards = 8;
    [SerializeField] private int slotCount = 3;

    [Header("Pool de cartas para recompensas")]
    [Tooltip("Todas las CardData existentes en el juego. Las que no tenga el jugador en su mazo podrán aparecer como recompensa.")]
    [SerializeField] private List<CardData> allCardsPool = new List<CardData>();

    private List<CardData> selectedCards = new List<CardData>();
    private List<CardData> availableDeck = new List<CardData>();

    private void Awake()
    {
        Instance = this;
    }

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

    // ─── Selección inicial ───────────────────────────────────────────────────

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

    // ─── Inicio de partida ───────────────────────────────────────────────────

    private void StartMatch()
    {
        OnInventoryHide?.Invoke(this, EventArgs.Empty);

        GameManager.Instance.StartGame();

        inventoryUI.HideInventory();
        slotsUI.ClearCards();

        RefillAvailableDeck();

        for (int i = 0; i < slotCount; i++)
        {
            CardData card = DrawFromDeck();
            slotsUI.AddCard(card);
        }

        foreach (var dragCard in slotsUI.GetAllCards())
            dragCard.OnCardUsed += HandleCardUsed;
    }

    // ─── Rotación de cartas ──────────────────────────────────────────────────

    private void RefillAvailableDeck()
    {
        availableDeck = new List<CardData>(selectedCards);

        foreach (var slotCard in slotsUI.GetAllCards())
            availableDeck.Remove(slotCard.cardData);

        ShuffleList(availableDeck);
    }

    private CardData DrawFromDeck()
    {
        if (availableDeck.Count == 0)
            RefillAvailableDeck();

        CardData card = availableDeck[0];
        availableDeck.RemoveAt(0);
        return card;
    }

    private void HandleCardUsed(DragCardUI usedCard)
    {
        int index = slotsUI.GetCardIndex(usedCard);
        if (index == -1) return;

        CardData newCard = DrawFromDeck();
        slotsUI.ReplaceCardAt(index, newCard);

        var newUI = slotsUI.GetAllCards()[index];
        newUI.OnCardUsed += HandleCardUsed;
    }

    // ─── API pública para CardRewardUI ───────────────────────────────────────

    /// <summary>
    /// Devuelve candidatas barajadas que el jugador NO tiene en su mazo.
    /// CardRewardUI coge las 2 primeras.
    /// </summary>
    public List<CardData> GetRewardCandidates()
    {
        List<CardData> candidates = new List<CardData>();
        foreach (var card in allCardsPool)
        {
            if (card != null && !selectedCards.Contains(card))
                candidates.Add(card);
        }

        ShuffleList(candidates);
        return candidates;
    }

    /// <summary>
    /// Añade la carta elegida al mazo del jugador y la mete en rotación inmediatamente.
    /// Llamado por CardRewardUI tras la elección del jugador.
    /// </summary>
    public void AddRewardCardToDeck(CardData chosen)
    {
        if (chosen == null) return;

        selectedCards.Add(chosen);
        availableDeck.Add(chosen);
        ShuffleList(availableDeck);

        Debug.Log($"[CardGameManager] Carta de recompensa añadida al mazo: {chosen.cardName}");
    }

    // ─── Utilidades ──────────────────────────────────────────────────────────

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = UnityEngine.Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
}