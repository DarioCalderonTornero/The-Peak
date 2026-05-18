using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RankingEntry
{
    public string playerName;
    public int rounds;
    public int killCount;
    public float playTime;
    public string date;
}

public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance { get; private set; }

    private const string KEY_COUNT = "RANKING_COUNT";
    private const string KEY_ENTRY = "RANKING_ENTRY_";
    private const int MAX_ENTRIES = 50;

    private string currentPlayerName = "???";
    private float sessionStartTime;

    [Header("Configuración")]
    [SerializeField] private bool rankingEnabled = true;
    public bool RankingEnabled => rankingEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPlayerName(string name)
    {
        currentPlayerName = string.IsNullOrWhiteSpace(name) ? "???" : name.Trim();
    }

    public void StartSession()
    {
        sessionStartTime = Time.time;
    }

    public void SaveCurrentSession(int rounds, int killCount)
    {
        float playTime = Time.time - sessionStartTime;

        var entry = new RankingEntry
        {
            playerName = currentPlayerName,
            rounds = rounds,
            killCount = killCount,
            playTime = playTime,
            date = DateTime.Now.ToString("HH:mm")
        };

        int count = PlayerPrefs.GetInt(KEY_COUNT, 0);
        string json = JsonUtility.ToJson(entry);
        Debug.Log($"[RankingManager] Guardando entrada #{count}: {json}");
        PlayerPrefs.SetString(KEY_ENTRY + count, json);
        PlayerPrefs.SetInt(KEY_COUNT, count + 1);
        PlayerPrefs.Save();
        Debug.Log($"[RankingManager] Guardado. Total entradas: {count + 1}");
    }

    public List<RankingEntry> GetRankingSorted()
    {
        int count = PlayerPrefs.GetInt(KEY_COUNT, 0);
        Debug.Log($"[RankingManager] Cargando ranking. Total entradas: {count}");
        var list = new List<RankingEntry>();

        for (int i = 0; i < count; i++)
        {
            string json = PlayerPrefs.GetString(KEY_ENTRY + i, "");
            if (!string.IsNullOrEmpty(json))
            {
                var entry = JsonUtility.FromJson<RankingEntry>(json);
                if (entry != null) list.Add(entry);
            }
        }

        // Ordenar por rondas de mayor a menor
        list.Sort((a, b) => b.rounds.CompareTo(a.rounds));
        return list;
    }

    public void ClearRanking()
    {
        int count = PlayerPrefs.GetInt(KEY_COUNT, 0);
        for (int i = 0; i < count; i++)
            PlayerPrefs.DeleteKey(KEY_ENTRY + i);
        PlayerPrefs.DeleteKey(KEY_COUNT);
        PlayerPrefs.Save();
    }
}