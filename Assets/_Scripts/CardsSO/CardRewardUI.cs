using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardRewardUI : MonoBehaviour
{
    public static CardRewardUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject rewardPanel;

    [Header("Carta A")]
    [SerializeField] private Button buttonA;
    [SerializeField] private Image imageA;
    [SerializeField] private TextMeshProUGUI nameTextA;

    [Header("Carta B")]
    [SerializeField] private Button buttonB;
    [SerializeField] private Image imageB;
    [SerializeField] private TextMeshProUGUI nameTextB;

    public event Action<CardData> OnCardChosen;

    private CardData optionA;
    private CardData optionB;
    private bool subscribed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    private void Start()
    {
        TrySubscribe();

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
    }

    private void OnEnable() => TrySubscribe();

    private void OnDisable()
    {
        if (subscribed && LevelExperienceManager.Instance != null)
        {
            LevelExperienceManager.Instance.OnCardRewardTriggered -= HandleCardRewardTriggered;
            subscribed = false;
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
    }

    private void HandlePlayerTurnStart()
    {
        if (!rewardPending) return;
        rewardPending = false;
        ShowReward();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        if (LevelExperienceManager.Instance != null)
        {
            LevelExperienceManager.Instance.OnCardRewardTriggered += HandleCardRewardTriggered;
            subscribed = true;
        }
        else
            StartCoroutine(SubscribeLate());
    }

    private IEnumerator SubscribeLate()
    {
        yield return new WaitUntil(() => LevelExperienceManager.Instance != null);
        if (!subscribed)
        {
            LevelExperienceManager.Instance.OnCardRewardTriggered += HandleCardRewardTriggered;
            subscribed = true;
        }
    }

    [SerializeField] private CardUnlockData cardUnlockData;

    private bool rewardPending = false;

    private void HandleCardRewardTriggered()
    {
        List<CardData> locked = cardUnlockData != null
            ? cardUnlockData.GetLockedCards()
            : (CardGameManager.Instance?.GetRewardCandidates() ?? new());

        if (locked == null || locked.Count == 0) return;

        locked.Sort((a, b) => UnityEngine.Random.Range(-1, 2));
        optionA = locked[0];
        optionB = locked.Count > 1 ? locked[1] : locked[0];

        // No mostrar ahora — guardar para el próximo turno de jugador
        rewardPending = true;
    }

    private void ShowReward()
    {
        if (rewardPanel == null) return;

        // Carta A
        if (imageA != null) imageA.sprite = optionA?.worldSprite;
        if (nameTextA != null) nameTextA.text = optionA?.cardName;

        // Carta B
        if (imageB != null) imageB.sprite = optionB?.worldSprite;
        if (nameTextB != null) nameTextB.text = optionB?.cardName;

        // Botones
        buttonA.onClick.RemoveAllListeners();
        buttonA.onClick.AddListener(() => Choose(optionA));

        buttonB.onClick.RemoveAllListeners();
        buttonB.onClick.AddListener(() => Choose(optionB));

        rewardPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Choose(CardData chosen)
    {
        Time.timeScale = 1f;
        if (rewardPanel != null) rewardPanel.SetActive(false);

        // Pasar a estado pendiente
        if (cardUnlockData != null)
            cardUnlockData.SetState(chosen, CardUnlockState.Pending);

        Debug.Log("Pasada a Pending: " + chosen.cardName);

        OnCardChosen?.Invoke(chosen);

        if (CardGameManager.Instance != null)
            CardGameManager.Instance.AddRewardCardToDeck(chosen);
    }
}