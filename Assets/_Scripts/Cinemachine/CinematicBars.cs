using System.Collections;
using UnityEngine;

public class CinematicBars : MonoBehaviour
{
    public static CinematicBars Instance { get; private set; }

    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform bottomBar;

    [SerializeField] private float barHeight = 200;
    [SerializeField] private float animationTime = 0.5f;

    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        topBar.anchoredPosition = new Vector2(0, barHeight);
        bottomBar.anchoredPosition = new Vector2(0, -barHeight);
    }

    public void ShowBars()
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateBars(0f));
    }

    public void HideBars()
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateBars(barHeight));
    }

    private IEnumerator AnimateBars(float targetY)
    {
        Vector2 topStart = topBar.anchoredPosition;
        Vector2 bottomStart = bottomBar.anchoredPosition;

        Vector2 topTarget = new Vector2(0, targetY);
        Vector2 bottomTarget = new Vector2(0, -targetY);

        float elapsedTime = 0f;

        while (elapsedTime < animationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / animationTime);

            topBar.anchoredPosition = Vector2.Lerp(topStart, topTarget, t);
            bottomBar.anchoredPosition = Vector2.Lerp(bottomStart, bottomTarget, t);

            yield return null;
        }

        topBar.anchoredPosition = topTarget;
        bottomBar.anchoredPosition = bottomTarget;
    }
}