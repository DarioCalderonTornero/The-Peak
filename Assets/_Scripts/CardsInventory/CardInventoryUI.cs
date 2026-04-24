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

    [Header("Paginación UI")]
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TextMeshProUGUI pageText;

    [Header("Configuración")]
    [SerializeField] private int maxSelectedCards = 8;
    [SerializeField] private int cardsPerPage = 9;
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

    // Página actual (0-based)
    private int currentPage = 0;

    [Header("Feedback slide de página")]
    [SerializeField] private float pageSlideDuration = 0.08f;
    [SerializeField] private float pageSlideDistance = 25f;

    private bool isChangingPage = false;


    private void Start()
    {
        if (startMatchButton != null)
        {
            startMatchButton.interactable = false;
            startMatchButton.onClick.AddListener(OnStartMatchButtonClicked);
        }

        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveAllListeners();
            prevPageButton.onClick.AddListener(GoToPrevPage);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.AddListener(GoToNextPage);
        }

        ClampCurrentPage();
        RefreshInventory();
        UpdateCountText();
        UpdatePaginationUI();
    }

    public void AddCard(CardData card)
    {
        if (card == null) return;

        if (!availableCards.Contains(card))
        {
            availableCards.Add(card);

            // Si la nueva carta crea una página nueva, no pasa nada:
            // mantenemos currentPage y refrescamos si el inventario está abierto.
            ClampCurrentPage();

            if (inventoryPanel != null && inventoryPanel.activeSelf)
            {
                RefreshInventory();
                UpdatePaginationUI();
            }
        }
    }

    public void ShowInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);

        currentPage = 0;

        ClampCurrentPage();
        RefreshInventory();
        UpdateCountText();
        ResetAllCardScales();
        UpdatePaginationUI();
    }

    public void HideInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    private void RefreshInventory()
    {
        if (cardContainer == null || cardPrefab == null) return;

        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        int total = availableCards.Count;
        if (total <= 0) return;

        int startIndex = currentPage * cardsPerPage;
        int endExclusive = Mathf.Min(startIndex + cardsPerPage, total);

        for (int i = startIndex; i < endExclusive; i++)
        {
            var cardData = availableCards[i];
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

            var flipComponent = cardObj.GetComponent<CardFlip>();
            if (flipComponent != null)
                flipComponent.ResetToFront();

            // Añadir efecto hover
            AddHoverEffect(cardObj);

            // Configurar botón de selección
            Button btn = cardObj.GetComponent<Button>();
            if (btn == null) btn = cardObj.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();

            // IMPORTANTE: capturamos variables locales para evitar closures raros
            CardData capturedData = cardData;
            GameObject capturedObj = cardObj;

            btn.onClick.AddListener(() => ToggleSelect(capturedObj, capturedData));

            // Mostrar color según estado
            var img = GetMainImage(cardObj);
            if (img != null)
                img.color = selectedCards.Contains(cardData) ? selectedColor : normalColor;
        }
    }

    private int GetTotalPages()
    {
        if (cardsPerPage <= 0) return 1;
        return Mathf.Max(1, Mathf.CeilToInt(availableCards.Count / (float)cardsPerPage));
    }

    private void ClampCurrentPage()
    {
        int totalPages = GetTotalPages();
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
    }

    private void GoToPrevPage()
    {
        if (isChangingPage || currentPage <= 0) return;
        StartCoroutine(ChangePageWithSlide(-1));
    }

    private void GoToNextPage()
    {
        int totalPages = GetTotalPages();
        if (isChangingPage || currentPage >= totalPages - 1) return;
        StartCoroutine(ChangePageWithSlide(1));
    }

    private void UpdatePaginationUI()
    {
        int totalPages = GetTotalPages();

        if (prevPageButton != null)
            prevPageButton.interactable = !isChangingPage && currentPage > 0;

        if (nextPageButton != null)
            nextPageButton.interactable = !isChangingPage && currentPage < totalPages - 1;

        if (pageText != null)
            pageText.text = $"{currentPage + 1} / {totalPages}";
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

    private IEnumerator ChangePageWithSlide(int direction)
    {
        isChangingPage = true;
        UpdatePaginationUI();

        List<RectTransform> visibleCards = GetVisibleCardRects();

        // 👇 Micro desliz direccional
        yield return StartCoroutine(SlideCards(visibleCards, direction));

        currentPage += direction;
        ClampCurrentPage();

        RefreshInventory();
        ResetAllCardScales();

        isChangingPage = false;
        UpdatePaginationUI();
    }

    private IEnumerator SlideCards(List<RectTransform> cards, int direction)
    {
        if (cards == null || cards.Count == 0)
            yield break;

        List<Vector2> originalPositions = new List<Vector2>(cards.Count);

        foreach (var rt in cards)
            originalPositions.Add(rt.anchoredPosition);

        float elapsed = 0f;

        // Dirección:
        // Derecha (+1) → cartas se mueven a la izquierda
        // Izquierda (-1) → cartas se mueven a la derecha
        float offset = -direction * pageSlideDistance;

        while (elapsed < pageSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pageSlideDuration);

            // curva tipo "punch" (ida y vuelta rápida)
            float curve = Mathf.Sin(t * Mathf.PI);

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;

                cards[i].anchoredPosition =
                    originalPositions[i] + new Vector2(offset * curve, 0f);
            }

            yield return null;
        }

        // Reset final
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            cards[i].anchoredPosition = originalPositions[i];
        }
    }

    private List<RectTransform> GetVisibleCardRects()
    {
        List<RectTransform> rects = new List<RectTransform>();

        if (cardContainer == null) return rects;

        foreach (Transform child in cardContainer)
        {
            RectTransform rt = child as RectTransform;
            if (rt != null)
                rects.Add(rt);
        }

        return rects;
    }

    private void UpdateCountText()
    {
        if (selectedCountText != null)
            selectedCountText.text = $"Select cards to start the game {selectedCards.Count} / {maxSelectedCards}";
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
        if (cardContainer == null) return;

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
        var img = cardObj.GetComponent<Image>();
        if (img == null)
            img = cardObj.GetComponentInChildren<Image>();
        return img;
    }
}