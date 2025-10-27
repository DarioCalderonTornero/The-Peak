using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardSlotsUI : MonoBehaviour
{
    [Header("Configuración UI")]
    [SerializeField] private RectTransform slotsContainer; // Contenedor con VerticalLayoutGroup
    public GameObject cardPrefab;        // Prefab que contiene DragCardUI
    [SerializeField] private List<CardData> startingCards; // Cartas iniciales que se mostrarán

    private readonly List<DragCardUI> currentCards = new List<DragCardUI>();

    private void Start()
    {
        if (slotsContainer == null)
            slotsContainer = GetComponent<RectTransform>();

        if (cardPrefab == null)
        {
            Debug.LogError("No se asignó el prefab de carta (DragCardUI).");
            return;
        }

        CreateInitialCards();
    }

    /// <summary>
    /// Instancia todas las cartas iniciales en la UI.
    /// </summary>
    private void CreateInitialCards()
    {
        ClearCards();

        foreach (var cardData in startingCards)
        {
            AddCard(cardData);
        }
    }

    /// <summary>
    /// Añade una carta nueva a la lista visual.
    /// </summary>
    public void AddCard(CardData data)
    {
        if (data == null) return;

        GameObject newCard = Instantiate(cardPrefab, slotsContainer);
        DragCardUI dragCard = newCard.GetComponent<DragCardUI>();

        if (dragCard != null)
        {
            dragCard.cardData = data;
            dragCard.mainCamera = Camera.main; // asegura que tenga cámara asignada
        }

        currentCards.Add(dragCard);
    }

    /// <summary>
    /// Elimina todas las cartas del contenedor.
    /// </summary>
    public void ClearCards()
    {
        foreach (Transform child in slotsContainer)
        {
            Destroy(child.gameObject);
        }

        currentCards.Clear();
    }

    /// <summary>
    /// Quita una carta específica (por índice).
    /// </summary>
    public void RemoveCardAt(int index)
    {
        if (index < 0 || index >= currentCards.Count)
            return;

        Destroy(currentCards[index].gameObject);
        currentCards.RemoveAt(index);
    }

    /// <summary>
    /// Devuelve la cantidad actual de cartas activas.
    /// </summary>
    public int GetSlotCount()
    {
        return currentCards.Count;
    }

    public DragCardUI[] GetAllCards()
    {
        return currentCards.ToArray();
    }

    // Devuelve el índice de una carta concreta
    public int GetCardIndex(DragCardUI card)
    {
        return currentCards.IndexOf(card);
    }

    // Reemplaza la carta en un slot específico
    public void ReplaceCardAt(int index, CardData newData)
    {
        if (index < 0 || index >= currentCards.Count) return;

        // Destruir la carta vieja
        Destroy(currentCards[index].gameObject);

        // Crear la nueva en el mismo slot
        GameObject newCardGO = Instantiate(cardPrefab, slotsContainer);
        DragCardUI dragCard = newCardGO.GetComponent<DragCardUI>();

        if (dragCard != null)
        {
            dragCard.cardData = newData;
            dragCard.mainCamera = Camera.main;
        }

        currentCards[index] = dragCard;
    }
}
