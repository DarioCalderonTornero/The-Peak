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

    private void Start() => TrySubscribe();
    private void OnEnable() => TrySubscribe();

    private void OnDisable()
    {
        if (subscribed && LevelExperienceManager.Instance != null)
        {
            LevelExperienceManager.Instance.OnCardRewardTriggered -= HandleCardRewardTriggered;
            subscribed = false;
        }
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

    private void HandleCardRewardTriggered()
    {
        if (CardGameManager.Instance == null) return;

        List<CardData> candidates = CardGameManager.Instance.GetRewardCandidates();
        if (candidates == null || candidates.Count == 0) return;

        optionA = candidates[0];
        optionB = candidates.Count > 1 ? candidates[1] : candidates[0];

        ShowReward();
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

        OnCardChosen?.Invoke(chosen);

        if (CardGameManager.Instance != null)
            CardGameManager.Instance.AddRewardCardToDeck(chosen);
    }
}