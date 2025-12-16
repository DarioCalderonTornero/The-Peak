using System.Collections;
using UnityEngine;
using TMPro;

public class PlacementFeedbackUI : MonoBehaviour
{
    [Header("Referencia")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animación")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float displayDuration = 1.5f;
    [SerializeField] private float riseDistance = 1f;
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3 originalPosition;
    private Coroutine currentAnimation;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (feedbackText == null)
            feedbackText = GetComponent<TextMeshProUGUI>();

        originalPosition = transform.localPosition;
        canvasGroup.alpha = 0f;
    }

    public void ShowMessage(string message)
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(AnimateMessage(message));
    }

    private IEnumerator AnimateMessage(string message)
    {
        // CAMBIAR TEXTO PRIMERO (invisible)
        feedbackText.text = message;

        // Reset posición
        transform.localPosition = originalPosition;

        // 1. FADE IN (ya con texto correcto)
        yield return Fade(0f, 1f, fadeDuration);

        // 2. MOSTRAR + SUBIR
        float elapsed = 0f;
        Vector3 startPos = transform.localPosition;

        while (elapsed < displayDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / displayDuration;
            float riseAmount = riseCurve.Evaluate(t) * riseDistance;
            transform.localPosition = startPos + Vector3.up * riseAmount;
            yield return null;
        }

        // 3. FADE OUT
        yield return Fade(1f, 0f, fadeDuration);
    }

    private IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }
        canvasGroup.alpha = toAlpha;
    }
}
