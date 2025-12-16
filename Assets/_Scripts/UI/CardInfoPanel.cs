using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardInfoPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.2f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Coroutine currentFade;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    public void ShowForCard(CardData cardData, RectTransform cardRect)
    {
        if (cardData == null || cardRect == null) return;

        titleText.text = cardData.cardName;
        descriptionText.text = cardData.description;

        PositionBesideCard(cardRect);

        gameObject.SetActive(true);

        if (currentFade != null)
            StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeCanvas(0f, 1f));
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (currentFade != null)
            StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeCanvas(1f, 0f));
    }

    private IEnumerator FadeCanvas(float from, float to)
    {
        if (canvasGroup == null)
            yield break;

        float t = 0f;
        canvasGroup.alpha = from;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, lerp);
            yield return null;
        }

        canvasGroup.alpha = to;

        if (Mathf.Approximately(to, 0f))
            gameObject.SetActive(false);
    }

    private void PositionBesideCard(RectTransform cardRect)
    {
        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

        Vector2 cardScreenPos = RectTransformUtility.WorldToScreenPoint(
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            cardRect.position
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            cardScreenPos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out Vector2 cardLocalPos
        );

        float offsetX = rectTransform.sizeDelta.x * 0.6f;
        bool cardOnRight = cardLocalPos.x > 0f;
        float x = cardLocalPos.x + (cardOnRight ? -offsetX : offsetX);
        float y = cardLocalPos.y;

        rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private void Update()
    {
        // Si el panel está activo y se hace click izquierdo en cualquier parte → cerrar con fade
        if (gameObject.activeSelf && Input.GetMouseButtonDown(0))
        {
            Hide();
        }
    }
}
