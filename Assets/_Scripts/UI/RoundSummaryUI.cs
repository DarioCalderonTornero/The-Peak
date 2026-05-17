using System.Collections;
using UnityEngine;
using TMPro;

public class RoundSummaryUI : MonoBehaviour
{
    public static RoundSummaryUI Instance { get; private set; }

    [Header("Configuración de Puntos e Hilos")]
    [SerializeField] private int pointsPerClimber = 5; //
    [SerializeField] private float dropDuration = 0.35f; //
    [SerializeField] private float fadeDuration = 0.4f; //

    [Header("Nuevos Ajustes del Inspector (Animación)")]
    [Tooltip("Tamaño gigante inicial con el que parten los números antes de impactar.")]
    [SerializeField] private float startScale = 4f;

    [Tooltip("Tiempo de espera en segundos entre la caída del primer número y el segundo.")]
    [SerializeField] private float delayBetweenTexts = 0.5f;

    [Tooltip("Tiempo que se queda la UI completamente estática y visible antes de empezar a desvanecerse.")]
    [SerializeField] private float visibleDuration = 2.0f;

    [Header("Textos de la UI")] //
    [SerializeField] private TextMeshProUGUI staticText; //
    [SerializeField] private TextMeshProUGUI killedText; //
    [SerializeField] private TextMeshProUGUI totalText; //
    [SerializeField] private GameObject summaryContainer; //

    private void Awake() //
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; } //
        Instance = this; //
    }

    private void Start() //
    {
        if (TurnManager.Instance != null) //
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart; //
    }

    private void OnDestroy() //
    {
        if (TurnManager.Instance != null) //
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart; //
    }

    private void HandlePlayerTurnStart() //
    {
        StartCoroutine(WaitAndShow()); //
    }

    private IEnumerator WaitAndShow()
    {
        // Espera a que terminen por completo todas las cinemáticas pendientes
        yield return new WaitUntil(() =>
            DeathCinematicManager.Instance != null && !DeathCinematicManager.Instance.IsProcessingDeaths()); //

        // Obtener las bajas reales registradas de esta ronda
        int deaths = 0;
        if (ClimberDeathPointsManager.Instance != null)
        {
            deaths = ClimberDeathPointsManager.Instance.GetRoundDeaths();
        }

        // Si no hay muertes, abortamos por completo sin mostrar nada
        if (deaths <= 0)
        {
            yield break;
        }

        int points = deaths * pointsPerClimber;

        if (killedText != null) killedText.text = deaths.ToString();
        if (totalText != null) totalText.text = $"{points}";

        // Ocultar y preparar componentes antes de lanzar las animaciones
        SetAlpha(staticText, 0f);
        SetAlpha(killedText, 0f);
        SetAlpha(totalText, 0f);
        if (killedText != null) killedText.transform.localScale = Vector3.zero;
        if (totalText != null) totalText.transform.localScale = Vector3.zero;

        // Activar el contenedor
        if (summaryContainer != null) summaryContainer.SetActive(true);

        // 1. Aparece el texto estático principal
        if (staticText != null) yield return StartCoroutine(FadeIn(staticText, 0.2f));

        // 2. Cae de golpe el primer número (Killed)
        if (killedText != null)
        {
            SetAlpha(killedText, 1f);
            yield return StartCoroutine(DropText(killedText));
        }

        yield return new WaitForSeconds(delayBetweenTexts);

        // 3. Cae de golpe el segundo número (Total)
        if (totalText != null)
        {
            SetAlpha(totalText, 1f);
            yield return StartCoroutine(DropText(totalText));
        }

        int totalPoints = deaths * pointsPerClimber;

        // ─── NUEVO CAMBIO: Lanzar los tokens visuales de puntos en la pantalla ───
        if (BalloonEventManager.Instance != null)
            BalloonEventManager.Instance.SpawnPointsFromCenter(totalPoints);
        else
            PointsManager.Instance?.AddPoints(deaths);

        yield return new WaitForSeconds(visibleDuration);

        // 4. Desvanecer todos los elementos en paralelo
        yield return StartCoroutine(FadeOutSummary());

        if (summaryContainer != null) summaryContainer.SetActive(false);

        // ─── NUEVO CAMBIO: Limpiamos los puntos de la ronda AQUÍ, justo tras ocultar el cartel ───
        if (ClimberDeathPointsManager.Instance != null)
        {
            ClimberDeathPointsManager.Instance.ResetRoundDeaths();
        }
    }

    private IEnumerator DropText(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        text.transform.localScale = Vector3.one * startScale;

        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / dropDuration);
            float eased = n * n; // EaseIn cuadrático — empieza lento, acaba rápido
            float scale = Mathf.Lerp(startScale, 1f, eased);
            text.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        text.transform.localScale = Vector3.one;
        yield return StartCoroutine(ShakeText(text));
    }

    private IEnumerator ShakeText(TextMeshProUGUI text)
    {
        if (text == null) yield break;

        RectTransform rt = text.GetComponent<RectTransform>();
        Vector2 originalPos = rt.anchoredPosition;

        float duration = 0.25f;
        float magnitude = 8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = Mathf.Lerp(magnitude, 0f, elapsed / duration); // decae
            float offsetX = UnityEngine.Random.Range(-1f, 1f) * strength;
            float offsetY = UnityEngine.Random.Range(-1f, 1f) * strength;
            rt.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);
            yield return null;
        }

        rt.anchoredPosition = originalPos;
    }

    private IEnumerator FadeIn(TextMeshProUGUI text, float dur) //
    {
        float elapsed = 0f; //
        while (elapsed < dur) //
        {
            elapsed += Time.deltaTime; //
            Color c = text.color; //
            c.a = Mathf.Clamp01(elapsed / dur); //
            text.color = c; //
            yield return null; //
        }
    }

    private IEnumerator FadeOutSummary()
    {
        Coroutine f1 = StartCoroutine(FadeOutText(staticText));
        Coroutine f2 = StartCoroutine(FadeOutText(killedText));
        Coroutine f3 = StartCoroutine(FadeOutText(totalText));
        yield return f1;
        yield return f2;
        yield return f3;
    }

    private IEnumerator FadeOutText(TextMeshProUGUI text) //
    {
        float elapsed = 0f; //
        while (elapsed < fadeDuration) //
        {
            elapsed += Time.deltaTime; //
            SetAlpha(text, Mathf.Lerp(1f, 0f, elapsed / fadeDuration)); //
            yield return null; //
        }
        SetAlpha(text, 0f); //
    }

    private void SetAlpha(TextMeshProUGUI text, float alpha) //
    {
        if (text != null) //
        {
            Color c = text.color; //
            c.a = alpha; //
            text.color = c; //
        }
    }

    private float EaseOutBack(float x) //
    {
        float c1 = 1.70158f; //
        float c3 = c1 + 1f; //
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); //
    }
}