using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class LevelExperienceUI : MonoBehaviour
{
    [Header("Bar")]
    [SerializeField] private Image levelBarImage;
    [SerializeField] private float fillSpeed = 1.5f;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI currentLevelText;
    [SerializeField] private TextMeshProUGUI currentXpText;
    [SerializeField] private TextMeshProUGUI xpToNextLevelText;

    private Coroutine fillCoroutine;

    private void Start()
    {
        LevelExperienceManager.Instance.OnExperienceChanged += OnExperienceChanged;
        LevelExperienceManager.Instance.OnLevelUp += OnLevelUp;

        // Inicialización
        levelBarImage.fillAmount =
            LevelExperienceManager.Instance.GetExperienceNormalized();

        UpdateTexts();
    }

    private void OnDestroy()
    {
        if (LevelExperienceManager.Instance == null) return;

        LevelExperienceManager.Instance.OnExperienceChanged -= OnExperienceChanged;
        LevelExperienceManager.Instance.OnLevelUp -= OnLevelUp;
    }

    // EVENTOS ------------------------------------------------

    private void OnExperienceChanged()
    {
        float targetFill =
            LevelExperienceManager.Instance.GetExperienceNormalized();

        if (fillCoroutine != null)
            StopCoroutine(fillCoroutine);

        fillCoroutine = StartCoroutine(AnimateBar(targetFill));

        UpdateTexts();
    }

    private void OnLevelUp(object sender, System.EventArgs e)
    {
        UpdateTexts();
    }

    // UI ----------------------------------------------------

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
        currentLevelText.text =
            LevelExperienceManager.Instance.GetLevel().ToString();

        currentXpText.text =
            LevelExperienceManager.Instance.GetCurrentXp().ToString();

        xpToNextLevelText.text = " / " +
            LevelExperienceManager.Instance.GetXpToNextLevel().ToString();
    }
}
