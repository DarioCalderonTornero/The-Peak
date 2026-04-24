using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla el outline cartoon del shader "UI/CardOutline".
/// Adjuntar al mismo GameObject que tiene la Image con ese material.
/// </summary>
[RequireComponent(typeof(Image))]
public class CardOutlineController : MonoBehaviour
{
    [Header("Colores")]
    [SerializeField] private Color outlineHoverColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color outlineNormalColor = new Color(0f, 0f, 0f, 0f);

    [Header("Grosor (0 = sin outline)")]
    [SerializeField] private float outlineHoverWidth = 0.018f;  // en UV space (0-0.05)
    [SerializeField] private float outlineNormalWidth = 0f;

    [Header("Velocidad")]
    [SerializeField] private float animDuration = 0.25f;

    // IDs de propiedad del shader (más rápido que strings)
    private static readonly int s_OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int s_OutlineWidth = Shader.PropertyToID("_OutlineWidth");

    private Material mat;       // instancia privada del material (no toca el shared)
    private Coroutine anim;

    private void Awake()
    {
        var img = GetComponent<Image>();

        // Creamos una instancia del material para que cada carta tenga el suyo
        // (si no, todas las cartas comparten el mismo material y el outline
        //  aparece en todas a la vez)
        mat = Instantiate(img.material);
        img.material = mat;

        // Estado inicial: sin outline
        mat.SetColor(s_OutlineColor, outlineNormalColor);
        mat.SetFloat(s_OutlineWidth, outlineNormalWidth);
    }

    private void OnDestroy()
    {
        // Limpiar la instancia del material al destruir la carta
        if (mat != null) Destroy(mat);
    }

    // ─── API pública ──────────────────────────────────────────────

    public void SetHover(bool active)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(
            mat.GetColor(s_OutlineColor),
            active ? outlineHoverColor : outlineNormalColor,
            mat.GetFloat(s_OutlineWidth),
            active ? outlineHoverWidth : outlineNormalWidth));
    }

    public void SetImmediate(bool active)
    {
        if (anim != null) StopCoroutine(anim);
        mat.SetColor(s_OutlineColor, active ? outlineHoverColor : outlineNormalColor);
        mat.SetFloat(s_OutlineWidth, active ? outlineHoverWidth : outlineNormalWidth);
    }

    // ─── Animación ────────────────────────────────────────────────

    private IEnumerator Animate(Color fromCol, Color toCol, float fromW, float toW)
    {
        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / animDuration);
            mat.SetColor(s_OutlineColor, Color.Lerp(fromCol, toCol, n));
            mat.SetFloat(s_OutlineWidth, Mathf.Lerp(fromW, toW, n));
            yield return null;
        }
        mat.SetColor(s_OutlineColor, toCol);
        mat.SetFloat(s_OutlineWidth, toW);
    }
}