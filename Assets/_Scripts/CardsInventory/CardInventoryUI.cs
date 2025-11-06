using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField][Range(1.01f, 1.5f)] private float hoverScaleMultiplier = 1.1f;
    [SerializeField] private float hoverSmoothTime = 0.12f;

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
        ResetAllCardScales();
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
            cardObj.transform.localScale = cardScale;

            // Si tiene DragCardUI, lo desactivamos dentro del inventario
            var dragComponent = cardObj.GetComponent<DragCardUI>();
            if (dragComponent != null)
                dragComponent.enabled = false;

            // Configurar la UI de la carta
            var cardUI = cardObj.GetComponent<DragCardUI>();
            if (cardUI != null)
            {
                cardUI.cardData = cardData;
                cardUI.SetupCardUI();
            }

            // Añadir efecto hover
            AddHoverEffect(cardObj);

            // Configurar botón de selección
            Button btn = cardObj.GetComponent<Button>();
            if (btn == null) btn = cardObj.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ToggleSelect(cardObj, cardData));

            // Mostrar color según estado
            var img = GetMainImage(cardObj);
            if (img != null)
                img.color = selectedCards.Contains(cardData) ? selectedColor : normalColor;
        }
    }

    private void AddHoverEffect(GameObject cardObj)
    {
        EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        Vector3 baseScale = cardScale;
        Vector3 targetScale = baseScale * hoverScaleMultiplier;
        Coroutine scaleCoroutine = null;

        void StartSmoothScale(Vector3 to)
        {
            if (scaleCoroutine != null)
                StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, to));
        }

        // Pointer Enter
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) => StartSmoothScale(targetScale));
        trigger.triggers.Add(entryEnter);

        // Pointer Exit
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) => StartSmoothScale(baseScale));
        trigger.triggers.Add(entryExit);
    }

    private IEnumerator SmoothScale(Transform target, Vector3 to)
    {
        Vector3 from = target.localScale;
        float elapsed = 0f;

        while (elapsed < hoverSmoothTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / hoverSmoothTime);
            target.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        target.localScale = to;
    }

    private void ToggleSelect(GameObject cardObj, CardData data)
    {
        bool isSelected = selectedCards.Contains(data);
        var img = GetMainImage(cardObj);

        if (isSelected)
        {
            selectedCards.Remove(data);
            if (img != null) img.color = normalColor;
            OnCardDeselected?.Invoke(data);
        }
        else
        {
            if (selectedCards.Count >= maxSelectedCards)
                return;

            selectedCards.Add(data);
            if (img != null) img.color = selectedColor;
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

    private void ResetAllCardScales()
    {
        foreach (Transform child in cardContainer)
            child.localScale = cardScale;
    }

    private void OnValidate()
    {
        if (cardContainer != null)
        {
            foreach (Transform child in cardContainer)
                child.localScale = cardScale;
        }
    }

    private Image GetMainImage(GameObject cardObj)
    {
        // Busca el Image principal de la carta
        var img = cardObj.GetComponent<Image>();
        if (img == null)
            img = cardObj.GetComponentInChildren<Image>();
        return img;
    }
}
