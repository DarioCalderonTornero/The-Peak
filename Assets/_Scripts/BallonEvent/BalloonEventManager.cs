using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BalloonEventManager : MonoBehaviour
{
    public static BalloonEventManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private GameObject balloonPrefab;
    [SerializeField] private RectTransform effectsCanvas;      // Canvas de efectos
    [SerializeField] private RectTransform pointsUITarget;     // Zona UI donde van los puntos
    [SerializeField] private int pointThreshold = 10;          // Puntos mínimos para activar
    [SerializeField] private float triggerChance = 0.3f;       // 30%
    [SerializeField] private int minPoints = 4;
    [SerializeField] private int maxPoints = 6;

    [Header("Animación globo")]
    [SerializeField] private float balloonSpeed = 300f;        // px/s cruzando pantalla
    [SerializeField] private float balloonY = 200f;            // altura del globo en canvas
    [SerializeField] private float fallDuration = 0.6f;        // tiempo de caída
    [SerializeField] private float explosionDuration = 0.3f;

    [Header("Puntos dispersados")]
    [SerializeField] private GameObject pointTokenPrefab;      // prefab del token de punto
    [SerializeField] private float dispersionRadius = 150f;
    [SerializeField] private float collectDelay = 0.4f;        // pausa antes de ir al target
    [SerializeField] private float collectDuration = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPlayerTurnStart += HandlePlayerTurnStart;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPlayerTurnStart -= HandlePlayerTurnStart;
    }

    private void HandlePlayerTurnStart()
    {
        if (PointsManager.Instance == null) return;
        if (PointsManager.Instance.GetCurrentPoints() > pointThreshold) return;
        if (Random.value > triggerChance) return;

        StartCoroutine(RunBalloonEvent());
    }

    private IEnumerator RunBalloonEvent()
    {
        // Instanciar globo fuera de pantalla
        GameObject balloon = Instantiate(balloonPrefab, effectsCanvas);
        RectTransform balloonRT = balloon.GetComponent<RectTransform>();

        float canvasWidth = effectsCanvas.rect.width;
        bool fromLeft = Random.value > 0.5f;

        float startX = fromLeft ? -canvasWidth / 2f - 100f : canvasWidth / 2f + 100f;
        float endX = fromLeft ? canvasWidth / 2f + 100f : -canvasWidth / 2f - 100f;

        balloonRT.anchoredPosition = new Vector2(startX, balloonY);

        // Escalar si va de derecha a izquierda
        if (!fromLeft)
            balloonRT.localScale = new Vector3(-1, 1, 1);

        // Hacer el globo clicable
        bool clicked = false;
        var btn = balloon.GetComponent<Button>();
        if (btn == null) btn = balloon.AddComponent<Button>();
        btn.onClick.AddListener(() => clicked = true);

        float distance = Mathf.Abs(endX - startX);
        float duration = distance / balloonSpeed;
        float t = 0f;
        float sineOffset = Random.Range(0f, Mathf.PI * 2f);

        while (t < duration && !clicked)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / duration);

            float x = Mathf.Lerp(startX, endX, n);
            float sineY = Mathf.Sin(t * 1.2f + sineOffset) * 30f
                        + Mathf.Sin(t * 2.8f + sineOffset) * 10f;

            balloonRT.anchoredPosition = new Vector2(x, balloonY + sineY);

            yield return null;
        }

        if (!clicked)
        {
            Destroy(balloon);
            yield break;
        }

        // ── CLIC: caída ──────────────────────────────────────────
        btn.interactable = false;
        Vector2 fallStart = balloonRT.anchoredPosition;
        float canvasBottom = -effectsCanvas.rect.height / 2f;
        Vector2 fallEnd = new Vector2(fallStart.x, canvasBottom);

        t = 0f;
        while (t < fallDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = t / fallDuration;
            float ease = n * n; // EaseIn
            balloonRT.anchoredPosition = Vector2.Lerp(fallStart, fallEnd, ease);
            yield return null;
        }

        Vector2 explosionPos = balloonRT.anchoredPosition;
        Destroy(balloon);

        // ── EXPLOSIÓN: soltar puntos ─────────────────────────────
        int pointCount = Random.Range(minPoints, maxPoints + 1);
        yield return StartCoroutine(SpawnPoints(explosionPos, pointCount));
    }

    private IEnumerator SpawnPoints(Vector2 origin, int count)
    {
        GameObject[] tokens = new GameObject[count];

        // Dispersar
        for (int i = 0; i < count; i++)
        {
            GameObject token = Instantiate(pointTokenPrefab, effectsCanvas);
            RectTransform rt = token.GetComponent<RectTransform>();
            rt.anchoredPosition = origin;
            tokens[i] = token;

            // Dirección aleatoria
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(50f, dispersionRadius);
            Vector2 target = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            StartCoroutine(MoveToken(rt, origin, target, 0.3f, null));
        }

        yield return new WaitForSecondsRealtime(collectDelay);

        // Recoger — todos van al target de puntos
        Vector2 pointsTargetPos = GetCanvasPosition(pointsUITarget);
        int collected = 0;

        for (int i = 0; i < count; i++)
        {
            if (tokens[i] == null) continue;
            RectTransform rt = tokens[i].GetComponent<RectTransform>();
            Vector2 from = rt.anchoredPosition;
            int captured = i;

            StartCoroutine(MoveToken(rt, from, pointsTargetPos, collectDuration, () =>
            {
                Destroy(tokens[captured]);
                PointsManager.Instance?.AddPoints(1);
                collected++;
            }));

            yield return new WaitForSecondsRealtime(0.08f); // pequeño delay entre cada uno
        }

        // Esperar a que todos lleguen
        float timeout = collectDuration + 2f;
        float elapsed = 0f;
        while (collected < count && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator MoveToken(RectTransform rt, Vector2 from, Vector2 to, float dur, System.Action onComplete)
    {
        float t = 0f;
        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f); // EaseOut
            rt.anchoredPosition = Vector2.Lerp(from, to, n);
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = to;
        onComplete?.Invoke();
    }

    private Vector2 GetCanvasPosition(RectTransform target)
    {
        // Convertir posición mundo del target a posición en el canvas de efectos
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            effectsCanvas, screenPoint, null, out Vector2 localPoint);
        return localPoint;
    }

    public void SpawnPointsFromCenter(int count)
    {
        StartCoroutine(SpawnPoints(Vector2.zero, count)); // centro del canvas
    }

    public void SpawnPointsFromWorldPosition(Vector3 worldPos, int count)
    {
        // Convertir posición 3D a posición en el canvas de efectos
        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam == null) { SpawnPointsFromCenter(count); return; }

        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            effectsCanvas, screenPos, null, out Vector2 canvasPos);

        StartCoroutine(SpawnPoints(canvasPos, count));
    }
}