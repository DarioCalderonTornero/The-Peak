using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManagerr : MonoBehaviour
{
    public static TutorialManagerr Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Image illustrationImage;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI nextButtonText;

    [Header("Ilustraciones")]
    [SerializeField] private Sprite illustrationMountain;
    [SerializeField] private Sprite illustrationCards;
    [SerializeField] private Sprite illustrationSkull;

    private const string TUTORIAL_DONE_KEY = "TUTORIAL_DONE";
    private int currentStep = 0;

    private readonly string[] messages = {
        "¡Eres una montaña que nunca ha sido escalada... y tienes que mantener esa fama!",
        "Arrastra las cartas de la zona inferior para colocar obstáculos y defenderte.",
        "Si un escalador llega a la cima... ¡has perdido!"
    };

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
    }

    public void TryShowTutorial()
    {
        if (PlayerPrefs.GetInt(TUTORIAL_DONE_KEY, 0) == 1) return;
        ShowStep(0);
    }

    private void ShowStep(int step)
    {
        currentStep = step;
        if (tutorialPanel != null) tutorialPanel.SetActive(true);

        if (messageText != null) messageText.text = messages[step];

        if (illustrationImage != null)
        {
            illustrationImage.sprite = step switch
            {
                0 => illustrationMountain,
                1 => illustrationCards,
                2 => illustrationSkull,
                _ => null
            };
        }

        if (nextButtonText != null)
            nextButtonText.text = step < messages.Length - 1 ? "Siguiente" : "¡Empezar!";
    }

    private void OnNext()
    {
        if (currentStep < messages.Length - 1)
        {
            ShowStep(currentStep + 1);
        }
        else
        {
            // Último paso — cerrar y marcar como visto
            PlayerPrefs.SetInt(TUTORIAL_DONE_KEY, 1);
            PlayerPrefs.Save();
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    [ContextMenu("Reset Tutorial")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TUTORIAL_DONE_KEY);
    }
}
