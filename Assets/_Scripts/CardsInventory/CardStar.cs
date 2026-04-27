using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estrella que aparece en la esquina superior izquierda de la carta.
/// - Hover: aparece animada, desaparece al salir (si no está seleccionada)
/// - Seleccionada: se queda fija con pulso idle
/// Adjunta al GameObject "Star" hijo de la carta (FrontCard y/o BackCard).
/// </summary>
public class CardStar : MonoBehaviour
{
    [Header("Imagen de la estrella")]
    [SerializeField] private Image starImage;

    [Header("Animación entrada")]
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private float rotateDegrees = 20f;

    [Header("Pulso idle (cuando está seleccionada)")]
    [SerializeField] private float pulseSpeed = 1.2f;
    [SerializeField] private float pulseMinScale = 0.88f;
    [SerializeField] private float pulseMaxScale = 1.15f;
    [SerializeField] private float idleRotSpeed = 18f;

    [Header("Glow")]
    [SerializeField] private Image glowImage;
    [SerializeField] private Color glowColorA = new Color(1f, 0.92f, 0.3f, 0.8f);
    [SerializeField] private Color glowColorB = new Color(1f, 0.65f, 0.05f, 0.3f);

    private bool isSelected = false;
    private bool isVisible = false;
    private float pulseT = 0f;

    private Coroutine appearRoutine;

    // ─── Init ─────────────────────────────────────────────────────

    private void Awake()
    {
        SetAlpha(0f);
        transform.localScale = Vector3.zero;
        if (glowImage != null) glowImage.color = new Color(glowColorA.r, glowColorA.g, glowColorA.b, 0f);
    }

    // ─── Update — pulso idle ──────────────────────────────────────

    private void Update()
    {
        if (!isVisible || !isSelected) return;

        pulseT += Time.unscaledDeltaTime * pulseSpeed;
        float s = Mathf.Lerp(pulseMinScale, pulseMaxScale,
            (Mathf.Sin(pulseT * Mathf.PI * 2f) + 1f) * 0.5f);
        transform.localScale = Vector3.one * s;
        transform.Rotate(0f, 0f, idleRotSpeed * Time.unscaledDeltaTime);

        if (glowImage != null)
            glowImage.color = Color.Lerp(glowColorA, glowColorB,
                (Mathf.Sin(pulseT * Mathf.PI * 2f) + 1f) * 0.5f);
    }

    // ─── API pública ──────────────────────────────────────────────

    /// <summary>Llamar cuando el ratón entra en la carta.</summary>
    public void OnHoverEnter()
    {
        if (isVisible) return;
        Show();
    }

    /// <summary>Llamar cuando el ratón sale de la carta.</summary>
    public void OnHoverExit()
    {
        if (isSelected) return; // si está seleccionada, no se oculta
        Hide();
    }

    /// <summary>Marca la carta como seleccionada — la estrella se queda.</summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        pulseT = 0f;

        if (selected)
        {
            // Si ya está visible (hover activo), no reaparecer — solo activar pulso
            if (!isVisible)
                Show();
            // si ya está visible, el Update se encarga del pulso automáticamente
        }
        else
        {
            Hide();
        }
    }

    // ─── Mostrar / Ocultar ────────────────────────────────────────

    private void Show()
    {
        // Activar el GameObject antes de lanzar la coroutine
        gameObject.SetActive(true);
        isVisible = true;
        if (appearRoutine != null) StopCoroutine(appearRoutine);
        appearRoutine = StartCoroutine(AppearAnim());
    }

    private void Hide()
    {
        isVisible = false;
        if (appearRoutine != null) StopCoroutine(appearRoutine);
        // Si el objeto ya está inactivo no hace falta animar
        if (!gameObject.activeInHierarchy)
        {
            transform.localScale = Vector3.zero;
            SetAlpha(0f);
            return;
        }
        appearRoutine = StartCoroutine(DisappearAnim());
    }

    private IEnumerator AppearAnim()
    {
        float t = 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, -rotateDegrees);

        while (t < appearDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = EaseOutBack(Mathf.Clamp01(t / appearDuration));
            transform.localScale = Vector3.one * n;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-rotateDegrees, 0f, n));
            SetAlpha(n);
            yield return null;
        }

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        SetAlpha(1f);
    }

    private IEnumerator DisappearAnim()
    {
        float startScale = transform.localScale.x;
        float t = 0f;
        float dur = appearDuration * 0.6f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Clamp01(t / dur);
            transform.localScale = Vector3.one * (startScale * n);
            SetAlpha(n);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        SetAlpha(0f);
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private void SetAlpha(float a)
    {
        if (starImage != null)
        {
            Color c = starImage.color;
            c.a = a;
            starImage.color = c;
        }
        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = a * 0.8f;
            glowImage.color = c;
        }
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}