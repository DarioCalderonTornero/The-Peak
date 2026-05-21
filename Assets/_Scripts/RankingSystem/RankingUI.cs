using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RankingUI : MonoBehaviour
{
    [SerializeField] private GameObject rankingPanel;
    [SerializeField] private Transform entriesContainer;   // padre de las filas
    [SerializeField] private GameObject entryPrefab;       // prefab de una fila
    [SerializeField] private Button closeButton;
    [SerializeField] private Button clearButton;           // opcional
    [SerializeField] private Button rankingButton;
    [SerializeField] private RankingUI rankingUI;

    [Header("Sprites de posición")]
    [SerializeField] private Sprite spriteGold;    // 1º
    [SerializeField] private Sprite spriteSilver;  // 2º
    [SerializeField] private Sprite spriteBronze;  // 3º
    [SerializeField] private Sprite spriteDefault; // resto

    public static RankingUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (clearButton != null)
            clearButton.onClick.AddListener(OnClear);

        if (rankingPanel != null) rankingPanel.SetActive(false);

        if (rankingButton != null)
            rankingButton.onClick.AddListener(() => rankingUI?.Show());
    }

    public void Show()
    {
        if (rankingPanel != null) rankingPanel.SetActive(true);
        RefreshEntries();
    }

    public void Hide()
    {
        if (rankingPanel != null) rankingPanel.SetActive(false);
    }

    private void RefreshEntries()
    {
        // Limpiar filas anteriores
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

            // Asignar sprite de fondo según posición
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

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}