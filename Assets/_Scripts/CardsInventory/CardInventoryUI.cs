using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardInventoryUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject cardPrefab;

    [Header("Header UI")]
    [SerializeField] private TextMeshProUGUI selectedCountText;
    [SerializeField] private Button startMatchButton;

    [Header("Configuración")]
    [SerializeField] private int maxSelectedCards = 8;
    [SerializeField] private Color selectedColor = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color normalColor = Color.white;

    [Header("Escala de las cartas en el inventario")]
    [SerializeField] private Vector3 cardScale = Vector3.one;

    [Header("Cartas disponibles (desde el editor o en runtime)")]
    [SerializeField] private List<CardData> availableCards = new();

    public event Action<CardData> OnCardSelected;
    public event Action<CardData> OnCardDeselected;
    public event Action<List<CardData>> OnStartMatch;

    private readonly List<CardData> selectedCards = new();

    private void Start()
    {
        if (startMatchButton != null)
        {
            startMatchButton.interactable = false;
            startMatchButton.onClick.AddListener(OnStartMatchButtonClicked);
        }

        RefreshInventory();
        UpdateCountText();
    }

    public void AddCard(CardData card)
    {
        if (card == null) return;

        if (!availableCards.Contains(card))
        {
            availableCards.Add(card);
            if (inventoryPanel.activeSelf)
                RefreshInventory();
        }
    }

    public void ShowInventory()
    {
        inventoryPanel.SetActive(true);
        RefreshInventory();
        UpdateCountText();
    }

    public void HideInventory()
    {
        inventoryPanel.SetActive(false);
    }

    private void RefreshInventory()
    {
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        foreach (var cardData in availableCards)
        {
            if (cardData == null) continue;

            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            cardObj.transform.localScale = cardScale; // Escala definida desde el editor

            var cardUI = cardObj.GetComponent<DragCardUI>();

            if (cardUI != null)
            {
                cardUI.cardData = cardData;
                cardUI.SetupCardUI();

                // Eliminamos el bloqueo de raycasts — permite pulsar las cartas
                // var cg = cardUI.GetComponent<CanvasGroup>();
                // if (cg) cg.blocksRaycasts = false;

                // Agregar selección por clic
                Button btn = cardObj.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => ToggleSelect(cardUI, cardData));

                // Mostrar color según estado
                cardUI.cardImage.color = selectedCards.Contains(cardData) ? selectedColor : normalColor;
            }
        }
    }

    private void ToggleSelect(DragCardUI ui, CardData data)
    {
        bool isSelected = selectedCards.Contains(data);

        if (isSelected)
        {
            selectedCards.Remove(data);
            ui.cardImage.color = normalColor;
            OnCardDeselected?.Invoke(data);
        }
        else
        {
            if (selectedCards.Count >= maxSelectedCards)
                return;

            selectedCards.Add(data);
            ui.cardImage.color = selectedColor;
            OnCardSelected?.Invoke(data);
        }

        UpdateCountText();
        UpdateStartButtonState();
    }

    private void UpdateCountText()
    {
        if (selectedCountText != null)
            selectedCountText.text = $"Selecciona cartas para comenzar la partida {selectedCards.Count} / {maxSelectedCards}";
    }

    private void UpdateStartButtonState()
    {
        if (startMatchButton != null)
            startMatchButton.interactable = selectedCards.Count == maxSelectedCards;
    }

    private void OnStartMatchButtonClicked()
    {
        if (selectedCards.Count == maxSelectedCards)
        {
            OnStartMatch?.Invoke(new List<CardData>(selectedCards));
            HideInventory();
        }
    }

    public List<CardData> GetSelectedCards()
    {
        return new List<CardData>(selectedCards);
    }

    // Permite ajustar la escala de las cartas en tiempo real desde el inspector
    private void OnValidate()
    {
        if (cardContainer != null)
        {
            foreach (Transform child in cardContainer)
                child.localScale = cardScale;
        }
    }
}
