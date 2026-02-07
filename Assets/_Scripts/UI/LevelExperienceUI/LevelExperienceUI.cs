using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;
using DG.Tweening;

public class LevelExperienceUI : MonoBehaviour
{
    [Header("Bar")]
    [SerializeField] private Image levelBarImage;
    [SerializeField] private float fillSpeed = 1.5f;
    private bool forceFillToFullOnLevelUp;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI currentLevelText;
    [SerializeField] private TextMeshProUGUI currentXpText;
    [SerializeField] private TextMeshProUGUI xpToNextLevelText;

    [Header("Star Icon (Main)")]
    [SerializeField] private Image starIconImage;

    [Tooltip("Duración del relleno de la estrella (fillAmount) al subir de nivel.")]
    [SerializeField] private float starFillDuration = 0.5f;

    [Header("Star FX - Bounce (Scale)")]
    [SerializeField] private float bounceUpScale = 1.18f;
    [SerializeField] private float bounceUpTime = 0.12f;
    [SerializeField] private float bounceDownTime = 0.10f;
    [SerializeField] private Ease bounceEaseUp = Ease.OutBack;
    [SerializeField] private Ease bounceEaseDown = Ease.OutQuad;

    [Header("Star FX - Flash (Color)")]
    [SerializeField] private bool useFlash = true;
    [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.6f, 1f);
    [SerializeField] private float flashInTime = 0.08f;
    [SerializeField] private float flashOutTime = 0.18f;

    [Header("Star FX - Subtle Rotation")]
    [SerializeField] private bool useRotation = true;
    [SerializeField] private float rotationDegrees = 12f;
    [SerializeField] private float rotationTime = 0.22f;
    [SerializeField] private Ease rotationEase = Ease.OutBack;

    [Header("Star FX - Glow (Optional Overlay Image)")]
    [Tooltip("Imagen opcional para glow detrás de la estrella. Recomiendo duplicar la estrella como child y ponerla detrás.")]
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
    [Tooltip("Nivel a partir del cual el gradiente se queda en 1. Ej: si pones 50, a nivel 50+ será el color final.")]
    [SerializeField] private int maxLevelForGradient = 50;

    [Header("Star - Auto Reset (Down Animation)")] // NEW
    [Tooltip("Segundos después del level up para vaciar la estrella de nuevo.")]
    [SerializeField] private float secondsBeforeReset = 10f; // NEW
    [Tooltip("Duración del vaciado (1 -> 0) al resetear.")]
    [SerializeField] private float downResetDuration = 0.35f; // NEW
    [Tooltip("Pequeño 'squeeze' al vaciar para que quede más chulo.")]
    [SerializeField] private float downSqueezeScale = 0.92f; // NEW
    [SerializeField] private Ease downResetEase = Ease.InOutQuad; // NEW

    // Internals
    private Coroutine fillCoroutine;
    private Tween idleTween;
    private Sequence starSequence;

    private RectTransform starRect;
    private Vector3 starBaseScale;
    private Color starBaseColor;

    private Tween resetTween; // NEW (para cancelar el reset si hace falta)

    private void Start()
    {
        LevelExperienceManager.Instance.OnExperienceChanged += OnExperienceChanged;
        LevelExperienceManager.Instance.OnLevelUp += OnLevelUp;

        if (starIconImage != null)
        {
            starRect = starIconImage.rectTransform;
            starBaseScale = starRect.localScale;
            starBaseColor = starIconImage.color;

            // Si quieres que empiece vacía, perfecto:
            // (tu nota decía que esto está bien)
            // starIconImage.fillAmount = 0f;
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
        resetTween?.Kill(); // NEW
        idleTween?.Kill();
        if (glowImage != null) glowImage.DOKill();
        if (starIconImage != null) starIconImage.DOKill();
        if (starRect != null) starRect.DOKill();
    }

    private void OnExperienceChanged()
    {
        float targetFill;

        if (forceFillToFullOnLevelUp)
        {
            // 1) Forzamos llenar la barra hasta 1
            targetFill = 1f;
            forceFillToFullOnLevelUp = false;

            if (fillCoroutine != null)
                StopCoroutine(fillCoroutine);

            fillCoroutine = StartCoroutine(AnimateBarAndReset());
        }
        else
        {
            targetFill = LevelExperienceManager.Instance.GetExperienceNormalized();

            if (fillCoroutine != null)
                StopCoroutine(fillCoroutine);

            fillCoroutine = StartCoroutine(AnimateBar(targetFill));
        }

        UpdateTexts();
    }


    private void OnLevelUp(object sender, EventArgs e)
    {
        resetTween?.Kill();

        forceFillToFullOnLevelUp = true;

        ApplyStarBaseVisualsForCurrentLevel();
        UpdateTexts();

        PlayStarLevelUpFX();
    }

    private IEnumerator AnimateBarAndReset()
    {
        yield return AnimateBar(1f);

        levelBarImage.fillAmount = 0f;
    }



    private IEnumerator AnimateBar(float target)
    {
        float start = levelBarImage.fillAmount;
        float time = 0f;

        while (time < 1f)
        {
            time += Time.deltaTime * fillSpeed;
            levelBarImage.fillAmount = Mathf.Lerp(start, target, time);
            yield return null;
        }

        levelBarImage.fillAmount = target;
    }

    private void UpdateTexts()
    {
        currentLevelText.text = LevelExperienceManager.Instance.GetLevel().ToString();
        currentXpText.text = "XP: " + LevelExperienceManager.Instance.GetCurrentXp().ToString() + "/ ";
        xpToNextLevelText.text = LevelExperienceManager.Instance.GetXpToNextLevel().ToString();
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

    private void PlayStarLevelUpFX()
    {
        if (starIconImage == null || starRect == null) return;

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

        // Flash
        if (useFlash)
        {
            starSequence.Join(
                starIconImage.DOColor(flashColor, flashInTime).SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        starIconImage.DOColor(starBaseColor, flashOutTime).SetEase(Ease.OutQuad);
                    })
            );
        }

        // Rotation
        if (useRotation)
        {
            starSequence.Join(
                starRect.DOLocalRotate(new Vector3(0f, 0f, rotationDegrees), rotationTime, RotateMode.FastBeyond360)
                    .SetEase(rotationEase)
                    .OnComplete(() =>
                    {
                        starRect.DOLocalRotate(Vector3.zero, 0.12f).SetEase(Ease.OutQuad);
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

        // When complete: resume idle + schedule down animation
        starSequence.OnComplete(() =>
        {
            starRect.localScale = starBaseScale;
            starRect.localRotation = Quaternion.identity;
            ResumeIdle();

            ScheduleDownReset(); // NEW
        });
    }

    // -------------------------
    // Down reset after N seconds
    // -------------------------

    private void ScheduleDownReset() // NEW
    {
        if (starIconImage == null || starRect == null) return;

        resetTween?.Kill();

        // DelayedCall: espera X segundos y luego hace el vaciado chulo
        resetTween = DOVirtual.DelayedCall(secondsBeforeReset, () =>
        {
            PlayStarDownFX();
        }).SetUpdate(false);
    }

    private void PlayStarDownFX() // NEW
    {
        if (starIconImage == null || starRect == null) return;

        // Si hay alguna animación de estrella en curso, la paramos
        starSequence?.Kill();
        StopIdleTemporarily();

        // Mini secuencia simple pero resultona:
        // - squeeze (encoger un pelín)
        // - vaciar fill 1->0
        // - volver a escala base
        Sequence downSeq = DOTween.Sequence();

        downSeq.Join(starRect.DOScale(starBaseScale * downSqueezeScale, downResetDuration * 0.35f).SetEase(Ease.InOutSine));
        downSeq.Join(starIconImage.DOFillAmount(0f, downResetDuration).SetEase(downResetEase));

        // Vuelve a base
        downSeq.Append(starRect.DOScale(starBaseScale, downResetDuration * 0.35f).SetEase(Ease.OutQuad));

        downSeq.OnComplete(() =>
        {
            starRect.localScale = starBaseScale;
            starRect.localRotation = Quaternion.identity;
            ResumeIdle();
        });
    }
}
