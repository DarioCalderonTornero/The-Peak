using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RankingUI : MonoBehaviour
{
    [SerializeField] private GameObject rankingPanel;
    [SerializeField] private Transform entriesContainer;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button rankingButton;

    [Header("Sprites de posición")]
    [SerializeField] private Sprite spriteGold;
    [SerializeField] private Sprite spriteSilver;
    [SerializeField] private Sprite spriteBronze;
    [SerializeField] private Sprite spriteDefault;

    public static RankingUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (rankingPanel != null)
            rankingPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (clearButton != null)
            clearButton.onClick.AddListener(OnClear);

        if (rankingButton != null)
            rankingButton.onClick.AddListener(Show);
    }

    public void Show()
    {
        if (rankingPanel != null)
            rankingPanel.SetActive(true);
        RefreshEntries();
    }

    public void Hide()
    {
        if (rankingPanel != null)
            rankingPanel.SetActive(false);
    }

    private void RefreshEntries()
    {
        foreach (Transform child in entriesContainer)
            Destroy(child.gameObject);

        if (RankingManager.Instance == null) return;

        List<RankingEntry> entries = RankingManager.Instance.GetRankingSorted();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            GameObject row = Instantiate(entryPrefab, entriesContainer);

            SetText(row, "Position", $"{i + 1}");
            SetText(row, "Name", entry.playerName);
            SetText(row, "Rounds", $"Rondas: {entry.rounds}");
            SetText(row, "Kills", $"{entry.killCount}");

            var bg = row.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = i switch
                {
                    0 => spriteGold,
                    1 => spriteSilver,
                    2 => spriteBronze,
                    _ => spriteDefault
                };
            }
        }
    }

    private void SetText(GameObject row, string childName, string value)
    {
        var t = row.transform.Find(childName);
        if (t == null) return;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = value;
    }

    private void OnClear()
    {
        RankingManager.Instance?.ClearRanking();
        RefreshEntries();
    }
}