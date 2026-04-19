using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Se añade al prefab visual de muerte (escalador sin lógica).
/// Ejecuta la animación de muerte según el tipo y dispara OnAnimationComplete al terminar.
/// </summary>
public class DeathAnimation : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Renderer del casco para aplicar el color del escalador original.")]
    [SerializeField] private Renderer helmetRenderer;

    [Header("Seguridad")]
    [SerializeField] private float destroyDelay = 5f;

    /// <summary>
    /// Se dispara cuando la animación de muerte termina.
    /// DeathClimberExplosion y DeathCinematicManager escuchan esto.
    /// </summary>
    public event Action OnAnimationComplete;

    // ─── Entrada ─────────────────────────────────────────────────────────────

    public void PlayAnimation(DeathCause deathCause, Color helmetColor)
    {
        if (helmetRenderer != null)
            helmetRenderer.material.color = helmetColor;

        switch (deathCause)
        {
            case DeathCause.Stamina: StartCoroutine(StaminaDeathRoutine()); break;
            case DeathCause.Mud: StartCoroutine(MudDeathRoutine()); break;
            case DeathCause.Quicksand: StartCoroutine(QuicksandDeathRoutine()); break;
            case DeathCause.BadBerry: StartCoroutine(BadBerryDeathRoutine()); break;
            case DeathCause.Geyser: StartCoroutine(GeyserDeathRoutine()); break;
            case DeathCause.Snow: StartCoroutine(SnowDeathRoutine()); break;
            default: StartCoroutine(DefaultDeathRoutine()); break;
        }

        // Seguro: si algo falla, el prefab se destruye solo
        //Destroy(gameObject, destroyDelay);
    }

    // ─── Animaciones por tipo ─────────────────────────────────────────────────

    private IEnumerator StaminaDeathRoutine()
    {
        //yield return null;
        yield return StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, 2f));
        StartCoroutine(NotifyComplete());
    }

    private IEnumerator MudDeathRoutine()
    {
        yield return StartCoroutine(FlattenRoutine(1f));
        StartCoroutine(NotifyComplete());
    }

    private IEnumerator QuicksandDeathRoutine()
    {
        yield return StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, 0.5f));
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, 0.2f));
        StartCoroutine(NotifyComplete());
    }

    private IEnumerator BadBerryDeathRoutine()
    {
        yield return StartCoroutine(ScaleRoutine(transform.localScale * 2.5f, 0.8f));
        yield return new WaitForSeconds(0.1f);
        StartCoroutine(NotifyComplete());
    }

    private IEnumerator GeyserDeathRoutine()
    {
        // El géiser ya sale volando por física
        // Notificamos inmediatamente para que la explosión ocurra ya
        yield return null;
        StartCoroutine(NotifyComplete());
    }

    private IEnumerator SnowDeathRoutine()
    {
        yield return StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, 0.3f));
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, 0.2f));
        StartCoroutine(NotifyComplete());
    }

    private IEnumerator DefaultDeathRoutine()
    {
        yield return StartCoroutine(FlattenRoutine(0.5f));
        StartCoroutine(NotifyComplete());
    }

    // ─── Notify ──────────────────────────────────────────────────────────────

    private IEnumerator NotifyComplete()
    {
        yield return null;
        //yield return new WaitForSeconds(1.0f);
        OnAnimationComplete?.Invoke();
        // No destruimos aquí — DeathCinematicManager decide cuándo
        // (la explosión puede tardar un poco más en verse)
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private IEnumerator FlattenRoutine(float duration)
    {
        Vector3 startScale = transform.localScale;
        // Solo baja a la mitad, no a 0
        Vector3 targetScale = startScale * 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float z = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = originalPos + new Vector3(x, 0f, z);
            yield return null;
        }

        transform.localPosition = originalPos;
    }
}
