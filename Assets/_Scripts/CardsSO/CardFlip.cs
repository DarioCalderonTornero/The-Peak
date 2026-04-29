using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gestiona el flip de la carta con CLICK DERECHO.
/// Solo se ejecuta si el DragCardUI está DESACTIVADO (o sea, en el inventario).
/// Adjuntar a la raíz del prefab de la carta.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardFlip : MonoBehaviour, IPointerClickHandler
{
    [Header("Caras de la carta")]
    [SerializeField] private GameObject cardFront;
    [SerializeField] private GameObject cardBack;

    [Header("Animación flip")]
    [SerializeField] private float flipDuration = 0.55f;

    [Header("Ref al drag (para detectar si estamos en inventario)")]
    [SerializeField] private DragCardUI dragCardUI;   // auto-detect en Awake si se deja vacío

    private RectTransform rt;
    private bool isFlipped = false;
    private bool isAnimating = false;
    private Coroutine flipRoutine;

    [Header("Carta pendiente de desbloqueo")]
    [SerializeField] private GameObject pendingCard;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();

        if (dragCardUI == null)
            dragCardUI = GetComponent<DragCardUI>();

        // Estado inicial: frente visible, reverso oculto
        if (cardFront != null) cardFront.SetActive(true);
        if (cardBack != null) cardBack.SetActive(false);
        isFlipped = false;
    }

    // ─── CLICK DERECHO ────────────────────────────────────────────
    private bool isLocked = false;
    public void SetLocked(bool locked) { isLocked = locked; }

    private bool isPending = false;
    public void SetPending(bool pending)
    {
        isPending = pending;

        if (pendingCard != null) pendingCard.SetActive(pending);
        if (cardFront != null) cardFront.SetActive(!pending);
        if (cardBack != null) cardBack.SetActive(false);

        // Ocultar el resto de hijos de la raíz mientras está pendiente
        foreach (Transform child in transform)
        {
            if (child.gameObject == pendingCard) continue;
            if (child.gameObject == cardFront) continue;
            if (child.gameObject == cardBack) continue;
            child.gameObject.SetActive(!pending);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked) return;

        // Click izquierdo en estado pendiente → animación de desbloqueo
        if (isPending && eventData.button == PointerEventData.InputButton.Left)
        {
            if (isAnimating) return;
            // La escala base la pasa CardInventoryUI al configurar la carta
            StartUnlockAnimation(_unlockBaseScale);
            return;
        }

        // Click derecho → flip normal (solo si no está pendiente)
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (isAnimating) return;
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        flipRoutine = StartCoroutine(FlipAnimation());
    }

    public event System.Action OnUnlockFlipComplete;

    public Vector3 _unlockBaseScale;
    private Vector3 _unlockBigScale;

    public void StartUnlockAnimation(Vector3 baseScale)
    {
        _unlockBaseScale = baseScale;
        _unlockBigScale = baseScale * 1.25f;
        if (isAnimating) return;
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        flipRoutine = StartCoroutine(UnlockFlipAnimation());
    }

    private IEnumerator UnlockFlipAnimation()
    {
        isAnimating = true;

        var trigger = GetComponent<EventTrigger>();
        if (trigger != null) trigger.enabled = false;

        float half = flipDuration * 0.5f;

        // ── Fase 1: hacerse grande ────────────────────────────────────
        float riseTime = 0.2f;
        float t = 0f;
        while (t < riseTime)
        {
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / riseTime), 2f);
            transform.localScale = Vector3.Lerp(_unlockBaseScale, _unlockBigScale, n);
            yield return null;
        }

        // ── Fase 2: flip primera mitad ────────────────────────────────
        yield return AnimateRotationY(0f, 90f, half, EaseInQuad);

        // Cambio de cara
        if (pendingCard != null) pendingCard.SetActive(false);
        if (cardFront != null) cardFront.SetActive(true);
        if (cardBack != null) cardBack.SetActive(false);

        // ── Fase 3: flip segunda mitad ────────────────────────────────
        yield return AnimateRotationY(90f, 0f, half, EaseOutQuad);

        // ── Fase 4: caer con impacto ──────────────────────────────────
        float fallTime = 0.3f;
        t = 0f;
        while (t < fallTime)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / fallTime);
            float scaleCurve = n < 0.7f
                ? Mathf.Lerp(1.25f, 0.92f, n / 0.7f)
                : Mathf.Lerp(0.92f, 1f, (n - 0.7f) / 0.3f);
            transform.localScale = _unlockBaseScale * scaleCurve;
            yield return null;
        }

        transform.localScale = _unlockBaseScale;

        isAnimating = false;
        isPending = false;

        if (pendingCard != null)
        {
            Destroy(pendingCard);
            pendingCard = null;
        }

        var trigger2 = GetComponent<EventTrigger>();
        if (trigger2 != null) trigger2.enabled = true;

        OnUnlockFlipComplete?.Invoke();
    }

    public IEnumerator FlipWithCallback(System.Action onMidFlip)
    {
        isAnimating = true;
        float half = flipDuration * 0.5f;

        yield return AnimateRotationY(0f, 90f, half, EaseInQuad);

        onMidFlip?.Invoke();

        yield return AnimateRotationY(90f, 0f, half, EaseOutQuad);

        isAnimating = false;
    }

    // ─── ANIMACIÓN FLIP 3D ────────────────────────────────────────
    private IEnumerator FlipAnimation()
    {
        isAnimating = true;
        float half = flipDuration * 0.5f;

        // Primera mitad: rotar de 0° a 90°
        yield return AnimateRotationY(0f, 90f, half, EaseInQuad);

        // Cambiar cara
        isFlipped = !isFlipped;
        if (cardFront != null) cardFront.SetActive(!isFlipped);
        if (cardBack != null) cardBack.SetActive(isFlipped);

        // Segunda mitad: rotar de 90° a 0°
        yield return AnimateRotationY(90f, 0f, half, EaseOutQuad);

        isAnimating = false;
    }

    private IEnumerator AnimateRotationY(float from, float to, float dur, System.Func<float, float> ease)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = ease(Mathf.Clamp01(t / dur));
            float angle = Mathf.LerpUnclamped(from, to, n);
            rt.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        rt.localRotation = Quaternion.Euler(0f, to, 0f);
    }

    // ─── EASING ───────────────────────────────────────────────────
    private float EaseInQuad(float t) => t * t;
    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    // ─── API PÚBLICA ──────────────────────────────────────────────
    public bool IsFlipped => isFlipped;

    /// <summary>
    /// Devuelve la carta al frente de golpe, sin animación.
    /// Útil cuando pasas de inventario a juego.
    /// </summary>
    public void ResetToFront()
    {
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        isAnimating = false;
        isFlipped = false;
        rt.localRotation = Quaternion.identity;
        if (cardFront != null) cardFront.SetActive(true);
        if (cardBack != null) cardBack.SetActive(false);
    }
}