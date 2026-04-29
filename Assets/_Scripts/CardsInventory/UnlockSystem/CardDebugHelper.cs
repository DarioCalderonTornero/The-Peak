using UnityEngine;

public class CardDebugHelper : MonoBehaviour
{
    [Header("Referencia al SO")]
    [SerializeField] private CardUnlockData cardUnlockData;

    [Header("Controles")]
    [Tooltip("Muestra en consola el estado actual de PlayerPrefs")]
    public bool logNow;

    [Tooltip("Borra todos los datos de PlayerPrefs de las cartas")]
    public bool resetAllNow;

    private void Update()
    {
        // Esto permite ejecutar funciones haciendo clic en los booleanos del Inspector
        if (logNow)
        {
            logNow = false;
            LogPlayerPrefs();
        }

        if (resetAllNow)
        {
            resetAllNow = false;
            ResetAll();
        }
    }

    public void LogPlayerPrefs()
    {
        if (cardUnlockData == null) return;

        // FORZAMOS LA CARGA antes de leer para asegurar sincronía
        cardUnlockData.Load();

        Debug.Log("<color=cyan><b>=== REVISIÓN DE PLAYERPREFS ===</b></color>");
        var cards = cardUnlockData.GetAllCards();

        foreach (var card in cards)
        {
            if (card == null) continue;
            string key = $"CardUnlock_{card.cardName}";

            if (PlayerPrefs.HasKey(key))
            {
                int val = PlayerPrefs.GetInt(key);
                Debug.Log($"<b>[OK]</b> {card.cardName} -> Valor: {val} ({(CardUnlockState)val})");
            }
            else
            {
                Debug.Log($"<b>[ERROR]</b> {card.cardName} -> Sigue sin existir en PlayerPrefs.");
            }
        }
    }

    private void ResetAll()
    {
        if (cardUnlockData != null)
        {
            cardUnlockData.ResetAll();
            Debug.Log("<color=red>PlayerPrefs de cartas reseteados.</color>");
        }
    }
}