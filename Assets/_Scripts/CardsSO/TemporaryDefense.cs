using System.Collections;
using UnityEngine;
using TMPro;

public class TemporaryDefense : MonoBehaviour
{
    private int turnsRemaining;
    private Camera mainCamera;
    private TemporaryDefenseConfig config;
    private GameObject indicatorInstance;

    public int GetTurnsRemaining() => turnsRemaining;

    public void Initialize(int turns)
    {
        Debug.Log($"[TemporaryDefense] Initialize llamado con turns={turns}");

        turnsRemaining = turns;
        mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        config = Resources.Load<TemporaryDefenseConfig>("TemporaryDefenseConfig");

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnEnd += OnClimberTurnEnd;
            TurnManager.Instance.OnPlayerTurnStart += OnPlayerTurnStart;
        }
        else
            Debug.LogWarning("[TemporaryDefense] TurnManager no encontrado.");
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnEnd -= OnClimberTurnEnd;
            TurnManager.Instance.OnPlayerTurnStart -= OnPlayerTurnStart;
        }
        if (indicatorInstance != null)
            Destroy(indicatorInstance);
    }

    private void OnClimberTurnEnd()
    {
        turnsRemaining--;
        Debug.Log($"[TemporaryDefense] {gameObject.name} — turnos restantes: {turnsRemaining}");

        if (turnsRemaining <= 0)
        {
            var cloud = GetComponent<CloudKillDefense>();
            if (cloud != null)
                StartCoroutine(cloud.DisappearAndDestroy());
            else
                Destroy(gameObject);
        }
    }

    private void OnPlayerTurnStart()
    {
        if (this == null || gameObject == null) return;
        StartCoroutine(ShowTurnsIndicator());
    }

    private IEnumerator ShowTurnsIndicator()
    {
        float floatHeight = config != null ? config.floatHeight : 2f;
        float showDuration = config != null ? config.showDuration : 1.5f;
        float fadeDuration = config != null ? config.fadeDuration : 0.4f;
        float numberScale = config != null ? config.numberScale : 0.015f;
        Color numberColor = config != null ? config.numberColor : Color.white;

        int previousTurns = turnsRemaining + 1;

        GameObject canvasGO = new GameObject("TurnsIndicator");
        canvasGO.transform.position = transform.position + Vector3.up * floatHeight;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = mainCamera;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(200f, 100f);
        canvasGO.transform.localScale = Vector3.one * numberScale;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.fontSize = 80f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = numberColor;
        if (config != null && config.numberFont != null)
            text.font = config.numberFont;

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        indicatorInstance = canvasGO;

        Vector3 startPos = canvasGO.transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.5f;
        float elapsed = 0f;

        // Fade in con número anterior
        text.text = previousTurns.ToString();
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / fadeDuration);
            text.alpha = n;
            canvasGO.transform.position = Vector3.Lerp(startPos, endPos, n);
            BillboardToCamera(canvasGO.transform);
            yield return null;
        }

        // Esperar 0.5s y cambiar al número actual
        yield return new WaitForSeconds(0.5f);
        text.text = turnsRemaining.ToString();

        // Hold
        elapsed = 0f;
        while (elapsed < showDuration)
        {
            elapsed += Time.deltaTime;
            BillboardToCamera(canvasGO.transform);
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / fadeDuration);
            text.alpha = 1f - n;
            BillboardToCamera(canvasGO.transform);
            yield return null;
        }

        Destroy(canvasGO);
        indicatorInstance = null;
    }

    private void BillboardToCamera(Transform t)
    {
        if (mainCamera == null) return;
        t.LookAt(t.position + mainCamera.transform.rotation * Vector3.forward,
                 mainCamera.transform.rotation * Vector3.up);
    }
}