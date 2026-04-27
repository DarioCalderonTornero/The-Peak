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
    [SerializeField] private Button returnButton;

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
    private int currentPage = 0;

    [Header("Feedback slide de página")]
    [SerializeField] private float pageSlideDuration = 0.08f;
    [SerializeField] private float pageSlideDistance = 25f;

    private bool isChangingPage = false;

    [Header("Contadores de tipos de carta")]
    [SerializeField] private TextMeshProUGUI permanentCountText;  // muestra "X / X"
    [SerializeField] private TextMeshProUGUI temporalCountText;   // muestra "X / X"

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

        if (returnButton != null)
            returnButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScenee"));

        ClampCurrentPage();
        RefreshInventory();
        UpdateCountText();
        UpdatePaginationUI();
        UpdateTypeCounters();
    }

    public void AddCard(CardData card)
    {
        if (card == null) return;
        if (!availableCards.Contains(card))
        {
            availableCards.Add(card);
            ClampCurrentPage();
            if (inventoryPanel != null && inventoryPanel.activeSelf)
            {
                RefreshInventory();
                UpdatePaginationUI();
                UpdateTypeCounters();
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
        UpdateTypeCounters();
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

            // Desactivar drag en inventario
            var dragComponent = cardObj.GetComponent<DragCardUI>();
            if (dragComponent != null)
                dragComponent.enabled = false;

            // Configurar UI
            var cardUI = cardObj.GetComponent<DragCardUI>();
            if (cardUI != null)
            {
                cardUI.cardData = cardData;
                cardUI.SetupCardUI();
            }

            // Reset flip ANTES de aplicar colores
            var flipComponent = cardObj.GetComponent<CardFlip>();
            if (flipComponent != null)
                flipComponent.ResetToFront();

            // Hover de escala
            AddHoverEffect(cardObj);

            // Botón selección
            Button btn = cardObj.GetComponent<Button>();
            if (btn == null) btn = cardObj.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();

            CardData capturedData = cardData;
            GameObject capturedObj = cardObj;
            btn.onClick.AddListener(() => ToggleSelect(capturedObj, capturedData));

            SetCardStar(cardObj, selectedCards.Contains(cardData));
        }
    }

    // ─── Selección ────────────────────────────────────────────────

    private void ToggleSelect(GameObject cardObj, CardData data)
    {
        bool isSelected = selectedCards.Contains(data);

        if (isSelected)
        {
            selectedCards.Remove(data);
            SetCardStar(cardObj, false);
            OnCardDeselected?.Invoke(data);
        }
        else
        {
            if (selectedCards.Count >= maxSelectedCards) return;
            selectedCards.Add(data);
            SetCardStar(cardObj, true);
            OnCardSelected?.Invoke(data);
        }

        UpdateCountText();
        UpdateStartButtonState();
    }


    // ─── Estrella ─────────────────────────────────────────────────

    /// <summary>
    /// Activa o desactiva la estrella de selección en la carta.
    /// </summary>
    private void SetCardStar(GameObject cardObj, bool selected)
    {
        // Aplica a todas las estrellas de la carta (front y back)
        foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
            star.SetSelected(selected);
    }


    // ─── Hover escala + estrella ──────────────────────────────────

    private void AddHoverEffect(GameObject cardObj)
    {
        EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        Vector3 baseScale = cardObj.transform.localScale;
        Vector3 targetScale = baseScale * hoverScaleMultiplier;
        Coroutine scaleCoroutine = null;

        void StartSmoothScale(Vector3 to)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, to));
        }

        // Pointer Enter: escala + estrella hover
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) =>
        {
            StartSmoothScale(targetScale);
            foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
                star.OnHoverEnter();
        });
        trigger.triggers.Add(entryEnter);

        // Pointer Exit: escala + estrella exit
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) =>
        {
            StartSmoothScale(baseScale);
            foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
                star.OnHoverExit();
        });
        trigger.triggers.Add(entryExit);
    }

    /// <summary>
    /// Aplica el color de selección a la Image correcta,
    /// independientemente de si la carta está flipeada o no.
    /// </summary>
    private void ApplySelectionColor(GameObject cardObj, bool selected)
    {
        Color color = selected ? selectedColor : normalColor;

        // Usar CardSelectionTarget si existe (aplica a front Y back a la vez)
        var target = cardObj.GetComponent<CardSelectionTarget>();
        if (target != null)
        {
            target.SetSelectionColor(color);
            return;
        }

        // Fallback: buscar todas las Images y aplicar a todas
        foreach (var img in cardObj.GetComponentsInChildren<Image>(true))
            img.color = color;
    }

    // ─── Hover escala ─────────────────────────────────────────────

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

    // ─── Paginación ───────────────────────────────────────────────

    private int GetTotalPages()
    {
        if (cardsPerPage <= 0) return 1;
        return Mathf.Max(1, Mathf.CeilToInt(availableCards.Count / (float)cardsPerPage));
    }

    private void ClampCurrentPage()
    {
        currentPage = Mathf.Clamp(currentPage, 0, GetTotalPages() - 1);
    }

    private void GoToPrevPage()
    {
        if (isChangingPage || currentPage <= 0) return;
        StartCoroutine(ChangePageWithSlide(-1));
    }

    private void GoToNextPage()
    {
        if (isChangingPage || currentPage >= GetTotalPages() - 1) return;
        StartCoroutine(ChangePageWithSlide(1));
    }

    private void UpdatePaginationUI()
    {
        int totalPages = GetTotalPages();
        if (prevPageButton != null) prevPageButton.interactable = !isChangingPage && currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = !isChangingPage && currentPage < totalPages - 1;
        if (pageText != null) pageText.text = $"Página {currentPage + 1} / {totalPages}";
    }

    private IEnumerator ChangePageWithSlide(int direction)
    {
        isChangingPage = true;
        UpdatePaginationUI();

        yield return StartCoroutine(SlideCards(GetVisibleCardRects(), direction));

        currentPage += direction;
        ClampCurrentPage();
        RefreshInventory();
        ResetAllCardScales();

        isChangingPage = false;
        UpdatePaginationUI();
    }

    private IEnumerator SlideCards(List<RectTransform> cards, int direction)
    {
        if (cards == null || cards.Count == 0) yield break;

        var originalPositions = new List<Vector2>(cards.Count);
        foreach (var rt in cards) originalPositions.Add(rt.anchoredPosition);

        float elapsed = 0f;
        float offset = -direction * pageSlideDistance;

        while (elapsed < pageSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float curve = Mathf.Sin(Mathf.Clamp01(elapsed / pageSlideDuration) * Mathf.PI);
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                cards[i].anchoredPosition = originalPositions[i] + new Vector2(offset * curve, 0f);
            }
            yield return null;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            cards[i].anchoredPosition = originalPositions[i];
        }
    }

    private List<RectTransform> GetVisibleCardRects()
    {
        var rects = new List<RectTransform>();
        if (cardContainer == null) return rects;
        foreach (Transform child in cardContainer)
        {
            var rt = child as RectTransform;
            if (rt != null) rects.Add(rt);
        }
        return rects;
    }

    // ─── UI helpers ───────────────────────────────────────────────

    private void UpdateCountText()
    {
        // if (selectedCountText != null)
            // selectedCountText.text = $"Select cards to start the game {selectedCards.Count} / {maxSelectedCards}";
    }

    private void UpdateStartButtonState()
    {
        if (startMatchButton != null)
            startMatchButton.interactable = selectedCards.Count == maxSelectedCards;
    }

    private void OnStartMatchButtonClicked()
    {
        if (selectedCards.Count != maxSelectedCards) return;

        // ✅ Guardar el deck antes de empezar la partida
        if (LastDeckManager.Instance != null)
            LastDeckManager.Instance.SaveDeck(new List<CardData>(selectedCards));

        OnStartMatch?.Invoke(new List<CardData>(selectedCards));
        HideInventory();
    }

    public List<CardData> GetSelectedCards() => new List<CardData>(selectedCards);

    private void ResetAllCardScales()
    {
        if (cardContainer == null) return;
        foreach (Transform child in cardContainer)
            child.localScale = cardScale;
    }


    private void UpdateTypeCounters()
    {
        int totalPermanent = 0;
        int totalTemporal = 0;

        foreach (var card in availableCards)
        {
            if (card == null) continue;
            if (card.cardType == CardData.CardType.Permanente) totalPermanent++;
            else if (card.cardType == CardData.CardType.Temporal) totalTemporal++;
            // Eventual no cuenta en ninguno de los dos contadores
        }

        if (permanentCountText != null)
            permanentCountText.text = $"{totalPermanent} / {totalPermanent}";

        if (temporalCountText != null)
            temporalCountText.text = $"{totalTemporal} / {totalTemporal}";
    }

    private void OnValidate()
    {
        if (cardContainer != null)
            foreach (Transform child in cardContainer)
                child.localScale = cardScale;
    }
}