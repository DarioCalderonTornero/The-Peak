using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardRewardUI : MonoBehaviour
{
    public static CardRewardUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject rewardPanel;

    [Header("Contenedores de cartas")]
    [SerializeField] private Transform cardContainerA;
    [SerializeField] private Transform cardContainerB;
    [SerializeField] private GameObject cardPrefab;

    [Header("Configuración visual")]
    [SerializeField] private Vector3 cardScale = Vector3.one;
    [SerializeField][Range(1.01f, 1.5f)] private float hoverScaleMultiplier = 1.1f;
    [SerializeField] private float hoverSmoothTime = 0.12f;
    [SerializeField] private float glowFadeDuration = 0.15f;

    [SerializeField] private CardUnlockData cardUnlockData;

    public event Action<CardData> OnCardChosen;

    private CardData optionA;
    private CardData optionB;
    private bool rewardPending = false;
    private GameObject cardObjA;
    private GameObject cardObjB;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (LevelExperienceManager.Instance != null)
        {
            LevelExperienceManager.Instance.OnCardRewardTriggered -= HandleCardRewardTriggered;
            LevelExperienceManager.Instance.OnCardRewardTriggered += HandleCardRewardTriggered;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
        }
    }

    private void OnDisable()
    {
        if (LevelExperienceManager.Instance != null)
            LevelExperienceManager.Instance.OnCardRewardTriggered -= HandleCardRewardTriggered;

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
    }

    private void Start()
    {
        // LevelExperienceManager
        if (LevelExperienceManager.Instance != null)
        {
            LevelExperienceManager.Instance.OnCardRewardTriggered -= HandleCardRewardTriggered;
            LevelExperienceManager.Instance.OnCardRewardTriggered += HandleCardRewardTriggered;
        }

        // TurnManager — puede llegar tarde
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
            Debug.Log("[CardRewardUI] Suscrito a OnPlayerTurnStart en Start");
        }
        else
            StartCoroutine(WaitForTurnManager());
    }

    private IEnumerator WaitForTurnManager()
    {
        Debug.Log("[CardRewardUI] Esperando TurnManager...");
        yield return new WaitUntil(() => TurnManager.Instance != null);
        TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
        TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
        Debug.Log("[CardRewardUI] Suscrito a OnPlayerTurnStart (late)");
    }

    private void HandleCardRewardTriggered()
    {
        Debug.Log("[CardRewardUI] HandleCardRewardTriggered EJECUTADO");

        List<CardData> locked = cardUnlockData != null
            ? cardUnlockData.GetLockedCards()
            : new List<CardData>();

        if (locked == null || locked.Count == 0) return;

        locked.Sort((a, b) => UnityEngine.Random.Range(-1, 2));
        optionA = locked[0];
        optionB = locked.Count > 1 ? locked[1] : locked[0];

        // Mostrar en el siguiente frame en lugar de esperar al próximo turno
        StartCoroutine(ShowRewardNextFrame());
    }

    private IEnumerator ShowRewardNextFrame()
    {
        yield return null; // esperar un frame
        yield return null; // esperar otro frame por seguridad
        ShowReward();
    }

    private void HandlePlayerTurnStart()
    {
        Debug.Log($"[CardRewardUI] HandlePlayerTurnStart | rewardPending={rewardPending}");
        if (!rewardPending) return;
        rewardPending = false;
        ShowReward();
    }

    private void ShowReward()
    {
        Debug.Log("[CardRewardUI] ShowReward llamado");
        if (rewardPanel == null || cardPrefab == null)
        {
            Debug.Log($"[CardRewardUI] ShowReward ABORTADO | rewardPanel={rewardPanel} | cardPrefab={cardPrefab}");
            return;
        }
        if (rewardPanel == null || cardPrefab == null) return;

        if (cardObjA != null) Destroy(cardObjA);
        if (cardObjB != null) Destroy(cardObjB);

        cardObjA = InstantiateCard(optionA, cardContainerA, () => Choose(optionA));
        cardObjB = InstantiateCard(optionB, cardContainerB, () => Choose(optionB));

        rewardPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private GameObject InstantiateCard(CardData data, Transform container, System.Action onChoose)
    {
        if (data == null || container == null) return null;

        GameObject cardObj = Instantiate(cardPrefab, container);
        cardObj.transform.localScale = cardScale;

        var dragUI = cardObj.GetComponent<DragCardUI>();
        if (dragUI != null)
        {
            dragUI.enabled = false;
            dragUI.cardData = data;
            dragUI.SetupCardUI();
        }

        var flipComp = cardObj.GetComponent<CardFlip>();
        if (flipComp != null) flipComp.ResetToFront();

        AddHoverEffect(cardObj);

        Button btn = cardObj.GetComponent<Button>();
        if (btn == null) btn = cardObj.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onChoose?.Invoke());

        return cardObj;
    }

    private void AddHoverEffect(GameObject cardObj)
    {
        EventTrigger trigger = cardObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        Vector3 baseScale = cardObj.transform.localScale;
        Vector3 targetScale = baseScale * hoverScaleMultiplier;
        Coroutine scaleCoroutine = null;
        Coroutine glowCoroutine = null;

        Image glowImage = null;
        var glowTransform = cardObj.transform.Find("Glow");
        if (glowTransform != null)
        {
            glowImage = glowTransform.GetComponent<Image>();
            if (glowImage != null)
            {
                Color c = glowImage.color;
                c.a = 0f;
                glowImage.color = c;
            }
        }

        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) =>
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, targetScale));
            if (glowCoroutine != null) StopCoroutine(glowCoroutine);
            if (glowImage != null) glowCoroutine = StartCoroutine(FadeGlow(glowImage, 1f));
            foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
                star.OnHoverEnter();
        });
        trigger.triggers.Add(entryEnter);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) =>
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(SmoothScale(cardObj.transform, baseScale));
            if (glowCoroutine != null) StopCoroutine(glowCoroutine);
            if (glowImage != null) glowCoroutine = StartCoroutine(FadeGlow(glowImage, 0f));
            foreach (var star in cardObj.GetComponentsInChildren<CardStar>(true))
                star.OnHoverExit();
        });
        trigger.triggers.Add(entryExit);
    }

    private IEnumerator SmoothScale(Transform target, Vector3 to)
    {
        if (target == null) yield break;
        Vector3 from = target.localScale;
        float elapsed = 0f;
        while (elapsed < hoverSmoothTime)
        {
            if (target == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / hoverSmoothTime);
            target.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        if (target != null) target.localScale = to;
    }

    private IEnumerator FadeGlow(Image img, float toAlpha)
    {
        float fromAlpha = img.color.a;
        float t = 0f;
        while (t < glowFadeDuration)
        {
            if (img == null) yield break;
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / glowFadeDuration), 2f);
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

    private void Choose(CardData chosen)
    {
        Time.timeScale = 1f;
        if (rewardPanel != null) rewardPanel.SetActive(false);

        if (cardUnlockData != null)
            cardUnlockData.SetState(chosen, CardUnlockState.Pending);

        if (cardObjA != null) { Destroy(cardObjA); cardObjA = null; }
        if (cardObjB != null) { Destroy(cardObjB); cardObjB = null; }

        OnCardChosen?.Invoke(chosen);

        if (CardGameManager.Instance != null)
            CardGameManager.Instance.AddRewardCardToDeck(chosen);
    }
}