using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class LevelExperienceUI : MonoBehaviour
{
    [SerializeField] private Image levelBarImage;
    [SerializeField] private float fillSpeed = 1.5f;
    [SerializeField] private TextMeshProUGUI currentLevelText;

    private Coroutine fillCoroutine;

    private void Start()
    {
        LevelExperienceManager.Instance.OnExperienceChanged += LevelExperienceManager_UpdateBarSmooth;
        LevelExperienceManager.Instance.OnLevelUp += LevelExperienceManager_OnLevelUp;

        levelBarImage.fillAmount = LevelExperienceManager.Instance.GetExperienceNormalized();
    }

    private void LevelExperienceManager_OnLevelUp(object sender, System.EventArgs e)
    {
        currentLevelText.text = LevelExperienceManager.Instance.GetLevel().ToString();
    }

    private void OnDestroy()
    {
        LevelExperienceManager.Instance.OnExperienceChanged -= LevelExperienceManager_UpdateBarSmooth;
    }

    private void LevelExperienceManager_UpdateBarSmooth()
    {
        float targetFill =
            LevelExperienceManager.Instance.GetExperienceNormalized();

        if (fillCoroutine != null)
            StopCoroutine(fillCoroutine);

        fillCoroutine = StartCoroutine(AnimateBar(targetFill));
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
}
