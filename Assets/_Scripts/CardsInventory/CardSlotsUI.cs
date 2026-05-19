using System.Collections.Generic;
using UnityEngine;

public class CardSlotsUI : MonoBehaviour
{
    public static CardSlotsUI Instance { get; private set; }

    [Header("Configuraci�n UI")]
    [SerializeField] private RectTransform slotsContainer; // Contenedor con VerticalLayoutGroup
    public GameObject cardPrefab;        // Prefab que contiene DragCardUI
    [SerializeField] private List<CardData> startingCards; // Cartas iniciales que se mostrar�n

    private readonly List<DragCardUI> currentCards = new List<DragCardUI>();

    [Header("Efecto mano de cartas")]
    [SerializeField] private float cardRotationAngle = 8f;   // grados de rotación lateral
    [SerializeField] private float cardLiftAmount = 15f;      // píxeles que sube cada carta lateral
    [SerializeField] private Vector2 cardSpacing = new Vector2(220f, 0f); // separación entre cartas

    private void Awake()
    {
        Instance = this;    
    }

    private void Start()
    {
        if (slotsContainer == null)
            slotsContainer = GetComponent<RectTransform>();

        if (cardPrefab == null)
        {
            Debug.LogError("No se asign� el prefab de carta (DragCardUI).");
            return;
        }

        // CreateInitialCards();
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
    /// A�ade una carta nueva a la lista visual.
    /// </summary>
    public void AddCard(CardData data)
    {
        if (data == null) return;

        GameObject newCard = Instantiate(cardPrefab, slotsContainer);
        DragCardUI dragCard = newCard.GetComponent<DragCardUI>();

        if (dragCard != null)
        {
            dragCard.cardData = data;
            dragCard.mainCamera = Camera.main; // asegura que tenga c�mara asignada
        }

        currentCards.Add(dragCard);

        ApplyHandLayout();
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
    /// Quita una carta espec�fica (por �ndice).
    /// </summary>
    public void RemoveCardAt(int index)
    {
        if (index < 0 || index >= currentCards.Count)
            return;

        Destroy(currentCards[index].gameObject);
        currentCards.RemoveAt(index);

        ApplyHandLayout();
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

    // Devuelve el �ndice de una carta concreta
    public int GetCardIndex(DragCardUI card)
    {
        return currentCards.IndexOf(card);
    }

    // Reemplaza la carta en un slot espec�fico
    public void ReplaceCardAt(int index, CardData newData)
    {
        if (index < 0 || index >= currentCards.Count) return;

        // Guarda el objeto viejo y su posición
        var oldCardGO = currentCards[index].gameObject;

        // Instancia la nueva carta como hija del mismo contenedor
        GameObject newCardGO = Instantiate(cardPrefab, slotsContainer);
        DragCardUI dragCard = newCardGO.GetComponent<DragCardUI>();

        if (dragCard != null)
        {
            dragCard.cardData = newData;
            dragCard.mainCamera = Camera.main;
        }

        // 🔹 Inserta el nuevo GameObject en la misma posición de la jerarquía
        newCardGO.transform.SetSiblingIndex(oldCardGO.transform.GetSiblingIndex());

        // Destruye la carta vieja
        Destroy(oldCardGO);

        // Actualiza la lista lógica
        currentCards[index] = dragCard;

        ApplyHandLayout();
    }

    public void ShowSlotContainer()
    {
        slotsContainer.gameObject.SetActive(true);
    }

    public void HideSlotContainer()
    {
        slotsContainer.gameObject.SetActive(false);
    }

    private void ApplyHandLayout()
    {
        int count = currentCards.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            if (currentCards[i] == null) continue;

            RectTransform rt = currentCards[i].GetComponent<RectTransform>();
            if (rt == null) continue;

            // Todas rectas, sin rotación
            rt.localRotation = Quaternion.identity;
            currentCards[i].handRotationAngle = 0f;

            var pos = rt.anchoredPosition;
            pos.y = 0f;
            rt.anchoredPosition = pos;
        }
    }
}
