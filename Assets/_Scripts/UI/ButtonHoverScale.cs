using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float duration = 0.15f;
    [SerializeField] private Ease easeIn = Ease.OutBack;
    [SerializeField] private Ease easeOut = Ease.OutQuad;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(originalScale * hoverScale, duration).SetEase(easeIn);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(originalScale, duration).SetEase(easeOut);
    }
}