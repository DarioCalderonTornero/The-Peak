using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public class CardInfoButton : MonoBehaviour, IPointerClickHandler
{
    private DragCardUI parentCard;
    private RectTransform cardRect;
    private CardInfoPanel infoPanel;

    private void Awake()
    {
        parentCard = GetComponentInParent<DragCardUI>();
        cardRect = parentCard.GetComponent<RectTransform>();

        // Busca el panel ya existente en la escena
        infoPanel = FindObjectOfType<CardInfoPanel>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (parentCard == null || parentCard.cardData == null || infoPanel == null)
            return;

        infoPanel.ShowForCard(parentCard.cardData, cardRect);
    }
}
