using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManagerr : MonoBehaviour
{
    public static TutorialManagerr Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI nextButtonText;

    [Header("Pasos")]
    [SerializeField] private GameObject step1;
    [SerializeField] private GameObject step2;
    [SerializeField] private GameObject step3;

    [Header("Animación")]
    [SerializeField] private float fadeDuration = 0.4f;

    private int currentStep = 0;
    private bool animating = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNext);

        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        SetAlpha(step1, 0f);
        SetAlpha(step2, 0f);
        SetAlpha(step3, 0f);
    }

    public void TryShowTutorial()
    {
        currentStep = 0;
        SetAlpha(step1, 0f);
        SetAlpha(step2, 0f);
        SetAlpha(step3, 0f);

        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        if (nextButtonText != null) nextButtonText.text = "Siguiente";

        StartCoroutine(FadeIn(GetStep(0)));
    }

    private void OnNext()
    {
        if (animating) return;

        if (currentStep < 2)
        {
            currentStep++;
            StartCoroutine(FadeIn(GetStep(currentStep)));
            if (currentStep == 2 && nextButtonText != null)
                nextButtonText.text = "¡Empezar!";
        }
        else
        {
            StartCoroutine(HideTutorial());
        }
    }

    private IEnumerator HideTutorial()
    {
        nextButton.interactable = false;
        StartCoroutine(FadeOut(step1));
        StartCoroutine(FadeOut(step2));
        yield return StartCoroutine(FadeOut(step3));
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        nextButton.interactable = true;
        Time.timeScale = 1f;
    }

    private GameObject GetStep(int index) => index switch
    {
        0 => step1,
        1 => step2,
        2 => step3,
        _ => null
    };

    private IEnumerator FadeIn(GameObject obj)
    {
        if (obj == null) yield break;
        animating = true;
        var cg = GetOrAddCanvasGroup(obj);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
        animating = false;
    }

    private IEnumerator FadeOut(GameObject obj)
    {
        if (obj == null) yield break;
        var cg = GetOrAddCanvasGroup(obj);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = 0f;
    }

    private void SetAlpha(GameObject obj, float alpha)
    {
        if (obj == null) return;
        GetOrAddCanvasGroup(obj).alpha = alpha;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        var cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }
}