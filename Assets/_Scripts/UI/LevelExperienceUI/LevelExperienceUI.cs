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

    [Header("Bar Shine")]
    [SerializeField] private RectTransform barShineRect;
    [SerializeField] private Image barShineImage;
    [SerializeField] private float shineDuration = 0.5f;
    [SerializeField] private float shineWidth = 50f;
    [SerializeField] private float shineMaxAlpha = 0.9f;

    [Header("Bar - Level Up FX")]
    [SerializeField] private float barPulseScale = 1.12f;
    [SerializeField] private float barPulseDuration = 0.12f;
    [SerializeField] private float barShakeDuration = 0.2f;
    [SerializeField] private float barShakeStrength = 6f;
    [SerializeField] private int barShakeVibrato = 20;

    [Header("Bar - Color Shift")]
    [SerializeField] private bool useBarColorShift = true;
    [SerializeField] private Color barColorBase = new Color(0.4f, 0.7f, 1f, 1f);
    [SerializeField] private Color barColorFull = new Color(0.1f, 0.4f, 1f, 1f);
    [SerializeField] private float barFlashDuration = 0.08f;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI currentLevelText;
    [SerializeField] private TextMeshProUGUI currentXpText;

    [Header("Star Icon (Main)")]
    [SerializeField] private Image starIconImage;

    [Header("Star FX - Spin 360")]
    [SerializeField] private float spinDuration = 0.45f;
    [SerializeField] private Ease spinEaseIn = Ease.InBack;
    [SerializeField] private Ease spinEaseOut = Ease.OutBack;

    [Header("Star FX - Bob")]
    [SerializeField] private float bobHeight = 30f;
    [SerializeField] private float squishY = 0.75f;
    [SerializeField] private float squishDuration = 0.12f;

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

    [Header("Star FX - Glow (Optional Overlay Image)")]
    [SerializeField] private Image glowImage;
    [SerializeField] private bool useGlow = true;
    [SerializeField] private float glowMaxAlpha = 0.65f;
    [SerializeField] private float glowInTime = 0.10f;
    [SerializeField] private float glowOutTime = 0.25f;
    [SerializeField] private float glowScaleMultiplier = 1.25f;

    [Header("Star FX - Particle Burst")]
    [SerializeField] private RectTransform starParticleContainer;
    [SerializeField] private int burstCount = 7;
    [SerializeField] private float burstRadius = 60f;
    [SerializeField] private float burstDuration = 0.5f;
    [SerializeField] private float burstParticleScale = 0.5f;
    [SerializeField] private Ease burstEase = Ease.OutCubic;

    [Header("Star - Idle Animation (Subtle)")]
    [SerializeField] private bool useIdle = true;
    [SerializeField] private float idleScaleAmount = 0.03f;
    [SerializeField] private float idlePeriod = 1.6f;

    [Header("Star - Color by Level (Gradient)")]
    [SerializeField] private bool useLevelColor = true;
    [SerializeField] private Gradient levelColorGradient;
    [SerializeField] private int maxLevelForGradient = 50;

    // Internals
    private Coroutine fillCoroutine;
    private Tween idleTween;
    private Sequence starSequence;

    private RectTransform starRect;
    private Vector3 starBaseScale;
    private Color starBaseColor;

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

        if (useBarColorShift)
            levelBarImage.color = barColorBase;

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
        idleTween?.Kill();
        if (glowImage != null) glowImage.DOKill();
        if (starIconImage != null) starIconImage.DOKill();
        if (starRect != null) starRect.DOKill();
        if (levelBarImage != null) levelBarImage.DOKill();
    }

    // -------------------------
    // XP / Level events
    // -------------------------

    private void OnExperienceChanged()
    {
        if (isProcessingLevelUp) return;

        float targetFill = LevelExperienceManager.Instance.GetExperienceNormalized();

        if (fillCoroutine != null)
            StopCoroutine(fillCoroutine);

        fillCoroutine = StartCoroutine(AnimateBar(targetFill));
        UpdateTexts();
    }

    private void OnLevelUp(object sender, EventArgs e)
    {
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

            ApplyStarBaseVisualsForCurrentLevel();
            UpdateTexts();

            if (fillCoroutine != null)
                StopCoroutine(fillCoroutine);

            bool barDone = false;
            fillCoroutine = StartCoroutine(AnimateBarLevelUp(() => barDone = true));
            yield return new WaitUntil(() => barDone);

            // Shine + Pulse + Shake + Flash al llegar al 100%
            StartCoroutine(PlayBarShine(1f));
            StartCoroutine(PlayBarLevelUpFX());
            yield return new WaitForSeconds(shineDuration);

            levelBarImage.fillAmount = 0f;

            if (useBarColorShift)
                levelBarImage.color = barColorBase;

            bool starDone = false;
            PlayStarLevelUpFX(() => starDone = true);
            yield return new WaitUntil(() => starDone);

            if (levelUpQueue.Count > 0)
                yield return new WaitForSeconds(0.3f);
        }

        isProcessingLevelUp = false;

        float targetFill = LevelExperienceManager.Instance.GetExperienceNormalized();
        if (fillCoroutine != null)
            StopCoroutine(fillCoroutine);
        fillCoroutine = StartCoroutine(AnimateBar(targetFill));

        UpdateTexts();
    }

    // -------------------------
    // Bar animations
    // -------------------------

    private IEnumerator AnimateBarLevelUp(Action onComplete)
    {
        float start = levelBarImage.fillAmount;
        float time = 0f;

        while (time < 1f)
        {
            time += Time.deltaTime * fillSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, time);
            levelBarImage.fillAmount = Mathf.Lerp(start, 1f, eased);

            if (useBarColorShift)
                levelBarImage.color = Color.Lerp(barColorBase, barColorFull, levelBarImage.fillAmount);

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

            if (useBarColorShift)
                levelBarImage.color = Color.Lerp(barColorBase, barColorFull, levelBarImage.fillAmount);

            yield return null;
        }

        time = 0f;
        float overshootStart = levelBarImage.fillAmount;
        while (time < 1f)
        {
            time += Time.deltaTime * (fillSpeed * 2f);
            levelBarImage.fillAmount = Mathf.Lerp(overshootStart, target, time);

            if (useBarColorShift)
                levelBarImage.color = Color.Lerp(barColorBase, barColorFull, levelBarImage.fillAmount);

            yield return null;
        }

        levelBarImage.fillAmount = target;
    }

    private IEnumerator PlayBarLevelUpFX()
    {
        RectTransform barRect = levelBarImage.rectTransform;

        // Flash blanco
        levelBarImage.DOColor(Color.white, barFlashDuration).OnComplete(() =>
        {
            levelBarImage.DOColor(barColorBase, barFlashDuration);
        });

        // Pulse scale Y
        barRect.DOScaleY(barPulseScale, barPulseDuration * 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            barRect.DOScaleY(1f, barPulseDuration * 0.5f).SetEase(Ease.OutBounce);
        });

        yield return new WaitForSeconds(barPulseDuration);

        // Shake horizontal
        barRect.DOShakeAnchorPos(barShakeDuration, new Vector2(barShakeStrength, 0f), barShakeVibrato, 0f);
    }

    private IEnumerator PlayBarShine(float fillAmount)
    {
        if (barShineRect == null || barShineImage == null) yield break;

        float barWidth = levelBarImage.rectTransform.rect.width;

        float startX = -(barWidth * 0.5f);
        float endX = startX + (barWidth * fillAmount);

        barShineRect.sizeDelta = new Vector2(shineWidth, barShineRect.sizeDelta.y);
        barShineRect.anchoredPosition = new Vector2(startX, 0f);
        barShineImage.color = new Color(1f, 1f, 1f, 0f);

        float elapsed = 0f;

        while (elapsed < shineDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shineDuration);

            barShineRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0f);

            float alpha = t < 0.5f
                ? Mathf.Lerp(0f, shineMaxAlpha, t / 0.5f)
                : Mathf.Lerp(shineMaxAlpha, 0f, (t - 0.5f) / 0.5f);

            barShineImage.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        barShineImage.color = new Color(1f, 1f, 1f, 0f);
    }

    // -------------------------
    // Texts
    // -------------------------

    private void UpdateTexts()
    {
        if (currentLevelText != null)
            currentLevelText.text = LevelExperienceManager.Instance.GetLevel().ToString();
        if (currentXpText != null)
            currentXpText.text = LevelExperienceManager.Instance.GetCurrentXp() + " / " +
                                 LevelExperienceManager.Instance.GetXpToNextLevel();
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
        starIconImage.color = starBaseColor;

        float startY = starRect.anchoredPosition.y;
        float halfSpin = spinDuration * 0.5f;

        starSequence = DOTween.Sequence();

        // Primera mitad: squish X 1 0 + sube
        starSequence.Append(
            DOTween.To(
                () => starRect.localScale,
                s => starRect.localScale = s,
                new Vector3(0f, starBaseScale.y * 1.1f, starBaseScale.z),
                halfSpin
            ).SetEase(spinEaseIn)
        );
        starSequence.Join(
            starRect.DOAnchorPosY(startY + bobHeight, halfSpin)
                .SetEase(Ease.OutQuad)
        );

        // Flash en el punto medio
        starSequence.AppendCallback(() =>
        {
            if (useFlash)
                starIconImage.color = Color.white;
        });

        // Segunda mitad: squish X 0 bounceUpScale + baja
        starSequence.Append(
            DOTween.To(
                () => starRect.localScale,
                s => starRect.localScale = s,
                new Vector3(starBaseScale.x * bounceUpScale, starBaseScale.y * 1.1f, starBaseScale.z),
                halfSpin
            ).SetEase(spinEaseOut)
        );
        starSequence.Join(
            starRect.DOAnchorPosY(startY, halfSpin)
                .SetEase(Ease.InQuad)
        );

        // Aterrizaje: volver a escala base + squish vertical
        starSequence.Append(
            DOTween.To(
                () => starRect.localScale,
                s => starRect.localScale = s,
                starBaseScale,
                bounceDownTime
            ).SetEase(bounceEaseDown)
        );
        starSequence.Join(
            DOTween.Sequence()
                .Append(starRect.DOScaleY(squishY, squishDuration * 0.5f).SetEase(Ease.OutQuad))
                .Append(starRect.DOScaleY(starBaseScale.y, squishDuration * 0.5f).SetEase(Ease.OutBack))
        );

        // Flash color
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

        // Particle burst
        starSequence.AppendCallback(() =>
        {
            SpawnStarBurst();
        });

        starSequence.AppendInterval(burstDuration * 0.5f);

        starSequence.OnComplete(() =>
        {
            starRect.localScale = starBaseScale;
            starRect.localRotation = Quaternion.identity;
            starRect.anchoredPosition = new Vector2(starRect.anchoredPosition.x, startY);
            starIconImage.color = starBaseColor;
            ResumeIdle();
            onComplete?.Invoke();
        });
    }

    // -------------------------
    // Particle Burst
    // -------------------------

    private void SpawnStarBurst()
    {
        if (starParticleContainer == null || starIconImage == null) return;

        Vector2 starWorldPos = starRect.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            starParticleContainer,
            RectTransformUtility.WorldToScreenPoint(null, starWorldPos),
            null,
            out Vector2 localCenter
        );

        for (int i = 0; i < burstCount; i++)
        {
            GameObject particle = new GameObject("StarParticle_" + i);
            particle.transform.SetParent(starParticleContainer, false);

            Image img = particle.AddComponent<Image>();
            img.sprite = starIconImage.sprite;
            img.color = starBaseColor;
            img.raycastTarget = false;

            RectTransform rt = particle.GetComponent<RectTransform>();
            rt.sizeDelta = starRect.sizeDelta * burstParticleScale;
            rt.anchoredPosition = localCenter;
            rt.localScale = Vector3.one * burstParticleScale;

            float angle = (360f / burstCount) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 targetPos = localCenter + dir * burstRadius;

            Sequence ps = DOTween.Sequence();
            ps.Append(rt.DOAnchorPos(targetPos, burstDuration).SetEase(burstEase));
            ps.Join(rt.DOScale(Vector3.zero, burstDuration).SetEase(Ease.InQuad));
            ps.Join(img.DOFade(0f, burstDuration * 0.6f).SetEase(Ease.InQuad).SetDelay(burstDuration * 0.4f));
            ps.OnComplete(() => Destroy(particle));
        }
    }
}