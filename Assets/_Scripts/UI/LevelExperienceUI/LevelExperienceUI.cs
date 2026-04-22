using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

public class LevelExperienceUI : MonoBehaviour
{
    [Header("Bar")]
    [SerializeField] private Image levelBarImage;
    [SerializeField] private float fillSpeed = 1.5f;
    private bool forceFillToFullOnLevelUp;

    [Header("Bar Shine")]
    [SerializeField] private RectTransform barShineRect;
    [SerializeField] private Image barShineImage;
    [SerializeField] private float shineDuration = 0.35f;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI currentLevelText;
    [SerializeField] private TextMeshProUGUI currentXpText;
    [SerializeField] private TextMeshProUGUI xpToNextLevelText;

    [Header("Star Icon (Main)")]
    [SerializeField] private Image starIconImage;

    [Tooltip("Duración del relleno de la estrella (fillAmount) al subir de nivel.")]
    [SerializeField] private float starFillDuration = 0.5f;

    [Header("Star FX - Bounce (Scale)")]
    [SerializeField] private float bounceUpScale = 1.35f;
    [SerializeField] private float bounceUpTime = 0.08f;
    [SerializeField] private float bounceDownTime = 0.20f;
    [SerializeField] private Ease bounceEaseUp = Ease.OutBack;
    [SerializeField] private Ease bounceEaseDown = Ease.OutQuad;

    [Header("Star FX - Flash (Color)")]
    [SerializeField] private bool useFlash = true;
    [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.6f, 1f);
    [SerializeField] private float flashInTime = 0.08f;
    [SerializeField] private float flashOutTime = 0.18f;

    [Header("Star FX - Wobble (Rotation)")]
    [SerializeField] private bool useRotation = true;
    [SerializeField] private float wobbleDuration = 0.4f;
    [SerializeField] private Ease wobbleEase = Ease.OutElastic;

    [Header("Star FX - Glow (Optional Overlay Image)")]
    [Tooltip("Imagen opcional para glow detrás de la estrella.")]
    [SerializeField] private Image glowImage;
    [SerializeField] private bool useGlow = true;
    [SerializeField] private float glowMaxAlpha = 0.65f;
    [SerializeField] private float glowInTime = 0.10f;
    [SerializeField] private float glowOutTime = 0.25f;
    [SerializeField] private float glowScaleMultiplier = 1.25f;

    [Header("Star - Idle Animation (Subtle)")]
    [SerializeField] private bool useIdle = true;
    [SerializeField] private float idleScaleAmount = 0.03f;
    [SerializeField] private float idlePeriod = 1.6f;

    [Header("Star - Color by Level (Gradient)")]
    [SerializeField] private bool useLevelColor = true;
    [Tooltip("Gradiente para el color base de la estrella según el nivel.")]
    [SerializeField] private Gradient levelColorGradient;
    [Tooltip("Nivel a partir del cual el gradiente se queda en 1.")]
    [SerializeField] private int maxLevelForGradient = 50;

    [Header("Star - Auto Reset (Down Animation)")]
    [Tooltip("Segundos después del level up para vaciar la estrella de nuevo.")]
    [SerializeField] private float secondsBeforeReset = 10f;
    [Tooltip("Duración del vaciado (1 -> 0) al resetear.")]
    [SerializeField] private float downResetDuration = 0.35f;
    [Tooltip("Pequeño squeeze al vaciar.")]
    [SerializeField] private float downSqueezeScale = 0.92f;
    [SerializeField] private Ease downResetEase = Ease.InOutQuad;

    // Internals
    private Coroutine fillCoroutine;
    private Tween idleTween;
    private Sequence starSequence;

    private RectTransform starRect;
    private Vector3 starBaseScale;
    private Color starBaseColor;

    private Tween resetTween;

    // Cola de level ups pendientes
    private readonly Queue<int> levelUpQueue = new Queue<int>();
    private bool isProcessingLevelUp = false;

    private void Start()
    {
        LevelExperienceManager.Instance.OnExperienceChanged += OnExperienceChanged;
        LevelExperienceManager.Instance.OnLevelUp += OnLevelUp;

        if (starIconImage != null)
        {
            starRect = starIconImage.rectTransform;
            starBaseScale = starRect.localScale;
            starBaseColor = starIconImage.color;
        }

        levelBarImage.fillAmount = LevelExperienceManager.Instance.GetExperienceNormalized();
        ApplyStarBaseVisualsForCurrentLevel();
        UpdateTexts();

        StartIdle();
    }

    private void OnDestroy()
    {
        if (LevelExperienceManager.Instance != null)
        {
            LevelExperienceManager.Instance.OnExperienceChanged -= OnExperienceChanged;
            LevelExperienceManager.Instance.OnLevelUp -= OnLevelUp;
        }

        starSequence?.Kill();
        resetTween?.Kill();
        idleTween?.Kill();
        if (glowImage != null) glowImage.DOKill();
        if (starIconImage != null) starIconImage.DOKill();
        if (starRect != null) starRect.DOKill();
    }

    private void OnExperienceChanged()
    {
        // Si estamos procesando un level up, no interrumpimos la animación
        if (isProcessingLevelUp) return;

        float targetFill = LevelExperienceManager.Instance.GetExperienceNormalized();

        if (fillCoroutine != null)
            StopCoroutine(fillCoroutine);

        fillCoroutine = StartCoroutine(AnimateBar(targetFill));

        UpdateTexts();
    }

    private void OnLevelUp(object sender, EventArgs e)
    {
        resetTween?.Kill();

        // Encolamos el nivel al que se sube
        levelUpQueue.Enqueue(LevelExperienceManager.Instance.GetLevel());

        if (!isProcessingLevelUp)
            StartCoroutine(ProcessLevelUpQueue());
    }

    private IEnumerator ProcessLevelUpQueue()
    {
        isProcessingLevelUp = true;

        while (levelUpQueue.Count > 0)
        {
            int level = levelUpQueue.Dequeue();

            // Actualizamos colores y textos para este nivel concreto
            ApplyStarBaseVisualsForCurrentLevel();
            UpdateTexts();

            // Animamos la barra llenándose hasta el final
            if (fillCoroutine != null)
                StopCoroutine(fillCoroutine);

            bool barDone = false;
            fillCoroutine = StartCoroutine(AnimateBarLevelUp(() => barDone = true));

            yield return new WaitUntil(() => barDone);

            // Shine y espera
            StartCoroutine(PlayBarShine(1f));
            yield return new WaitForSeconds(shineDuration);

            // Reset de la barra a 0
            levelBarImage.fillAmount = 0f;

            // Animación de la estrella — esperamos a que termine
            bool starDone = false;
            PlayStarLevelUpFX(() => starDone = true);

            yield return new WaitUntil(() => starDone);

            // Pequeña pausa entre level ups encadenados
            if (levelUpQueue.Count > 0)
                yield return new WaitForSeconds(0.3f);
        }

        isProcessingLevelUp = false;

        // Ahora que terminamos todos los level ups, animamos al valor real de XP
        float targetFill = LevelExperienceManager.Instance.GetExperienceNormalized();
        if (fillCoroutine != null)
            StopCoroutine(fillCoroutine);
        fillCoroutine = StartCoroutine(AnimateBar(targetFill));

        UpdateTexts();
    }

    private IEnumerator AnimateBarLevelUp(Action onComplete)
    {
        float start = levelBarImage.fillAmount;
        float time = 0f;

        while (time < 1f)
        {
            time += Time.deltaTime * fillSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, time);
            levelBarImage.fillAmount = Mathf.Lerp(start, 1f, eased);
            yield return null;
        }

        levelBarImage.fillAmount = 1f;
        onComplete?.Invoke();
    }

    private IEnumerator AnimateBar(float target)
    {
        float start = levelBarImage.fillAmount;
        float time = 0f;

        float overshootTarget = Mathf.Min(target + 0.03f, 1f);

        while (time < 1f)
        {
            time += Time.deltaTime * fillSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, time);
            levelBarImage.fillAmount = Mathf.Lerp(start, overshootTarget, eased);
            yield return null;
        }

        time = 0f;
        float overshootStart = levelBarImage.fillAmount;
        while (time < 1f)
        {
            time += Time.deltaTime * (fillSpeed * 2f);
            levelBarImage.fillAmount = Mathf.Lerp(overshootStart, target, time);
            yield return null;
        }

        levelBarImage.fillAmount = target;
    }

    private IEnumerator PlayBarShine(float fillAmount)
    {
        if (barShineRect == null || barShineImage == null) yield break;

        float barWidth = levelBarImage.rectTransform.rect.width;
        float shineWidth = 15f;

        float startX = -(barWidth * 0.5f);
        float endX = startX + (barWidth * fillAmount);

        barShineRect.sizeDelta = new Vector2(shineWidth, 0f);
        barShineRect.anchoredPosition = new Vector2(startX, 0f);

        barShineImage.color = new Color(1f, 1f, 1f, 0f);

        float elapsed = 0f;

        while (elapsed < shineDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shineDuration);

            barShineRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0f);

            float alpha = t < 0.5f
                ? Mathf.Lerp(0f, 0.6f, t / 0.5f)
                : Mathf.Lerp(0.6f, 0f, (t - 0.5f) / 0.5f);

            barShineImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        barShineImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private void UpdateTexts()
    {
        if (currentLevelText != null)
            currentLevelText.text = LevelExperienceManager.Instance.GetLevel().ToString();
        if (currentXpText != null)
            currentXpText.text = LevelExperienceManager.Instance.GetCurrentXp() + " / " +
                                 LevelExperienceManager.Instance.GetXpToNextLevel();
        // xpToNextLevelText ya no se usa, puedes borrarlo del script también
    }

    // -------------------------
    // Star visuals / FX
    // -------------------------

    private void ApplyStarBaseVisualsForCurrentLevel()
    {
        if (starIconImage == null) return;

        if (useLevelColor && maxLevelForGradient > 0)
        {
            int level = LevelExperienceManager.Instance.GetLevel();
            float t = Mathf.Clamp01(level / (float)maxLevelForGradient);
            starBaseColor = levelColorGradient.Evaluate(t);
            starIconImage.color = starBaseColor;
        }
        else
        {
            starBaseColor = starIconImage.color;
        }

        if (glowImage != null)
        {
            Color gc = starBaseColor;
            gc.a = 0f;
            glowImage.color = gc;
        }
    }

    private void StartIdle()
    {
        if (!useIdle || starRect == null) return;

        idleTween?.Kill();
        starRect.localScale = starBaseScale;

        idleTween = starRect
            .DOScale(starBaseScale * (1f + idleScaleAmount), idlePeriod * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopIdleTemporarily()
    {
        idleTween?.Kill();
        if (starRect != null)
            starRect.localScale = starBaseScale;
    }

    private void ResumeIdle()
    {
        if (useIdle)
            StartIdle();
    }

    private void PlayStarLevelUpFX(Action onComplete = null)
    {
        if (starIconImage == null || starRect == null)
        {
            onComplete?.Invoke();
            return;
        }

        starSequence?.Kill();
        StopIdleTemporarily();

        starRect.localScale = starBaseScale;
        starRect.localRotation = Quaternion.identity;
        starIconImage.fillAmount = 0f;

        starSequence = DOTween.Sequence();

        // FILL
        starSequence.Append(
            starIconImage.DOFillAmount(1f, starFillDuration).SetEase(Ease.InOutBack)
        );

        // Bounce
        starSequence.Join(
            starRect.DOScale(starBaseScale * bounceUpScale, bounceUpTime).SetEase(bounceEaseUp)
                .OnComplete(() =>
                {
                    starRect.DOScale(starBaseScale, bounceDownTime).SetEase(bounceEaseDown);
                })
        );

        // Flash — primero blanco puro, luego al color base
        if (useFlash)
        {
            starSequence.Join(
                starIconImage.DOColor(Color.white, 0.05f).SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        starIconImage.DOColor(flashColor, flashInTime).SetEase(Ease.OutQuad)
                            .OnComplete(() =>
                            {
                                starIconImage.DOColor(starBaseColor, flashOutTime).SetEase(Ease.OutQuad);
                            });
                    })
            );
        }

        // Wobble
        if (useRotation)
        {
            starSequence.Join(
                starRect.DOLocalRotate(new Vector3(0f, 0f, 15f), wobbleDuration * 0.25f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        starRect.DOLocalRotate(new Vector3(0f, 0f, -10f), wobbleDuration * 0.25f)
                            .SetEase(Ease.OutQuad)
                            .OnComplete(() =>
                            {
                                starRect.DOLocalRotate(new Vector3(0f, 0f, 5f), wobbleDuration * 0.25f)
                                    .SetEase(Ease.OutQuad)
                                    .OnComplete(() =>
                                    {
                                        starRect.DOLocalRotate(Vector3.zero, wobbleDuration * 0.25f)
                                            .SetEase(Ease.OutQuad);
                                    });
                            });
                    })
            );
        }

        // Glow
        if (useGlow && glowImage != null)
        {
            glowImage.rectTransform.localScale = starBaseScale * glowScaleMultiplier;

            Color gc = starBaseColor;
            gc.a = 0f;
            glowImage.color = gc;

            starSequence.Join(
                glowImage.DOFade(glowMaxAlpha, glowInTime).SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        glowImage.DOFade(0f, glowOutTime).SetEase(Ease.OutQuad);
                    })
            );
        }

        // Al terminar
        starSequence.OnComplete(() =>
        {
            starRect.localScale = starBaseScale;
            starRect.localRotation = Quaternion.identity;
            ResumeIdle();
            ScheduleDownReset();
            onComplete?.Invoke();
        });
    }

    // -------------------------
    // Down reset after N seconds
    // -------------------------

    private void ScheduleDownReset()
    {
        if (starIconImage == null || starRect == null) return;

        resetTween?.Kill();

        resetTween = DOVirtual.DelayedCall(secondsBeforeReset, () =>
        {
            PlayStarDownFX();
        }).SetUpdate(false);
    }

    private void PlayStarDownFX()
    {
        if (starIconImage == null || starRect == null) return;

        starSequence?.Kill();
        StopIdleTemporarily();

        Sequence downSeq = DOTween.Sequence();

        downSeq.Join(starRect.DOScale(starBaseScale * downSqueezeScale, downResetDuration * 0.35f).SetEase(Ease.InOutSine));
        downSeq.Join(starIconImage.DOFillAmount(0f, downResetDuration).SetEase(downResetEase));
        downSeq.Append(starRect.DOScale(starBaseScale, downResetDuration * 0.35f).SetEase(Ease.OutQuad));

        downSeq.OnComplete(() =>
        {
            starRect.localScale = starBaseScale;
            starRect.localRotation = Quaternion.identity;
            ResumeIdle();
        });
    }
}