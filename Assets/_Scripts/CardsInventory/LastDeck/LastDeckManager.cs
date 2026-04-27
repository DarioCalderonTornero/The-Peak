using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guarda y carga el último deck jugado usando PlayerPrefs.
/// Usa el cardName de cada CardData como identificador único.
/// </summary>
public class LastDeckManager : MonoBehaviour
{
    private const string PREFS_KEY = "LastDeck";
    private const char SEPARATOR = '|';

    public static LastDeckManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── GUARDAR ─────────────────────────────────────────────────

    /// <summary>
    /// Llama esto justo antes de empezar la partida, pasando las cartas seleccionadas.
    /// </summary>
    public void SaveDeck(List<CardData> deck)
    {
        if (deck == null || deck.Count == 0)
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            return;
        }

        var names = new List<string>(deck.Count);
        foreach (var card in deck)
            if (card != null) names.Add(card.cardName);

        PlayerPrefs.SetString(PREFS_KEY, string.Join(SEPARATOR.ToString(), names));
        PlayerPrefs.Save();
    }

    // ─── CARGAR ──────────────────────────────────────────────────

    /// <summary>
    /// Devuelve los nombres de las cartas del último deck.
    /// Devuelve lista vacía si no hay ninguno guardado.
    /// </summary>
    public List<string> LoadLastDeckNames()
    {
        string raw = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(raw)) return new List<string>();

        var parts = raw.Split(SEPARATOR);
        return new List<string>(parts);
    }

    /// <summary>
    /// Resuelve los nombres a CardData buscando en la lista de cartas disponibles.
    /// </summary>
    public List<CardData> LoadLastDeck(List<CardData> allCards)
    {
        var names = LoadLastDeckNames();
        var result = new List<CardData>(names.Count);

        foreach (var name in names)
        {
            var found = allCards.Find(c => c != null && c.cardName == name);
            if (found != null) result.Add(found);
        }

        return result;
    }

    public bool HasLastDeck() => PlayerPrefs.HasKey(PREFS_KEY);

    public void ClearLastDeck()
    {
        PlayerPrefs.DeleteKey(PREFS_KEY);
        PlayerPrefs.Save();
    }
}