using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardUnlockData", menuName = "TowerDefense/CardUnlockData")]
public class CardUnlockData : ScriptableObject
{
    [System.Serializable]
    public class CardEntry
    {
        public CardData card;
        public CardUnlockState defaultState = CardUnlockState.Locked;

        // [NonSerialized] asegura que el SO no guarde este valor en el disco del Editor
        [System.NonSerialized] public CardUnlockState currentState;
    }

    [SerializeField] private List<CardEntry> entries = new();

    /* private void OnEnable()
    {
        // Se carga automáticamente al entrar en memoria (al dar al Play)
        Load();
    }*/

    // Mantenemos el nombre "Load" para que CardInventoryUI no de error
    public void Load()
    {
        bool firstTimeInitialization = false;

        foreach (var e in entries)
        {
            if (e.card == null) continue;

            // Usamos cardName para evitar errores de nombres de archivo
            string key = $"CardUnlock_{e.card.cardName}";

            if (PlayerPrefs.HasKey(key))
            {
                e.currentState = (CardUnlockState)PlayerPrefs.GetInt(key);
            }
            else
            {
                // Si no existe, usamos el default y marcamos para guardar
                e.currentState = e.defaultState;
                PlayerPrefs.SetInt(key, (int)e.currentState);
                firstTimeInitialization = true;
            }
        }

        if (firstTimeInitialization)
        {
            PlayerPrefs.Save();
            Debug.Log("[CardUnlockData] Primera ejecución: Se han creado las llaves en PlayerPrefs.");
        }
    }

    // Mantenemos el nombre "Save" pero con la lógica de PlayerPrefs
    public void Save()
    {
        foreach (var e in entries)
        {
            if (e.card == null) continue;
            PlayerPrefs.SetInt($"CardUnlock_{e.card.name}", (int)e.currentState);
        }
        PlayerPrefs.Save();
        Debug.Log("[CardUnlockData] Datos guardados en PlayerPrefs.");
    }

    public void SetState(CardData card, CardUnlockState state)
    {
        var entry = entries.Find(e => e.card == card);
        if (entry != null)
        {
            entry.currentState = state;
            Save(); // Guarda inmediatamente cada vez que algo cambia
        }
    }

    public CardUnlockState GetState(CardData card)
    {
        var entry = entries.Find(e => e.card == card);
        return entry != null ? entry.currentState : CardUnlockState.Unlocked;
    }

    // --- MÉTODOS QUE FALTABAN Y CAUSABAN ERRORES ---

    public List<CardData> GetLockedCards()
    {
        var result = new List<CardData>();
        foreach (var e in entries)
        {
            if (e.currentState == CardUnlockState.Locked)
                result.Add(e.card);
        }
        return result;
    }

    public List<CardData> GetAllCards()
    {
        var result = new List<CardData>();
        foreach (var e in entries)
        {
            if (e.card != null)
                result.Add(e.card);
        }
        return result;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset All States")]
    public void ResetAll()
    {
        foreach (var e in entries)
        {
            if (e.card == null) continue;
            PlayerPrefs.DeleteKey($"CardUnlock_{e.card.name}");
            e.currentState = e.defaultState;
        }
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs borrados y estados reseteados.");
    }
#endif

    [ContextMenu("DEBUG: Log PlayerPrefs States")]
    public void LogPlayerPrefsStates()
    {
        Debug.Log("<color=cyan><b>=== REVISIÓN DE PLAYERPREFS (ESTADOS DE CARTAS) ===</b></color>");

        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("La lista de entradas está vacía.");
            return;
        }

        foreach (var e in entries)
        {
            if (e.card == null) continue;

            string key = $"CardUnlock_{e.card.name}";

            if (PlayerPrefs.HasKey(key))
            {
                int savedValue = PlayerPrefs.GetInt(key);
                // Convertimos el int a nuestro Enum para que sea legible
                CardUnlockState state = (CardUnlockState)savedValue;

                Debug.Log($"<b>[REGISTRO]</b> Carta: <color=yellow>{e.card.name}</color> | " +
                          $"Llave: <i>{key}</i> | " +
                          $"Valor: <color=green>{savedValue} ({state})</color>");
            }
            else
            {
                Debug.Log($"<b>[VACÍO]</b> Carta: {e.card.name} | " +
                          $"<color=grey>No existe llave en PlayerPrefs (usará por defecto: {e.defaultState})</color>");
            }
        }

        Debug.Log("<color=cyan><b>=================================================</b></color>");
    }
}