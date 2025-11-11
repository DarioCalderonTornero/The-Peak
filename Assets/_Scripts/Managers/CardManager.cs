using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CardManager - Gestión centralizada del sistema de cartas
/// =========================================================
/// Responsabilidades:
/// - Inventario completo de cartas disponibles
/// - Gestión de mazo (deck) - 8 cartas seleccionadas pre-partida
/// - Gestión de mano (hand) - 3 cartas activas durante partida
/// - Sistema de robo y recarga automática del mazo
/// - Validación de jugadas (turno, coste, disponibilidad)
/// - Integración con TurnManager, PointsManager, DefenseManager
/// - Sistema de reemplazo de cartas
/// 
/// Patrón: Singleton
/// Ubicación: Assets/_Scripts/Managers/CardManager.cs
/// </summary>
public class CardManager : MonoBehaviour
{
    #region Singleton
    public static CardManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region Configuration
    [Header("Configuration")]
    [SerializeField] private int handSize = 3;
    [SerializeField] private int deckSize = 8;
    [SerializeField] private bool logCardActions = true;
    #endregion

    #region References
    [Header("UI References")]
    [SerializeField] private CardInventoryUI cardInventoryUI;
    [SerializeField] private CardSlotsUI cardSlotsUI;

    private TurnManager turnManager;
    private PointsManager pointsManager;
    private DefenseManager defenseManager;
    #endregion

    #region Card Collections
    [Header("Card Collections (Debug)")]
    [SerializeField] private List<CardData> availableCards = new List<CardData>();
    
    private List<CardData> selectedDeck = new List<CardData>();
    private List<CardData> currentDeck = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    #endregion

    #region Events
    public event Action<CardData> OnCardAddedToHand;
    public event Action<CardData> OnCardRemovedFromHand;
    public event Action<CardData, Vector3> OnCardPlayed;
    public event Action OnDeckRefilled;
    public event Action OnHandUpdated;
    #endregion

    #region Initialization
    private void Start()
    {
        InitializeReferences();
        SubscribeToEvents();

        if (logCardActions)
            Debug.Log("[CardManager] Initialized. Waiting for card selection...");
    }

    private void InitializeReferences()
    {
        turnManager = TurnManager.Instance;
        pointsManager = PointsManager.Instance;
        defenseManager = DefenseManager.Instance;

        if (cardInventoryUI == null)
            cardInventoryUI = FindFirstObjectByType<CardInventoryUI>();
        
        if (cardSlotsUI == null)
            cardSlotsUI = FindFirstObjectByType<CardSlotsUI>();
    }

    private void SubscribeToEvents()
    {
        if (cardInventoryUI != null)
        {
            cardInventoryUI.OnStartMatch += HandleMatchStart;
        }

        if (turnManager != null)
        {
            turnManager.OnPlayerTurnStart += HandlePlayerTurnStart;
            turnManager.OnClimberTurnStart += HandleClimberTurnStart;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        if (Instance == this)
            Instance = null;
    }

    private void UnsubscribeFromEvents()
    {
        if (cardInventoryUI != null)
        {
            cardInventoryUI.OnStartMatch -= HandleMatchStart;
        }

        if (turnManager != null)
        {
            turnManager.OnPlayerTurnStart -= HandlePlayerTurnStart;
            turnManager.OnClimberTurnStart -= HandleClimberTurnStart;
        }
    }
    #endregion

    #region Inventory Management
    public void AddCardToInventory(CardData card)
    {
        if (card == null) return;

        if (!availableCards.Contains(card))
        {
            availableCards.Add(card);
            
            if (logCardActions)
                Debug.Log($"[CardManager] Card added to inventory: {card.cardName}");
        }
    }

    public void RemoveCardFromInventory(CardData card)
    {
        if (card == null) return;

        if (availableCards.Contains(card))
        {
            availableCards.Remove(card);
            
            if (logCardActions)
                Debug.Log($"[CardManager] Card removed from inventory: {card.cardName}");
        }
    }

    public List<CardData> GetAvailableCards()
    {
        return new List<CardData>(availableCards);
    }
    #endregion

    #region Deck Management
    private void HandleMatchStart(List<CardData> selectedCards)
    {
        if (selectedCards == null || selectedCards.Count != deckSize)
        {
            Debug.LogError($"[CardManager] Invalid card selection! Expected {deckSize}, got {selectedCards?.Count}");
            return;
        }

        selectedDeck = new List<CardData>(selectedCards);
        currentDeck = new List<CardData>(selectedCards);
        hand.Clear();
        discardPile.Clear();

        ShuffleDeck();
        FillInitialHand();

        if (logCardActions)
            Debug.Log($"[CardManager] Match started with {deckSize} cards in deck");
    }

    private void ShuffleDeck()
    {
        for (int i = 0; i < currentDeck.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, currentDeck.Count);
            CardData temp = currentDeck[i];
            currentDeck[i] = currentDeck[randomIndex];
            currentDeck[randomIndex] = temp;
        }

        if (logCardActions)
            Debug.Log("[CardManager] Deck shuffled");
    }

    private void RefillDeck()
    {
        currentDeck.Clear();

        foreach (var card in selectedDeck)
        {
            if (!hand.Contains(card))
            {
                currentDeck.Add(card);
            }
        }

        ShuffleDeck();
        OnDeckRefilled?.Invoke();

        if (logCardActions)
            Debug.Log($"[CardManager] Deck refilled! Cards in deck: {currentDeck.Count}");
    }
    #endregion

    #region Hand Management
    private void FillInitialHand()
    {
        for (int i = 0; i < handSize; i++)
        {
            DrawCard();
        }

        UpdateHandUI();

        if (logCardActions)
            Debug.Log($"[CardManager] Initial hand filled with {hand.Count} cards");
    }

    public CardData DrawCard()
    {
        if (currentDeck.Count == 0)
        {
            RefillDeck();

            if (currentDeck.Count == 0)
            {
                Debug.LogWarning("[CardManager] No cards available to draw!");
                return null;
            }
        }

        CardData drawnCard = currentDeck[0];
        currentDeck.RemoveAt(0);
        hand.Add(drawnCard);

        OnCardAddedToHand?.Invoke(drawnCard);

        if (logCardActions)
            Debug.Log($"[CardManager] Drew card: {drawnCard.cardName}. Cards left in deck: {currentDeck.Count}");

        return drawnCard;
    }

    private void RemoveCardFromHand(CardData card)
    {
        if (hand.Contains(card))
        {
            hand.Remove(card);
            OnCardRemovedFromHand?.Invoke(card);

            if (logCardActions)
                Debug.Log($"[CardManager] Removed card from hand: {card.cardName}");
        }
    }

    public List<CardData> GetHand()
    {
        return new List<CardData>(hand);
    }

    private void UpdateHandUI()
    {
        if (cardSlotsUI == null) return;

        cardSlotsUI.ClearCards();

        foreach (var card in hand)
        {
            cardSlotsUI.AddCard(card);
        }

        OnHandUpdated?.Invoke();
    }
    #endregion

    #region Card Playing
    public bool PlayCard(CardData card, Vector3 position)
    {
        if (card == null)
        {
            Debug.LogWarning("[CardManager] Cannot play null card!");
            return false;
        }

        if (!CanPlayCard(card, out string reason))
        {
            if (logCardActions)
                Debug.LogWarning($"[CardManager] Cannot play card {card.cardName}: {reason}");
            return false;
        }

        if (!pointsManager.SpendPoints(card.cost))
        {
            Debug.LogError("[CardManager] Failed to spend points!");
            return false;
        }

        GameObject placedDefense = defenseManager.PlaceDefenseAndRegister(card.defensePrefab, position);
        
        if (placedDefense == null)
        {
            pointsManager.AddPoints(card.cost);
            Debug.LogError("[CardManager] Failed to place defense!");
            return false;
        }

        RemoveCardFromHand(card);
        CardData newCard = DrawCard();
        UpdateHandUI();

        OnCardPlayed?.Invoke(card, position);

        if (logCardActions)
            Debug.Log($"[CardManager] Card played successfully: {card.cardName} at {position}");

        return true;
    }

    public bool CanPlayCard(CardData card, out string reason)
    {
        reason = string.Empty;

        if (card == null)
        {
            reason = "Card is null";
            return false;
        }

        if (!hand.Contains(card))
        {
            reason = "Card not in hand";
            return false;
        }

        if (turnManager != null && !turnManager.IsPlayerTurn())
        {
            reason = "Not player's turn";
            return false;
        }

        if (pointsManager != null && !pointsManager.CanAfford(card.cost))
        {
            reason = "Not enough points";
            return false;
        }

        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive())
        {
            reason = "Game is not active";
            return false;
        }

        return true;
    }

    public bool CanPlayCard(CardData card)
    {
        return CanPlayCard(card, out _);
    }
    #endregion

    #region Card Replacement System
    public bool ReplaceCardInHand(CardData oldCard, CardData newCard)
    {
        if (oldCard == null || newCard == null)
        {
            Debug.LogWarning("[CardManager] Cannot replace with null cards!");
            return false;
        }

        if (!hand.Contains(oldCard))
        {
            Debug.LogWarning($"[CardManager] Card {oldCard.cardName} not in hand!");
            return false;
        }

        int index = hand.IndexOf(oldCard);
        hand[index] = newCard;
        UpdateHandUI();

        if (logCardActions)
            Debug.Log($"[CardManager] Replaced {oldCard.cardName} with {newCard.cardName}");

        return true;
    }
    #endregion

    #region Turn Integration
private void HandlePlayerTurnStart()
    {
        // Mostrar cartas durante turno de jugador
        if (cardSlotsUI != null)
            cardSlotsUI.ShowSlotContainer();
        
        if (logCardActions)
            Debug.Log("[CardManager] Player turn started - cards enabled and visible");
    }

private void HandleClimberTurnStart()
    {
        // Ocultar cartas durante turno de escaladores
        if (cardSlotsUI != null)
            cardSlotsUI.HideSlotContainer();
        
        if (logCardActions)
            Debug.Log("[CardManager] Climber turn started - cards disabled and hidden");
    }
    #endregion

    #region Debug & Utility
    public void ResetCardSystem()
    {
        selectedDeck.Clear();
        currentDeck.Clear();
        hand.Clear();
        discardPile.Clear();

        if (cardSlotsUI != null)
            cardSlotsUI.ClearCards();

        if (logCardActions)
            Debug.Log("[CardManager] Card system reset");
    }

    [ContextMenu("Print Card System Status")]
    private void PrintStatus()
    {
        Debug.Log($"=== CARD MANAGER STATUS ===");
        Debug.Log($"Available Cards: {availableCards.Count}");
        Debug.Log($"Selected Deck: {selectedDeck.Count}");
        Debug.Log($"Current Deck: {currentDeck.Count}");
        Debug.Log($"Hand: {hand.Count}");
        Debug.Log($"========================");
    }
    #endregion
}
