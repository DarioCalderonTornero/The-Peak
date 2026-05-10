using System.Collections;
using UnityEngine;
using TMPro;

public class LockedCardMessage : MonoBehaviour
{
    public static LockedCardMessage Instance { get; private set; }

    [SerializeField] private GameObject messagePrefab;     // prefab con TextMeshProUGUI
    [SerializeField] private RectTransform spawnParent;    // canvas donde se instancia
    [SerializeField] private Vector2 spawnPosition = new Vector2(0f, -400f); // parte inferior
    [SerializeField] private Vector2 stopPosition = new Vector2(0f, -200f); // donde se para
    [SerializeField] private float riseDuration = 0.4f;
    [SerializeField] private float holdDuration = 1.2f;
    [SerializeField] private float fadeDuration = 0.4f;

    [SerializeField] private float riseAmount = 80f;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show(string message, Vector3 worldCenter)
    {
        StartCoroutine(ShowRoutine(message, worldCenter));
    }

    private IEnumerator ShowRoutine(string message, Vector3 worldCenter)
    {
        GameObject obj = Instantiate(messagePrefab, spawnParent);
        RectTransform rt = obj.GetComponent<RectTransform>();
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();

        Vector2 cardScreenPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            spawnParent,
            RectTransformUtility.WorldToScreenPoint(null, worldCenter),
            null,
            out cardScreenPos
        );

        Vector2 startPos = new Vector2(cardScreenPos.x, cardScreenPos.y);
        Vector2 stopPos = startPos + new Vector2(0f, riseAmount);

        rt.anchoredPosition = startPos;
        text.text = message;
        text.alpha = 1f;

        float t = 0f;
        while (t < riseDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / riseDuration), 3f);
            rt.anchoredPosition = Vector2.Lerp(startPos, stopPos, n);
            yield return null;
        }
        rt.anchoredPosition = stopPos;

        yield return new WaitForSecondsRealtime(holdDuration);

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            text.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }

        Destroy(obj);
    }
}