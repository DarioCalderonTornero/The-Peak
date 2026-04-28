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
    public void OnPointerClick(PointerEventData eventData)
    {
        // Solo click derecho
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        // Solo si NO estamos en juego (DragCardUI desactivado = inventario)
        // if (dragCardUI != null && dragCardUI.enabled)
            // return;

        // Evitar doble click mientras anima
        if (isAnimating)
            return;

        if (flipRoutine != null) StopCoroutine(flipRoutine);
        flipRoutine = StartCoroutine(FlipAnimation());
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