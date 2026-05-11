using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
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
    [SerializeField] private Image permanentIcon;        // ← nuevo
    [SerializeField] private Image temporalIcon;         // ← nuevo
    [SerializeField] private Sprite permanentIconNormal; // ← nuevo
    [SerializeField] private Sprite permanentIconPending;// ← nuevo
    [SerializeField] private Sprite temporalIconNormal;  // ← nuevo
    [SerializeField] private Sprite temporalIconPending; // ← nuevo

    [Header("Glow hover")]
    [SerializeField] private float glowFadeDuration = 0.15f;

    [Header("Card Unlock")]
    [SerializeField] private CardUnlockData cardUnlockData;
    [SerializeField] private Sprite lockedSprite; 
    [SerializeField] private Sprite pendingSprite;


    private void Awake()
    {
        if (cardUnlockData != null)
            cardUnlockData.Load();
    }

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
        ResetAllCardScales();
        UpdatePaginationUI();
        UpdateTypeCounters();
    }

    public void HideInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    #if UNITY_EDITOR
        [ContextMenu("Reset Card Unlock States")]
        private void ResetCardUnlockStates()
        {
            if (cardUnlockData != null)
            {
                cardUnlockData.ResetAll();
                Debug.Log("[CardInventoryUI] Estados de desbloqueo reseteados.");
            }
        }
    #endif

    [ContextMenu("Debug PlayerPrefs")]
    private void DebugPlayerPrefs()
    {
        if (cardUnlockData == null) return;
        foreach (var card in cardUnlockData.GetAllCards())
        {
            string key = $"CardUnlock_{card.name}";
            Debug.Log($"[PlayerPrefs] {key} = {PlayerPrefs.GetInt(key, -999)}");
        }
    }

    private void RefreshInventory()
    {
        if (cardContainer == null || cardPrefab == null) return;
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        // Usar todas las cartas del CardUnlockData si está asignado
        var allCards = cardUnlockData != null
            ? cardUnlockData.GetAllCards()
            : availableCards;

        int total = allCards.Count;
        if (total <= 0) return;

        int startIndex = currentPage * cardsPerPage;
        int endExclusive = Mathf.Min(startIndex + cardsPerPage, total);

        for (int i = startIndex; i < endExclusive; i++)
        {
            var cardData = allCards[i];
            if (cardData == null) continue;

            var state = cardUnlockData != null
                ? cardUnlockData.GetState(cardData)
                : CardUnlockState.Unlocked;

            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            cardObj.transform.localScale = cardScale;

            if (state == CardUnlockState.Locked)
            {
                SetupLockedCard(cardObj, cardData, lockedSprite);
                continue; // no añadir hover ni botón
            }

            if (state == CardUnlockState.Pending)
            {
                SetupPendingCard(cardObj, cardData);
                continue;
            }

            // Estado normal — código existente
            var dragComponent = cardObj.GetComponent<DragCardUI>();
            if (dragComponent != null) dragComponent.enabled = false;

            var cardUI = cardObj.GetComponent<DragCardUI>();
            if (cardUI != null)
            {
                cardUI.cardData = cardData;
                cardUI.SetupCardUI();
            }

            var flipComponent = cardObj.GetComponent<CardFlip>();
            if (flipComponent != null) flipComponent.ResetToFront();

            AddHoverEffect(cardObj);

            Button btn = cardObj.GetComponent<Button>();
            if (btn == null) btn = cardObj.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            CardData capturedData = cardData;
            GameObject capturedObj = cardObj;
            btn.onClick.AddListener(() => ToggleSelect(capturedObj, capturedData));

            SetCardStar(cardObj, selectedCards.Contains(cardData));
        }
    }

    [SerializeField] private Sprite pendingSpritePermanente;
    [SerializeField] private Sprite pendingSpriteTemporal;

    private void SetupLockedCard(GameObject cardObj, CardData cardData, Sprite sprite)
    {
        var drag = cardObj.GetComponent<DragCardUI>();
        if (drag != null) drag.enabled = false;

        var cg = cardObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
        cg.interactable = true;  // sí interactuable para capturar el click
        cg.blocksRaycasts = true;

        HideAllExceptSprite(cardObj, sprite);

        // Click → temblor + mensaje
        Button btn = cardObj.GetComponent<Button>();
        if (btn == null) btn = cardObj.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        bool isShaking = false; // ← flag local por carta

        btn.onClick.AddListener(() =>
        {
            if (isShaking) return; // ← ignorar si ya está vibrando

            RectTransform cardRT = cardObj.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            cardRT.GetWorldCorners(corners);
            Vector3 capturedCenter = (corners[0] + corners[2]) / 2f;

            StartCoroutine(ShakeCardLocked(cardObj, () => isShaking = false));
            isShaking = true;

            if (!string.IsNullOrEmpty(cardData.lockedMessage))
                LockedCardMessage.Instance?.Show(cardData.lockedMessage, capturedCenter);
        });
    }

    private IEnumerator ShakeCardLocked(GameObject cardObj, System.Action onComplete)
    {
        RectTransform rt = cardObj.GetComponent<RectTransform>();
        Vector2 originalPos = rt.anchoredPosition;
        float duration = 0.3f;
        float magnitude = 8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float offsetX = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            rt.anchoredPosition = originalPos + new Vector2(offsetX, 0f);
            yield return null;
        }

        rt.anchoredPosition = originalPos;
        onComplete?.Invoke();
    }

    private void SetupPendingCard(GameObject cardObj, CardData cardData)
    {
        var drag = cardObj.GetComponent<DragCardUI>();
        if (drag != null) drag.enabled = false;

        // Configurar datos reales en la carta (para que estén listos cuando se voltee)
        var dragUI = cardObj.GetComponent<DragCardUI>();
        if (dragUI != null)
        {
            dragUI.cardData = cardData;
            dragUI.SetupCardUI();
        }

        // Activar estado pendiente en CardFlip
        var flip = cardObj.GetComponent<CardFlip>();
        if (flip != null)
        {
            // Elegir sprite según tipo
            Sprite pendingSprite = cardData.cardType == CardData.CardType.Permanente
                ? pendingSpritePermanente
                : pendingSpriteTemporal;

            flip.SetPendingSprite(pendingSprite);
            flip.SetPending(true);
            // Pasar la escala base para la animación
            flip._unlockBaseScale = cardScale;

            flip.OnUnlockFlipComplete += () =>
            {
                // Reactivar explícitamente todos los hijos de la raíz
                foreach (Transform child in cardObj.transform)
                    child.gameObject.SetActive(true);

                var backCard = cardObj.transform.Find("BackCard");
                if (backCard != null) backCard.gameObject.SetActive(false);

                // Resetear Glow a alpha 0 para que AddHoverEffect lo encuentre activo
                var glowTransform = cardObj.transform.Find("Glow");
                if (glowTransform != null)
                {
                    glowTransform.gameObject.SetActive(true);
                    var glowImg = glowTransform.GetComponent<Image>();
                    if (glowImg != null)
                    {
                        Color c = glowImg.color;
                        c.a = 0f;
                        glowImg.color = c;
                    }
                }

                cardUnlockData.SetState(cardData, CardUnlockState.Unlocked);
                if (!availableCards.Contains(cardData))
                    availableCards.Add(cardData);

                var btn = cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.interactable = true;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => ToggleSelect(cardObj, cardData));
                }

                AddHoverEffect(cardObj);
                SetCardStar(cardObj, selectedCards.Contains(cardData));
                UpdateTypeCounters();
            };
        }

        // Hover de escala solamente mientras está pendiente
        AddScaleOnlyHover(cardObj);

        // Botón para capturar el click izquierdo (CardFlip lo gestiona via OnPointerClick)
        var btnSetup = cardObj.GetComponent<Button>();
        if (btnSetup == null) btnSetup = cardObj.AddComponent<Button>();
        btnSetup.onClick.RemoveAllListeners();
        // El click lo maneja CardFlip.OnPointerClick directamente
    }

    private void AddScaleOnlyHover(GameObject cardObj)
    {
        Vector3 baseScale = cardObj.transform.localScale;
        Vector3 targetScale = baseScale * hoverScaleMultiplier;
        Coroutine scaleCoroutine = null;

        EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) =>
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, targetScale));
        });
        trigger.triggers.Add(entryEnter);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) =>
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, baseScale));
        });
        trigger.triggers.Add(entryExit);
    }

    private void HideAllExceptSprite(GameObject cardObj, Sprite sprite)
    {
        // Desactivar todos los hijos de la raíz excepto FrontCard
        foreach (Transform child in cardObj.transform)
        {
            if (child.name != "FrontCard")
                child.gameObject.SetActive(false);
        }

        // Dentro de FrontCard, desactivar todo excepto FrontImage
        var frontCard = cardObj.transform.Find("FrontCard");
        if (frontCard == null) return;

        foreach (Transform child in frontCard)
        {
            if (child.name != "FrontImage")
                child.gameObject.SetActive(false);
        }

        // Asignar el sprite al FrontImage
        var frontImage = frontCard.Find("FrontImage")?.GetComponent<Image>();
        if (frontImage != null && sprite != null)
            frontImage.sprite = sprite;
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

    void AddHoverEffect(GameObject cardObj)
    {
        EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        Vector3 baseScale = cardObj.transform.localScale;
        Vector3 targetScale = baseScale * hoverScaleMultiplier;
        Coroutine scaleCoroutine = null;
        Coroutine glowCoroutine = null;

        // Buscar la Image del Glow por nombre
        Image glowImage = null;
        var glowTransform = cardObj.transform.Find("Glow");
        if (glowTransform != null)
        {
            glowImage = glowTransform.GetComponent<Image>();
            // Empezar invisible
            if (glowImage != null)
            {
                Color c = glowImage.color;
                c.a = 0f;
                glowImage.color = c;
            }
        }

        void StartSmoothScale(Vector3 to)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, to));
        }

        void StartGlowFade(float toAlpha)
        {
            if (glowCoroutine != null) StopCoroutine(glowCoroutine);
            if (glowImage != null)
                glowCoroutine = StartCoroutine(FadeGlow(glowImage, toAlpha));
        }

        // Pointer Enter
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) =>
        {
            StartSmoothScale(targetScale);
            StartGlowFade(1f);
            foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
                star.OnHoverEnter();
        });
        trigger.triggers.Add(entryEnter);

        // Pointer Exit
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) =>
        {
            StartSmoothScale(baseScale);
            StartGlowFade(0f);
            foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
                star.OnHoverExit();
        });
        trigger.triggers.Add(entryExit);
    }

    private IEnumerator FadeGlow(Image img, float toAlpha)
    {
        float fromAlpha = img.color.a;
        float duration = glowFadeDuration;
        float t = 0f;

        while (t < duration)
        {
            if (img == null) yield break;
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / duration);
            n = 1f - Mathf.Pow(1f - n, 2f); // EaseOut
            Color c = img.color;
            c.a = Mathf.Lerp(fromAlpha, toAlpha, n);
            img.color = c;
            yield return null;
        }

        if (img != null)
        {
            Color c = img.color;
            c.a = toAlpha;
            img.color = c;
        }
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
        if (target == null) yield break;

        Vector3 from = target.localScale;
        float elapsed = 0f;
        while (elapsed < hoverSmoothTime)
        {
            // Comprobar que el objeto no fue destruido entre frames
            if (target == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / hoverSmoothTime);
            target.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        if (target != null)
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
        if (cardUnlockData == null) return;

        int unlockedPermanent = 0;
        int totalPermanent = 0;
        int unlockedTemporal = 0;
        int totalTemporal = 0;
        bool hasPendingPermanent = false;
        bool hasPendingTemporal = false;

        foreach (var card in cardUnlockData.GetAllCards())
        {
            if (card == null) continue;
            CardUnlockState state = cardUnlockData.GetState(card);

            if (card.cardType == CardData.CardType.Permanente)
            {
                totalPermanent++;
                if (state == CardUnlockState.Unlocked) unlockedPermanent++;
                if (state == CardUnlockState.Pending) hasPendingPermanent = true;
            }
            else if (card.cardType == CardData.CardType.Temporal)
            {
                totalTemporal++;
                if (state == CardUnlockState.Unlocked) unlockedTemporal++;
                if (state == CardUnlockState.Pending) hasPendingTemporal = true;
            }
        }

        if (permanentCountText != null)
            permanentCountText.text = $"{unlockedPermanent} / {totalPermanent}";

        if (temporalCountText != null)
            temporalCountText.text = $"{unlockedTemporal} / {totalTemporal}";

        // Iconos: pending = icono especial, sin pending = normal
        if (permanentIcon != null)
            permanentIcon.sprite = hasPendingPermanent ? permanentIconPending : permanentIconNormal;

        if (temporalIcon != null)
            temporalIcon.sprite = hasPendingTemporal ? temporalIconPending : temporalIconNormal;
    }

    private void OnValidate()
    {
        if (cardContainer != null)
            foreach (Transform child in cardContainer)
                child.localScale = cardScale;
    }
}