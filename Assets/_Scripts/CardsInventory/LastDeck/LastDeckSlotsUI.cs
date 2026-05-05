using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rellena los slots del "Last Deck" con los iconos de las cartas
/// de la última partida jugada.
///
/// SETUP:
///   - Adjunta este script al GameObject que contiene los 8 slots.
///   - Cada slot debe tener un componente Image (el icono) como hijo
///     o directamente en él.
///   - Asigna en el Inspector:
///       · slots         → los 8 GameObjects de los slots (en orden)
///       · availableCards → todas las CardData del juego
///       · emptyIcon     → sprite que se muestra cuando el slot está vacío (opcional)
/// </summary>
public class LastDeckSlotsUI : MonoBehaviour
{
    [Header("Slots (en orden, deben ser 8)")]
    [SerializeField] private List<GameObject> slots = new();

    [Header("Cartas disponibles para resolver nombres")]
    [SerializeField] private List<CardData> availableCards = new();

    [Header("Icono vacío (cuando no hay carta en ese slot)")]
    [SerializeField] private Sprite emptyIcon;

    [Header("Color cuando el slot está vacío")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color filledColor = new Color(1f, 1f, 1f, 1f);

    private void OnEnable() => Refresh();
    private void Start() => Refresh();

    // ─── API pública ──────────────────────────────────────────────

    public void Refresh()
    {
        if (LastDeckManager.Instance == null)
        {
            ClearAllSlots();
            return;
        }

        List<CardData> lastDeck = LastDeckManager.Instance.LoadLastDeck(availableCards);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            var img = GetSlotImage(slots[i]);
            if (img == null) continue;

            if (i < lastDeck.Count && lastDeck[i] != null)
            {
                // Usa deckSlotIcon si existe, si no worldSprite como fallback
                Sprite icon = lastDeck[i].deckSlotIcon != null
                    ? lastDeck[i].deckSlotIcon: lastDeck[i].icon;

                if (icon != null)
                {
                    img.sprite = icon;
                    img.color = filledColor;
                }
                else
                {
                    img.sprite = emptyIcon;
                    img.color = emptyColor;
                }
            }
            else
            {
                // Slot vacío
                img.sprite = emptyIcon;
                img.color = emptyColor;
            }
        }
    }

    public void ClearAllSlots()
    {
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            var img = GetSlotImage(slot);
            if (img == null) continue;
            img.sprite = emptyIcon;
            img.color = emptyColor;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private Image GetSlotImage(GameObject slot)
    {
        // Primero busca en el propio slot
        var img = slot.GetComponent<Image>();
        if (img != null) return img;

        // Si no, en el primer hijo
        return slot.GetComponentInChildren<Image>();
    }
}