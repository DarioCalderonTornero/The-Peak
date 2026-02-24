using System.Collections;
using UnityEngine;

public class OccupiedCellMarkerAnim : MonoBehaviour
{
    [Header("Anim")]
    [SerializeField] private float appearHeight = 0.35f;   // cuánto “sale” del suelo
    [SerializeField] private float duration = 0.12f;       // velocidad anim
    [SerializeField] private float surfaceOffset = 0.03f;  // separarlo un pelín del suelo

    [Header("Visual")]
    [SerializeField] private Vector3 markerScale = new Vector3(1f, 1f, 1f);

    private Coroutine routine;
    private bool targetVisible;

    private Vector3 surfacePos;     // punto en la montaña
    private Quaternion surfaceRot;  // rot alineada a la normal
    private Vector3 surfaceNormal;  // normal (para animar arriba/abajo en la normal)

    private void Awake()
    {
        // Arranca oculto (bajo tierra). Importante para que “no aparezca de golpe”.
        targetVisible = false;
    }

    /// <summary>
    /// Se llama cada frame desde DragCardUI para actualizar dónde debe estar el marker.
    /// </summary>
    public void SetTargetPose(Vector3 pos, Quaternion rot)
    {
        surfacePos = pos;
        surfaceRot = rot;
        surfaceNormal = (rot * Vector3.up).normalized;

        // Mantener rotación/escala siempre correctas
        transform.rotation = surfaceRot;
        transform.localScale = markerScale;

        // Si está oculto, mantenerlo “enterrado” en la nueva pose
        if (!targetVisible)
        {
            transform.position = HiddenPos();
        }
        else
        {
            // Si está visible, mantenlo visible (sin pegar saltos)
            transform.position = VisiblePos();
        }
    }

    public void Show()
    {
        if (targetVisible) return; // ✅ NO reiniciar animación cada frame
        targetVisible = true;

        gameObject.SetActive(true);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(HiddenPos(), VisiblePos(), duration, disableAtEnd: false));
    }

    public void Hide()
    {
        if (!targetVisible) return; // ✅ NO reiniciar
        targetVisible = false;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(transform.position, HiddenPos(), duration, disableAtEnd: true));
    }

    private Vector3 VisiblePos()
    {
        return surfacePos + surfaceNormal * surfaceOffset;
    }

    private Vector3 HiddenPos()
    {
        // “Bajo tierra” en la normal
        return surfacePos - surfaceNormal * appearHeight;
    }

    private IEnumerator Animate(Vector3 from, Vector3 to, float time, bool disableAtEnd)
    {
        float t = 0f;
        if (time <= 0f)
        {
            transform.position = to;
            if (disableAtEnd) gameObject.SetActive(false);
            yield break;
        }

        while (t < 1f)
        {
            t += Time.deltaTime / time;

            // easing suave
            float e = t * t * (3f - 2f * t);

            // Importante: NO tocar rot aquí (ya se ajusta por SetTargetPose)
            transform.position = Vector3.LerpUnclamped(from, to, e);

            yield return null;
        }

        transform.position = to;

        if (disableAtEnd)
            gameObject.SetActive(false);
    }
}