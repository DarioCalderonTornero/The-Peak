using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rellena el reverso de la carta con los datos del CardData.
/// Adjuntar al GameObject "CardBack" (el hijo del reverso).
/// </summary>
public class CardBackSetup : MonoBehaviour
{
    [Header("Referencias UI del reverso")]
    [SerializeField] private Image backgroundImage;          // fondo del reverso
    // [SerializeField] private Image illustrationImage;        // icono/ilustración grande
    // [SerializeField] private TextMeshProUGUI titleText;      // título (nombre carta)
    // [SerializeField] private TextMeshProUGUI typeText;       // "🏔 PERMANENTE · COSTE 3"
    // [SerializeField] private TextMeshProUGUI loreText;       // frase de lore
    // [SerializeField] private TextMeshProUGUI descriptionText;// descripción extendida

    [Header("Counter (opcional)")]
    [SerializeField] private GameObject counterGroup;        // contenedor del counter
    [SerializeField] private Image counterIconImage;
    [SerializeField] private TextMeshProUGUI counterNameText;

    /// <summary>
    /// Llama a esto cuando quieras configurar el reverso con los datos de una carta.
    /// </summary>
    public void Setup(CardData data)
    {
        if (data == null)
        {
            Debug.LogWarning("CardBackSetup: CardData es null");
            return;
        }

        // ── Fondo ──
        if (backgroundImage != null && data.backBackground != null)
            backgroundImage.sprite = data.backBackground;

        // ── Ilustración grande ──
        /* if (illustrationImage != null)
        {
            if (data.backIllustration != null)
            {
                illustrationImage.sprite = data.backIllustration;
                illustrationImage.gameObject.SetActive(true);
            }
            else if (data.icon != null)
            {
                // Fallback: usa el icono del frente si no hay ilustración específica
                illustrationImage.sprite = data.icon;
                illustrationImage.gameObject.SetActive(true);
            }
            else
            {
                illustrationImage.gameObject.SetActive(false);
            }
        }*/

        // ── Título (nombre carta) ──
        /*if (titleText != null)
            titleText.text = data.cardName.ToUpper();

        // ── Tipo + coste ──
        if (typeText != null)
        {
            string typeLabel = data.cardType switch
            {
                CardData.CardType.Permanente => "PERMANENTE",
                CardData.CardType.Temporal => "TEMPORAL",
                CardData.CardType.Eventual => "EVENTUAL · GLOBAL",
                _ => ""
            };

            // Si es eventual no mostramos coste (aparece random)
            if (data.cardType == CardData.CardType.Eventual)
                typeText.text = typeLabel;
            else
                typeText.text = $"{typeLabel} · COSTE {data.cost}";
        }

        // ── Lore ──
        if (loreText != null)
        {
            if (!string.IsNullOrEmpty(data.loreText))
            {
                loreText.text = $"\"{data.loreText}\"";
                loreText.gameObject.SetActive(true);
            }
            else
            {
                loreText.gameObject.SetActive(false);
            }
        }

        // ── Descripción extendida ──
        if (descriptionText != null)
        {
            // Usa extendedDescription si existe, si no la normal
            string desc = !string.IsNullOrEmpty(data.extendedDescription)
                ? data.extendedDescription
                : data.description;

            descriptionText.text = desc;
        }*/

        // ── Counter ──
        bool hasCounter = data.counterIcon != null || !string.IsNullOrEmpty(data.counterName);

        if (counterGroup != null)
            counterGroup.SetActive(hasCounter);

        if (hasCounter)
        {
            if (counterIconImage != null && data.counterIcon != null)
            {
                counterIconImage.sprite = data.counterIcon;
                counterIconImage.gameObject.SetActive(true);
            }

            if (counterNameText != null)
                counterNameText.text = data.counterName;
        }

        var playButton = GetComponentInChildren<CardPlayButton>(true);
        if (playButton != null)
            playButton.SetClip(data.explanationVideo);
    }
}