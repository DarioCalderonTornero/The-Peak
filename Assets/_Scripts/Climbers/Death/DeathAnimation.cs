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

    [Header("StormyCloud")]
    [SerializeField] private float stormyCloudAnimDelay = 0.5f;

    [Header("Snow")]
    [SerializeField] private GameObject snowVFXGameObject;

    [Header("Lodo")]
    [SerializeField] private GameObject mudVFXGameObject;

    private DeathClimberExplosion climberExplosion;

    /// <summary>
    /// Se dispara cuando la animación de muerte termina.
    /// DeathClimberExplosion y DeathCinematicManager escuchan esto.
    /// </summary>
    public event Action OnAnimationComplete;
    public event Action OnReadyForExplosion;

    // ─── Entrada ─────────────────────────────────────────────────────────────

    public void PlayAnimation(DeathCause deathCause, Color helmetColor)
    {
        if (helmetRenderer != null)
            helmetRenderer.material.color = helmetColor;

        climberExplosion = GetComponent<DeathClimberExplosion>();
        if (climberExplosion != null)
            climberExplosion.SetDeathCause(deathCause);

        switch (deathCause)
        {
            case DeathCause.Stamina: StaminaDeathRoutine(); break;
            case DeathCause.Mud: MudDeathRoutine(); break;
            case DeathCause.Bramble: BrambleDeathRoutine(); break;
            //case DeathCause.Quicksand: StartCoroutine(QuicksandDeathRoutine()); break;
            case DeathCause.BadBerry: BadBerryDeathRoutine(); break;
            case DeathCause.Geyser: StartCoroutine(GeyserDeathRoutine()); break;
            case DeathCause.Snow: SnowDeathRoutine(); break;
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

        Shake_ScaleRoutine(targetAngle, animDuration, shakeMagnitude);
        StartCoroutine(ShakeRoutine(animDuration, shakeMagnitude)); 
        Invoke(nameof(NotifyReadyForExplosion), animDuration);
        Invoke(nameof(NotifyComplete), animDuration);
    }

    private void BrambleDeathRoutine()
    {
        NotifyComplete();
        NotifyReadyForExplosion();
    }

    private void Shake_ScaleRoutine(float targetAngle, float animDuration, float shakeMagnitude)
    {
        StartCoroutine(ScaleRoutine(transform.localScale * targetAngle, animDuration));
        StartCoroutine(ShakeRoutine(animDuration, shakeMagnitude));
    }

    private void MudDeathRoutine()
    {
        float mudDelayExplosion = 0.75f;

        if (mudVFXGameObject != null)
        {
            GameObject vfx = Instantiate(mudVFXGameObject, transform.position + Vector3.up * 0.25f, Quaternion.identity);
            Destroy(vfx, 1f);
        }

        Invoke(nameof(NotifyReadyForExplosion), mudDelayExplosion);
        Invoke(nameof(NotifyComplete), mudDelayExplosion);
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
        StartCoroutine(BodyColorToRedRoutine(new Color(148f / 255f, 0f / 255f, 211f / 255f), animDuration));
        Invoke(nameof(NotifyComplete), animDuration);
        Invoke(nameof(NotifyReadyForExplosion), animDuration);
    }

    private IEnumerator GeyserDeathRoutine()
    {
        // El géiser ya sale volando por física
        // Notificamos inmediatamente para que la explosión ocurra ya
        yield return null;
        NotifyComplete();
        NotifyReadyForExplosion();
    }

    private void SnowDeathRoutine()
    {
        if (snowVFXGameObject != null)
        {
            Instantiate(snowVFXGameObject, transform.position, Quaternion.identity);
        }

        float snowDelayExplosion = 1.0f;
        StartCoroutine(ShakeRoutine(snowDelayExplosion, 0.1f));
        Invoke(nameof(NotifyReadyForExplosion), snowDelayExplosion);
        Invoke(nameof(NotifyComplete), snowDelayExplosion);
    }

    private void StormyCloudDeath()
    {
        Invoke(nameof(NotifyReadyForExplosion), stormyCloudAnimDelay);
    }

    private IEnumerator DefaultDeathRoutine()
    {
        yield return StartCoroutine(FlattenRoutine(0.5f));
        NotifyComplete();
    }

    // ─── Notify ──────────────────────────────────────────────────────────────

    public void NotifyComplete()
    {
        OnAnimationComplete?.Invoke();
    }

    public void NotifyReadyForExplosion()
    {
        OnReadyForExplosion?.Invoke();
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

    public IEnumerator ScaleRoutine(Vector3 targetScale, float duration)
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

    public IEnumerator ShakeRoutine(float duration, float magnitude)
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

    public IEnumerator BodyColorToRedRoutine(Color deathColor, float duration)
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
