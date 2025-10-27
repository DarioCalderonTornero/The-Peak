using UnityEngine;

public class CardsProve : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CardSlotsUI cardSlotsUI;
    [SerializeField] private CardData cardToAdd;
    [SerializeField] private CardReplacementController cardReplacementController;

    private void Update()
    {
        if (cardSlotsUI == null)
            return;

        // Añadir una carta con tecla A
        if (Input.GetKeyDown(KeyCode.A))
        {
            AddCard();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            cardReplacementController.StartReplacement();
        }

        // Eliminar una carta específica según número
        if (Input.GetKeyDown(KeyCode.Alpha1))
            RemoveCardAtIndex(0);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            RemoveCardAtIndex(1);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            RemoveCardAtIndex(2);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            RemoveCardAtIndex(3);
    }

    private void AddCard()
    {
        if (cardToAdd != null)
        {
            cardSlotsUI.AddCard(cardToAdd);
            Debug.Log($"Añadida carta: {cardToAdd.cardName}");
        }
        else
        {
            Debug.LogWarning("No se asignó una CardData para añadir.");
        }
    }

    private void RemoveCardAtIndex(int index)
    {
        if (index < 0 || index >= cardSlotsUI.GetSlotCount())
        {
            Debug.LogWarning($"No existe carta en el índice {index}");
            return;
        }

        cardSlotsUI.RemoveCardAt(index);
        Debug.Log($"Eliminada carta en índice {index}");
    }
}
