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
    [SerializeField] private Renderer[] bodyRenderer;

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
            case DeathCause.Stamina: StaminaDeathRoutine(); break;
            case DeathCause.Mud: MudDeathRoutine(); break;
            //case DeathCause.Quicksand: StartCoroutine(QuicksandDeathRoutine()); break;
            case DeathCause.BadBerry: BadBerryDeathRoutine(); break;
            case DeathCause.Geyser: StartCoroutine(GeyserDeathRoutine()); break;
            case DeathCause.Snow: StartCoroutine(SnowDeathRoutine()); break;
            case DeathCause.StormyCloud: StormyCloudDeath(); break;
            default: StartCoroutine(DefaultDeathRoutine()); break;
        }

        // Seguro: si algo falla, el prefab se destruye solo
        //Destroy(gameObject, destroyDelay);
    }

    // ─── Animaciones por tipo ─────────────────────────────────────────────────

    private void StaminaDeathRoutine()
    {
        float targetAngle = 1.5f;
        float animDuration = 0.85f;
        float shakeMagnitude = 0.01f;

        StartCoroutine(ScaleRoutine(transform.localScale * targetAngle, animDuration));
        StartCoroutine(ShakeRoutine(animDuration, shakeMagnitude)); // ← orden correcto
        Invoke(nameof(NotifyComplete), animDuration);
    }

    private void MudDeathRoutine()
    {
        float targetAngle = 1.5f;
        float animDuration = 1.25f;
        float shakeMagnitude = 0.01f;

        StartCoroutine(ScaleRoutine(transform.localScale * targetAngle, animDuration));
        StartCoroutine(ShakeRoutine(animDuration, shakeMagnitude)); // ← orden correcto
        Invoke(nameof(NotifyComplete), animDuration);
    }

    private IEnumerator QuicksandDeathRoutine()
    {
        yield return StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, 0.5f));
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, 0.2f));
        NotifyComplete();
    }

    private void BadBerryDeathRoutine()
    {
        float animDuration = 2.0f;
        float shakeMagnitude = 0.01f;

        StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, animDuration));
        StartCoroutine(ShakeRoutine(animDuration, shakeMagnitude));
        StartCoroutine(BodyColorToRedRoutine(Color.red, animDuration));
        Invoke(nameof(NotifyComplete), animDuration);
    }

    private IEnumerator GeyserDeathRoutine()
    {
        // El géiser ya sale volando por física
        // Notificamos inmediatamente para que la explosión ocurra ya
        yield return null;
        NotifyComplete();
    }

    private IEnumerator SnowDeathRoutine()
    {
        yield return StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, 0.3f));
        yield return StartCoroutine(ScaleRoutine(Vector3.zero, 0.2f));
        NotifyComplete();
    }

    private void StormyCloudDeath()
    {
        float animDuration = 1.0f;
        float shakeMagnitude = 0.01f;

        StartCoroutine(ScaleRoutine(transform.localScale * 1.5f, animDuration));
        StartCoroutine(ShakeRoutine(animDuration, shakeMagnitude));
        StartCoroutine(BodyColorToRedRoutine(Color.black, animDuration));
        Invoke(nameof(NotifyComplete), animDuration);
    }

    private IEnumerator DefaultDeathRoutine()
    {
        yield return StartCoroutine(FlattenRoutine(0.5f));
        NotifyComplete();
    }

    // ─── Notify ──────────────────────────────────────────────────────────────

    private void NotifyComplete()
    {
        OnAnimationComplete?.Invoke();
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

    private IEnumerator BodyColorToRedRoutine(Color deathColor, float duration)
    {
        if (bodyRenderer == null || bodyRenderer.Length == 0) yield break;

        // Recogemos los colores originales de cada renderer
        Color[] startColors = new Color[bodyRenderer.Length];
        for (int i = 0; i < bodyRenderer.Length; i++)
        {
            if (bodyRenderer[i] != null)
                startColors[i] = bodyRenderer[i].material.color;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            for (int i = 0; i < bodyRenderer.Length; i++)
            {
                if (bodyRenderer[i] != null)
                    bodyRenderer[i].material.color = Color.Lerp(startColors[i], deathColor, t);
            }

            yield return null;
        }

        // Aseguramos que llega a rojo puro al final
        for (int i = 0; i < bodyRenderer.Length; i++)
        {
            if (bodyRenderer[i] != null)
                bodyRenderer[i].material.color = Color.red;
        }
    }
}
