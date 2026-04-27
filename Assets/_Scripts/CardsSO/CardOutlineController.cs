using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardOutlineController : MonoBehaviour
{
    [Header("Colores")]
    [SerializeField] private Color outlineHoverColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color outlineNormalColor = new Color(0f, 0f, 0f, 0f);

    [Header("Grosor (0 = sin outline)")]
    [SerializeField] private float outlineHoverWidth = 0.018f;
    [SerializeField] private float outlineNormalWidth = 0f;

    [Header("Velocidad")]
    [SerializeField] private float animDuration = 0.25f;

    private static readonly int s_OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int s_OutlineWidth = Shader.PropertyToID("_OutlineWidth");

    private Material mat;
    private bool isReady = false;   // solo opera si el setup fue correcto
    private Coroutine anim;

    private void Awake()
    {
        var img = GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError($"[CardOutlineController] No hay Image en {gameObject.name}");
            return;
        }

        // El material DEBE ser el CardOutlineMat (shader UI/CardOutline)
        // Si la Image no tiene material asignado en el Inspector, img.material
        // devuelve el material por defecto de Unity UI, que NO tiene _OutlineColor.
        if (img.material == null || img.material.shader.name != "UI/CardOutline")
        {
            Debug.LogWarning($"[CardOutlineController] '{gameObject.name}' no tiene el shader " +
                             "UI/CardOutline asignado en el campo Material de la Image. " +
                             "Asigna CardOutlineMat en el Inspector.");
            return;
        }

        // Instanciamos para que cada carta tenga su propia copia del material
        mat = Instantiate(img.material);
        img.material = mat;

        mat.SetColor(s_OutlineColor, outlineNormalColor);
        mat.SetFloat(s_OutlineWidth, outlineNormalWidth);

        isReady = true;
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    // ─── API pública ──────────────────────────────────────────────

    public void SetHover(bool active)
    {
        if (!isReady) return;   // si el setup falló, ignoramos silenciosamente

        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(
            mat.GetColor(s_OutlineColor),
            active ? outlineHoverColor : outlineNormalColor,
            mat.GetFloat(s_OutlineWidth),
            active ? outlineHoverWidth : outlineNormalWidth));
    }

    public void SetImmediate(bool active)
    {
        if (!isReady) return;

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