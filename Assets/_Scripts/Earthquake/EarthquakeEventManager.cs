using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class EarthquakeEventManager : MonoBehaviour
{
    public static EarthquakeEventManager Instance { get; private set; }

    [Header("Referencias UI")]
    [SerializeField] private RectTransform effectsCanvas;
    [SerializeField] private GameObject cardPrefab;         // prefab de carta con flip
    [SerializeField] private Canvas mainCanvas;

    [Header("Texto CATÁSTROFE")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color catastropheColor = new Color(1f, 0.3f, 0.1f);
    [SerializeField] private float textSpeed = 1800f;       // px/s

    [Header("Carta")]
    [SerializeField] private Sprite cardFrontSprite;        // sprite frontal del terremoto
    [SerializeField] private Sprite cardBackSprite;         // reverso de la carta
    [SerializeField] private float cardFloatAmplitude = 8f;
    [SerializeField] private float cardFloatSpeed = 1.5f;
    [SerializeField] private float hoverScaleMultiplier = 1.08f;

    [Header("Descripción")]
    [SerializeField] private string earthquakeDescription = "Un terremoto sacudirá el terreno.\nDestroza el 20% de escaladores y defensas.";
    [SerializeField] private int turnsUntilActivation = 3;

    [Header("Audio")]
    [SerializeField] private AudioClip warningSound;
    [SerializeField] private AudioClip revealSound;

    [Header("Countdown UI")]
    [SerializeField] private GameObject countdownWidget;    // widget esquina superior
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Image countdownCardIcon;

    private int currentTurns;
    private bool eventActive = false;
    private Coroutine floatRoutine;

    [SerializeField] private Sprite newCardSprite;

    [Header("Probabilidad y cooldown")]
    [SerializeField] private float triggerChance = 0.1f;    // 10%
    [SerializeField] private int cooldownTurns = 2;         // turnos de cooldown tras activar

    private int cooldownRemaining = 0;

    [Header("Probabilidad destrucción")]
    [SerializeField] private float climberDestroyChance = 0.2f;  // 20%
    [SerializeField] private float defenseDestroyChance = 0.2f;  // 20%

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (countdownWidget != null) countdownWidget.SetActive(false);

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
        // En cooldown → decrementar y salir
        if (cooldownRemaining > 0)
        {
            cooldownRemaining--;
            return;
        }

        // Ya hay evento activo → no lanzar otro
        if (eventActive) return;

        // Probabilidad 10%
        if (Random.value > triggerChance) return;

        TriggerEvent();
    }

    // ── Llamar esto para iniciar el evento (manualmente o por código) ──
    [ContextMenu("Trigger Earthquake Event")]
    public void TriggerEvent()
    {
        if (eventActive) return;
        eventActive = true;
        StartCoroutine(RevealSequence());
    }

    // ═══════════════════════════════════════════════════
    //  FASE 1 — REVELACIÓN
    // ═══════════════════════════════════════════════════

    private IEnumerator RevealSequence()
    {
        // 1. Sonido de aviso + shake
        if (warningSound != null)
            Temporal_Sound_Music.Instance?.Play2DSound(warningSound, 1f);
        CameraShake.Instance?.ShakeMainCamera(5f, 6f, 0.4f);
        yield return new WaitForSeconds(0.5f);

        // 2. Texto CATÁSTROFE cruza pantalla dos veces
        yield return StartCoroutine(ShowCatastropheText());
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(ShowCatastropheText());
        yield return new WaitForSeconds(0.5f);

        // 3. Aparecer carta con sprite "New", sin flip habilitado
        GameObject card = SpawnCard();

        // Asignar sprite "New" al FrontImage
        if (newCardSprite != null)
        {
            var frontImg = card.transform.Find("FrontCard/FrontImage")?.GetComponent<Image>();
            if (frontImg != null) frontImg.sprite = newCardSprite;
        }

        // Desactivar CardFlip para que no se pueda girar
        var flipComp = card.GetComponent<CardFlip>();
        if (flipComp != null) flipComp.enabled = false;

        yield return StartCoroutine(AnimateCardAppear(card));
        SetupCardHover(card);

        // 4. Esperar click izquierdo en la carta → flip a sprite frontal
        bool clickedOnce = false;
        var btn = card.GetComponent<Button>();
        if (btn == null) btn = card.AddComponent<Button>();
        btn.onClick.AddListener(() => clickedOnce = true);
        yield return new WaitUntil(() => clickedOnce);
        btn.onClick.RemoveAllListeners();

        // Flip para revelar sprite frontal
        yield return StartCoroutine(FlipToFront(card));

        if (revealSound != null)
            Temporal_Sound_Music.Instance?.Play2DSound(revealSound, 1f);

        // Escalar carta grande
        yield return StartCoroutine(ScaleTo(card.transform, Vector3.one * 1.6f, 0.3f));

        // Mostrar descripción
        GameObject descObj = CreateDescriptionText();

        // 5. Esperar click en cualquier parte
        yield return null;
        yield return new WaitUntil(() => Input.GetMouseButtonDown(0));

        // Fade out descripción
        yield return StartCoroutine(FadeOutDescription(descObj));
        Destroy(descObj);

        // Escalar carta de vuelta a normal
        yield return StartCoroutine(ScaleTo(card.transform, Vector3.one * 1.5f, 0.2f));

        // Parar flotación
        if (floatRoutine != null) StopCoroutine(floatRoutine);

        // 6. Carta vuela directamente a esquina sin flip
        yield return StartCoroutine(FlyToCountdown(card));
        Destroy(card);

        StartCountdown();
    }


    private GameObject CreateDescriptionText()
    {
        GameObject obj = new GameObject("Description");
        obj.transform.SetParent(effectsCanvas, false);

        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -280f);
        rt.sizeDelta = new Vector2(700f, 200f);

        var text = obj.AddComponent<TextMeshProUGUI>();
        text.text = earthquakeDescription;
        text.fontSize = 36f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 1f, 1f, 0f);
        if (font != null) text.font = font;

        StartCoroutine(FadeInText(text, 0.3f));
        return obj;
    }

    private IEnumerator WaitForAnyClick(System.Action onClick)
    {
        yield return null; // ignorar el frame actual
        yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
        onClick?.Invoke();
    }

    private IEnumerator FadeInText(TextMeshProUGUI text, float dur)
    {
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            Color c = text.color;
            c.a = Mathf.Clamp01(elapsed / dur);
            text.color = c;
            yield return null;
        }
    }

    private IEnumerator FadeOutDescription(GameObject obj)
    {
        var text = obj.GetComponent<TextMeshProUGUI>();
        if (text == null) yield break;
        float elapsed = 0f;
        float dur = 0.3f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            Color c = text.color;
            c.a = Mathf.Lerp(1f, 0f, elapsed / dur);
            text.color = c;
            yield return null;
        }
    }

    private IEnumerator ScaleTo(Transform t, Vector3 to, float dur)
    {
        Vector3 from = t.localScale;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / dur), 3f);
            t.localScale = Vector3.Lerp(from, to, n);
            yield return null;
        }
        t.localScale = to;
    }

    // ── Texto CATÁSTROFE cruzando pantalla ──
    private IEnumerator ShowCatastropheText()
    {
        GameObject obj = new GameObject("CatastropheText");
        obj.transform.SetParent(effectsCanvas, false);

        var rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1200f, 150f);

        var text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;

        text.text = "catastrofe en camino";
        text.fontSize = 90f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = catastropheColor;
        text.outlineWidth = 0.3f;
        text.outlineColor = Color.black;

        var shadow = obj.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(4f, -4f);

        float canvasWidth = effectsCanvas.rect.width;
        float startX = -canvasWidth / 2f - 700f;
        float endX = canvasWidth / 2f + 700f;
        rt.anchoredPosition = new Vector2(startX, 0f);

        float distance = endX - startX;
        float duration = distance / textSpeed;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, n), 0f);

            // Pequeño shake vertical
            rt.anchoredPosition += new Vector2(0f, Mathf.Sin(t * 40f) * 3f);
            yield return null;
        }

        Destroy(obj);
    }

    // ── Spawn carta centrada ──
    private GameObject SpawnCard()
    {
        GameObject card = Instantiate(cardPrefab, effectsCanvas);

        RectTransform rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        card.transform.localScale = Vector3.zero;

        var dragUI = card.GetComponent<DragCardUI>();
        if (dragUI != null) dragUI.enabled = false;

        var flipComp = card.GetComponent<CardFlip>();
        if (flipComp != null) flipComp.enabled = false;

        // Sprite frontal
        var frontImg = card.transform.Find("FrontCard/FrontImage")?.GetComponent<Image>();
        if (frontImg != null && cardFrontSprite != null)
            frontImg.sprite = cardFrontSprite;

        // Ocultar todo en FrontCard excepto FrontImage
        var frontCard = card.transform.Find("FrontCard");
        if (frontCard != null)
        {
            foreach (Transform child in frontCard)
            {
                if (child.name != "FrontImage")
                    child.gameObject.SetActive(false);
            }
        }

        // Ocultar todo en BackCard excepto BackImage
        var backCard = card.transform.Find("BackCard");
        if (backCard != null)
        {
            foreach (Transform child in backCard)
            {
                if (child.name != "BackImage")
                    child.gameObject.SetActive(false);
            }
        }

        // Sprite trasero
        var backImg = card.transform.Find("BackCard/BackImage")?.GetComponent<Image>();
        if (backImg != null && cardBackSprite != null)
            backImg.sprite = cardBackSprite;

        // Ocultar Glow y otros elementos raíz
        var glow = card.transform.Find("Glow");
        if (glow != null) glow.gameObject.SetActive(false);

        var star = card.transform.Find("Star");
        if (star != null) star.gameObject.SetActive(false);

        var pending = card.transform.Find("PendingCard");
        if (pending != null) pending.gameObject.SetActive(false);

        return card;
    }

    // ── Carta aparece con pop ──
    private IEnumerator AnimateCardAppear(GameObject card)
    {
        float dur = 0.4f;
        float elapsed = 0f;
        Vector3 targetScale = Vector3.one * 1.5f; // ← escala 1.5
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / dur), 3f);
            card.transform.localScale = targetScale * n;
            yield return null;
        }
        card.transform.localScale = targetScale;

        floatRoutine = StartCoroutine(FloatCard(card));
    }

    // ── Flotación suave ──
    private IEnumerator FloatCard(GameObject card)
    {
        var rt = card.GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;
        float t = 0f;
        while (card != null)
        {
            t += Time.deltaTime * cardFloatSpeed;
            rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(t) * cardFloatAmplitude);
            yield return null;
        }
    }

    // ── Hover con glow ──
    private void SetupCardHover(GameObject card)
    {
        var trigger = card.GetComponent<EventTrigger>();
        if (trigger == null) trigger = card.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        Vector3 baseScale = Vector3.one * 1.5f;
        Vector3 hoverScale = Vector3.one * 1.5f * hoverScaleMultiplier;

        var glowTransform = card.transform.Find("Glow");
        Image glowImg = glowTransform?.GetComponent<Image>();
        if (glowImg != null) { var c = glowImg.color; c.a = 0f; glowImg.color = c; }

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) =>
        {
            StopCoroutine(nameof(LerpScale));
            StartCoroutine(LerpScale(card.transform, hoverScale, 0.1f));
            if (glowImg != null) StartCoroutine(FadeGlow(glowImg, 1f));
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) =>
        {
            StopCoroutine(nameof(LerpScale));
            StartCoroutine(LerpScale(card.transform, baseScale, 0.1f));
            if (glowImg != null) StartCoroutine(FadeGlow(glowImg, 0f));
        });
        trigger.triggers.Add(exit);
    }

    // ── Revelar descripción (carta grande) ──
    private IEnumerator RevealDescription(GameObject card)
    {
        if (floatRoutine != null) StopCoroutine(floatRoutine);

        var rt = card.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;

        // Escalar grande
        float dur = 0.3f;
        float elapsed = 0f;
        Vector3 bigScale = Vector3.one * 1.6f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / dur), 3f);
            card.transform.localScale = Vector3.Lerp(Vector3.one, bigScale, n);
            yield return null;
        }

        // Mostrar descripción encima de la carta
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(effectsCanvas, false);
        var descRT = descObj.AddComponent<RectTransform>();
        descRT.anchoredPosition = new Vector2(0f, -200f);
        descRT.sizeDelta = new Vector2(700f, 200f);

        var descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = earthquakeDescription;
        descText.fontSize = 36f;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = Color.white;
        if (font != null) descText.font = font;

        // Fade in descripción
        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            Color c = descText.color;
            c.a = Mathf.Clamp01(elapsed / 0.3f);
            descText.color = c;
            yield return null;
        }

        // Destruir descripción al segundo click (se hace en RevealSequence)
        // Guardar referencia para destruirla
        yield return new WaitForSeconds(0.1f);
        Destroy(descObj);
    }

    private IEnumerator FlipToFront(GameObject card)
    {
        var frontImg = card.transform.Find("FrontCard/FrontImage")?.GetComponent<Image>();

        // Rotar a 90°
        float elapsed = 0f;
        float halfFlip = 0.15f;
        while (elapsed < halfFlip)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / halfFlip);
            card.transform.localRotation = Quaternion.Euler(0f, n * 90f, 0f);
            yield return null;
        }

        // Cambiar sprite a frontal
        if (frontImg != null && cardFrontSprite != null)
            frontImg.sprite = cardFrontSprite;

        // Rotar de vuelta a 0°
        elapsed = 0f;
        while (elapsed < halfFlip)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / halfFlip);
            card.transform.localRotation = Quaternion.Euler(0f, 90f - n * 90f, 0f);
            yield return null;
        }

        card.transform.localRotation = Quaternion.identity;
    }

    // ── Flip a reverso con turnos ──
    private IEnumerator FlipToBack(GameObject card)
    {
        // Volver a escala normal
        float dur = 0.2f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / dur);
            card.transform.localScale = Vector3.Lerp(card.transform.localScale, Vector3.one, n);
            yield return null;
        }

        // Flip: rotar Y de 0 a 90, cambiar sprite, rotar de 90 a 0
        var img = card.GetComponent<Image>();
        elapsed = 0f;
        float halfFlip = 0.2f;
        while (elapsed < halfFlip)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / halfFlip);
            card.transform.localRotation = Quaternion.Euler(0f, n * 90f, 0f);
            yield return null;
        }

        if (img != null && cardBackSprite != null)
            img.sprite = cardBackSprite;

        // Mostrar turnos en la carta
        GameObject turnsObj = new GameObject("TurnsOnCard");
        turnsObj.transform.SetParent(card.transform, false);
        var turnsRT = turnsObj.AddComponent<RectTransform>();
        turnsRT.anchoredPosition = Vector2.zero;
        turnsRT.sizeDelta = new Vector2(300f, 100f);
        var turnsText = turnsObj.AddComponent<TextMeshProUGUI>();
        turnsText.text = $"{turnsUntilActivation} turnos";
        turnsText.fontSize = 48f;
        turnsText.fontStyle = FontStyles.Bold;
        turnsText.alignment = TextAlignmentOptions.Center;
        turnsText.color = Color.white;
        if (font != null) turnsText.font = font;

        elapsed = 0f;
        while (elapsed < halfFlip)
        {
            elapsed += Time.deltaTime;
            float n = Mathf.Clamp01(elapsed / halfFlip);
            card.transform.localRotation = Quaternion.Euler(0f, 90f - n * 90f, 0f);
            yield return null;
        }
        card.transform.localRotation = Quaternion.identity;
    }

    // ── Carta vuela a la esquina ──
    private IEnumerator FlyToCountdown(GameObject card)
    {
        var rt = card.GetComponent<RectTransform>();
        Vector2 from = rt.anchoredPosition;
        // Esquina superior derecha aproximada
        Vector2 to = new Vector2(effectsCanvas.rect.width / 2f - 100f,
                                  effectsCanvas.rect.height / 2f - 80f);

        float dur = 0.5f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / dur), 3f);
            rt.anchoredPosition = Vector2.Lerp(from, to, n);
            card.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.3f, n);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════
    //  FASE 2 — COUNTDOWN
    // ═══════════════════════════════════════════════════

    private IEnumerator ShowWarningText(string message)
    {
        GameObject obj = new GameObject("WarningText");
        obj.transform.SetParent(effectsCanvas, false);

        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 120f);
        rt.anchoredPosition = new Vector2(0f, 150f);

        var text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;

        text.text = message;
        text.fontSize = 60f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(catastropheColor.r, catastropheColor.g, catastropheColor.b, 0f);
        text.outlineWidth = 0.3f;
        text.outlineColor = Color.black;

        // Fade in
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            Color c = text.color;
            c.a = Mathf.Clamp01(elapsed / 0.3f);
            text.color = c;
            yield return null;
        }

        yield return new WaitForSeconds(2f);

        // Fade out
        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            Color c = text.color;
            c.a = Mathf.Lerp(1f, 0f, elapsed / 0.3f);
            text.color = c;
            yield return null;
        }

        Destroy(obj);
    }

    private void StartCountdown()
    {
        currentTurns = turnsUntilActivation;

        if (countdownWidget != null) countdownWidget.SetActive(true);
        if (countdownText != null) countdownText.text = currentTurns.ToString();
        if (countdownCardIcon != null && cardFrontSprite != null)
            countdownCardIcon.sprite = cardFrontSprite;

        TurnManager.Instance.OnClimberTurnEnd += OnClimberTurnEnd;
    }

    private void OnClimberTurnEnd()
    {
        currentTurns--;
        if (countdownText != null) countdownText.text = currentTurns.ToString();

        if (currentTurns == 2)
        {
            CameraShake.Instance?.ShakeMainCamera(3f, 4f, 0.3f);
            if (warningSound != null)
                Temporal_Sound_Music.Instance?.Play2DSound(warningSound, 1f);
            StartCoroutine(ShowWarningText("TERREMOTO INMINENTE"));
        }

        if (currentTurns <= 0)
        {
            TurnManager.Instance.OnClimberTurnEnd -= OnClimberTurnEnd;
            if (countdownWidget != null) countdownWidget.SetActive(false);
            StartCoroutine(WaitForCinematicsAndActivate());
        }
    }

    private IEnumerator WaitForCinematicsAndActivate()
    {
        // Esperar a que terminen todas las cinemáticas de muerte
        yield return new WaitUntil(() =>
            DeathCinematicManager.Instance == null ||
            !DeathCinematicManager.Instance.IsProcessingDeaths()
        );

        yield return null;
        yield return null;

        yield return StartCoroutine(ActivateEarthquake());
    }

    // ═══════════════════════════════════════════════════
    //  FASE 3 — ACTIVACIÓN
    // ═══════════════════════════════════════════════════

    private IEnumerator ActivateEarthquake()
    {
        yield return StartCoroutine(ShowAndBurnCard());

        CameraShake.Instance?.ShakeMainCamera(8f, 10f, 1f);
        if (warningSound != null)
            Temporal_Sound_Music.Instance?.Play2DSound(warningSound, 1f);

        yield return new WaitForSeconds(1f);

        var climbers = Object.FindObjectsOfType<ClimberMovement>();
        foreach (var c in climbers)
            if (c != null && Random.value < climberDestroyChance) Destroy(c.gameObject);

        var defenses = Object.FindObjectsOfType<BaseDefense>();
        foreach (var d in defenses)
            if (d != null && Random.value < defenseDestroyChance) Destroy(d.gameObject);

        eventActive = false;

        // Iniciar cooldown — no podrá triggear hasta que pasen cooldownTurns turnos
        cooldownRemaining = cooldownTurns;
    }

    private IEnumerator ShowAndBurnCard()
    {
        // Instanciar carta grande en centro
        GameObject card = SpawnCard();
        var rt = card.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        card.transform.localScale = Vector3.zero;

        // Pop in
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.3f), 3f);
            card.transform.localScale = Vector3.one * n * 1.5f;
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        // Fade out (quemar)
        var img = card.GetComponent<Image>();
        elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            if (img != null)
            {
                Color c = img.color;
                c.a = Mathf.Lerp(1f, 0f, elapsed / 0.4f);
                img.color = c;
            }
            card.transform.localScale *= 1.02f; // crece ligeramente al quemarse
            yield return null;
        }

        Destroy(card);
    }

    // ── Helpers ──

    private IEnumerator LerpScale(Transform t, Vector3 to, float dur)
    {
        Vector3 from = t.localScale;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (t == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            t.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / dur));
            yield return null;
        }
        if (t != null) t.localScale = to;
    }

    private IEnumerator FadeGlow(Image img, float toAlpha)
    {
        float from = img.color.a;
        float elapsed = 0f;
        float dur = 0.15f;
        while (elapsed < dur)
        {
            if (img == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            Color c = img.color;
            c.a = Mathf.Lerp(from, toAlpha, Mathf.Clamp01(elapsed / dur));
            img.color = c;
            yield return null;
        }
    }
}